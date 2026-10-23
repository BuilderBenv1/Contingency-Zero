using System;
using System.Collections.Generic;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Match;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.State;
using Godot;

namespace ContingencyZero.Game.UI;

/// <summary>
/// Broadcast-style panel for replays and spectators: one card per player (name in team colour,
/// metal and energy income, army value, unit count, kills and a sparkline of army value over the
/// last five minutes) plus a mini graph of everybody's income. Toggled with C or the Casting
/// button on the replay bar. Samples the sim every two seconds through <see cref="SimSession.Stepped"/>,
/// so it keeps up while a replay seeks to a bookmark.
/// </summary>
public partial class CastingOverlay : CanvasLayer
{
    /// <summary>Ticks between samples (2 seconds).</summary>
    public const int SampleTicks = 2 * World.TicksPerSecond;
    /// <summary>Samples kept: five minutes.</summary>
    public const int History = 150;
    /// <summary>Where the panel sits below the resource bar when there is no replay bar above it.</summary>
    private const int TopWithoutReplayBar = 56;

    private readonly MatchScene _match;
    private Control _root = null!;
    private PanelContainer _panel = null!;
    private PlayerCard[] _cards = Array.Empty<PlayerCard>();
    private EconomyGraph _graph = null!;
    private bool _shown;
    private bool _built;
    private int _lastSampleBucket = -1;
    private int _lastShownTick = -1;

    /// <summary>Army value (metal cost of mobile armed units) per player, oldest first.</summary>
    private readonly List<float>[] _army;
    private readonly List<float>[] _metalIncome;
    private readonly List<float>[] _energyIncome;

    public CastingOverlay(MatchScene match)
    {
        _match = match;
        int n = match.Session.World.Players.Length;
        _army = new List<float>[n];
        _metalIncome = new List<float>[n];
        _energyIncome = new List<float>[n];
        for (int p = 0; p < n; p++)
        {
            _army[p] = new List<float>(History + 1);
            _metalIncome[p] = new List<float>(History + 1);
            _energyIncome[p] = new List<float>(History + 1);
        }
    }

    /// <summary>Whether the panel is on screen.</summary>
    public bool Shown
    {
        get => _shown;
        set
        {
            _shown = value;
            if (_built) { _panel.Visible = value; if (value) RefreshNow(); }
        }
    }

    public void Toggle() => Shown = !Shown;

    /// <summary>Moves the panel's top edge (the replay / spectator bar calls this to sit it underneath).</summary>
    public void SetTop(float top)
    {
        _top = top;
        if (_built) _panel.OffsetTop = top;
    }

    private float _top = -1;

    public override void _Ready()
    {
        Layer = 14; // under the replay bar (15) and modal panels, over the HUD (default 1)
        _root = new Control { Theme = UiTheme.Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        World world = _match.Session.World;
        int n = world.Players.Length;
        bool compact = n > 4;

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", compact ? 8 : 14);
        _panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _panel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.04f, 0.05f, 0.07f, 0.9f), UiTheme.PanelEdge, 1, 0, compact ? 6 : 8));
        _panel.AnchorLeft = 0; _panel.AnchorRight = 1;
        _panel.OffsetLeft = 12; _panel.OffsetRight = -12;
        _panel.OffsetTop = _top >= 0 ? _top : _match.Kind == MatchKind.Replay ? ReplayControls.PanelBottom + 6 : TopWithoutReplayBar;
        _panel.AddChild(row);
        _root.AddChild(_panel);

        _cards = new PlayerCard[n];
        for (int p = 0; p < n; p++)
        {
            _cards[p] = new PlayerCard(world.Players[p], compact);
            _cards[p].SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(_cards[p]);
        }

        _graph = new EconomyGraph(world, _metalIncome, _energyIncome, compact);
        row.AddChild(_graph);

        _panel.Visible = _shown;
        _built = true;
        _match.Session.Stepped += OnStepped;
        OnStepped();
        RefreshNow();
    }

    public override void _ExitTree()
    {
        _match.Session.Stepped -= OnStepped;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.C, CtrlPressed: false, AltPressed: false })
        {
            Toggle();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Every tick of the sim (also during replay seeks): sample statistics every two seconds.</summary>
    private void OnStepped()
    {
        World world = _match.Session.World;
        int bucket = world.Tick / SampleTicks;
        if (bucket == _lastSampleBucket) return;
        _lastSampleBucket = bucket;

        int n = world.Players.Length;
        Span<float> army = stackalloc float[n];
        UnitStore u = world.Units;
        UnitCatalog catalog = world.Catalog;
        for (int id = 0; id < u.Count; id++)
        {
            if (!u.Alive[id] || !u.Complete[id]) continue;
            UnitDef def = catalog[u.TypeIndex[id]];
            if (def.CanMove && def.IsArmed) army[u.Owner[id]] += def.MetalCost.Raw / 65536f;
        }
        for (int p = 0; p < n; p++)
        {
            PlayerState ps = world.Players[p];
            Push(_army[p], army[p]);
            Push(_metalIncome[p], ps.MetalIncome.Raw / 65536f * World.TicksPerSecond);
            Push(_energyIncome[p], ps.EnergyIncome.Raw / 65536f * World.TicksPerSecond);
        }
    }

    private static void Push(List<float> series, float value)
    {
        series.Add(value);
        if (series.Count > History) series.RemoveAt(0);
    }

    public override void _Process(double delta)
    {
        if (!_built || !_shown) return;
        int tick = _match.Session.World.Tick;
        if (tick == _lastShownTick) return;
        _lastShownTick = tick;
        RefreshNow();
    }

    private void RefreshNow()
    {
        if (!_built) return;
        World world = _match.Session.World;
        int n = world.Players.Length;
        Span<float> army = stackalloc float[n];
        Span<int> count = stackalloc int[n];
        UnitStore u = world.Units;
        UnitCatalog catalog = world.Catalog;
        for (int id = 0; id < u.Count; id++)
        {
            if (!u.Alive[id] || !u.Complete[id]) continue;
            UnitDef def = catalog[u.TypeIndex[id]];
            count[u.Owner[id]]++;
            if (def.CanMove && def.IsArmed) army[u.Owner[id]] += def.MetalCost.Raw / 65536f;
        }
        for (int p = 0; p < n; p++)
            _cards[p].Show(world.Players[p], army[p], count[p], _army[p], world.WinningTeam);
        _graph.QueueRedraw();
    }

    /// <summary>One player's numbers and army sparkline.</summary>
    private partial class PlayerCard : VBoxContainer
    {
        private readonly Label _name;
        private readonly Label _income;
        private readonly Label _army;
        private readonly Sparkline _spark;
        private readonly Color _colour;

        public PlayerCard(PlayerState player, bool compact)
        {
            MouseFilter = MouseFilterEnum.Ignore;
            AddThemeConstantOverride("separation", compact ? 0 : 1);
            _colour = UiTheme.TeamColour(player.Id);
            _name = UiTheme.Label(player.Name, compact ? 14 : 17, _colour);
            _name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            AddChild(_name);
            _income = UiTheme.Label("", compact ? 12 : 14, UiTheme.Ink);
            _income.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            AddChild(_income);
            _army = UiTheme.Label("", compact ? 12 : 14, UiTheme.Dim);
            TooltipText = "Army: metal value of armed mobile units. Killed / lost: units destroyed by and of this player.";
            _army.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            AddChild(_army);
            _spark = new Sparkline(_colour) { CustomMinimumSize = new Vector2(compact ? 90 : 140, compact ? 22 : 30), MouseFilter = MouseFilterEnum.Ignore };
            _spark.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddChild(_spark);
        }

        public void Show(PlayerState p, float army, int units, List<float> armyHistory, int? winningTeam)
        {
            string tag = p.Defeated ? "  (out)" : winningTeam is { } t && t == p.Team ? "  (winner)" : "";
            string name = $"{p.Name}{tag}";
            if (_name.Text != name) _name.Text = name;
            _name.AddThemeColorOverride("font_color", p.Defeated ? UiTheme.Dim : _colour);
            float m = p.MetalIncome.Raw / 65536f * World.TicksPerSecond, e = p.EnergyIncome.Raw / 65536f * World.TicksPerSecond;
            string income = $"M +{m:0}/s   E +{e:0}/s";
            if (_income.Text != income) _income.Text = income;
            string armyText = $"Army {army:0}  ·  {units} units  ·  {p.Kills} killed / {p.UnitsLost} lost";
            if (_army.Text != armyText) _army.Text = armyText;
            _spark.Samples = armyHistory;
            _spark.Current = army;
            _spark.QueueRedraw();
        }
    }

    /// <summary>A small filled line chart of one series, scaled to its own maximum.</summary>
    private partial class Sparkline : Control
    {
        private readonly Color _colour;
        public List<float>? Samples;
        public float Current;

        public Sparkline(Color colour) { _colour = colour; }

        public override void _Draw()
        {
            Vector2 size = Size;
            DrawRect(new Rect2(Vector2.Zero, size), new Color(0, 0, 0, 0.35f));
            DrawRect(new Rect2(Vector2.Zero, size), new Color(UiTheme.PanelEdge, 0.5f), false, 1);
            if (Samples is null || Samples.Count == 0) return;
            float max = 1;
            foreach (float v in Samples) max = Math.Max(max, v);
            max = Math.Max(max, Current);
            int n = Samples.Count;
            // The window is always five minutes wide; a young match fills from the left.
            float stepX = (size.X - 4) / Math.Max(1, History - 1);
            var points = new Vector2[Math.Max(2, n)];
            for (int i = 0; i < n; i++)
                points[i] = new Vector2(2 + i * stepX, size.Y - 2 - (size.Y - 4) * Samples[i] / max);
            if (n == 1) points[1] = points[0] + new Vector2(stepX, 0);
            // Fill under the line.
            var poly = new Vector2[points.Length + 2];
            Array.Copy(points, poly, points.Length);
            poly[points.Length] = new Vector2(points[^1].X, size.Y - 2);
            poly[points.Length + 1] = new Vector2(points[0].X, size.Y - 2);
            DrawColoredPolygon(poly, new Color(_colour, 0.18f));
            DrawPolyline(points, _colour, 1.5f, true);
            // Peak label, tiny.
            Font font = UiTheme.Theme.DefaultFont ?? ThemeDB.FallbackFont;
            DrawString(font, new Vector2(4, 11), $"{max:0}", HorizontalAlignment.Left, -1, 10, new Color(UiTheme.Dim, 0.8f));
        }
    }

    /// <summary>Everybody's metal and energy income over the last five minutes, one line per player.</summary>
    private partial class EconomyGraph : Control
    {
        private readonly World _world;
        private readonly List<float>[] _metal;
        private readonly List<float>[] _energy;
        private readonly bool _compact;

        public EconomyGraph(World world, List<float>[] metal, List<float>[] energy, bool compact)
        {
            _world = world;
            _metal = metal;
            _energy = energy;
            _compact = compact;
            MouseFilter = MouseFilterEnum.Ignore;
            CustomMinimumSize = new Vector2(compact ? 200 : 260, compact ? 78 : 96);
        }

        public override void _Draw()
        {
            Font font = UiTheme.Theme.DefaultFont ?? ThemeDB.FallbackFont;
            float h = (Size.Y - 4) / 2;
            DrawSeries(new Rect2(0, 0, Size.X, h), _metal, "Metal income", UiTheme.Metal, font);
            DrawSeries(new Rect2(0, h + 4, Size.X, h), _energy, "Energy income", UiTheme.Energy, font);
        }

        private void DrawSeries(Rect2 area, List<float>[] series, string title, Color titleColour, Font font)
        {
            DrawRect(area, new Color(0, 0, 0, 0.35f));
            DrawRect(area, new Color(UiTheme.PanelEdge, 0.5f), false, 1);
            float max = 1;
            foreach (List<float> s in series) foreach (float v in s) max = Math.Max(max, v);
            float stepX = (area.Size.X - 4) / Math.Max(1, History - 1);
            for (int p = 0; p < series.Length; p++)
            {
                List<float> s = series[p];
                if (s.Count < 2) continue;
                var points = new Vector2[s.Count];
                for (int i = 0; i < s.Count; i++)
                    points[i] = new Vector2(area.Position.X + 2 + i * stepX, area.Position.Y + area.Size.Y - 2 - (area.Size.Y - 4) * s[i] / max);
                DrawPolyline(points, _world.Players[p].Defeated ? new Color(UiTheme.TeamColour(p), 0.4f) : UiTheme.TeamColour(p), 1.5f, true);
            }
            int fontSize = _compact ? 10 : 11;
            DrawString(font, area.Position + new Vector2(4, fontSize + 1), $"{title}  (peak {max:0}/s)", HorizontalAlignment.Left, -1, fontSize, titleColour);
        }
    }
}
