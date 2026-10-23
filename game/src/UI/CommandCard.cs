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
/// Orders and the build menu for the selection. Builders show what they can construct (and the
/// saved build templates); factories show their units with queue counts (left click +1, shift +5,
/// right click -1), salvaged enemy designs (tagged), and the rally rotation setting. Structures can be saved as a template. The
/// gamepad walks the buttons with the d-pad (<see cref="NavMove"/>, <see cref="NavPress"/>).
/// </summary>
public partial class CommandCard : VBoxContainer
{
    private SimSession _session = null!;
    private Selection _selection = null!;
    private InputController _input = null!;
    private HFlowContainer _orders = null!;
    private HFlowContainer _templates = null!;
    private GridContainer _build = null!;
    private Label _title = null!;
    private readonly Dictionary<int, (Button Button, Label Count)> _factoryButtons = new();
    private Button? _fireButton;
    private Button? _repeatButton;
    private Button? _ferryButton;
    private int _salvagedShown = -1;

    // D-pad navigation: rows of buttons, top to bottom.
    private readonly List<List<Button>> _navRows = new();
    private (int Row, int Col) _nav = (-1, -1);
    private Button? _navButton;
    private static readonly StyleBoxFlat NavBox = UiTheme.Box(new Color(0.3f, 0.28f, 0.14f), UiTheme.Amber, 2, 1, 6);

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
        _templates = new HFlowContainer();
        _templates.AddThemeConstantOverride("h_separation", 4);
        _templates.AddThemeConstantOverride("v_separation", 4);
        AddChild(_templates);
        _title = UiTheme.Label("", 13, UiTheme.Dim);
        AddChild(_title);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _build = new GridContainer { Columns = 6 };
        _build.AddThemeConstantOverride("h_separation", 4);
        _build.AddThemeConstantOverride("v_separation", 4);
        scroll.AddChild(_build);
        AddChild(scroll);
        input.TemplateSaveRequested += AskTemplateName;
        TemplateStore.Changed += Refresh;
        TreeExiting += () => TemplateStore.Changed -= Refresh;
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
        foreach (Node c in _templates.GetChildren()) c.QueueFree();
        foreach (Node c in _build.GetChildren()) c.QueueFree();
        _factoryButtons.Clear();
        _fireButton = null;
        _repeatButton = null;
        _ferryButton = null;
        _salvagedShown = -1;
        _title.Text = "";
        _navRows.Clear();
        _navButton = null;
        _nav = (-1, -1);

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
        bool anyStructure = defs.Any(d => d.IsStructure);

        var orderRow = new List<Button>();
        if (anyMobile) orderRow.Add(OrderButton("Move", "Move (M)", () => _input.SetMode(ClickMode.Move)));
        if (anyArmed && anyMobile) orderRow.Add(OrderButton("Attack", "Attack a unit, or attack-move to a point (A). Ctrl-click fires at the ground. Drag a circle to attack every enemy in an area.", () => _input.SetMode(ClickMode.Fight)));
        orderRow.Add(OrderButton("Stop", "Stop (S)", () => _input.IssueToSelection(CommandType.Stop)));
        if (anyMobile) orderRow.Add(OrderButton("Patrol", "Patrol (P)", () => _input.SetMode(ClickMode.Patrol)));
        if (anyAssist && anyMobile) orderRow.Add(OrderButton("Guard", "Guard / help build / repair a friendly unit (G). Drag a circle to repair everything in an area.", () => _input.SetMode(ClickMode.Assist)));
        if (anyReclaim) orderRow.Add(OrderButton("Reclaim", "Reclaim a wreck, rock or tree (R). Drag a circle to clear an area.", () => _input.SetMode(ClickMode.Reclaim)));
        if (anyDGun) orderRow.Add(OrderButton("D-Gun", "Breach Cannon: destroys almost anything in its path. 500 energy. (D)", () => _input.SetMode(ClickMode.DGun)));
        if (anyArmed) orderRow.Add(_fireButton = OrderButton(FireText(mine), "Cycle fire state: fire at will / return fire / hold fire (F)", () => _input.CycleFireState(mine)));
        if (anyFactory)
        {
            int factory = mine.First(id => World.Catalog[World.Units.TypeIndex[id]].IsFactory);
            orderRow.Add(_repeatButton = OrderButton(World.Units.FactoryRepeat[factory] ? "Repeat: on" : "Repeat: off", "Loop the build queue", () =>
            {
                bool on = !World.Units.FactoryRepeat[factory];
                foreach (int id in Mine())
                    if (World.Catalog[World.Units.TypeIndex[id]].IsFactory)
                        _input.Issue(new Command(0, Me, CommandType.FactoryRepeat, id, Arg: on ? 1 : 0));
            }));
            orderRow.Add(_ferryButton = OrderButton(FerryText(factory),
                "How new units share the rally stops. Right-click the ground sets the rally point; Shift+right-click adds up to three more stops.\n"
                + "In turn: each new unit goes to the next stop. Every 2nd / 3rd / 4th: that unit goes to the next extra stop, the rest to the first.",
                () => _input.SetFerryEvery(NextFerry(World.Units.RallyEvery[factory]))));
            orderRow.Add(OrderButton("Clear rally", "Forget every rally point (Ctrl+right-click)", _input.ClearRallies));
        }
        if (anyStructure)
            orderRow.Add(OrderButton("Save as template", "Save the selected structures and their layout as a build template (Ctrl+T)", _input.RequestTemplateSave));
        _navRows.Add(orderRow);

        // Build options: factories first, else builders (options shared by the first builder).
        int fid = mine.FirstOrDefault(id => World.Catalog[World.Units.TypeIndex[id]].IsFactory, -1);
        int bid = mine.FirstOrDefault(id => World.Catalog[World.Units.TypeIndex[id]].CanBuild, -1);
        var buildButtons = new List<Button>();
        if (fid >= 0)
        {
            UnitDef fdef = World.Catalog[World.Units.TypeIndex[fid]];
            _title.Text = $"{fdef.Name.ToUpperInvariant()}: left click +1 (shift +5), right click -1";
            foreach (int t in fdef.BuildOptions) buildButtons.Add(AddFactoryButton(t));
            // Enemy designs reverse-engineered from reclaimed wrecks.
            var salvaged = World.SalvagedOptions(fdef, Me);
            _salvagedShown = salvaged.Count;
            foreach (int t in salvaged) buildButtons.Add(AddFactoryButton(t, salvaged: true));
        }
        else if (bid >= 0)
        {
            UnitDef bdef = World.Catalog[World.Units.TypeIndex[bid]];
            _title.Text = "BUILD: pick a structure, then click the map";
            BuildTemplateRow();
            foreach (int t in bdef.BuildOptions.OrderBy(t => Category(World.Catalog[t])).ThenBy(t => World.Catalog[t].MetalCost.Raw))
                buildButtons.Add(AddBuildButton(t));
        }
        for (int i = 0; i < buildButtons.Count; i += _build.Columns)
            _navRows.Add(buildButtons.Skip(i).Take(_build.Columns).ToList());
    }

    private void BuildTemplateRow()
    {
        var row = new List<Button>();
        var label = UiTheme.Label("TEMPLATES", 12, UiTheme.Dim);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.CustomMinimumSize = new Vector2(0, 28);
        _templates.AddChild(label);
        foreach (BuildTemplate t in TemplateStore.All)
        {
            BuildTemplate template = t;
            var b = UiTheme.Button(t.Name, () => _input.BeginTemplatePlacement(template), 0, 13);
            b.CustomMinimumSize = new Vector2(0, 28);
            b.TooltipText = $"{t.Name}: {t.Items.Count} structures\n{string.Join(", ", t.Items.GroupBy(i => i.Type).Select(g => $"{g.Count()}x {NameOf(g.Key)}"))}\nClick, then click the map to stamp it. R turns it.";
            b.Name = "Template_" + t.Name;
            _templates.AddChild(b);
            row.Add(b);
        }
        var manage = UiTheme.Button(TemplateStore.All.Count == 0 ? "No templates yet" : "Manage", () => GetTree().Root.AddChild(TemplateDialogs.ManageDialog()), 0, 13);
        manage.CustomMinimumSize = new Vector2(0, 28);
        manage.TooltipText = "Rename or delete templates. Select structures and press Ctrl+T to make one.";
        _templates.AddChild(manage);
        row.Add(manage);
        _navRows.Add(row);
    }

    private string NameOf(string typeId) => World.Catalog.TryIndexOf(typeId, out int i) ? World.Catalog[i].Name : typeId;

    private void AskTemplateName(IReadOnlyList<int> structures)
    {
        var ids = structures.ToList();
        GetTree().Root.AddChild(TemplateDialogs.SaveDialog(ids.Count, name =>
        {
            BuildTemplate? t = BuildTemplate.FromStructures(World, ids, Me, name);
            if (t is null) return;
            TemplateStore.Add(t);
            Audio.Sfx.Play(Audio.SfxKind.Click);
        }));
    }

    private static int NextFerry(int every) => every switch { 0 => 2, 2 => 3, 3 => 4, _ => 0 };

    private string FerryText(int factory)
    {
        int stops = World.Units.RallyPoints(factory).Count();
        string mode = World.Units.RallyEvery[factory] switch
        {
            0 => "in turn",
            2 => "every 2nd",
            3 => "every 3rd",
            int n => $"every {n}th",
        };
        return stops switch { 0 => "Rally: none", 1 => "Rally: 1 stop", _ => $"Rally {stops}: {mode}" };
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

    private Button IconButton(int type, bool salvaged = false)
    {
        UnitDef d = World.Catalog[type];
        var b = new Button
        {
            CustomMinimumSize = new Vector2(68, 88),
            TooltipText = salvaged
                ? $"SALVAGED DESIGN ({d.Faction})\nReverse-engineered from {World.SalvageNeeded(type)} reclaimed wrecks.\n\n" + Tooltip(d)
                : Tooltip(d),
            Name = (salvaged ? "Salvaged_" : "Build_") + d.Id,
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
        if (salvaged)
        {
            // A cyan strip across the foot of the icon marks enemy designs.
            var tag = new Label
            {
                Text = "SALVAGED",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
                ClipText = true,
            };
            var box = UiTheme.Box(new Color(0.06f, 0.28f, 0.32f, 0.9f), new Color(0.4f, 0.9f, 1f), 1, 2, 0);
            box.SetContentMarginAll(0);
            tag.AddThemeStyleboxOverride("normal", box);
            tag.AddThemeFontSizeOverride("font_size", 8);
            tag.AddThemeColorOverride("font_color", new Color(0.8f, 0.98f, 1f));
            b.AddChild(tag);
            tag.Position = new Vector2(6, 40);
            tag.Size = new Vector2(56, 13);
        }
        _build.AddChild(b);
        return b;
    }

    private Button AddBuildButton(int type)
    {
        Button b = IconButton(type);
        b.Pressed += () =>
        {
            Audio.Sfx.Play(Audio.SfxKind.Click);
            _input.BeginPlacement(type);
        };
        return b;
    }

    private Button AddFactoryButton(int type, bool salvaged = false)
    {
        Button b = IconButton(type, salvaged);
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
        // The gamepad's A press arrives as Pressed (no GuiInput): queue one.
        b.Pressed += () => { if (_navButton == b) _input.FactoryQueue(type, 1); };
        _factoryButtons[type] = (b, count);
        return b;
    }

    // ---- Gamepad navigation ----------------------------------------------------------------------

    /// <summary>Index of the highlighted button (row * 100 + column), or -1 while the cursor is in charge.</summary>
    public int NavIndex => _navButton is null ? -1 : _nav.Row * 100 + _nav.Col;
    public Button? NavButton => _navButton;

    /// <summary>Moves the highlight; returns false when there is nothing to walk.</summary>
    public bool NavMove(int dx, int dy)
    {
        var rows = _navRows.Where(r => r.Count > 0).ToList();
        if (rows.Count == 0 || !IsVisibleInTree()) return false;
        int row = _nav.Row, col = _nav.Col;
        if (_navButton is null) { row = 0; col = 0; }
        else
        {
            row = Math.Clamp(row + dy, 0, rows.Count - 1);
            col = dx == 0 ? Math.Clamp(col, 0, rows[row].Count - 1) : ((col + dx) % rows[row].Count + rows[row].Count) % rows[row].Count;
        }
        Highlight(rows[row][col], row, col);
        return true;
    }

    /// <summary>Presses the highlighted button.</summary>
    public bool NavPress()
    {
        if (_navButton is null || !IsInstanceValid(_navButton)) return false;
        Audio.Sfx.Play(Audio.SfxKind.Click);
        _navButton.EmitSignal(BaseButton.SignalName.Pressed);
        return true;
    }

    public void NavClear() => Highlight(null, -1, -1);

    private void Highlight(Button? b, int row, int col)
    {
        if (_navButton is not null && IsInstanceValid(_navButton))
        {
            _navButton.RemoveThemeStyleboxOverride("normal");
            _navButton.RemoveThemeStyleboxOverride("hover");
        }
        _navButton = b;
        _nav = (row, col);
        if (b is null) return;
        b.AddThemeStyleboxOverride("normal", NavBox);
        b.AddThemeStyleboxOverride("hover", NavBox);
        if (b.GetParent() == _build && _build.GetParent() is ScrollContainer sc) sc.EnsureControlVisible(b);
    }

    public void OnTick()
    {
        if (_factoryButtons.Count == 0 && _fireButton is null) return;
        var mine = Mine();
        if (_fireButton is not null) _fireButton.Text = FireText(mine);
        int fid = mine.FirstOrDefault(id => World.Catalog[World.Units.TypeIndex[id]].IsFactory, -1);
        if (fid < 0) return;
        if (_repeatButton is not null) _repeatButton.Text = World.Units.FactoryRepeat[fid] ? "Repeat: on" : "Repeat: off";
        if (_ferryButton is not null) _ferryButton.Text = FerryText(fid);
        // A design was salvaged while the factory was selected: show it.
        if (_salvagedShown >= 0 && World.SalvagedOptions(World.Catalog[World.Units.TypeIndex[fid]], Me).Count != _salvagedShown) { Refresh(); return; }
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
