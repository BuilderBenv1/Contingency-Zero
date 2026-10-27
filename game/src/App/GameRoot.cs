using System;
using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Game.Audio;
using ContingencyZero.Game.Match;
using ContingencyZero.Game.UI;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using ContingencyZero.Sim.Replay;
using Godot;

namespace ContingencyZero.Game.App;

/// <summary>
/// The root of the game. Owns the current screen (menus, a match, the map editor), the music,
/// the sound pool and the icon renderer, and moves between them.
/// </summary>
public partial class GameRoot : Node
{
    public static GameRoot Instance { get; private set; } = null!;

    private Node? _screen;          // current UI screen (CanvasLayer)
    private MatchScene? _match;     // current match, including the menu backdrop
    private Func<SimSession>? _restart;
    private DevAutomation? _dev;

    public override void _Ready()
    {
        Instance = this;
        GetTree().Root.Title = "Contingency Zero";
        RenderingServer.SetDefaultClearColor(new Color(0.035f, 0.035f, 0.03f));
        Settings.Current.Apply();
        Sfx.Init(this);
        var music = new MusicPlayer { Name = "Music" };
        AddChild(music);
        MusicHost.Register(music);
        AddChild(new IconFactory { Name = "Icons" });
        AddChild(new Controls.MenuNavigator { Name = "MenuNavigator" }); // controller navigation for every menu
        Settings.Current.Apply();

        _dev = DevAutomation.FromCommandLine(this);
        if (_dev is not null)
        {
            AddChild(_dev);
            return;
        }
        if (ReplayFiles.OpenFromCommandLine()) return; // a .czr on the command line (double-clicked file) plays straight away
        ShowMainMenu();
    }

    // ---- Screens -----------------------------------------------------------------------------

    private void SetScreen(Node? screen)
    {
        _screen?.QueueFree();
        _screen = screen;
        if (screen is not null) AddChild(screen);
    }

    public void ShowMainMenu()
    {
        EnsureMenuBackdrop();
        MusicHost.Play(MusicPlayer.Track.Menu);
        SetScreen(new MainMenu());
    }

    public void ShowSkirmishSetup() => SetScreen(new SkirmishSetup());
    public void ShowCampaign() => SetScreen(new CampaignScreen());
    public void ShowReplays() => SetScreen(new ReplayBrowser());
    public void ShowMultiplayer() => SetScreen(new MultiplayerLobby());

    public void ShowEditor()
    {
        EndMatch();
        SetScreen(null);
        var editor = new MapEditor.MapEditorScene();
        SetScreen(editor);
        editor.Exited += ShowMainMenu;
    }

    private bool _backdropLoading;
    private System.Threading.Tasks.Task? _backdropTask;
    private volatile bool _shuttingDown;

    /// <summary>
    /// An AI-versus-AI battle behind the main menu. It is fast-forwarded a few minutes on a worker
    /// thread (so the menu appears at once) and faded in when ready.
    /// </summary>
    private void EnsureMenuBackdrop()
    {
        if (_match is not null && _match.Kind == MatchKind.MenuBackground) return;
        if (_backdropLoading) return;
        EndMatch();
        var maps = GameData.Maps().Where(m => m.BuiltIn).ToList();
        MapData map = (maps.FirstOrDefault(m => m.Key == "frostline") ?? maps[0]).Load();
        var players = new List<PlayerSetup>();
        for (int i = 0; i < Math.Min(4, map.StartPositions.Count); i++)
            players.Add(new PlayerSetup($"AI {i + 1}", i, i % 2 == 0 ? "lattice" : "crucible", i, AiDifficulty.Hard));
        ulong seed = (ulong)Time.GetTicksUsec();
        var scenario = new Scenario(map, seed, players, new MatchOptions(StartMetal: 3000, StartEnergy: 3000, RevealMap: true), new List<Spawn>());
        var session = new SimSession(scenario, GameData.Catalog, -1, new LocalChannel()) { Speed = 1.0 };

        _backdropLoading = true;
        _backdropTask = System.Threading.Tasks.Task.Run(() =>
        {
            // Fast-forward a few minutes so there's a battle to look at. Nothing else touches
            // this world until it's handed to the scene below.
            for (int i = 0; i < 6 * 60 * 15 && !_shuttingDown; i++) session.World.Step();
        });
        _backdropTask.ContinueWith(_ =>
        {
            if (_shuttingDown) return;
            Callable.From(() =>
        {
            _backdropLoading = false;
            // Only show it if the player is still in the menus.
            if (_match is null && _screen is CanvasLayer && IsInsideTree() && !_shuttingDown)
                StartMatch(session, MatchKind.MenuBackground, "Backdrop", null);
        }).CallDeferred();
        });
    }

    public override void _ExitTree()
    {
        // Stop the backdrop worker before the engine tears down.
        _shuttingDown = true;
        _backdropTask?.Wait(3000);
    }

    private void EndMatch()
    {
        if (_match is null) return;
        _match.QueueFree();
        _match = null;
    }

    /// <summary>Starts a match. <paramref name="restart"/> rebuilds the same setup for "play again".</summary>
    public MatchScene StartMatch(SimSession session, MatchKind kind, string title, Func<SimSession>? restart)
    {
        EndMatch();
        if (kind != MatchKind.MenuBackground) SetScreen(null);
        _restart = restart;
        var match = new MatchScene { Name = "Match" };
        AddChild(match);
        MoveChild(match, 0);
        match.Start(session, kind, title);
        match.Exited += () =>
        {
            EndMatch();
            if (_afterMatch is { } after) { _afterMatch = null; after(); return; }
            ShowMainMenu();
        };
        match.RestartRequested += () =>
        {
            if (kind == MatchKind.MenuBackground)
            {
                EndMatch();
                EnsureMenuBackdrop();
                return;
            }
            if (_restart is null) { EndMatch(); ShowMainMenu(); return; }
            var again = _restart();
            StartMatch(again, kind, title, _restart);
        };
        _match = match;
        return match;
    }

    public void StartSkirmish(MapData map, IReadOnlyList<PlayerSetup> players, MatchOptions options, ulong seed = 0)
    {
        SimSession Make()
        {
            var scenario = new Scenario(map, seed != 0 ? seed : (ulong)Time.GetTicksUsec(), players, options, new List<Spawn>());
            return new SimSession(scenario, GameData.Catalog, 0, new LocalChannel());
        }
        StartMatch(Make(), MatchKind.Skirmish, "Skirmish", Make);
    }

    /// <summary>Set by the map editor's test play: what to open when that match is left, instead of the main menu.</summary>
    private Action? _afterMatch;

    /// <summary>True while a map-editor test play is running (leaving it goes back to the editor).</summary>
    public bool ReturnsToEditor => _afterMatch is not null;

    /// <summary>A skirmish started from the map editor. Leaving the match calls <paramref name="afterMatch"/> (back to the editor).</summary>
    public void StartTestPlay(MapData map, IReadOnlyList<PlayerSetup> players, MatchOptions options, Action afterMatch)
    {
        StartSkirmish(map, players, options);
        _afterMatch = afterMatch;
    }

    public void StartMission(Missions.MissionInfo mission)
    {
        SimSession Make() => new(mission.BuildScenario(), GameData.Catalog, mission.HumanPlayer, new LocalChannel());
        MatchScene m = StartMatch(Make(), MatchKind.Mission, mission.Title, Make);
        Missions.CampaignProgress.Track(m, mission);
    }

    /// <summary>Watches a replay. <paramref name="startTick"/> runs the sim ahead to that tick first (bookmark jumps).</summary>
    public void StartReplay(MatchReplay replay, string title, int startTick = 0)
    {
        var session = new SimSession(replay.Scenario, GameData.Catalog, -1, new ReplayChannel(replay));
        MatchScene m = StartMatch(session, MatchKind.Replay, title, null);
        m.AddChild(new ReplayControls(m, replay));
        if (startTick > 0) session.SeekTo(startTick);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) Settings.Current.Save();
    }
}
