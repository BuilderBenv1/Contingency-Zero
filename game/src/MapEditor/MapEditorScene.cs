using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Match;
using ContingencyZero.Game.View;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using Godot;

namespace ContingencyZero.Game.MapEditor;

/// <summary>Passes unhandled input to the editor. Added after the camera so it sees events first (for Ctrl+wheel and friends).</summary>
public partial class EditorInputCatcher : Node
{
    public Action<InputEvent>? Handler { get; set; }
    public override void _UnhandledInput(InputEvent e) => Handler?.Invoke(e);
}

/// <summary>
/// The map editor: sculpt the ground, place metal, trees, rocks and start positions, check the map
/// is playable, save it to the player's maps and try it out in a quick skirmish. One edit (a brush
/// stroke, a click, a slider drag) is one undo step.
/// </summary>
public partial class MapEditorScene : Node3D
{
    /// <summary>Raised when the player leaves for the main menu.</summary>
    public event Action? Exited;

    /// <summary>The editor on screen, if any (for dev automation).</summary>
    public static MapEditorScene? Current { get; private set; }

    /// <summary>What to come back to after a test play.</summary>
    private sealed record ResumeState(EditorDocument Document, Vector3 Focus, float Zoom, EditorTool Tool, Symmetry Symmetry,
        float Radius, float Strength, float Density, int Plateau, float Yield);
    private static ResumeState? _resume;

    public EditorDocument? Document { get; private set; }
    public EditorTool Tool { get; private set; } = EditorTool.Raise;
    public Symmetry Symmetry { get; private set; } = Symmetry.None;
    /// <summary>Brush radius in tiles.</summary>
    public float BrushRadius { get; set; } = 6;
    /// <summary>0 to 1.</summary>
    public float BrushStrength { get; set; } = 0.5f;
    /// <summary>0 to 1.</summary>
    public float FeatureDensity { get; set; } = 0.4f;
    /// <summary>Target height for the Plateau tool, in height steps.</summary>
    public int PlateauLevel { get; set; } = 120;
    public float MetalYield { get; set; } = 2f;
    public bool ShowingPassability => _terrain.ShowPassability;
    public Vector2 CameraFocus => new(_rig.Focus.X, _rig.Focus.Z);
    public bool ModalOpen => _modal is not null && IsInstanceValid(_modal) && !_modal.IsQueuedForDeletion();
    public EditorHud Hud => _hud;
    public EditorTerrain TerrainNode => _terrain;
    public List<MapCheck> Checks { get; private set; } = new();

    private WorldEnvironment _env = null!;
    private DirectionalLight3D _sun = null!;
    private EditorTerrain _terrain = null!;
    private EditorFeatures _features = null!;
    private StartMarkers _starts = null!;
    private BrushCursor _cursor = null!;
    private CameraRig _rig = null!;
    private EditorHud _hud = null!;
    private TerrainBrush? _brush;
    private Node? _modal;

    private enum DragKind { None, Brush, Features, MoveStart, Pan }
    private DragKind _drag;
    private BrushOp _op;
    private bool _primary;
    private bool _eraseAll;
    private float _flattenTarget;
    private int _movingStart = -1;
    private Vector2 _panLast;
    private Vector3? _hover;
    private double _featureTimer, _refreshTimer;
    private double _validateIn = -1, _sliderCommitIn = -1, _waterRebuildIn = -1;
    private bool _waterDirty;
    private readonly Random _rng = new();
    private readonly List<(int X0, int Y0, int X1, int Y1)> _dirtyRects = new();

    // ---- Setup ----------------------------------------------------------------------------------

    public override void _Ready()
    {
        Current = this;
        _env = new WorldEnvironment { Environment = new Godot.Environment() };
        AddChild(_env);
        _sun = new DirectionalLight3D { RotationDegrees = new Vector3(-52, -38, 0), ShadowEnabled = true, DirectionalShadowMaxDistance = 600 };
        AddChild(_sun);
        ApplyEnvironment("temperate");

        _terrain = new EditorTerrain { Name = "Terrain" };
        AddChild(_terrain);
        _features = new EditorFeatures { Name = "Features" };
        AddChild(_features);
        _starts = new StartMarkers { Name = "Starts" };
        AddChild(_starts);
        _cursor = new BrushCursor { Name = "Cursor" };
        AddChild(_cursor);
        _rig = new CameraRig { Name = "Camera" };
        AddChild(_rig);
        AddChild(new EditorInputCatcher { Name = "Input", Handler = OnInput });

        var labels = new CanvasLayer { Name = "Labels", Layer = 4 };
        AddChild(labels);
        var overlay = new EditorOverlay();
        labels.AddChild(overlay);
        overlay.Init(this);

        _hud = new EditorHud { Name = "Hud" };
        AddChild(_hud);
        _hud.Init(this);

        if (_resume is { } r)
        {
            _resume = null;
            Tool = r.Tool; Symmetry = r.Symmetry; BrushRadius = r.Radius; BrushStrength = r.Strength;
            FeatureDensity = r.Density; PlateauLevel = r.Plateau; MetalYield = r.Yield;
            Open(r.Document, frame: false);
            _rig.Focus = r.Focus;
            _rig.SetZoom(r.Zoom);
            _hud.Toast("Back in the editor. Your map is as you left it.", UiTheme.Accent);
        }
        else
        {
            _hud.SyncAll();
            ShowStart(canCancel: false);
        }
        MusicHost.Play(Audio.MusicPlayer.Track.Menu);
    }

    public override void _ExitTree()
    {
        if (Current == this) Current = null;
    }

    private void ApplyEnvironment(string theme)
    {
        // Same light as a match (MatchScene.Start), so the map looks here as it will in play.
        TerrainPalette palette = TerrainView.PaletteFor(theme);
        Godot.Environment env = _env.Environment;
        env.BackgroundMode = Godot.Environment.BGMode.Color;
        env.BackgroundColor = palette.Sky.Darkened(0.6f);
        env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
        env.AmbientLightColor = palette.Sky.Lerp(Colors.White, 0.5f);
        env.AmbientLightEnergy = 0.55f;
        env.TonemapMode = Godot.Environment.ToneMapper.Filmic;
        _sun.LightEnergy = theme == "lava" ? 0.8f : 1.05f;
        _sun.LightColor = theme == "lava" ? new Color(1f, 0.82f, 0.7f) : new Color(1f, 0.97f, 0.92f);
    }

    /// <summary>Starts editing a document: builds everything and frames the map.</summary>
    public void Open(EditorDocument doc, bool frame = true)
    {
        EndDrag(commit: false);
        Document = doc;
        _brush = new TerrainBrush(doc.Map);
        MapData m = doc.Map;
        _rig.MapWidth = m.Width;
        _rig.MapHeight = m.Height;
        _rig.MaxSize = Math.Max(700, Math.Max(m.Width, m.Height) * 1.05f);
        RebuildAll();
        if (frame) FrameMap();
    }

    private void FrameMap()
    {
        if (Document is null) return;
        MapData m = Document.Map;
        _rig.Focus = new Vector3(m.Width / 2f, 0, m.Height / 2f);
        _rig.SetZoom(Math.Clamp(Math.Max(m.Width, m.Height) * 0.94f, 160, _rig.MaxSize));
    }

    /// <summary>Redraws everything from the document (after opening, undo, theme changes).</summary>
    private void RebuildAll()
    {
        if (Document is null) return;
        MapData m = Document.Map;
        ApplyEnvironment(m.Theme);
        _terrain.Build(m);
        _features.Rebuild(m, _terrain);
        _starts.Refresh(m, _terrain);
        _hud.Minimap.Refresh(m);
        _hud.SyncAll();
        Validate();
    }

    // ---- Dialogs ---------------------------------------------------------------------------------

    private void ShowModal(Node modal)
    {
        if (ModalOpen) _modal!.QueueFree();
        _modal = modal;
        AddChild(modal);
        _cursor.Clear();
    }

    private void ShowStart(bool canCancel)
    {
        ShowModal(StartDialog.Create(canCancel, CreateNew, OpenEntry, canCancel ? () => { } : () => Exited?.Invoke(),
            canCancel ? "Cancel" : "Back to main menu"));
    }

    /// <summary>Runs <paramref name="then"/> once unsaved changes are saved or knowingly thrown away.</summary>
    private void GuardUnsaved(string action, Action then)
    {
        if (Document is not { Dirty: true } doc) { then(); return; }
        ShowModal(ConfirmDialog.Create("UNSAVED CHANGES", $"Save your changes to \"{doc.Map.Name}\" before you {action}?",
            new (string, Action, bool)[]
            {
                ("Cancel", () => { }, false),
                ("Don't save", then, false),
                ("Save", () => Save(then), true),
            }, () => { }));
    }

    public void RequestExit() => GuardUnsaved("leave", () => Exited?.Invoke());
    public void RequestNew() => GuardUnsaved("start a new map", () => ShowStart(canCancel: true));
    public void RequestOpen() => GuardUnsaved("open another map", () => ShowStart(canCancel: true));
    public void ShowHelp() => ShowModal(HelpDialog.Create(() => { }));

    private void CreateNew(StartDialog.NewMapSpec spec)
    {
        var map = new MapData(spec.TilesX, spec.TilesY)
        {
            Name = spec.Name,
            Theme = spec.Theme,
            Author = Settings.Current.PlayerName,
            WaterLevel = Math.Max(0, spec.BaseHeight - 40),
        };
        Array.Fill(map.Heights, spec.BaseHeight);
        int ix = Math.Max(6, spec.TilesX / 8), iy = Math.Max(6, spec.TilesY / 8);
        if (spec.Players == 2)
        {
            Symmetry = Symmetry.Rotate;
            MapEdits.AddStart(map, ix, iy, Symmetry);
        }
        else if (spec.Players == 4)
        {
            Symmetry = Symmetry.FourWay;
            MapEdits.AddStart(map, ix, iy, Symmetry); // top left, top right, bottom left, bottom right, like the built-in maps
        }
        else
        {
            Symmetry = Symmetry.None;
        }
        PlateauLevel = spec.BaseHeight + 40;
        Open(new EditorDocument(map) { Origin = "New map" });
        _hud.Toast("New map ready. Pick a tool on the left and drag on the ground.", UiTheme.Accent, 5);
    }

    private void OpenEntry(MapEntry entry)
    {
        MapData map;
        try { map = entry.Load(); }
        catch (Exception e)
        {
            _hud.Toast($"Couldn't open that map: {e.Message}", UiTheme.Bad, 5);
            ShowStart(Document is not null);
            return;
        }
        var doc = new EditorDocument(map)
        {
            FileName = entry.BuiltIn ? null : entry.Key["user:".Length..],
            Origin = entry.BuiltIn ? "Built-in map (saving makes your own copy)" : "Your map",
        };
        Symmetry = Symmetry.None;
        PlateauLevel = Math.Clamp(map.Heights[map.Heights.Length / 2] + 20, 0, TerrainBrush.MaxHeight);
        Open(doc);
        _hud.Toast($"Opened {map.Name}.", UiTheme.Accent);
    }

    // ---- Saving and test play ----------------------------------------------------------------

    /// <summary>Saves to the current file, or asks for a name first. <paramref name="then"/> runs after a successful save.</summary>
    public void Save(Action? then = null)
    {
        if (Document is not { } doc) return;
        if (doc.FileName is null) { SaveAs(then); return; }
        WriteMap(doc.FileName);
        then?.Invoke();
    }

    public void SaveAs(Action? then = null)
    {
        if (Document is not { } doc) return;
        ShowModal(SaveDialog.Create(doc.Map, doc.FileName, (name, description, file) =>
        {
            doc.Map.Name = name;
            doc.Map.Description = description;
            if (doc.Map.Author.Length == 0) doc.Map.Author = Settings.Current.PlayerName;
            WriteMap(file);
            then?.Invoke();
        }, () => { }));
    }

    /// <summary>Writes the map to user://maps/<paramref name="file"/> and checks it reads back the same.</summary>
    public bool WriteMap(string file)
    {
        if (Document is not { } doc) return false;
        try
        {
            GameData.SaveUserMap(doc.Map, file);
            string text = Godot.FileAccess.GetFileAsString($"{GameData.UserMapsDir}/{file}");
            MapData back = MapData.FromJson(text);
            if (back.Fingerprint() != doc.Map.Fingerprint()) throw new InvalidOperationException("the saved file didn't read back the same");
            doc.FileName = file;
            doc.Origin = "Your map";
            doc.Dirty = false;
            _hud.SyncTitle();
            int problems = Checks.FindAll(c => c.Level >= CheckLevel.Warning).Count;
            _hud.Toast(problems == 0
                ? $"Saved \"{doc.Map.Name}\". It's in the Skirmish map list."
                : $"Saved \"{doc.Map.Name}\" (with {problems} warning{(problems == 1 ? "" : "s")}: see Checks).", problems == 0 ? UiTheme.Good : UiTheme.Amber, 4);
            Audio.Sfx.Play(Audio.SfxKind.BuildComplete);
            return true;
        }
        catch (Exception e)
        {
            _hud.Toast($"Couldn't save: {e.Message}", UiTheme.Bad, 6);
            Audio.Sfx.Play(Audio.SfxKind.Error);
            return false;
        }
    }

    /// <summary>Starts a 1v1 against a Normal AI on the map as it is now. Leaving the match comes back here.</summary>
    public void TestPlay()
    {
        if (Document is not { } doc) return;
        if (doc.Map.StartPositions.Count < MapEdits.MinStarts)
        {
            _hud.Toast("Test play needs at least 2 start positions. Use the Start positions tool (0).", UiTheme.Amber, 5);
            Audio.Sfx.Play(Audio.SfxKind.Error);
            return;
        }
        int errors = Checks.FindAll(c => c.Level == CheckLevel.Error).Count;
        if (errors > 0 && !ModalOpen)
        {
            ShowModal(ConfirmDialog.Create("TEST PLAY", $"The checks found {errors} problem{(errors == 1 ? "" : "s")} that may spoil the game (see Checks). Play anyway?",
                new (string, Action, bool)[] { ("Cancel", () => { }, false), ("Play anyway", StartTestPlay, true) }, () => { }));
            return;
        }
        StartTestPlay();
    }

    private void StartTestPlay()
    {
        if (Document is not { } doc) return;
        EndDrag(commit: true);
        MapData map = doc.CloneMap();
        string mine = Settings.Current.LastFaction == "crucible" ? "crucible" : "lattice";
        string theirs = mine == "lattice" ? "crucible" : "lattice";
        var players = new List<PlayerSetup>
        {
            new(Settings.Current.PlayerName, 0, mine, 0),
            new("Normal AI", 1, theirs, 1, AiDifficulty.Normal),
        };
        _resume = new ResumeState(doc, _rig.Focus, _rig.Size, Tool, Symmetry, BrushRadius, BrushStrength, FeatureDensity, PlateauLevel, MetalYield);
        GameRoot.Instance.StartTestPlay(map, players, new MatchOptions(), () => GameRoot.Instance.ShowEditor());
    }

    // ---- Settings from the HUD ---------------------------------------------------------------

    public void SetTool(EditorTool t)
    {
        if (Tool == t) return;
        EndDrag(commit: true);
        Tool = t;
        _starts.Highlight = -1;
        _hud.SyncTool();
    }

    public void SetSymmetry(Symmetry s)
    {
        Symmetry = s;
        _hud.SyncTool();
        if (s != Symmetry.None) _hud.Toast($"{ToolInfo.SymmetryName(s)}: new edits are copied to the mirrored side.", UiTheme.Accent);
    }

    public void TogglePassability()
    {
        _terrain.SetShowPassability(!_terrain.ShowPassability);
        _hud.SyncTool();
    }

    public void SetTheme(string theme)
    {
        if (Document is not { } doc || doc.Map.Theme == theme) return;
        doc.Begin();
        doc.Map.Theme = theme;
        doc.Commit();
        RebuildAll();
    }

    /// <summary>Live water level from the slider. The undo step closes when the slider is released.</summary>
    public void SetWaterLevel(int level)
    {
        if (Document is not { } doc || doc.Map.WaterLevel == level) return;
        doc.Begin();
        doc.Map.WaterLevel = level;
        _sliderCommitIn = 0.6;
        // Rebuilding the water plane is the heavy part: do it a few times a second while dragging.
        _waterDirty = true;
        if (_waterRebuildIn < 0) _waterRebuildIn = 0.12;
        _hud.SyncMap();
    }

    public void SetWind(int value, bool isMin)
    {
        if (Document is not { } doc) return;
        doc.Begin();
        Fix64 v = Fix64.FromInt(value);
        if (isMin)
        {
            doc.Map.WindMin = v;
            if (doc.Map.WindMax < v) doc.Map.WindMax = v;
        }
        else
        {
            doc.Map.WindMax = v;
            if (doc.Map.WindMin > v) doc.Map.WindMin = v;
        }
        _sliderCommitIn = 0.6;
        _hud.SyncMap();
    }

    /// <summary>A slider was let go: close its undo step now.</summary>
    public void EndSliderEdit()
    {
        _sliderCommitIn = 0;
    }

    private void CommitSliderEdit()
    {
        _sliderCommitIn = -1;
        if (Document is not { } doc || !doc.InChange || _drag != DragKind.None) return;
        bool water = _waterDirty;
        _waterDirty = false;
        _waterRebuildIn = -1;
        if (water)
        {
            _terrain.Build(doc.Map);
            _features.Rebuild(doc.Map, _terrain);
            _starts.Refresh(doc.Map, _terrain);
        }
        if (doc.Commit()) AfterEdit(terrainChanged: water);
    }

    public void Undo()
    {
        if (Document is not { } doc) return;
        EndDrag(commit: true);
        if (doc.Undo()) { RebuildAll(); _hud.Toast("Undone", UiTheme.Dim, 1.2); }
        else _hud.Toast("Nothing to undo", UiTheme.Dim, 1.2);
    }

    public void Redo()
    {
        if (Document is not { } doc) return;
        EndDrag(commit: true);
        if (doc.Redo()) { RebuildAll(); _hud.Toast("Redone", UiTheme.Dim, 1.2); }
        else _hud.Toast("Nothing to redo", UiTheme.Dim, 1.2);
    }

    /// <summary>Points the camera at a spot on the map (from the checks list or the overview).</summary>
    public void LookAt(Vector2 p, bool keepZoom = false)
    {
        _rig.JumpTo(new Vector3(p.X, 0, p.Y));
        if (!keepZoom) _rig.SetZoom(Math.Min(_rig.Size, 220));
    }

    // ---- After edits -------------------------------------------------------------------------

    /// <summary>Called after every committed edit: refresh what depends on the map and re-check it soon.</summary>
    private void AfterEdit(bool terrainChanged)
    {
        if (Document is null) return;
        if (terrainChanged) _hud.Minimap.Refresh(Document.Map);
        _hud.SyncTitle();
        _hud.SyncMap();
        _validateIn = 0.25;
    }

    /// <summary>
    /// Re-runs the checks. They take tens of milliseconds on big maps (path searches and flood
    /// fills), so normally they run on a worker thread against a copy of the map.
    /// </summary>
    private void Validate(bool now = false)
    {
        _validateIn = -1;
        if (Document is not { } doc) return;
        _hud.Minimap.Refresh(doc.Map);
        int generation = ++_validateGeneration;
        if (now)
        {
            ShowChecks(MapValidator.Run(doc.Map));
            return;
        }
        MapData copy = CopyForChecks(doc.Map);
        Task.Run(() => MapValidator.Run(copy)).ContinueWith(t => Callable.From(() =>
        {
            // Only the newest run counts, and only if the editor is still here.
            if (!IsInstanceValid(this) || generation != _validateGeneration) return;
            if (t.IsCompletedSuccessfully) ShowChecks(t.Result);
            else GD.PushWarning($"Map checks failed: {t.Exception?.GetBaseException().Message}");
        }).CallDeferred());
    }

    private int _validateGeneration;

    private void ShowChecks(List<MapCheck> checks)
    {
        Checks = checks;
        _hud.ShowChecks(checks);
    }

    /// <summary>The parts of a map the checks read, copied so editing can carry on meanwhile.</summary>
    private static MapData CopyForChecks(MapData m)
    {
        var copy = new MapData(m.TilesX, m.TilesY) { WaterLevel = m.WaterLevel, Name = m.Name };
        Array.Copy(m.Heights, copy.Heights, m.Heights.Length);
        copy.MetalSpots.AddRange(m.MetalSpots);
        copy.StartPositions.AddRange(m.StartPositions);
        return copy;
    }

    // ---- Input -------------------------------------------------------------------------------

    private void OnInput(InputEvent e)
    {
        if (Document is null || ModalOpen) return;
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown, Pressed: true } wheel
                when wheel.CtrlPressed || wheel.ShiftPressed:
            {
                int dir = wheel.ButtonIndex == MouseButton.WheelUp ? 1 : -1;
                if (wheel.CtrlPressed) ChangeRadius(dir);
                else ChangeStrength(dir);
                GetViewport().SetInputAsHandled();
                break;
            }
            case InputEventMouseButton { ButtonIndex: MouseButton.Left or MouseButton.Right } mb:
                if (mb.Pressed)
                {
                    bool primary = mb.ButtonIndex == MouseButton.Left;
                    if (_drag != DragKind.None) break;
                    if (primary && Input.IsKeyPressed(Key.Space))
                    {
                        _primary = true;
                        _drag = DragKind.Pan;
                        _panLast = mb.Position;
                    }
                    else
                    {
                        UpdateHover(mb.Position);
                        BeginAction(primary, mb.CtrlPressed, mb.ShiftPressed);
                    }
                    GetViewport().SetInputAsHandled();
                }
                else if ((mb.ButtonIndex == MouseButton.Left) == _primary || _drag == DragKind.Pan)
                {
                    EndDrag(commit: true);
                }
                break;
            case InputEventMouseMotion mm when _drag == DragKind.Pan:
            {
                Vector2 d = mm.Position - _panLast;
                _panLast = mm.Position;
                float unitsPerPixel = _rig.Size / GetViewport().GetVisibleRect().Size.Y;
                _rig.JumpTo(_rig.Focus - _rig.GroundRight * d.X * unitsPerPixel - _rig.GroundForward * d.Y * unitsPerPixel / Mathf.Sin(Mathf.DegToRad(35f)));
                break;
            }
            case InputEventKey { Pressed: true } k:
                OnKey(k);
                break;
        }
    }

    private void OnKey(InputEventKey k)
    {
        bool ctrl = k.CtrlPressed || k.MetaPressed;
        if (ctrl)
        {
            switch (k.Keycode)
            {
                case Key.Z when k.ShiftPressed: Redo(); break;
                case Key.Z: Undo(); break;
                case Key.Y: Redo(); break;
                case Key.S when k.ShiftPressed: if (!k.Echo) SaveAs(); break;
                case Key.S: if (!k.Echo) Save(); break;
                case Key.N: if (!k.Echo) RequestNew(); break;
                case Key.O: if (!k.Echo) RequestOpen(); break;
                default: return;
            }
            GetViewport().SetInputAsHandled();
            return;
        }
        if (k.Echo && k.Keycode is not (Key.Bracketleft or Key.Bracketright or Key.Minus or Key.Equal)) return;
        foreach (EditorTool t in ToolInfo.All)
        {
            if (k.Keycode != ToolInfo.Hotkey(t)) continue;
            SetTool(t);
            GetViewport().SetInputAsHandled();
            return;
        }
        switch (k.Keycode)
        {
            case Key.Bracketleft: ChangeRadius(-1); break;
            case Key.Bracketright: ChangeRadius(1); break;
            case Key.Minus or Key.KpSubtract: ChangeStrength(-1); break;
            case Key.Equal or Key.KpAdd: ChangeStrength(1); break;
            case Key.Tab: TogglePassability(); break;
            case Key.Home: FrameMap(); break;
            case Key.F1: ShowHelp(); break;
            case Key.F5: TestPlay(); break;
            case Key.Escape:
                if (_drag != DragKind.None) EndDrag(commit: true);
                else RequestExit();
                break;
            default: return;
        }
        GetViewport().SetInputAsHandled();
    }

    private void ChangeRadius(int dir)
    {
        BrushRadius = Math.Clamp(BrushRadius + dir, 1, 30);
        _hud.SyncTool();
    }

    private void ChangeStrength(int dir)
    {
        if (ToolInfo.IsFeature(Tool)) FeatureDensity = Math.Clamp(MathF.Round(FeatureDensity * 20 + dir) / 20f, 0.05f, 1f);
        else BrushStrength = Math.Clamp(MathF.Round(BrushStrength * 20 + dir) / 20f, 0.05f, 1f);
        _hud.SyncTool();
    }

    // ---- Tools -------------------------------------------------------------------------------

    private static BrushOp OpFor(EditorTool t, bool primary) => t switch
    {
        EditorTool.Raise => primary ? BrushOp.Raise : BrushOp.Lower,
        EditorTool.Lower => primary ? BrushOp.Lower : BrushOp.Raise,
        EditorTool.Smooth => BrushOp.Smooth,
        EditorTool.Flatten => primary ? BrushOp.Flatten : BrushOp.Smooth,
        EditorTool.Plateau => primary ? BrushOp.Plateau : BrushOp.Smooth,
        _ => primary ? BrushOp.Roughen : BrushOp.Smooth,
    };

    private float RadiusWorld => BrushRadius * MapData.TileSize;

    private void BeginAction(bool primary, bool ctrl, bool shift)
    {
        if (Document is not { } doc || _hover is not { } hit) return;
        MapData map = doc.Map;
        var p = new Vector2(hit.X, hit.Z);
        _primary = primary;
        var (tx, ty) = MapEdits.TileAt(map, p);

        if (ToolInfo.IsTerrain(Tool))
        {
            if (Tool == EditorTool.Plateau && primary && ctrl)
            {
                PlateauLevel = (int)MathF.Round(TerrainBrush.HeightStepsAt(map, p.X, p.Y));
                _hud.SyncTool();
                _hud.Toast($"Plateau height set to {PlateauLevel / (float)MapData.HeightStepsPerUnit:0.##}", UiTheme.Accent, 1.5);
                return;
            }
            doc.Begin();
            _brush!.BeginStroke(_rng.Next());
            _op = OpFor(Tool, primary);
            _flattenTarget = TerrainBrush.HeightStepsAt(map, p.X, p.Y);
            _drag = DragKind.Brush;
            _refreshTimer = 0;
            return;
        }

        switch (Tool)
        {
            case EditorTool.Trees or EditorTool.Rocks:
                doc.Begin();
                _drag = DragKind.Features;
                _eraseAll = !primary && shift;
                _featureTimer = 0;
                return;

            case EditorTool.Metal:
                doc.Begin();
                int changed = primary
                    ? MapEdits.AddMetalSpot(map, tx, ty, Fix64.Parse(MetalYield.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture)), Symmetry)
                    : MapEdits.RemoveMetalSpot(map, p, MapData.TileSize * 1.6f, Symmetry);
                // Trees and rocks don't stand on metal spots.
                if (primary && changed > 0)
                    map.Features.RemoveAll(f => MapEdits.MetalSpotOnTile(map, MapData.TileOf(f.Position.X), MapData.TileOf(f.Position.Y)) >= 0);
                if (doc.Commit())
                {
                    // The discs belong to the game's terrain view; rebuild it only if they can't be updated in place.
                    if (!_terrain.RefreshMetalDiscs()) _terrain.Build(map);
                    _features.Rebuild(map, _terrain);
                    AfterEdit(terrainChanged: true);
                    Audio.Sfx.Play(primary ? Audio.SfxKind.BuildStart : Audio.SfxKind.Reclaim);
                }
                else
                {
                    _hud.Toast(primary ? "There's already a metal spot on that tile." : "No metal spot there to remove.", UiTheme.Dim, 1.5);
                }
                return;

            case EditorTool.Starts:
            {
                int near = MapEdits.NearestStart(map, p, MapData.TileSize * 2.2f);
                if (primary && near >= 0)
                {
                    doc.Begin();
                    _drag = DragKind.MoveStart;
                    _movingStart = near;
                    return;
                }
                doc.Begin();
                if (primary)
                {
                    if (map.StartPositions.Count >= MapEdits.MaxStarts)
                    {
                        doc.Cancel();
                        _hud.Toast($"A map can have at most {MapEdits.MaxStarts} start positions.", UiTheme.Amber);
                        Audio.Sfx.Play(Audio.SfxKind.Error);
                        return;
                    }
                    MapEdits.AddStart(map, tx, ty, Symmetry);
                }
                else if (near >= 0)
                {
                    map.StartPositions.RemoveAt(near);
                }
                if (doc.Commit())
                {
                    _starts.Refresh(map, _terrain);
                    AfterEdit(terrainChanged: false);
                    Audio.Sfx.Play(primary ? Audio.SfxKind.Acknowledge : Audio.SfxKind.Reclaim);
                }
                else if (!primary)
                {
                    _hud.Toast("Right click on a start marker to remove it.", UiTheme.Dim, 1.5);
                }
                else
                {
                    _hud.Toast("Too close to another start.", UiTheme.Dim, 1.5);
                }
                return;
            }
        }
    }

    private void EndDrag(bool commit)
    {
        DragKind was = _drag;
        _drag = DragKind.None;
        _movingStart = -1;
        if (Document is not { } doc || was is DragKind.None or DragKind.Pan) return;
        if (!commit) { doc.Cancel(); RebuildAll(); return; }
        bool changed = doc.Commit();
        if (!changed) return;
        if (was == DragKind.Brush)
        {
            _terrain.SettleAfterStroke();
            _features.Rebuild(doc.Map, _terrain);
            _starts.Refresh(doc.Map, _terrain);
        }
        AfterEdit(terrainChanged: was == DragKind.Brush);
    }

    // ---- Per frame ---------------------------------------------------------------------------

    private void UpdateHover(Vector2 screen)
    {
        if (Document is null) { _hover = null; return; }
        Camera3D cam = _rig.Camera;
        _hover = _terrain.RayHit(cam.ProjectRayOrigin(screen), cam.ProjectRayNormal(screen));
        if (_hover is { } h && (h.X < 0 || h.Z < 0 || h.X > Document.Map.Width || h.Z > Document.Map.Height)) _hover = null;
    }

    public override void _Process(double delta)
    {
        if (Document is not { } doc) return;
        _rig.InputEnabled = !ModalOpen && _drag != DragKind.Pan;

        Vector2 mouse = GetViewport().GetMousePosition();
        bool overUi = GetViewport().GuiGetHoveredControl() is not null;
        if (ModalOpen || (overUi && _drag == DragKind.None)) _hover = null;
        else UpdateHover(mouse);

        MapData map = doc.Map;
        // A button let go over a panel never reaches us as an event: notice it here.
        if (_drag != DragKind.None && !Input.IsMouseButtonPressed(_primary || _drag == DragKind.Pan ? MouseButton.Left : MouseButton.Right))
            EndDrag(commit: true);
        StepDrag(map, delta);

        DrawCursor(map);
        UpdateCursorInfo(map);

        if (_validateIn >= 0 && _drag == DragKind.None)
        {
            _validateIn -= delta;
            if (_validateIn < 0) Validate();
        }
        if (_waterRebuildIn >= 0)
        {
            _waterRebuildIn -= delta;
            if (_waterRebuildIn < 0)
            {
                _terrain.Build(map);
                _features.Rebuild(map, _terrain);
                _starts.Refresh(map, _terrain);
            }
        }
        if (_sliderCommitIn >= 0)
        {
            _sliderCommitIn -= delta;
            if (_sliderCommitIn < 0 && !Input.IsMouseButtonPressed(MouseButton.Left)) CommitSliderEdit();
            else if (_sliderCommitIn < 0) _sliderCommitIn = 0.1;
        }
    }

    /// <summary>One frame of whatever drag is in progress: brush, painting, or moving a start.</summary>
    private void StepDrag(MapData map, double delta)
    {
        float dt = (float)Math.Min(delta, 0.05);
        switch (_drag)
        {
            case DragKind.Brush when _hover is { } hit && _brush is not null:
            {
                float target = _op == BrushOp.Plateau ? PlateauLevel : _flattenTarget;
                foreach (var (c, flipX, flipY) in MapEdits.MirrorFrames(map, new Vector2(hit.X, hit.Z), Symmetry))
                    _brush.Apply(_op, c.X, c.Y, RadiusWorld, BrushStrength, dt, target, flipX, flipY);
                _dirtyRects.Clear();
                if (_brush.TakeDirty(_dirtyRects)) _terrain.UpdateRegions(_dirtyRects);
                _refreshTimer -= delta;
                if (_refreshTimer <= 0)
                {
                    // Keep things standing on the ground while it moves (cheap, but not every frame).
                    _refreshTimer = 0.1;
                    if (map.Features.Count > 0) _features.Rebuild(map, _terrain);
                    _starts.Refresh(map, _terrain);
                    _terrain.RefreshMetalDiscs();
                }
                break;
            }
            case DragKind.Features when _hover is { } hit:
            {
                _featureTimer -= delta;
                if (_featureTimer > 0) break;
                _featureTimer = 0.05;
                FeatureKind kind = Tool == EditorTool.Trees ? FeatureKind.Tree : FeatureKind.Rock;
                var p = new Vector2(hit.X, hit.Z);
                int n = _primary
                    ? MapEdits.PaintFeatures(map, kind, p, RadiusWorld, FeatureDensity, Symmetry, _rng, 2 + (int)BrushRadius)
                    : MapEdits.EraseFeatures(map, _eraseAll ? null : kind, p, RadiusWorld, Symmetry);
                if (n > 0) _features.Rebuild(map, _terrain);
                break;
            }
            case DragKind.MoveStart when _hover is { } hit:
            {
                var (tx, ty) = MapEdits.TileAt(map, new Vector2(hit.X, hit.Z));
                if (MapEdits.MoveStart(map, _movingStart, tx, ty)) _starts.Refresh(map, _terrain);
                break;
            }
        }
    }

    // ---- Automation hooks (EditorDevScript drives the same code paths as the mouse) -----------

    internal void DevCloseModal()
    {
        if (ModalOpen) _modal!.QueueFree();
        _modal = null;
    }

    internal void DevNewMap(StartDialog.NewMapSpec spec) { DevCloseModal(); CreateNew(spec); }
    internal void DevOpen(MapEntry entry) { DevCloseModal(); OpenEntry(entry); }
    internal void DevShowStart() => ShowStart(canCancel: Document is not null);
    internal void DevShowSave() => SaveAs();
    internal void DevValidateNow() => Validate(now: true);
    internal Vector3 DevCameraFocus { get => _rig.Focus; set => _rig.JumpTo(value); }
    internal void DevZoom(float size) => _rig.SetZoom(size);
    internal Camera3D DevCamera => _rig.Camera;
    internal void DevSetHover(Vector2 p) =>
        _hover = Document is null ? null : new Vector3(p.X, _terrain.HeightAt(p.X, p.Y), p.Y);

    /// <summary>A whole drag along <paramref name="path"/>, as if the mouse held still <paramref name="seconds"/> at each point at 60 fps.</summary>
    internal void DevStroke(EditorTool tool, bool primary, IReadOnlyList<Vector2> path, float seconds, bool shift = false,
        Func<double>? clock = null, List<double>? stepMs = null)
    {
        if (Document is not { } doc || path.Count == 0) return;
        clock ??= () => Time.GetTicksUsec() / 1000.0;
        SetTool(tool);
        DevSetHover(path[0]);
        BeginAction(primary, ctrl: false, shift: shift);
        int frames = Math.Max(1, (int)(seconds * 60));
        foreach (Vector2 p in path)
        {
            DevSetHover(p);
            for (int f = 0; f < frames; f++)
            {
                double t0 = clock();
                StepDrag(doc.Map, 1.0 / 60);
                stepMs?.Add(clock() - t0);
            }
        }
        double e0 = clock();
        EndDrag(commit: true);
        LastStrokeEndMs = clock() - e0;
    }

    /// <summary>How long finishing the last automated stroke took (settling colours, metal discs, features).</summary>
    internal double LastStrokeEndMs { get; private set; }

    /// <summary>A single click with a tool at a point.</summary>
    internal void DevClick(EditorTool tool, bool primary, Vector2 p, bool ctrl = false)
    {
        SetTool(tool);
        DevSetHover(p);
        BeginAction(primary, ctrl, shift: false);
        EndDrag(commit: true);
    }

    /// <summary>Drags start <paramref name="index"/> to a point, as the Start positions tool does.</summary>
    internal void DevMoveStart(int index, Vector2 to)
    {
        if (Document is not { } doc) return;
        SetTool(EditorTool.Starts);
        DevSetHover(MapEdits.ToVec(doc.Map.StartPositions[index]));
        BeginAction(primary: true, ctrl: false, shift: false);
        DevSetHover(to);
        StepDrag(doc.Map, 1.0 / 60);
        EndDrag(commit: true);
    }

    /// <summary>Saves under a name without the dialog (what the Save dialog's button does).</summary>
    internal bool DevSaveAs(string name, string description)
    {
        if (Document is not { } doc) return false;
        doc.Map.Name = name;
        doc.Map.Description = description;
        if (doc.Map.Author.Length == 0) doc.Map.Author = Settings.Current.PlayerName;
        return WriteMap(GameData.SafeFileName(name));
    }

    private void DrawCursor(MapData map)
    {
        int highlight = -1;
        if (_hover is not { } hit || ModalOpen)
        {
            _cursor.Clear();
            if (_starts.Highlight != -1) { _starts.Highlight = -1; _starts.Refresh(map, _terrain); }
            return;
        }
        var p = new Vector2(hit.X, hit.Z);
        float pixel = _rig.Size / GetViewport().GetVisibleRect().Size.Y;
        Color colour = ToolInfo.Colour(Tool);
        bool erasing = _drag != DragKind.None && !_primary;

        if (ToolInfo.UsesBrush(Tool))
        {
            if (erasing && ToolInfo.IsFeature(Tool)) colour = UiTheme.Bad;
            float? level = Tool == EditorTool.Plateau ? PlateauLevel * TerrainView.HeightScale : null;
            _cursor.DrawBrush(_terrain, MapEdits.Mirrors(map, p, Symmetry), RadiusWorld, colour, pixel, level);
        }
        else if (Tool == EditorTool.Metal)
        {
            var (tx, ty) = MapEdits.TileAt(map, p);
            int existing = MapEdits.NearestMetalSpot(map, p, MapData.TileSize * 1.6f);
            var centres = new List<Vector2>();
            if (existing >= 0)
            {
                // Right click would remove this one.
                FixVec2 sp = map.MetalSpots[existing].Position;
                foreach (var (mx, my) in MapEdits.MirrorTiles(map, MapData.TileOf(sp.X), MapData.TileOf(sp.Y), Symmetry))
                    centres.Add(MapEdits.ToVec(MapEdits.TileCentre(mx, my)));
                colour = MapEdits.MetalSpotOnTile(map, tx, ty) >= 0 ? UiTheme.Bad : colour;
            }
            else
            {
                foreach (var (mx, my) in MapEdits.MirrorTiles(map, tx, ty, Symmetry))
                    centres.Add(MapEdits.ToVec(MapEdits.TileCentre(mx, my)));
            }
            _cursor.DrawMarker(_terrain, centres, 3.6f, colour, pixel, square: true);
        }
        else
        {
            int near = _drag == DragKind.MoveStart ? _movingStart : MapEdits.NearestStart(map, p, MapData.TileSize * 2.2f);
            if (near >= 0)
            {
                highlight = near;
                _cursor.DrawMarker(_terrain, new List<Vector2> { MapEdits.ToVec(map.StartPositions[near]) }, 7f, UiTheme.TeamColour(near), pixel, square: false);
            }
            else
            {
                var (tx, ty) = MapEdits.TileAt(map, p);
                var centres = new List<Vector2>();
                foreach (var (mx, my) in MapEdits.MirrorTiles(map, tx, ty, Symmetry))
                    centres.Add(MapEdits.ToVec(MapEdits.TileCentre(mx, my)));
                Color c = map.StartPositions.Count >= MapEdits.MaxStarts ? UiTheme.Bad : UiTheme.TeamColour(map.StartPositions.Count);
                _cursor.DrawMarker(_terrain, centres, 5f, c, pixel, square: true);
            }
        }
        if (_starts.Highlight != highlight)
        {
            _starts.Highlight = highlight;
            _starts.Refresh(map, _terrain);
        }
    }

    private void UpdateCursorInfo(MapData map)
    {
        if (_hover is not { } hit)
        {
            _hud.SetCursorInfo("", UiTheme.Dim);
            return;
        }
        var (tx, ty) = MapEdits.TileAt(map, new Vector2(hit.X, hit.Z));
        float h = TerrainBrush.HeightStepsAt(map, hit.X, hit.Z) / MapData.HeightStepsPerUnit;
        int depth = map.TileDepth(tx, ty);
        (string what, Color colour) = _terrain.Class(tx, ty) switch
        {
            0 => ("flat: buildable", UiTheme.Good),
            1 => ("slope: vehicles and bots", new Color(0.95f, 0.9f, 0.45f)),
            2 => ("steep: bots only", new Color(1f, 0.62f, 0.3f)),
            3 => (depth > 0 ? "underwater cliff: impassable" : "cliff: impassable", UiTheme.Bad),
            4 => ($"shallows ({depth / (float)MapData.HeightStepsPerUnit:0.##} deep): wade", UiTheme.Accent),
            _ => ($"deep water ({depth / (float)MapData.HeightStepsPerUnit:0.##} deep): ships", UiTheme.Accent),
        };
        _hud.SetCursorInfo($"Tile {tx}, {ty}  ·  height {h:0.0}  ·  {what}", colour);
    }
}
