using System;
using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Match;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Combat;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.State;
using Godot;

namespace ContingencyZero.Game.UI;

/// <summary>Details of the selection: one unit in full, or a breakdown of a group by type.</summary>
public partial class SelectionPanel : VBoxContainer
{
    private SimSession _session = null!;
    private Selection _selection = null!;
    private InputController _input = null!;
    private int _shownSingle = -2;

    private World World => _session.World;

    // Single-unit widgets, kept and updated every tick.
    private TextureRect? _icon;
    private Label? _name;
    private Label? _desc;
    private ProgressBar? _health;
    private Label? _healthText;
    private Label? _status;
    private HBoxContainer? _queue;

    public void Init(SimSession session, Selection selection, InputController input)
    {
        _session = session;
        _selection = selection;
        _input = input;
        AddThemeConstantOverride("separation", 4);
        Refresh();
    }

    private void Clear()
    {
        foreach (Node c in GetChildren()) c.QueueFree();
        _icon = null; _name = null; _desc = null; _health = null; _healthText = null; _status = null; _queue = null;
    }

    public void Refresh()
    {
        var ids = _selection.Ids.Where(World.Units.IsValid).ToList();
        if (ids.Count == 1)
        {
            if (_shownSingle != ids[0]) BuildSingle(ids[0]);
            OnTick();
            return;
        }
        _shownSingle = -2;
        Clear();
        if (ids.Count == 0)
        {
            var hint = UiTheme.Label((App.Settings.Current.ClassicMouse ? "Left click or drag to select, left click again to order.  Right click deselects." : "Left click or drag to select.  Right click to order.") + "\nA attack-move   S stop   R reclaim   G guard   D Breach Cannon   F fire state\nCtrl+1..9 make a group, 1..9 recall it.  Home: find your Commander.  Q/E turn the view.", 14, UiTheme.Dim);
            hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            AddChild(hint);
            return;
        }

        AddChild(UiTheme.Label($"{ids.Count} units selected", 15, UiTheme.Dim));
        var grid = new GridContainer { Columns = 9 };
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);
        foreach (var group in ids.GroupBy(id => World.Units.TypeIndex[id]).OrderByDescending(g => g.Count()).Take(18))
        {
            UnitDef def = World.Catalog[group.Key];
            int owner = World.Units.Owner[group.First()];
            var b = new Button
            {
                Icon = IconFactory.For(def, UiTheme.TeamColour(owner)),
                ExpandIcon = true,
                CustomMinimumSize = new Vector2(52, 52),
                TooltipText = $"{def.Name}: click to select just these, shift-click to deselect them",
                Text = group.Count().ToString(),
                IconAlignment = HorizontalAlignment.Center,
                VerticalIconAlignment = VerticalAlignment.Top,
            };
            b.AddThemeFontSizeOverride("font_size", 12);
            int type = group.Key;
            var members = group.ToList();
            b.Pressed += () =>
            {
                if (Input.IsKeyPressed(Key.Shift)) _selection.Set(_selection.Ids.Where(i => World.Units.TypeIndex[i] != type).ToList());
                else _selection.Set(members);
            };
            grid.AddChild(b);
        }
        AddChild(grid);
    }

    private void BuildSingle(int id)
    {
        Clear();
        _shownSingle = id;
        UnitDef def = World.Catalog[World.Units.TypeIndex[id]];
        int owner = World.Units.Owner[id];

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        _icon = new TextureRect
        {
            Texture = IconFactory.For(def, UiTheme.TeamColour(owner)),
            CustomMinimumSize = new Vector2(96, 96),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        row.AddChild(_icon);

        var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 3);
        string ownerName = World.Players[owner].Name;
        _name = UiTheme.Label($"{def.Name}", 19, UiTheme.FactionColour(def.Faction));
        col.AddChild(_name);
        col.AddChild(UiTheme.Label($"{(def.Faction == "lattice" ? "Lattice" : "Crucible")} · Tier {def.Tier} · {ownerName}", 13, UiTheme.Dim));
        _desc = UiTheme.Label(def.Description, 13, UiTheme.Ink);
        _desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(_desc);

        var hrow = new HBoxContainer();
        _health = new ProgressBar { CustomMinimumSize = new Vector2(180, 10), MaxValue = 1, ShowPercentage = false, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        _healthText = UiTheme.Label("", 13, UiTheme.Ink);
        hrow.AddChild(_health);
        hrow.AddChild(_healthText);
        col.AddChild(hrow);
        _status = UiTheme.Label("", 13, UiTheme.Accent);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(_status);
        row.AddChild(col);
        AddChild(row);

        if (def.IsFactory)
        {
            _queue = new HBoxContainer();
            _queue.AddThemeConstantOverride("separation", 2);
            AddChild(_queue);
        }
    }

    private string _queueKey = "";

    public void OnTick()
    {
        if (_shownSingle < 0 || _health is null || _healthText is null || _status is null) return;
        int id = _shownSingle;
        UnitStore u = World.Units;
        if (!u.IsValid(id)) { Refresh(); return; }
        UnitDef def = World.Catalog[u.TypeIndex[id]];

        float hp = u.Health[id].Raw / 65536f, max = def.MaxHealth.Raw / 65536f;
        _health.Value = hp / Math.Max(1, max);
        _health.AddThemeStyleboxOverride("fill", UiTheme.Box(hp / max > 0.6f ? UiTheme.Good : hp / max > 0.3f ? UiTheme.Amber : UiTheme.Bad, Colors.Transparent, 0, 2, 0));
        _healthText.Text = $"  {hp:0} / {max:0}";

        var parts = new List<string>();
        if (!u.Complete[id])
            parts.Add($"Under construction: {100.0 * u.BuildProgress[id].Raw / Math.Max(1, def.BuildTime.Raw):0}%");
        else
        {
            Order o = u.CurrentOrder(id);
            parts.Add(o.Type switch
            {
                OrderType.None => def.IsFactory ? (u.FactoryCurrent[id] >= 0 ? "Producing" : "Idle") : "Idle",
                OrderType.Move => "Moving",
                OrderType.Attack => "Attacking",
                OrderType.AttackGround => "Firing at the ground",
                OrderType.Fight => "Attack-moving",
                OrderType.Build => o.TypeIndex >= 0 ? $"Building {World.Catalog[o.TypeIndex].Name}" : "Building",
                OrderType.Reclaim => "Reclaiming",
                OrderType.Assist => "Assisting",
                OrderType.Patrol => "Patrolling",
                OrderType.DGun => "Firing the Breach Cannon",
                _ => "",
            });
            if (u.Orders[id].Count > 1) parts.Add($"+{u.Orders[id].Count - 1} queued");
            int rank = CombatSystem.VeteranRank(u.Kills[id]);
            if (u.Kills[id] > 0) parts.Add($"{u.Kills[id]} kills{(rank > 0 ? $" ({new string('*', rank)})" : "")}");
            if (def.IsArmed) parts.Add(u.Fire[id] switch { FireState.HoldFire => "Hold fire", FireState.ReturnFire => "Return fire", _ => "Fire at will" });
            float m = (def.MetalIncomePerTick + u.ExtractorYield[id]).Raw / 65536f * World.TicksPerSecond;
            float e = (def.EnergyIncomePerTick.Raw / 65536f * World.TicksPerSecond) + (def.UsesWind ? World.Wind.Raw / 65536f : 0);
            if (m > 0.01f) parts.Add($"+{m:0.#} metal/s");
            if (e > 0.01f) parts.Add($"+{e:0} energy/s");
            if (def.IsFactory && u.FactoryCurrent[id] >= 0 && u.IsValid(u.FactoryCurrent[id]))
            {
                int f = u.FactoryCurrent[id];
                UnitDef fd = World.Catalog[u.TypeIndex[f]];
                parts.Add($"{fd.Name} {100.0 * u.BuildProgress[f].Raw / Math.Max(1, fd.BuildTime.Raw):0}%");
            }
            if (def.IsFactory && u.FactoryRepeat[id]) parts.Add("Repeat on");
        }
        _status.Text = string.Join("  ·  ", parts);

        if (_queue is not null)
        {
            string key = string.Join(",", u.FactoryQueue[id]);
            if (key != _queueKey)
            {
                _queueKey = key;
                foreach (Node c in _queue.GetChildren()) c.QueueFree();
                foreach (int t in u.FactoryQueue[id].Take(14))
                {
                    _queue.AddChild(new TextureRect
                    {
                        Texture = IconFactory.For(World.Catalog[t], UiTheme.TeamColour(u.Owner[id])),
                        CustomMinimumSize = new Vector2(30, 30),
                        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    });
                }
                if (u.FactoryQueue[id].Count > 14) _queue.AddChild(UiTheme.Label($"+{u.FactoryQueue[id].Count - 14}", 13, UiTheme.Dim));
            }
        }
    }
}
