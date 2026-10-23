using System;
using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Match;
using ContingencyZero.Sim.Replay;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using Godot;

namespace ContingencyZero.Game.UI;

/// <summary>Pick a map, set up the players (you plus computer opponents and allies), and go.</summary>
public partial class SkirmishSetup : CanvasLayer
{
    private List<MapEntry> _maps = new();
    private MapData? _map;
    private ItemList _mapList = null!;
    private TextureRect _preview = null!;
    private Label _mapInfo = null!;
    private VBoxContainer _slots = null!;
    private OptionButton _resources = null!;
    private OptionButton _win = null!;
    private CheckBox _watch = null!;
    private Missions.SurvivalOptions _survival = null!;
    private readonly List<SlotRow> _rows = new();

    private static readonly string[] SlotTypes = { "You", "Easy AI", "Normal AI", "Hard AI", "Brutal AI", "Closed" };

    private sealed class SlotRow
    {
        public required OptionButton Type;
        public required OptionButton Faction;
        public required OptionButton Team;
        public required ColorRect Colour;
    }

    public override void _Ready()
    {
        Layer = 10;
        var root = new Control { Theme = UiTheme.Theme };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        var shade = new ColorRect { Color = new Color(0.035f, 0.035f, 0.03f, 0.8f) };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(shade);

        var outer = new VBoxContainer();
        outer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        outer.OffsetLeft = 60; outer.OffsetTop = 40; outer.OffsetRight = -60; outer.OffsetBottom = -40;
        outer.AddThemeConstantOverride("separation", 14);
        root.AddChild(outer);
        outer.AddChild(UiTheme.Label("SKIRMISH", 36, UiTheme.Ink));

        var cols = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        cols.AddThemeConstantOverride("separation", 20);
        outer.AddChild(cols);

        // Maps.
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(300, 0) };
        left.AddThemeConstantOverride("separation", 8);
        left.AddChild(UiTheme.Label("MAP", 15, UiTheme.Dim));
        _mapList = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _mapList.AddThemeFontSizeOverride("font_size", 17);
        _maps = GameData.Maps();
        foreach (MapEntry m in _maps) _mapList.AddItem(m.Name);
        _mapList.ItemSelected += i => SelectMap((int)i);
        left.AddChild(_mapList);
        var paste = UiTheme.Button("Paste map code", PasteMapCode, 0, 15);
        paste.TooltipText = "Add a map someone sent you as a share code (" + ShareCodes.MapPrefix + "...). It is saved in your maps.";
        paste.Name = "PasteMapCode";
        left.AddChild(paste);
        cols.AddChild(UiTheme.Panel(left));

        var middle = new VBoxContainer { CustomMinimumSize = new Vector2(320, 0) };
        middle.AddThemeConstantOverride("separation", 8);
        _preview = new TextureRect { CustomMinimumSize = new Vector2(300, 300), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
        middle.AddChild(_preview);
        _mapInfo = UiTheme.Label("", 15, UiTheme.Ink);
        _mapInfo.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _mapInfo.CustomMinimumSize = new Vector2(300, 0);
        middle.AddChild(_mapInfo);
        cols.AddChild(UiTheme.Panel(middle));

        // Players and options.
        var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 10);
        right.AddChild(UiTheme.Label("PLAYERS", 15, UiTheme.Dim));
        _slots = new VBoxContainer();
        _slots.AddThemeConstantOverride("separation", 6);
        right.AddChild(_slots);

        right.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });
        right.AddChild(UiTheme.Label("OPTIONS", 15, UiTheme.Dim));
        var opts = new GridContainer { Columns = 2 };
        opts.AddThemeConstantOverride("h_separation", 14);
        opts.AddThemeConstantOverride("v_separation", 8);
        opts.AddChild(UiTheme.Label("Starting resources", 16));
        _resources = new OptionButton();
        foreach (string r in new[] { "Standard (1,000)", "Boosted (2,500)", "Rich (5,000)" }) _resources.AddItem(r);
        opts.AddChild(_resources);
        opts.AddChild(UiTheme.Label("Victory", 16));
        _win = new OptionButton();
        _win.AddItem("Destroy the Commander");
        _win.AddItem("Annihilation (destroy everything)");
        opts.AddChild(_win);
        _survival = Missions.SurvivalOptions.AddTo(opts); // survival game type (game/src/Missions/Survival.cs)
        right.AddChild(opts);
        _watch = new CheckBox { Text = "Watch only: computer players in every seat, you spectate", Name = "WatchOnly" };
        _watch.TooltipText = "Spectate an AI battle with the casting panel (C), vision per player and speed controls";
        _watch.Toggled += on => ApplyWatch(on);
        right.AddChild(_watch);
        right.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        var note = UiTheme.Label("Brutal AI sees the whole map and earns 50% more.", 13, UiTheme.Dim);
        right.AddChild(note);
        cols.AddChild(UiTheme.Panel(right));

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddThemeConstantOverride("separation", 12);
        buttons.AddChild(UiTheme.Button("Back", GameRoot.Instance.ShowMainMenu, 140));
        var start = UiTheme.Button("Start battle", StartGame, 220, 20);
        start.AddThemeColorOverride("font_color", UiTheme.Amber);
        buttons.AddChild(start);
        outer.AddChild(buttons);

        int initial = Math.Max(0, _maps.FindIndex(m => m.Key == Settings.Current.LastMap));
        _mapList.Select(initial);
        SelectMap(initial);
    }

    /// <summary>Opens the paste box for a map code; the imported map is added to the list and selected.</summary>
    public void PasteMapCode()
    {
        string? file = null;
        AddChild(ShareCodeDialog.Paste("PASTE MAP CODE", "map", ShareCodes.MapPrefix, text =>
        {
            MapData map = ShareCodes.DecodeMap(text);
            file = ShareCodes.SaveMap(map);
            return $"Added \"{map.Name}\" to your maps";
        }, () =>
        {
            _maps = GameData.Maps();
            _mapList.Clear();
            foreach (MapEntry m in _maps) _mapList.AddItem(m.Name);
            int i = Math.Max(0, _maps.FindIndex(m => m.Key == "user:" + file));
            _mapList.Select(i);
            _mapList.EnsureCurrentIsVisible();
            SelectMap(i);
        }));
    }

    private void SelectMap(int index)
    {
        MapEntry entry = _maps[index];
        _map = entry.Load();
        var img = Minimap.RenderBase(_map, 256, 256 * _map.TilesY / _map.TilesX);
        // Mark start positions with their slot numbers' colours.
        for (int i = 0; i < _map.StartPositions.Count; i++)
        {
            var s = _map.StartPositions[i];
            int x = (int)(s.X.Raw / 65536f / _map.Width * img.GetWidth()), y = (int)(s.Y.Raw / 65536f / _map.Height * img.GetHeight());
            for (int dy = -4; dy <= 4; dy++)
            for (int dx = -4; dx <= 4; dx++)
                if (dx * dx + dy * dy <= 16 && x + dx >= 0 && y + dy >= 0 && x + dx < img.GetWidth() && y + dy < img.GetHeight())
                    img.SetPixel(x + dx, y + dy, UiTheme.TeamColour(i));
        }
        _preview.Texture = ImageTexture.CreateFromImage(img);
        _mapInfo.Text = $"{_map.Name}\n{_map.Description}\n\nSize {_map.Width} x {_map.Height} · Up to {_map.StartPositions.Count} players · {_map.MetalSpots.Count} metal spots · Wind {_map.WindMin.Raw / 65536}-{_map.WindMax.Raw / 65536}";
        Settings.Current.LastMap = entry.Key;
        BuildSlots(_map.StartPositions.Count);
    }

    private void BuildSlots(int count)
    {
        foreach (Node c in _slots.GetChildren()) c.QueueFree();
        _rows.Clear();
        for (int i = 0; i < count; i++)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var colour = new ColorRect { Color = UiTheme.TeamColour(i), CustomMinimumSize = new Vector2(18, 30) };
            row.AddChild(colour);
            row.AddChild(UiTheme.Label($"{i + 1}", 16));
            var type = new OptionButton { CustomMinimumSize = new Vector2(150, 0) };
            if (i == 0) foreach (string t in SlotTypes.Take(SlotTypes.Length - 1)) type.AddItem(t); // seat 1 is never closed
            else foreach (string t in SlotTypes.Skip(1)) type.AddItem(t);
            if (i > 0) type.Select(i == 1 ? 1 : 4); // second slot Normal AI, the rest closed
            var faction = new OptionButton { CustomMinimumSize = new Vector2(140, 0) };
            faction.AddItem("Lattice");
            faction.AddItem("Crucible");
            faction.Select(i == 0 ? (Settings.Current.LastFaction == "crucible" ? 1 : 0) : (i % 2));
            if (i == 0) faction.ItemSelected += f => Settings.Current.LastFaction = f == 1 ? "crucible" : "lattice";
            var team = new OptionButton { CustomMinimumSize = new Vector2(110, 0) };
            for (int t = 0; t < Math.Max(2, count); t++) team.AddItem($"Team {t + 1}");
            team.Select(i);
            row.AddChild(type);
            row.AddChild(faction);
            row.AddChild(team);
            _slots.AddChild(row);
            _rows.Add(new SlotRow { Type = type, Faction = faction, Team = team, Colour = colour });
            if (i == 0) type.ItemSelected += t => { if (_watch.ButtonPressed != (t != 0)) _watch.SetPressedNoSignal(t != 0); };
        }
        if (_watch.ButtonPressed) ApplyWatch(true);
    }

    /// <summary>Watch only: seat 1 becomes a Hard AI (and back to you when turned off).</summary>
    private void ApplyWatch(bool on)
    {
        if (_rows.Count == 0) return;
        OptionButton seat = _rows[0].Type;
        if (on && seat.Selected == 0) seat.Select(3);
        else if (!on) seat.Select(0);
    }

    private void StartGame()
    {
        if (_map is null) return;
        if (_survival.On) { _survival.Start(_map, _resources.Selected switch { 1 => 2500, 2 => 5000, _ => 1000 }, _rows[0].Faction.Selected == 1 ? "crucible" : "lattice", _rows.Skip(1).Count(r => r.Type.GetItemText(r.Type.Selected) != "Closed")); return; }
        var players = new List<PlayerSetup>();
        for (int i = 0; i < _rows.Count; i++)
        {
            SlotRow r = _rows[i];
            string kind = r.Type.GetItemText(r.Type.Selected);
            if (kind == "Closed") continue;
            AiDifficulty ai = kind switch
            {
                "Easy AI" => AiDifficulty.Easy,
                "Normal AI" => AiDifficulty.Normal,
                "Hard AI" => AiDifficulty.Hard,
                "Brutal AI" => AiDifficulty.Brutal,
                _ => AiDifficulty.None,
            };
            string faction = r.Faction.Selected == 1 ? "crucible" : "lattice";
            string name = kind == "You" ? Settings.Current.PlayerName : $"{kind} ({(faction == "lattice" ? "Lattice" : "Crucible")})";
            players.Add(new PlayerSetup(name, r.Team.Selected, faction, i, ai));
        }
        if (players.Select(p => p.Team).Distinct().Count() < 2)
        {
            Audio.Sfx.Play(Audio.SfxKind.Error);
            _mapInfo.Text += "\n\nYou need at least one opponent on another team.";
            return;
        }
        int res = _resources.Selected switch { 1 => 2500, 2 => 5000, _ => 1000 };
        var options = new MatchOptions(res, res, _win.Selected == 1 ? WinCondition.Annihilation : WinCondition.Commander);
        Settings.Current.Save();
        if (players.Any(p => p.Ai == AiDifficulty.None)) GameRoot.Instance.StartSkirmish(_map, players, options);
        else StartSpectating(_map, players, options);
    }

    /// <summary>An AI-only skirmish with nobody at the controls (LocalPlayer -1): the spectator bar and casting panel come up.</summary>
    public static void StartSpectating(MapData map, IReadOnlyList<PlayerSetup> players, MatchOptions options)
    {
        SimSession Make()
        {
            var scenario = new Scenario(map, (ulong)Time.GetTicksUsec(), players, options, new List<Spawn>());
            return new SimSession(scenario, GameData.Catalog, -1, new LocalChannel());
        }
        GameRoot.Instance.StartMatch(Make(), MatchKind.Skirmish, "Spectating", Make);
    }
}
