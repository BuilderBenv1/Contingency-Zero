using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Audio;
using ContingencyZero.Game.UI;
using ContingencyZero.Game.View;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Replay;
using Godot;

namespace ContingencyZero.Game.Match;

/// <summary>What kind of match this is, for menus and the end screen.</summary>
public enum MatchKind { Skirmish, Mission, Multiplayer, Replay, MenuBackground }

/// <summary>
/// A running match: the sim session plus everything that draws it and takes the player's input.
/// Built in code by <see cref="Start"/>; tells its owner when the player leaves via <see cref="Exited"/>.
/// </summary>
public partial class MatchScene : Node3D
{
    public SimSession Session { get; private set; } = null!;
    public MatchKind Kind { get; private set; }
    public string Title { get; private set; } = "";

    public TerrainView Terrain { get; private set; } = null!;
    public UnitView Units { get; private set; } = null!;
    public FeatureView Features { get; private set; } = null!;
    public EffectsView Effects { get; private set; } = null!;
    public OverlayView Overlay { get; private set; } = null!;
    public CameraRig Rig { get; private set; } = null!;
    public InputController Input { get; private set; } = null!;
    public Hud? Hud { get; private set; }
    public Selection Selection { get; } = new();
    /// <summary>Moments worth jumping to, saved into the replay (see <see cref="ReplayBookmarks"/>).</summary>
    public ReplayBookmarks Bookmarks { get; } = new();

    /// <summary>Raised when the player quits back to the menu.</summary>
    public event Action? Exited;
    /// <summary>Raised when the player asks to play the same setup again.</summary>
    public event Action? RestartRequested;

    private PauseMenu? _pause;
    private GameOverPanel? _gameOver;
    private int _fogTimer;
    private bool _endHandled;

    /// <summary>Skips the opening tips (set by dev automation for clean screenshots).</summary>
    public static bool SuppressHints { get; set; }
    private int _viewerTeam;

    public void Start(SimSession session, MatchKind kind, string title)
    {
        Session = session;
        Kind = kind;
        Title = title;
        World world = session.World;
        _viewerTeam = session.LocalPlayer >= 0 && kind != MatchKind.Replay ? world.Players[session.LocalPlayer].Team : -1;

        var env = new Godot.Environment();
        var sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-52, -38, 0),
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 600,
        };
        TerrainView.ConfigureLighting(env, sun, world.Map.Theme);
        AddChild(new WorldEnvironment { Environment = env });
        AddChild(sun);

        Terrain = new TerrainView { Name = "Terrain" };
        AddChild(Terrain);
        Terrain.Build(world.Map);

        Rig = new CameraRig { Name = "Camera", MapWidth = world.Map.Width, MapHeight = world.Map.Height };
        AddChild(Rig);

        Units = new UnitView { Name = "Units" };
        AddChild(Units);
        Units.Init(world, Terrain, _viewerTeam);

        Features = new FeatureView { Name = "Features" };
        AddChild(Features);
        Features.Init(world, Terrain, _viewerTeam);

        Overlay = new OverlayView { Name = "Overlay" };
        AddChild(Overlay);

        Effects = new EffectsView { Name = "Effects" };
        AddChild(Effects);
        AddChild(MakeVignette());
        if (RetroLook.Enabled) AddChild(RetroLook.MakeLayer());

        // Start looking at our own Commander (or the middle when watching).
        Vector3 focus = new(world.Map.Width / 2f, 0, world.Map.Height / 2f);
        if (session.LocalPlayer >= 0)
        {
            for (int id = 0; id < world.Units.Count; id++)
                if (world.Units.Owner[id] == session.LocalPlayer && world.Catalog[world.Units.TypeIndex[id]].IsCommander)
                    focus = Units.PositionOf(id, 1);
        }
        Rig.Focus = focus;
        Rig.SetZoom(kind == MatchKind.MenuBackground ? 170 : 115);
        Rig.InputEnabled = kind != MatchKind.MenuBackground;

        Overlay.Init(world, Terrain, Units, Selection, Rig.Camera, session.LocalPlayer, _viewerTeam);
        Effects.Init(world, Terrain, Units, Rig.Camera, () => Rig.Focus, _viewerTeam, session.LocalPlayer);

        Input = new InputController { Name = "Input" };
        AddChild(Input);
        Input.Init(session, Rig, Terrain, Units, Overlay, Selection);

        if (kind != MatchKind.MenuBackground)
        {
            Hud = new Hud { Name = "Hud" };
            AddChild(Hud);
            Hud.Init(session, Input, Selection, Rig, Terrain);
            Hud.MenuRequested += TogglePause;
            if (session.LocalPlayer < 0) AddChild(new CastingOverlay(this) { Name = "Casting" }); // replays and spectators: broadcast panel (key C)
            // Watching a local AI-vs-AI skirmish: the spectator bar (speed, vision, casting). Replays add their own.
            if (session.LocalPlayer < 0 && kind != MatchKind.Replay && !session.IsMultiplayer) AddChild(new ReplayControls(this) { Name = "SpectatorControls" });
            if (world.Mission is { } m)
            {
                Hud.Post(m.Title, UiTheme.Amber, 8);
            }
            else if ((kind == MatchKind.Skirmish || kind == MatchKind.Multiplayer) && !SuppressHints && session.LocalPlayer >= 0)
            {
                Hud.Post("Build metal extractors on the dark patches of metal ore, then energy, then a factory.", UiTheme.Accent, 12);
                Hud.Post("Destroy the enemy Commander to win. Keep yours alive.", UiTheme.Accent, 12);
            }
        }

        // Render build-menu portraits for our side in the background so menus open fully drawn.
        if (session.LocalPlayer >= 0 && kind != MatchKind.MenuBackground)
        {
            string faction = world.Players[session.LocalPlayer].Faction;
            Color team = UiTheme.TeamColour(session.LocalPlayer);
            for (int t = 0; t < world.Catalog.Count; t++)
                if (world.Catalog[t].Faction == faction) IconFactory.For(world.Catalog[t], team);
        }

        Session.Ticked += OnTicked;
        Terrain.UpdateFog(world.Vision, _viewerTeam);
        if (kind != MatchKind.MenuBackground) MusicHost.Play(MusicPlayer.Track.Battle);
    }

    /// <summary>A dark edge around the battlefield view (under the HUD), for a grittier picture.</summary>
    private static CanvasLayer MakeVignette()
    {
        var layer = new CanvasLayer { Layer = 0, Name = "Vignette" };
        var rect = new TextureRect
        {
            Texture = new GradientTexture2D
            {
                Gradient = new Gradient
                {
                    Colors = new[] { new Color(0, 0, 0, 0), new Color(0, 0, 0, 0.12f), new Color(0, 0, 0, 0.55f) },
                    Offsets = new[] { 0.45f, 0.7f, 1f },
                },
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(1.05f, 1.05f),
                Width = 256,
                Height = 256,
            },
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(rect);
        return layer;
    }

    private void OnTicked()
    {
        Units.Snapshot();
        Effects.OnTick();
        Hud?.OnTick();
        Bookmarks.OnTick(Session.World);
        if (Session.World.Tick % 5 == 0) Selection.Prune(Session.World);
        if (Session.World.IsOver && !_endHandled) OnGameOver();
    }

    public override void _Process(double delta)
    {
        if (Session is null) return;
        Session.Update(delta);
        float alpha = Session.Alpha;
        Units.SetMeta("alpha", alpha);
        Effects.SetMeta("alpha", alpha);
        Overlay.SetMeta("alpha", alpha);
        if (++_fogTimer % 6 == 0) Terrain.UpdateFog(Session.World.Vision, _viewerTeam);

        if (Kind == MatchKind.MenuBackground) DirectBackdropCamera((float)delta);
    }

    private Vector3 _shotTarget;
    private float _shotTimer;
    private bool _firstShot = true;

    /// <summary>
    /// The menu backdrop's "camera operator": every so often picks somewhere interesting (a fight,
    /// or failing that a base) and glides there slowly, drifting a little while it waits.
    /// </summary>
    private void DirectBackdropCamera(float delta)
    {
        _shotTimer -= delta;
        if (_shotTimer <= 0)
        {
            _shotTimer = 18f;
            World w = Session.World;
            var u = w.Units;
            // Prefer a unit that is shooting at something; else a random factory.
            var candidates = new List<int>();
            for (int id = 0; id < u.Count; id++)
                if (u.Alive[id] && u.Target0[id] >= 0 && !w.Catalog[u.TypeIndex[id]].IsAir) candidates.Add(id);
            if (candidates.Count == 0)
                for (int id = 0; id < u.Count; id++)
                    if (u.Alive[id] && w.Catalog[u.TypeIndex[id]].IsFactory) candidates.Add(id);
            if (candidates.Count > 0)
            {
                int pick = candidates[(int)(GD.Randi() % (uint)candidates.Count)];
                _shotTarget = new Vector3(u.Position[pick].X.Raw / 65536f, 0, u.Position[pick].Y.Raw / 65536f);
                if (_firstShot || Rig.Focus.DistanceTo(_shotTarget) > 400) Rig.Focus = _shotTarget;
                _firstShot = false;
            }
            else _shotTarget = Rig.Focus;
        }
        Vector3 drift = new Vector3(Mathf.Sin(Time.GetTicksMsec() / 9000f), 0, Mathf.Cos(Time.GetTicksMsec() / 11000f)) * 6f;
        // Keep well inside the map so the frame isn't half empty border.
        float margin = Mathf.Min(230f, Session.World.Map.Width / 3f);
        Vector3 goal = _shotTarget + drift;
        goal = new Vector3(Mathf.Clamp(goal.X, margin, Session.World.Map.Width - margin), 0,
            Mathf.Clamp(goal.Z, margin, Session.World.Map.Height - margin));
        Rig.Focus = Rig.Focus.Lerp(goal, 1 - Mathf.Exp(-0.35f * delta));
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (Kind == MatchKind.MenuBackground || e is not InputEventKey { Pressed: true, Echo: false } k) return;
        switch (k.Keycode)
        {
            case Key.Escape when Input.Mode == ClickMode.Select:
            case Key.F10:
                TogglePause();
                GetViewport().SetInputAsHandled();
                break;
            case Key.Pause:
                if (!Session.IsMultiplayer) Session.Paused = !Session.Paused;
                break;
            case Key.Equal or Key.KpAdd:
                if (!Session.IsMultiplayer) Session.Speed = Math.Min(8, Session.Speed * 2);
                break;
            case Key.Minus or Key.KpSubtract:
                if (!Session.IsMultiplayer) Session.Speed = Math.Max(0.25, Session.Speed / 2);
                break;
        }
    }

    private void TogglePause()
    {
        if (_gameOver is not null) return;
        if (_pause is null)
        {
            _pause = new PauseMenu();
            AddChild(_pause);
            _pause.Init(this);
            if (!Session.IsMultiplayer) Session.Paused = true;
            _pause.Closed += () =>
            {
                _pause = null;
                if (!Session.IsMultiplayer) Session.Paused = false;
            };
        }
        else
        {
            _pause.Close();
        }
    }

    private void OnGameOver()
    {
        _endHandled = true;
        World world = Session.World;
        bool spectator = Session.LocalPlayer < 0 || Kind == MatchKind.Replay;
        bool won = !spectator && world.WinningTeam == world.Players[Session.LocalPlayer].Team;
        if (Kind == MatchKind.MenuBackground)
        {
            RestartRequested?.Invoke();
            return;
        }
        string? saved = Kind == MatchKind.Replay ? null : SaveReplay();
        if (!spectator)
        {
            Sfx.Play(won ? SfxKind.Victory : SfxKind.Defeat);
            MusicHost.Play(won ? MusicPlayer.Track.Victory : MusicPlayer.Track.Defeat);
        }
        _pause?.Close();
        // Let the final explosions play out before the panel appears.
        GetTree().CreateTimer(2.5).Timeout += () =>
        {
            _gameOver = new GameOverPanel();
            AddChild(_gameOver);
            _gameOver.Init(this, won, spectator, saved);
        };
    }

    /// <summary>Saves the match so far as a replay in user://replays. Returns the file name.</summary>
    public string? SaveReplay()
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(GameData.ReplaysDir);
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string map = GameData.SafeFileName(Session.World.Map.Name).Replace(".json", "");
            string file = $"{stamp}_{map}.czr";
            MatchReplay replay = Session.BuildReplay($"{Title} · {Session.World.Map.Name}");
            Bookmarks.AttachTo(replay);
            using var ms = new MemoryStream();
            replay.Write(ms);
            using var f = Godot.FileAccess.Open($"{GameData.ReplaysDir}/{file}", Godot.FileAccess.ModeFlags.Write);
            f?.StoreBuffer(ms.ToArray());
            return file;
        }
        catch (Exception e)
        {
            GD.PushWarning($"Couldn't save replay: {e.Message}");
            return null;
        }
    }

    public void Quit()
    {
        // Keep a replay of matches left early too (if anything happened and it wasn't saved at game over).
        if (Kind is MatchKind.Skirmish or MatchKind.Mission or MatchKind.Multiplayer && !_endHandled
            && Session.World.Tick > 30 * World.TicksPerSecond)
            SaveReplay();
        Session.Ticked -= OnTicked;
        if (Session.Channel is IDisposable d) d.Dispose();
        Exited?.Invoke();
    }

    public void Restart() => RestartRequested?.Invoke();

    /// <summary>Changes whose eyes we see through (replays and spectating). -1 shows everything.</summary>
    public void SetViewer(int team)
    {
        _viewerTeam = team;
        Units.SetViewerTeam(team);
        Features.SetViewerTeam(team);
        Effects.SetViewerTeam(team);
        Overlay.SetViewerTeam(team);
        Terrain.UpdateFog(Session.World.Vision, team);
    }
}
