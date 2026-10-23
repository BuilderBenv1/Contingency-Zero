using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Match;
using ContingencyZero.Game.UI;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using ContingencyZero.Sim.Missions;
using ContingencyZero.Sim.Replay;
using Godot;
using FileAccess = Godot.FileAccess;

namespace ContingencyZero.Game.Missions;

/// <summary>Starting a single-player survival game. The rules are the sim's (Sim.Missions.SurvivalMode).</summary>
public static class SurvivalGame
{
    /// <summary>Is this match a survival match?</summary>
    public static bool IsSurvival(World world) => world.Mission is { Mode: SurvivalMode.ModeKey };

    /// <summary>
    /// You (and any computer allies, who stand in for extra defenders) against the waves.
    /// <paramref name="allies"/> extra defenders are played by the computer.
    /// </summary>
    public static void Start(MapData map, AiDifficulty difficulty, int startResources, string faction, int allies = 0)
    {
        SimSession Make()
        {
            var defenders = new List<PlayerSetup> { new(Settings.Current.PlayerName, 0, faction, 0) };
            int count = Math.Min(SurvivalMode.Capacity(map), 1 + Math.Max(0, allies));
            for (int i = 1; i < count; i++)
            {
                string f = i % 2 == 1 ? (faction == "lattice" ? "crucible" : "lattice") : faction;
                defenders.Add(new PlayerSetup($"Ally {i} (AI)", 0, f, i, AiDifficulty.Hard));
            }
            int seed = (int)(Time.GetTicksUsec() & 0x7fffffff);
            Scenario scenario = SurvivalMode.CreateScenario(map, defenders, difficulty, seed, startResources: startResources);
            return new SimSession(scenario, GameData.Catalog, 0, new LocalChannel());
        }
        GameRoot.Instance.StartMatch(Make(), MatchKind.Skirmish, "Survival", Make);
    }
}

/// <summary>
/// Best survival scores, one per map (by map name), in user://survival_best.json. A match is
/// recorded once, when it ends; the best keeps the highest points.
/// </summary>
public static class SurvivalScores
{
    private const string FilePath = "user://survival_best.json";
    private static Dictionary<string, Entry>? _best;
    // Matches already recorded, and what the best was before them (to say "new best").
    private static readonly ConditionalWeakTable<World, Result> Recorded = new();

    public sealed class Entry
    {
        public int Points { get; set; }
        public int Waves { get; set; }
        public int Seconds { get; set; }
        public string Difficulty { get; set; } = "";
        public int Players { get; set; } = 1;
        public string Date { get; set; } = "";
    }

    /// <summary>A finished match's score, the map's best (after recording it), and whether this set it.</summary>
    public sealed record Result(SurvivalMode.SurvivalScore Score, Entry? Best, bool NewBest);

    private static Dictionary<string, Entry> Best
    {
        get
        {
            if (_best is not null) return _best;
            _best = new Dictionary<string, Entry>(StringComparer.Ordinal);
            if (FileAccess.FileExists(FilePath))
            {
                try
                {
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, Entry>>(FileAccess.GetFileAsString(FilePath));
                    if (loaded is not null) foreach (var (k, v) in loaded) _best[k] = v;
                }
                catch (JsonException) { }
            }
            return _best;
        }
    }

    public static Entry? BestFor(string mapName) => Best.TryGetValue(mapName, out Entry? e) ? e : null;

    /// <summary>
    /// Records a finished survival match (once; later calls return the same result). Spectators and
    /// replays pass <paramref name="save"/> false: they see the score without it counting as theirs.
    /// </summary>
    public static Result? Record(World world, bool save = true)
    {
        if (!world.IsOver || SurvivalMode.Score(world) is not { } score) return null;
        if (Recorded.TryGetValue(world, out Result? done)) return done;
        string map = world.Map.Name;
        Entry? before = BestFor(map);
        bool better = before is null || score.Points > before.Points;
        Result result;
        if (save && better)
        {
            var e = new Entry
            {
                Points = score.Points,
                Waves = score.Waves,
                Seconds = score.Seconds,
                Difficulty = score.Difficulty,
                Players = world.Mission!.HumanPlayers.Count,
                Date = DateTime.Now.ToString("yyyy-MM-dd"),
            };
            Best[map] = e;
            Save();
            result = new Result(score, e, true);
        }
        else result = new Result(score, before, false);
        Recorded.AddOrUpdate(world, result);
        return result;
    }

    private static void Save()
    {
        using var f = FileAccess.Open(FilePath, FileAccess.ModeFlags.Write);
        f?.StoreString(JsonSerializer.Serialize(Best.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => kv.Value)));
    }

    public static string Clock(int seconds) => $"{seconds / 60}:{seconds % 60:D2}";

    /// <summary>One line for an entry: "1,234 pts · wave 12 · 14:05 · Hard".</summary>
    public static string Describe(Entry e) =>
        $"{e.Points:N0} pts  ·  {e.Waves} waves  ·  {Clock(e.Seconds)}  ·  {e.Difficulty}{(e.Players > 1 ? $"  ·  {e.Players} players" : "")}";

    /// <summary>Lines for the game-over screen, or null if the match isn't survival.</summary>
    public static string? GameOverText(World world, bool save)
    {
        if (Record(world, save) is not { } r) return null;
        string head = $"SURVIVED {r.Score.Waves} WAVES  ·  {Clock(r.Score.Seconds)}  ·  {r.Score.Points:N0} POINTS ({r.Score.Difficulty})";
        string best = r.NewBest ? "New best on this map!" : r.Best is { } b ? $"Best on {world.Map.Name}: {Describe(b)}" : "";
        return best.Length > 0 ? head + "\n" + best : head;
    }
}

/// <summary>
/// The survival read-out at the top of the screen: wave counter, countdown to the next wave, time
/// survived, score and the map's best. Display only: reads the sim, never changes it.
/// </summary>
public partial class SurvivalBanner : PanelContainer
{
    private readonly SimSession _session;
    private Label _wave = null!;
    private Label _next = null!;
    private Label _score = null!;
    private Label _best = null!;
    private int _shownSecond = -1;
    private int _flashWave;
    private double _flash;

    public SurvivalBanner(SimSession session) { _session = session; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.06f, 0.05f, 0.04f, 0.86f), new Color(0.85f, 0.45f, 0.2f), 2, 4, 8));
        AnchorLeft = 0.5f; AnchorRight = 0.5f;
        OffsetLeft = -290; OffsetRight = 290; OffsetTop = 100; OffsetBottom = 168;
        var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 0);
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 26);
        _wave = UiTheme.Label("WAVE 0", 26, UiTheme.Amber);
        _next = UiTheme.Label("", 18, UiTheme.Ink);
        _score = UiTheme.Label("", 18, UiTheme.Ink);
        foreach (Label l in new[] { _wave, _next, _score })
        {
            l.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(l);
        }
        col.AddChild(row);
        _best = UiTheme.Label("", 13, UiTheme.Dim, HorizontalAlignment.Center);
        col.AddChild(_best);
        AddChild(col);
        string map = _session.World.Map.Name;
        _best.Text = SurvivalScores.BestFor(map) is { } b ? $"Best on {map}: {SurvivalScores.Describe(b)}" : $"No survival score on {map} yet";
    }

    public override void _Process(double delta)
    {
        World w = _session.World;
        if (w.Mission is not { } m) return;
        if (m.Wave != _flashWave)
        {
            _flashWave = m.Wave;
            _flash = 1.5;
        }
        if (_flash > 0)
        {
            _flash -= delta;
            float t = (float)Math.Max(0, _flash / 1.5);
            _wave.AddThemeColorOverride("font_color", UiTheme.Amber.Lerp(UiTheme.Bad, t));
        }
        int second = w.Tick / World.TicksPerSecond;
        if (second == _shownSecond && !w.IsOver) return;
        _shownSecond = second;
        _wave.Text = $"WAVE {m.Wave}";
        if (w.IsOver)
        {
            _next.Text = "Overrun";
            _next.AddThemeColorOverride("font_color", UiTheme.Bad);
        }
        else
        {
            int next = m.NextPeriodicTick;
            int left = next < 0 ? 0 : Math.Max(0, (next - w.Tick + World.TicksPerSecond - 1) / World.TicksPerSecond);
            _next.Text = $"Next wave {SurvivalScores.Clock(left)}";
            _next.AddThemeColorOverride("font_color", left <= 10 ? UiTheme.Bad : UiTheme.Ink);
        }
        if (SurvivalMode.Score(w) is { } s)
            _score.Text = $"Held {SurvivalScores.Clock(s.Seconds)}  ·  {s.Points:N0} pts";
    }
}

/// <summary>
/// The survival rows in the skirmish setup's options (kept here so the setup screen needs only a
/// couple of lines): game type and wave difficulty. Other seats that aren't closed become computer
/// allies defending alongside you.
/// </summary>
public sealed class SurvivalOptions
{
    private static readonly AiDifficulty[] Difficulties = { AiDifficulty.Easy, AiDifficulty.Normal, AiDifficulty.Hard, AiDifficulty.Brutal };
    private OptionButton _type = null!;
    private OptionButton _difficulty = null!;
    private Label _difficultyLabel = null!;
    private Label _note = null!;

    public bool On => _type.Selected == 1;
    public AiDifficulty Difficulty => Difficulties[Math.Clamp(_difficulty.Selected, 0, Difficulties.Length - 1)];

    public static SurvivalOptions AddTo(GridContainer grid)
    {
        var o = new SurvivalOptions();
        var typeLabel = UiTheme.Label("Game type", 16);
        typeLabel.Name = "GameTypeLabel";
        grid.AddChild(typeLabel);
        o._type = new OptionButton { Name = "GameType" };
        o._type.AddItem("Battle");
        o._type.AddItem("Survival (hold out against waves)");
        grid.AddChild(o._type);
        o._difficultyLabel = UiTheme.Label("Wave difficulty", 16);
        grid.AddChild(o._difficultyLabel);
        o._difficulty = new OptionButton { Name = "WaveDifficulty" };
        foreach (AiDifficulty d in Difficulties) o._difficulty.AddItem($"{d} (a wave every {SurvivalMode.WaveSeconds(d)} s)");
        o._difficulty.Select(1);
        grid.AddChild(o._difficulty);
        grid.AddChild(new Control());
        o._note = UiTheme.Label("Survival: seats that are not Closed become computer allies defending with you (close seat 2 to hold out alone). The waves are the enemy; Victory is ignored.", 13, UiTheme.Dim);
        o._note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        o._note.CustomMinimumSize = new Vector2(300, 0);
        grid.AddChild(o._note);
        o._type.ItemSelected += _ => o.Sync();
        o.Sync();
        return o;
    }

    /// <summary>Turns survival on and picks a difficulty (dev automation).</summary>
    public void Set(bool on, AiDifficulty difficulty)
    {
        _type.Select(on ? 1 : 0);
        _difficulty.Select(Math.Max(0, Array.IndexOf(Difficulties, difficulty)));
        Sync();
    }

    private void Sync()
    {
        _difficultyLabel.Visible = _difficulty.Visible = _note.Visible = On;
    }

    public void Start(MapData map, int startResources, string faction, int allies)
    {
        Settings.Current.Save();
        SurvivalGame.Start(map, Difficulty, startResources, faction, allies);
    }
}
