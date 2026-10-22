using System;
using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Match;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.State;
using Godot;

namespace ContingencyZero.Game.UI;

/// <summary>
/// Orders and the build menu for the selection. Builders show what they can construct; factories
/// show their units with queue counts (left click +1, shift +5, right click -1).
/// </summary>
public partial class CommandCard : VBoxContainer
{
    private SimSession _session = null!;
    private Selection _selection = null!;
    private InputController _input = null!;
    private HFlowContainer _orders = null!;
    private GridContainer _build = null!;
    private Label _title = null!;
    private readonly Dictionary<int, (Button Button, Label Count)> _factoryButtons = new();
    private Button? _fireButton;
    private Button? _repeatButton;

    private World World => _session.World;
    private int Me => _session.LocalPlayer;

    public void Init(SimSession session, Selection selection, InputController input)
    {
        _session = session;
        _selection = selection;
        _input = input;
        AddThemeConstantOverride("separation", 6);
        _orders = new HFlowContainer();
        _orders.AddThemeConstantOverride("h_separation", 4);
        _orders.AddThemeConstantOverride("v_separation", 4);
        AddChild(_orders);
        _title = UiTheme.Label("", 13, UiTheme.Dim);
        AddChild(_title);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _build = new GridContainer { Columns = 6 };
        _build.AddThemeConstantOverride("h_separation", 4);
        _build.AddThemeConstantOverride("v_separation", 4);
        scroll.AddChild(_build);
        AddChild(scroll);
        Refresh();
    }

    private List<int> Mine() =>
        _selection.Ids.Where(id => World.Units.IsValid(id) && World.Units.Owner[id] == Me && World.Units.Complete[id]).ToList();

    private Button OrderButton(string text, string tip, Action act)
    {
        var b = UiTheme.Button(text, act, 0, 14);
        b.CustomMinimumSize = new Vector2(72, 32);
        b.TooltipText = tip;
        _orders.AddChild(b);
        return b;
    }

    public void Refresh()
    {
        foreach (Node c in _orders.GetChildren()) c.QueueFree();
        foreach (Node c in _build.GetChildren()) c.QueueFree();
        _factoryButtons.Clear();
        _fireButton = null;
        _repeatButton = null;
        _title.Text = "";

        var mine = Mine();
        bool show = mine.Count > 0 && !_session.IsReplay;
        if (GetParent() is Control frame) frame.Visible = show; // hide the empty panel
        if (!show) return;
        var defs = mine.Select(id => World.Catalog[World.Units.TypeIndex[id]]).ToList();

        bool anyMobile = defs.Any(d => d.CanMove);
        bool anyArmed = defs.Any(d => d.IsArmed);
        bool anyReclaim = defs.Any(d => d.CanReclaim);
        bool anyAssist = defs.Any(d => d.CanAssist || d.CanMove);
        bool anyDGun = defs.Any(d => d.HasDGun);
        bool anyFactory = defs.Any(d => d.IsFactory);

        if (anyMobile) OrderButton("Move", "Move (M)", () => _input.SetMode(ClickMode.Move));
        if (anyArmed && anyMobile) OrderButton("Attack", "Attack a unit, or attack-move to a point (A). Ctrl-click fires at the ground.", () => _input.SetMode(ClickMode.Fight));
        OrderButton("Stop", "Stop (S)", () => _input.IssueToSelection(CommandType.Stop));
        if (anyMobile) OrderButton("Patrol", "Patrol (P)", () => _input.SetMode(ClickMode.Patrol));
        if (anyAssist && anyMobile) OrderButton("Guard", "Guard / help build / repair a friendly unit (G)", () => _input.SetMode(ClickMode.Assist));
        if (anyReclaim) OrderButton("Reclaim", "Reclaim a wreck, rock or tree (R)", () => _input.SetMode(ClickMode.Reclaim));
        if (anyDGun) OrderButton("D-Gun", "Breach Cannon: destroys almost anything in its path. 500 energy. (D)", () => _input.SetMode(ClickMode.DGun));
        if (anyArmed) _fireButton = OrderButton(FireText(mine), "Cycle fire state: fire at will / return fire / hold fire (F)", () => _input.CycleFireState(mine));
        if (anyFactory)
        {
            int factory = mine.First(id => World.Catalog[World.Units.TypeIndex[id]].IsFactory);
            _repeatButton = OrderButton(World.Units.FactoryRepeat[factory] ? "Repeat: on" : "Repeat: off", "Loop the build queue", () =>
            {
                bool on = !World.Units.FactoryRepeat[factory];
                foreach (int id in Mine())
                    if (World.Catalog[World.Units.TypeIndex[id]].IsFactory)
                        _input.Issue(new Command(0, Me, CommandType.FactoryRepeat, id, Arg: on ? 1 : 0));
            });
        }

        // Build options: factories first, else builders (options shared by the first builder).
        int fid = mine.FirstOrDefault(id => World.Catalog[World.Units.TypeIndex[id]].IsFactory, -1);
        int bid = mine.FirstOrDefault(id => World.Catalog[World.Units.TypeIndex[id]].CanBuild, -1);
        if (fid >= 0)
        {
            UnitDef fdef = World.Catalog[World.Units.TypeIndex[fid]];
            _title.Text = $"{fdef.Name.ToUpperInvariant()}: left click +1 (shift +5), right click -1";
            foreach (int t in fdef.BuildOptions) AddFactoryButton(t);
        }
        else if (bid >= 0)
        {
            UnitDef bdef = World.Catalog[World.Units.TypeIndex[bid]];
            _title.Text = "BUILD: pick a structure, then click the map";
            foreach (int t in bdef.BuildOptions.OrderBy(t => Category(World.Catalog[t])).ThenBy(t => World.Catalog[t].MetalCost.Raw))
                AddBuildButton(t);
        }
    }

    private static int Category(UnitDef d) =>
        d.ExtractorMultiplier.Raw > 0 || d.EnergyIncomePerTick.Raw > 0 || d.UsesWind || d.MetalStorage.Raw > 0 || d.EnergyStorage.Raw > 0 || d.ConvertMetalPerTick.Raw > 0 ? 0
        : d.IsFactory ? 1
        : d.IsArmed ? 2
        : 3;

    private string FireText(List<int> mine)
    {
        var armed = mine.Where(id => World.Catalog[World.Units.TypeIndex[id]].IsArmed).ToList();
        if (armed.Count == 0) return "Fire";
        return World.Units.Fire[armed[0]] switch { FireState.HoldFire => "Hold fire", FireState.ReturnFire => "Return fire", _ => "Fire at will" };
    }

    private static string Tooltip(UnitDef d)
    {
        var lines = new List<string>
        {
            d.Name,
            d.Description,
            $"Metal {d.MetalCost.Raw / 65536f:0}   Energy {d.EnergyCost.Raw / 65536f:0}   Build time {d.BuildTime.Raw / 65536f:0}",
            $"Health {d.MaxHealth.Raw / 65536f:0}" + (d.CanMove ? $"   Speed {d.SpeedPerTick.Raw / 65536f * World.TicksPerSecond:0.#}" : ""),
        };
        foreach (WeaponDef w in d.Weapons)
        {
            if (w.Kind == WeaponKind.DGun) continue;
            float dps = w.Damage.Raw / 65536f / (w.ReloadTicks / (float)World.TicksPerSecond);
            lines.Add($"{w.Kind}: range {w.Range.Raw / 65536f:0}, {dps:0} damage/s{(w.TargetsAir && !w.TargetsGround ? ", anti-air" : "")}");
        }
        float m = d.MetalIncomePerTick.Raw / 65536f * World.TicksPerSecond, e = d.EnergyIncomePerTick.Raw / 65536f * World.TicksPerSecond;
        if (m > 0) lines.Add($"Makes {m:0.#} metal/s");
        if (e > 0) lines.Add($"Makes {e:0} energy/s");
        return string.Join("\n", lines.Where(l => l.Length > 0));
    }

    private Button IconButton(int type)
    {
        UnitDef d = World.Catalog[type];
        var b = new Button
        {
            CustomMinimumSize = new Vector2(68, 88),
            TooltipText = Tooltip(d),
        };
        var icon = new TextureRect
        {
            Texture = IconFactory.For(d, UiTheme.TeamColour(Me)),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            Position = new Vector2(4, 3),
            Size = new Vector2(60, 56),
        };
        b.AddChild(icon);
        var name = new Label
        {
            Text = d.Name,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            ClipText = true,
            MouseFilter = MouseFilterEnum.Ignore,
            Position = new Vector2(1, 58),
            Size = new Vector2(66, 28),
        };
        name.AddThemeFontSizeOverride("font_size", 10);
        name.AddThemeConstantOverride("line_spacing", -3);
        name.AddThemeColorOverride("font_color", UiTheme.Ink);
        b.AddChild(name);
        _build.AddChild(b);
        return b;
    }

    private void AddBuildButton(int type)
    {
        Button b = IconButton(type);
        b.Pressed += () =>
        {
            Audio.Sfx.Play(Audio.SfxKind.Click);
            _input.BeginPlacement(type);
        };
    }

    private void AddFactoryButton(int type)
    {
        Button b = IconButton(type);
        var count = UiTheme.Label("", 16, UiTheme.Amber);
        count.Position = new Vector2(4, 0);
        count.AddThemeConstantOverride("outline_size", 4);
        count.AddThemeColorOverride("font_outline_color", Colors.Black);
        b.AddChild(count);
        b.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true } mb)
            {
                int n = mb.ShiftPressed ? 5 : mb.CtrlPressed ? 20 : 1;
                if (mb.ButtonIndex == MouseButton.Left) _input.FactoryQueue(type, n);
                else if (mb.ButtonIndex == MouseButton.Right) _input.FactoryQueue(type, -n);
                b.AcceptEvent();
            }
        };
        _factoryButtons[type] = (b, count);
    }

    public void OnTick()
    {
        if (_factoryButtons.Count == 0 && _fireButton is null) return;
        var mine = Mine();
        if (_fireButton is not null) _fireButton.Text = FireText(mine);
        int fid = mine.FirstOrDefault(id => World.Catalog[World.Units.TypeIndex[id]].IsFactory, -1);
        if (fid < 0) return;
        if (_repeatButton is not null) _repeatButton.Text = World.Units.FactoryRepeat[fid] ? "Repeat: on" : "Repeat: off";
        foreach (var (type, (_, label)) in _factoryButtons)
        {
            int n = 0;
            foreach (int id in mine)
                if (World.Catalog[World.Units.TypeIndex[id]].IsFactory)
                    n += World.Units.FactoryQueue[id].Count(t => t == type);
            label.Text = n > 0 ? n.ToString() : "";
        }
    }
}
