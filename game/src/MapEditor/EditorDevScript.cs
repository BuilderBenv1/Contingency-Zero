using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Match;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using Godot;

namespace ContingencyZero.Game.MapEditor;

/// <summary>
/// Scripted editor sessions for automated checks, started by DevAutomation with
/// <c>--dev-editor-script=&lt;name&gt;</c>. Each prints "EDITOR-DEV:" lines, saves screenshots to
/// --dev-shots, and quits. Scripts:
///   roundtrip  make a map with every tool, undo/redo, save, reload, check fingerprints, start a
///              sim World on it, test play and come back (the default)
///   perf       time the live preview on 160 and 256 tile maps
///   shots      just the screens (start dialog, a built-in map, dialogs) for layout checks
/// </summary>
public partial class EditorDevScript : Node
{
    private readonly string _script;
    private readonly string _shots;
    private int _failures;
    private int _shotIndex;

    public EditorDevScript(string script, string shots)
    {
        _script = script.Length == 0 ? "roundtrip" : script;
        _shots = shots;
    }

    public EditorDevScript() : this("roundtrip", "") { }

    public override void _Ready() => _ = Run();

    private static void Log(string text) => GD.Print($"EDITOR-DEV: {text}");

    private void Check(bool ok, string what)
    {
        Log($"{(ok ? "PASS" : "FAIL")} {what}");
        if (!ok) _failures++;
    }

    private async Task Frames(int n)
    {
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Shot(string name)
    {
        await Frames(3);
        if (_shots.Length == 0) return;
        DirAccess.MakeDirRecursiveAbsolute(_shots);
        string path = $"{_shots}/editor_{_shotIndex++:00}_{name}.png";
        GetViewport().GetTexture().GetImage().SavePng(path);
        Log($"shot {path}");
    }

    private async Task<MapEditorScene> Editor()
    {
        if (MapEditorScene.Current is null) GameRoot.Instance.ShowEditor();
        for (int i = 0; i < 60 && MapEditorScene.Current?.IsNodeReady() != true; i++) await Frames(1);
        await Frames(2);
        return MapEditorScene.Current!;
    }

    private async Task Run()
    {
        await Frames(2);
        try
        {
            switch (_script)
            {
                case "perf": await Perf(); break;
                case "shots": await Shots(); break;
                default: await RoundTrip(); break;
            }
        }
        catch (Exception e)
        {
            _failures++;
            Log($"FAIL exception {e}");
        }
        Log(_failures == 0 ? "RESULT PASS" : $"RESULT FAIL ({_failures} failures)");
        await Frames(1);
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private static List<Vector2> Line(Vector2 a, Vector2 b, int points)
    {
        var list = new List<Vector2>();
        for (int i = 0; i < points; i++) list.Add(a.Lerp(b, points == 1 ? 0 : i / (float)(points - 1)));
        return list;
    }

    // ---- roundtrip ------------------------------------------------------------------------------

    private async Task RoundTrip()
    {
        MapEditorScene ed = await Editor();
        await Shot("start_dialog");
        Check(ed.Document is null && ed.ModalOpen, "editor opens on the start dialog");

        ed.DevNewMap(new StartDialog.NewMapSpec("Dev Test Map", 96, 96, "temperate", 80, 2));
        await Frames(2);
        MapData map = ed.Document!.Map;
        Check(map.TilesX == 96 && map.StartPositions.Count == 2 && ed.Symmetry == Symmetry.Rotate, "new 96x96 map with 2 starts and half-turn symmetry");
        ulong blank = map.Fingerprint();
        await Shot("new_map");

        float w = map.Width, h = map.Height;
        var c = new Vector2(w / 2, h / 2);
        // Hills, a lake, a mesa, rough ground, smoothed edges and flattened bases.
        ed.BrushRadius = 10; ed.BrushStrength = 0.8f;
        ed.DevStroke(EditorTool.Raise, true, Line(new Vector2(w * 0.3f, h * 0.55f), new Vector2(w * 0.45f, h * 0.7f), 6), 0.4f);
        ed.DevStroke(EditorTool.Lower, true, Line(new Vector2(w * 0.62f, h * 0.25f), new Vector2(w * 0.75f, h * 0.3f), 5), 0.5f);
        ed.BrushRadius = 6; ed.PlateauLevel = 160;
        ed.DevStroke(EditorTool.Plateau, true, new List<Vector2> { c + new Vector2(-60, 40) }, 0.5f);
        ed.BrushRadius = 8; ed.BrushStrength = 0.5f;
        ed.DevStroke(EditorTool.Roughen, true, Line(new Vector2(w * 0.15f, h * 0.8f), new Vector2(w * 0.4f, h * 0.9f), 6), 0.3f);
        ed.DevStroke(EditorTool.Smooth, true, Line(new Vector2(w * 0.3f, h * 0.55f), new Vector2(w * 0.45f, h * 0.7f), 6), 0.3f);
        foreach (FixPoint s in Starts(map)) ed.DevStroke(EditorTool.Flatten, true, new List<Vector2> { s.P }, 0.6f);
        Check(map.Fingerprint() != blank, "brush strokes changed the heights");
        Check(SymmetricHeights(map), "half-turn symmetry kept the heights symmetric");
        await Shot("sculpted");

        // Metal round each start (mirrored), rich spots in the middle, trees and rocks.
        Vector2 s0 = MapEdits.ToVec(map.StartPositions[0]);
        foreach (Vector2 off in new[] { new Vector2(40, 0), new Vector2(0, 40), new Vector2(32, 32), new Vector2(-24, 40) })
            ed.DevClick(EditorTool.Metal, true, s0 + off);
        ed.MetalYield = 4f;
        ed.DevClick(EditorTool.Metal, true, c + new Vector2(-20, -20));
        int spots = map.MetalSpots.Count;
        Check(spots == 10, $"metal spots placed with mirroring ({spots}, expected 10)");
        Check(map.MetalSpots.All(m => ((int)(m.Position.X.Raw >> 16) - 4) % MapData.TileSize == 0 && ((int)(m.Position.Y.Raw >> 16) - 4) % MapData.TileSize == 0),
            "metal spots sit on tile centres");
        ed.DevClick(EditorTool.Metal, false, c + new Vector2(-20, -20));
        Check(map.MetalSpots.Count == spots - 2, "right click removed a rich spot and its mirror");
        ed.DevClick(EditorTool.Metal, true, c + new Vector2(-20, -20));

        ed.BrushRadius = 9; ed.FeatureDensity = 0.6f;
        ed.DevStroke(EditorTool.Trees, true, Line(new Vector2(w * 0.12f, h * 0.45f), new Vector2(w * 0.25f, h * 0.3f), 8), 0.2f);
        ed.BrushRadius = 7; ed.FeatureDensity = 0.5f;
        ed.DevStroke(EditorTool.Rocks, true, Line(new Vector2(w * 0.55f, h * 0.55f), new Vector2(w * 0.6f, h * 0.45f), 4), 0.3f);
        int trees = map.Features.Count(f => f.Kind == FeatureKind.Tree), rocks = map.Features.Count(f => f.Kind == FeatureKind.Rock);
        Check(trees > 20 && rocks > 2, $"painted {trees} trees and {rocks} rocks");
        Check(trees % 2 == 0, "every tree has a mirrored twin");
        ed.BrushRadius = 3;
        ed.DevStroke(EditorTool.Trees, false, new List<Vector2> { new(w * 0.18f, h * 0.38f) }, 0.1f);
        Check(map.Features.Count(f => f.Kind == FeatureKind.Tree) < trees, "right drag cleared some trees");

        // Starts: add a third (no symmetry), move it, remove it.
        ed.SetSymmetry(Symmetry.None);
        ed.DevClick(EditorTool.Starts, true, new Vector2(w * 0.5f, h * 0.15f));
        Check(map.StartPositions.Count == 3, "added a third start");
        ed.DevMoveStart(2, new Vector2(w * 0.55f, h * 0.2f));
        Vector2 moved = MapEdits.ToVec(map.StartPositions[2]);
        Check(MathF.Abs(moved.X - (MathF.Floor(w * 0.55f / 8) * 8 + 4)) < 0.01f, "dragged the start to a new tile centre");
        ed.DevClick(EditorTool.Starts, false, moved);
        Check(map.StartPositions.Count == 2, "right click removed the start");
        ed.SetSymmetry(Symmetry.Rotate);

        // Map settings through the same calls the sliders make.
        ed.SetWaterLevel(58);
        ed.EndSliderEdit();
        await Frames(3);
        ed.SetWind(8, true); ed.SetWind(24, false);
        ed.EndSliderEdit();
        await Frames(3);
        Check(map.WaterLevel == 58 && map.WindMin.Raw >> 16 == 8 && map.WindMax.Raw >> 16 == 24, "water level and wind set");

        // Undo and redo, one step each.
        ulong before = map.Fingerprint();
        ed.SetTheme("desert");
        await Shot("desert_theme");
        ed.Undo();
        Check(map.Theme == "temperate", "undo restored the theme");
        ed.Redo();
        Check(map.Theme == "desert", "redo re-applied the theme");
        ed.Undo();
        ed.Undo(); // the wind
        Check(map.WindMax.Raw >> 16 != 24, "undo stepped back over the wind change");
        ed.Redo();
        Check(map.Fingerprint() == before, "redo got back to the same map (fingerprint)");

        ed.DevValidateNow();
        foreach (MapCheck chk in ed.Checks) Log($"check [{chk.Level}] {chk.Text}");
        Check(!ed.Checks.Any(x => x.Level == CheckLevel.Error), "no errors in the checks panel");
        ed.DevCameraFocus = new Vector3(w * 0.4f, 0, h * 0.6f);
        ed.DevZoom(330);
        await Shot("objects_closeup");
        ed.TogglePassability();
        await Shot("unit_paths_view");
        ed.TogglePassability();

        // Live input path: a real mouse stroke through the viewport.
        await MouseStroke(ed, map);

        // Dialogs.
        ed.ShowHelp();
        await Shot("help");
        ed.DevCloseModal();
        ed.DevShowSave();
        await Shot("save_dialog");
        ed.DevCloseModal();

        // Save, list, reload, fingerprints.
        string file = GameData.SafeFileName("Dev Test Map");
        if (SaveDialog.UserMapExists(file)) DirAccess.RemoveAbsolute($"{GameData.UserMapsDir}/{file}");
        Check(ed.DevSaveAs("Dev Test Map", "Made by the editor's dev script."), "saved to user://maps");
        Check(!ed.Document!.Dirty && ed.Document.FileName == file, "document is clean and knows its file");
        ulong saved = map.Fingerprint();
        string savedJson = map.ToJson();
        MapEntry? entry = GameData.Maps().FirstOrDefault(m => m.Key == "user:" + file);
        Check(entry is not null && entry.Name.StartsWith("Dev Test Map"), "the map appears in GameData.Maps() (the Skirmish list)");
        MapData loaded = entry!.Load();
        Check(loaded.Fingerprint() == saved, $"reloaded fingerprint matches ({loaded.Fingerprint():x16})");
        Check(loaded.ToJson() == savedJson, "reloaded JSON is identical, name, theme and description included");
        ed.DevOpen(entry);
        await Frames(2);
        Check(ed.Document!.Map.Fingerprint() == saved && ed.Document.FileName == file && !ed.Document.Dirty, "reopened in the editor identically");
        await Shot("reopened");

        // The sim accepts the map.
        var players = new List<PlayerSetup> { new("A", 0, "lattice", 0, AiDifficulty.Normal), new("B", 1, "crucible", 1, AiDifficulty.Normal) };
        var world = new World(GameData.Catalog, loaded, 1234, players, new MatchOptions());
        for (int i = 0; i < 300; i++) world.Step();
        Check(world.Units.Count >= 2 && !world.IsOver, $"sim World runs 300 ticks on it ({world.Units.Count} units)");

        // Test play and come back.
        ed.TestPlay();
        await Frames(4);
        var match = GameRoot.Instance.GetNodeOrNull<MatchScene>("Match");
        Check(match is not null && match.Kind == MatchKind.Skirmish && match.Session.World.Map.Fingerprint() == saved, "test play started a skirmish on the map");
        Check(match?.Session.World.Players.Length == 2 && match.Session.World.Players[1].Name == "Normal AI", "1v1 against a Normal AI");
        await Frames(20);
        await Shot("test_play");
        match?.Quit();
        await Frames(4);
        MapEditorScene? back = MapEditorScene.Current;
        Check(back is not null && back != ed && back.Document?.Map.Fingerprint() == saved && back.Document.FileName == file, "leaving the match came back to the editor with the same map");
        await Shot("back_in_editor");

        // Unsaved-changes guard.
        back!.DevStroke(EditorTool.Raise, true, new List<Vector2> { c }, 0.2f);
        Check(back.Document!.Dirty, "editing marks the map unsaved");
        back.RequestExit();
        await Frames(1);
        Check(back.ModalOpen, "leaving with unsaved changes asks first");
        await Shot("unsaved_prompt");
        back.DevCloseModal();
        DirAccess.RemoveAbsolute($"{GameData.UserMapsDir}/{file}");
    }

    private readonly record struct FixPoint(Vector2 P);

    private static IEnumerable<FixPoint> Starts(MapData map) => map.StartPositions.Select(s => new FixPoint(MapEdits.ToVec(s)));

    private static bool SymmetricHeights(MapData map)
    {
        int vw = map.TilesX + 1, vh = map.TilesY + 1, worst = 0;
        for (int y = 0; y < vh; y++)
        for (int x = 0; x < vw; x++)
            worst = Math.Max(worst, Math.Abs(map.Heights[y * vw + x] - map.Heights[(vh - 1 - y) * vw + (vw - 1 - x)]));
        Log($"largest asymmetry: {worst} height steps");
        return worst <= 2;
    }

    /// <summary>Raises ground with real mouse events, to prove input reaches the editor through the HUD.</summary>
    private async Task MouseStroke(MapEditorScene ed, MapData map)
    {
        ed.SetTool(EditorTool.Raise);
        ed.BrushRadius = 5; ed.BrushStrength = 1f;
        var target = new Vector3(map.Width * 0.5f, 0, map.Height * 0.5f);
        ed.DevCameraFocus = target;
        ed.DevZoom(300);
        await Frames(3);
        target.Y = ed.TerrainNode.HeightAt(target.X, target.Z);
        Vector2 screen = ed.DevCamera.UnprojectPosition(target);
        ulong beforeFp = map.Fingerprint();
        bool wasUndoable = ed.Document!.CanUndo;
        Input.WarpMouse(screen);
        Input.ParseInputEvent(new InputEventMouseMotion { Position = screen, GlobalPosition = screen });
        await Frames(2);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = screen, GlobalPosition = screen, ButtonMask = MouseButtonMask.Left });
        await Frames(6);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = screen, GlobalPosition = screen });
        await Frames(2);
        Check(map.Fingerprint() != beforeFp && wasUndoable && ed.Document.CanUndo, $"a real mouse drag at {screen} raised the ground");
        await Key(Godot.Key.Z, ctrl: true);
        Check(map.Fingerprint() == beforeFp, "and one real Ctrl+Z key press took the whole stroke back");
        await Key(Godot.Key.Key4);
        Check(ed.Tool == EditorTool.Flatten, "pressing 4 picks Flatten");
        await Key(Godot.Key.Bracketright);
        Check(ed.BrushRadius == 6, "] makes the brush bigger");
    }

    private async Task Key(Key key, bool ctrl = false)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true, CtrlPressed = ctrl });
        await Frames(1);
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false, CtrlPressed = ctrl });
        await Frames(1);
    }

    // ---- perf ---------------------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    private struct Timespec { public long Sec; public long Nsec; }

    [DllImport("libc", EntryPoint = "clock_gettime")]
    private static extern int ClockGetTime(int clock, out Timespec ts);

    /// <summary>
    /// CPU time used by this thread, in ms (Linux). Wall time is meaningless on a machine shared
    /// with other busy processes; this counts only the work the editor itself did.
    /// </summary>
    private static double ThreadCpuMs()
    {
        try
        {
            const int ClockThreadCputimeId = 3;
            return ClockGetTime(ClockThreadCputimeId, out Timespec t) == 0 ? t.Sec * 1000.0 + t.Nsec / 1e6 : double.NaN;
        }
        catch (Exception) { return double.NaN; }
    }

    private static string Stats(List<double> v)
    {
        if (v.Count == 0 || double.IsNaN(v[0])) return "n/a";
        var s = v.OrderBy(x => x).ToList();
        return $"avg {s.Average():0.00}, p95 {s[(int)(s.Count * 0.95)]:0.00}, max {s[^1]:0.00} ms";
    }

    private async Task Perf()
    {
        MapEditorScene ed = await Editor();
        Log($"timings: 'cpu' is this thread's CPU time per 60 fps frame of brushing (immune to other processes); 'wall' includes waiting for the CPU. Cores: {System.Environment.ProcessorCount}");
        foreach (int n in new[] { 64, 160, 256 })
        {
            ed.DevNewMap(new StartDialog.NewMapSpec($"Perf {n}", n, n, "temperate", 80, 4));
            await Frames(2);
            MapData map = ed.Document!.Map;
            double c0 = ThreadCpuMs();
            ed.TerrainNode.Build(map);
            Log($"{n}x{n}: full rebuild (TerrainView.Build + all ground chunks): cpu {ThreadCpuMs() - c0:0.0} ms, wall {ed.TerrainNode.LastBuildMs:0.0} ms");
            foreach (float radius in new[] { 6f, 16f, 30f })
            {
                ed.BrushRadius = radius;
                ed.BrushStrength = 0.6f;
                var path = Line(new Vector2(map.Width * 0.2f, map.Height * 0.3f), new Vector2(map.Width * 0.45f, map.Height * 0.4f), 20);
                var cpu = new List<double>();
                ed.DevStroke(EditorTool.Raise, true, path, 0.1f, clock: ThreadCpuMs, stepMs: cpu);
                double endCpu = ed.LastStrokeEndMs;
                await Frames(2);
                var smooth = new List<double>();
                ed.DevStroke(EditorTool.Smooth, true, path, 0.1f, clock: ThreadCpuMs, stepMs: smooth);
                await Frames(2);
                var wall = new List<double>();
                ed.DevStroke(EditorTool.Lower, true, path, 0.1f, stepMs: wall);
                await Frames(2);
                Log($"{n}x{n} four-way symmetry, brush radius {radius} tiles: raise per frame cpu {Stats(cpu)}; smooth cpu {Stats(smooth)}; " +
                    $"lower wall {Stats(wall)}; chunks per update {ed.TerrainNode.LastUpdateChunks}; stroke end cpu {endCpu:0.0} ms");
            }
            Log($"{n}x{n} process memory {OS.GetStaticMemoryUsage() / 1048576} MB static, {System.GC.GetTotalMemory(false) / 1048576} MB managed");
            double v0 = ThreadCpuMs();
            List<MapCheck> checks = MapValidator.Run(map);
            Log($"{n}x{n} validation (worker thread, after each edit): cpu {ThreadCpuMs() - v0:0.0} ms");
            double m0 = ThreadCpuMs();
            ed.Hud.Minimap.Refresh(map, now: true);
            Log($"{n}x{n} overview picture render (worker thread, after ground edits): cpu {ThreadCpuMs() - m0:0.0} ms");
            var clicks = new List<double>();
            for (int k = 0; k < 4; k++)
            {
                double k0 = ThreadCpuMs();
                ed.DevClick(EditorTool.Metal, true, new Vector2(map.Width * (0.3f + k * 0.05f), map.Height * 0.6f));
                clicks.Add(ThreadCpuMs() - k0);
            }
            Log($"{n}x{n} placing a metal spot (four-way): cpu {Stats(clicks)}");
            await Shot($"perf_{n}");
        }
    }

    // ---- shots ----------------------------------------------------------------------------------

    private async Task Shots()
    {
        MapEditorScene ed = await Editor();
        await Shot("start_dialog");
        MapEntry builtIn = GameData.Maps().First(m => m.BuiltIn && m.Key == "twin_rivers");
        ed.DevOpen(builtIn);
        await Frames(2);
        await Shot("twin_rivers");
        foreach (EditorTool t in new[] { EditorTool.Plateau, EditorTool.Metal, EditorTool.Trees, EditorTool.Starts })
        {
            ed.SetTool(t);
            Vector2 p = MapEdits.ToVec(ed.Document!.Map.StartPositions[0]) + new Vector2(30, -10);
            ed.DevCameraFocus = new Vector3(p.X, 0, p.Y);
            ed.DevZoom(260);
            await Frames(2);
            Input.WarpMouse(ed.DevCamera.UnprojectPosition(new Vector3(p.X, ed.TerrainNode.HeightAt(p.X, p.Y), p.Y)));
            await Shot($"tool_{t.ToString().ToLowerInvariant()}");
        }
        ed.DevShowStart();
        (ed.DevModal as StartDialog)?.DevPick(4, 5);
        await Shot("open_dialog_custom_size");
        ed.DevCloseModal();
        ed.ShowHelp();
        await Shot("help");
        ed.DevCloseModal();
    }
}
