using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Match;
using ContingencyZero.Sim.Replay;
using Godot;
using FileAccess = Godot.FileAccess;

namespace ContingencyZero.Game.UI;

/// <summary>Reading, importing, exporting and describing replay files (user://replays/*.czr).</summary>
public static class ReplayFiles
{
    public const string Extension = ".czr";

    /// <summary>Loads a replay from a Godot path (user://...) or an absolute OS path.</summary>
    public static MatchReplay Load(string path)
    {
        byte[] bytes = path.StartsWith("user://") || path.StartsWith("res://")
            ? FileAccess.GetFileAsBytes(path)
            : File.ReadAllBytes(path);
        if (bytes.Length == 0) throw new IOException($"Couldn't read {path}");
        using var ms = new MemoryStream(bytes);
        return MatchReplay.Read(ms);
    }

    /// <summary>Saved replay file names, newest first.</summary>
    public static List<string> List()
    {
        DirAccess.MakeDirRecursiveAbsolute(GameData.ReplaysDir);
        using var dir = DirAccess.Open(GameData.ReplaysDir);
        if (dir is null) return new List<string>();
        return dir.GetFiles().Where(f => f.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Copies a replay file from anywhere on disk into the replays folder (checking it parses first).
    /// Returns the file name it got. A name clash gets a numbered suffix.
    /// </summary>
    public static string Import(string osPath)
    {
        MatchReplay replay = Load(osPath); // throws on a bad file
        DirAccess.MakeDirRecursiveAbsolute(GameData.ReplaysDir);
        string baseName = Path.GetFileNameWithoutExtension(osPath);
        if (baseName.Length == 0) baseName = "replay";
        string name = baseName + Extension;
        for (int i = 2; FileAccess.FileExists($"{GameData.ReplaysDir}/{name}"); i++) name = $"{baseName} ({i}){Extension}";
        using var ms = new MemoryStream();
        replay.Write(ms); // re-save, so an imported older-format file is upgraded and bookmarks round-trip
        using var f = FileAccess.Open($"{GameData.ReplaysDir}/{name}", FileAccess.ModeFlags.Write)
            ?? throw new IOException($"Couldn't write {name}: {FileAccess.GetOpenError()}");
        f.StoreBuffer(ms.ToArray());
        return name;
    }

    /// <summary>Copies a saved replay to an OS path of the player's choosing (byte for byte).</summary>
    public static void Export(string fileName, string osPath)
    {
        byte[] bytes = FileAccess.GetFileAsBytes($"{GameData.ReplaysDir}/{fileName}");
        if (bytes.Length == 0) throw new IOException($"Couldn't read {fileName}");
        if (!osPath.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)) osPath += Extension;
        File.WriteAllBytes(osPath, bytes);
    }

    public static string Clock(int ticks)
    {
        int secs = Math.Max(0, ticks) / ContingencyZero.Sim.World.TicksPerSecond;
        return $"{secs / 60}:{secs % 60:D2}";
    }

    /// <summary>The result as recorded in the bookmarks, or a note that the match wasn't finished.</summary>
    public static string Result(MatchReplay r)
    {
        ReplayBookmark end = r.Bookmarks.LastOrDefault(b => b.Kind == ReplayBookmarkKind.GameOver);
        if (end.Text is { Length: > 0 } t) return t.StartsWith("Game over: ") ? t["Game over: ".Length..] : t;
        return $"Unfinished (saved at {Clock(r.TickCount)})";
    }

    /// <summary>A short summary to paste next to a shared file.</summary>
    public static string ShareText(MatchReplay r, string? fileName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Contingency Zero replay: {r.Title}");
        sb.AppendLine($"Map: {r.Scenario.Map.Name}  |  Length: {Clock(r.TickCount)}");
        sb.AppendLine("Players: " + string.Join(" vs ", r.Scenario.Players
            .GroupBy(p => p.Team).OrderBy(g => g.Key)
            .Select(g => string.Join(" + ", g.Select(p => $"{p.Name} ({Faction(p.Faction)}{(p.Ai != ContingencyZero.Sim.Match.AiDifficulty.None ? $" {p.Ai} AI" : "")})")))));
        sb.AppendLine($"Result: {Result(r)}");
        var highlights = r.Bookmarks.Where(b => b.Kind is ReplayBookmarkKind.CommanderKilled or ReplayBookmarkKind.Wave or ReplayBookmarkKind.PlayerDefeated)
            .Take(4).Select(b => $"{Clock(b.Tick)} {b.Text}").ToList();
        if (highlights.Count > 0) sb.AppendLine("Moments: " + string.Join("; ", highlights));
        if (fileName is not null) sb.AppendLine($"File: {fileName}  (Replays > Import, or double-click it)");
        return sb.ToString().TrimEnd();
    }

    private static string Faction(string id) => id.Length == 0 ? "?" : char.ToUpperInvariant(id[0]) + id[1..];

    /// <summary>
    /// If a replay file was passed on the command line (a double-clicked .czr once the extension is
    /// associated with the game), plays it straight away. Returns true if it did.
    /// </summary>
    public static bool OpenFromCommandLine()
    {
        foreach (string arg in OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs()))
        {
            string path = arg.Trim('"');
            if (!path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)) continue;
            if (!File.Exists(path)) { GD.PushWarning($"Replay not found: {path}"); continue; }
            try
            {
                MatchReplay replay = Load(path);
                GD.Print($"Opening replay from the command line: {path}");
                GameRoot.Instance.StartReplay(replay, replay.Title.Length > 0 ? replay.Title : Path.GetFileNameWithoutExtension(path));
                return true;
            }
            catch (Exception e)
            {
                GD.PushWarning($"Couldn't open replay {path}: {e.Message}");
            }
        }
        return false;
    }

    /// <summary>Colour for a bookmark in lists: its player's team colour, or by kind.</summary>
    public static Color Colour(ReplayBookmark b) => b.Kind switch
    {
        ReplayBookmarkKind.GameOver => UiTheme.Amber,
        ReplayBookmarkKind.CommanderKilled or ReplayBookmarkKind.PlayerDefeated => b.Player >= 0 ? UiTheme.TeamColour(b.Player) : UiTheme.Bad,
        _ => b.Player >= 0 ? UiTheme.TeamColour(b.Player) : UiTheme.Ink,
    };
}

/// <summary>Lists saved replays (newest first) with their bookmarks, and plays, imports, exports or shares the chosen one.</summary>
public partial class ReplayBrowser : CanvasLayer
{
    private ItemList _list = null!;
    private ItemList _marks = null!;
    private Label _info = null!;
    private Label _marksHint = null!;
    private Label _status = null!;
    private readonly List<string> _files = new();
    private MatchReplay? _current;
    private int _currentIndex = -1;

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
        outer.OffsetLeft = 80; outer.OffsetTop = 50; outer.OffsetRight = -80; outer.OffsetBottom = -50;
        outer.AddThemeConstantOverride("separation", 12);
        root.AddChild(outer);
        outer.AddChild(UiTheme.Label("REPLAYS", 36));
        outer.AddChild(UiTheme.Label("Every match you finish (or quit) is saved here automatically. Import a friend's .czr file, or export one to share.", 15, UiTheme.Dim));

        var columns = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 16);
        outer.AddChild(columns);

        var left = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 8);
        columns.AddChild(left);
        _list = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _list.AddThemeFontSizeOverride("font_size", 16);
        _list.ItemSelected += i => Describe((int)i);
        _list.ItemActivated += i => Play((int)i, 0);
        left.AddChild(_list);
        _info = UiTheme.Label("", 15);
        _info.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        left.AddChild(_info);

        var right = new VBoxContainer { CustomMinimumSize = new Vector2(440, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 8);
        columns.AddChild(right);
        right.AddChild(UiTheme.Label("BOOKMARKS", 16, UiTheme.Dim));
        _marks = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _marks.AddThemeFontSizeOverride("font_size", 15);
        _marks.ItemActivated += i => { if (_current is not null && _currentIndex >= 0) Play(_currentIndex, _current.Bookmarks[(int)i].Tick); };
        right.AddChild(_marks);
        _marksHint = UiTheme.Label("Select a replay to see its moments. Double-click one to start watching from there.", 13, UiTheme.Dim);
        _marksHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        right.AddChild(_marksHint);

        _status = UiTheme.Label("", 14, UiTheme.Accent);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        outer.AddChild(_status);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddThemeConstantOverride("separation", 10);
        buttons.AddChild(UiTheme.Button("Back", GameRoot.Instance.ShowMainMenu, 120));
        buttons.AddChild(UiTheme.Button("Open folder", () => OS.ShellOpen(ProjectSettings.GlobalizePath(GameData.ReplaysDir)), 140));
        buttons.AddChild(UiTheme.Button("Import...", Import, 120));
        buttons.AddChild(UiTheme.Button("Export...", Export, 120));
        var share = UiTheme.Button("Copy share text", CopyShareText, 170);
        share.TooltipText = "Puts a short summary (title, map, players, length, result) on the clipboard to paste next to the file";
        buttons.AddChild(share);
        buttons.AddChild(UiTheme.Button("Delete", Delete, 110));
        buttons.AddChild(UiTheme.Button("Watch", () => { if (_currentIndex >= 0) Play(_currentIndex, 0); }, 150));
        outer.AddChild(buttons);
        Load();
    }

    private void Load(string? select = null)
    {
        _list.Clear();
        _marks.Clear();
        _files.Clear();
        _current = null;
        _currentIndex = -1;
        foreach (string f in ReplayFiles.List())
        {
            _files.Add(f);
            _list.AddItem(f.Replace(ReplayFiles.Extension, "").Replace('_', ' '));
        }
        if (_files.Count == 0) _info.Text = "No replays yet. Finish a match and it will appear here, or import one.";
        else
        {
            int i = select is null ? 0 : Math.Max(0, _files.IndexOf(select));
            _list.Select(i);
            _list.EnsureCurrentIsVisible();
            Describe(i);
        }
    }

    private MatchReplay? Read(int i)
    {
        if (i == _currentIndex && _current is not null) return _current;
        try
        {
            _current = ReplayFiles.Load($"{GameData.ReplaysDir}/{_files[i]}");
            _currentIndex = i;
            return _current;
        }
        catch (Exception e)
        {
            _info.Text = $"Couldn't read that replay: {e.Message}";
            _current = null;
            _currentIndex = -1;
            return null;
        }
    }

    private void Describe(int i)
    {
        _marks.Clear();
        MatchReplay? r = Read(i);
        if (r is null) return;
        string players = string.Join(", ", r.Scenario.Players.Select(p => $"{p.Name} (team {p.Team + 1})"));
        _info.Text = $"{r.Title}\n{r.Scenario.Map.Name} · {ReplayFiles.Clock(r.TickCount)} · {players}\nResult: {ReplayFiles.Result(r)}";
        if (r.CatalogFingerprint != 0 && r.CatalogFingerprint != GameData.Catalog.Fingerprint)
            _info.Text += "\nThis replay was recorded with different unit stats and may not play back correctly.";

        foreach (ReplayBookmark b in r.Bookmarks)
        {
            int idx = _marks.AddItem($"{ReplayFiles.Clock(b.Tick),6}   {b.Text}");
            _marks.SetItemCustomFgColor(idx, ReplayFiles.Colour(b));
        }
        _marksHint.Text = r.Bookmarks.Count == 0
            ? "No bookmarks in this replay (recorded before bookmarks existed, or nothing much happened)."
            : $"{r.Bookmarks.Count} moments. Double-click one to start watching just before it.";
    }

    private void Play(int i, int tick)
    {
        MatchReplay? r = Read(i);
        if (r is null) return;
        GameRoot.Instance.StartReplay(r, r.Title, Math.Max(0, tick - ReplayControls.LeadInTicks));
    }

    private void Delete()
    {
        if (_currentIndex < 0) return;
        DirAccess.RemoveAbsolute($"{GameData.ReplaysDir}/{_files[_currentIndex]}");
        Load();
    }

    private void CopyShareText()
    {
        if (_currentIndex < 0 || Read(_currentIndex) is not { } r) return;
        string text = ReplayFiles.ShareText(r, _files[_currentIndex]);
        DisplayServer.ClipboardSet(text);
        _status.Text = "Copied to the clipboard:\n" + text;
    }

    private FileDialog MakeDialog(FileDialog.FileModeEnum mode, string title)
    {
        var dlg = new FileDialog
        {
            FileMode = mode,
            Access = FileDialog.AccessEnum.Filesystem,
            Title = title,
            UseNativeDialog = DisplayServer.HasFeature(DisplayServer.Feature.NativeDialogFile),
            Size = new Vector2I(820, 560),
            Theme = UiTheme.Theme,
        };
        dlg.Filters = new[] { $"*{ReplayFiles.Extension} ; Contingency Zero replay" };
        dlg.Canceled += dlg.QueueFree;
        AddChild(dlg);
        return dlg;
    }

    private void Import()
    {
        FileDialog dlg = MakeDialog(FileDialog.FileModeEnum.OpenFile, "Import a replay");
        dlg.FileSelected += path =>
        {
            dlg.QueueFree();
            try
            {
                string name = ReplayFiles.Import(path);
                _status.Text = $"Imported {name}";
                Load(name);
            }
            catch (Exception e)
            {
                _status.Text = $"Couldn't import {Path.GetFileName(path)}: {e.Message}";
            }
        };
        dlg.PopupCentered();
    }

    private void Export()
    {
        if (_currentIndex < 0) return;
        string file = _files[_currentIndex];
        FileDialog dlg = MakeDialog(FileDialog.FileModeEnum.SaveFile, "Export a copy of the replay");
        dlg.CurrentFile = file;
        dlg.FileSelected += path =>
        {
            dlg.QueueFree();
            try
            {
                ReplayFiles.Export(file, path);
                _status.Text = $"Exported to {path}";
            }
            catch (Exception e)
            {
                _status.Text = $"Couldn't export: {e.Message}";
            }
        };
        dlg.PopupCentered();
    }
}

/// <summary>
/// Playback bar for replays: pause, speed, whose view to watch, the casting panel, and a strip of
/// bookmark buttons to jump to (N for the next one). Jumping forward runs the sim ahead; jumping
/// back rebuilds the match from the replay and runs forward from the start.
/// </summary>
public partial class ReplayControls : CanvasLayer
{
    /// <summary>A jump lands this long before the bookmark, so you see it happen.</summary>
    public const int LeadInTicks = 3 * ContingencyZero.Sim.World.TicksPerSecond;
    /// <summary>Bottom edge of the bar, for panels that sit under it.</summary>
    public const int PanelBottom = 146;

    private readonly MatchScene _match;
    private readonly MatchReplay? _replay;
    private Label _time = null!;
    private Label _seeking = null!;
    private Label _next = null!;
    private ProgressBar _progress = null!;
    private readonly List<Button> _markButtons = new();
    private bool _wasSeeking;

    public ReplayControls(MatchScene match, MatchReplay? replay = null)
    {
        _match = match;
        _replay = replay;
    }

    private IReadOnlyList<ReplayBookmark> Bookmarks => _replay?.Bookmarks ?? (IReadOnlyList<ReplayBookmark>)Array.Empty<ReplayBookmark>();

    public override void _Ready()
    {
        Layer = 15;
        var root = new Control { Theme = UiTheme.Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 6);
        var panel = UiTheme.Panel(rows, 8);
        panel.AnchorLeft = 0.5f; panel.AnchorRight = 0.5f;
        panel.OffsetLeft = -470; panel.OffsetRight = 470; panel.OffsetTop = 58;
        root.AddChild(panel);

        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 8);
        rows.AddChild(bar);
        bar.AddChild(UiTheme.Button("Pause", () => _match.Session.Paused = !_match.Session.Paused, 76, 14));
        foreach (double s in new[] { 0.5, 1, 2, 4, 8 })
            bar.AddChild(UiTheme.Button($"x{s}", () => _match.Session.Speed = s, 48, 14));
        var view = new OptionButton();
        view.AddItem("Everyone");
        var players = _match.Session.World.Players;
        foreach (var p in players) view.AddItem(p.Name);
        view.ItemSelected += i => _match.SetViewer(i == 0 ? -1 : players[i - 1].Team);
        bar.AddChild(view);
        _progress = new ProgressBar { CustomMinimumSize = new Vector2(110, 10), MaxValue = 1, ShowPercentage = false, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        bar.AddChild(_progress);
        _time = UiTheme.Label("", 14);
        _time.CustomMinimumSize = new Vector2(96, 0);
        _time.VerticalAlignment = VerticalAlignment.Center;
        bar.AddChild(_time);
        var next = UiTheme.Button("Next >", JumpToNext, 70, 14);
        next.TooltipText = "Jump to the next bookmark (N)";
        bar.AddChild(next);
        var casting = UiTheme.Button("Casting", () => _match.GetNodeOrNull<CastingOverlay>("Casting")?.Toggle(), 80, 14);
        casting.TooltipText = "Broadcast panel: incomes, army value, kills and graphs for every player (C)";
        bar.AddChild(casting);

        // Bookmark strip: one small button per moment, coloured by player, with the text as a tooltip.
        var stripRow = new HBoxContainer();
        stripRow.AddThemeConstantOverride("separation", 6);
        rows.AddChild(stripRow);
        var strip = new HBoxContainer();
        strip.AddThemeConstantOverride("separation", 3);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0, 30), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(strip);
        _seeking = UiTheme.Label("", 13, UiTheme.Amber);
        _seeking.VerticalAlignment = VerticalAlignment.Center;
        _next = UiTheme.Label("", 13, UiTheme.Dim);
        _next.VerticalAlignment = VerticalAlignment.Center;
        _next.CustomMinimumSize = new Vector2(230, 0);
        _next.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        stripRow.AddChild(_next);
        stripRow.AddChild(scroll);
        stripRow.AddChild(_seeking);
        if (Bookmarks.Count == 0)
        {
            strip.AddChild(UiTheme.Label("No bookmarks in this replay", 13, UiTheme.Dim));
        }
        for (int i = 0; i < Bookmarks.Count; i++)
        {
            ReplayBookmark b = Bookmarks[i];
            int tick = b.Tick;
            var button = new Button { Text = ReplayFiles.Clock(b.Tick), TooltipText = $"{ReplayFiles.Clock(b.Tick)}  {b.Text}", CustomMinimumSize = new Vector2(0, 26) };
            button.AddThemeFontSizeOverride("font_size", 12);
            button.AddThemeColorOverride("font_color", ReplayFiles.Colour(b));
            button.AddThemeStyleboxOverride("normal", UiTheme.Box(new Color(0.12f, 0.12f, 0.11f), new Color(ReplayFiles.Colour(b), 0.6f), 1, 1, 3));
            button.AddThemeStyleboxOverride("hover", UiTheme.Box(new Color(0.22f, 0.23f, 0.19f), UiTheme.Accent, 1, 1, 3));
            button.AddThemeStyleboxOverride("pressed", UiTheme.Box(new Color(0.28f, 0.31f, 0.18f), UiTheme.Accent, 1, 1, 3));
            button.Pressed += () => { Audio.Sfx.Play(Audio.SfxKind.Click); JumpTo(tick); };
            strip.AddChild(button);
            _markButtons.Add(button);
        }
    }

    /// <summary>Starts watching from just before <paramref name="tick"/> (forward: run ahead; back: rebuild from the replay).</summary>
    public void JumpTo(int tick)
    {
        int target = Math.Max(0, tick - LeadInTicks);
        SimSession s = _match.Session;
        if (target >= s.World.Tick)
        {
            s.SeekTo(target);
        }
        else if (_replay is not null)
        {
            // Can't step backwards: start the replay again and run forward to the target.
            MatchReplay replay = _replay;
            string title = _match.Title;
            Callable.From(() => GameRoot.Instance.StartReplay(replay, title, target)).CallDeferred();
        }
    }

    /// <summary>Jumps to the first bookmark after the current time.</summary>
    public void JumpToNext()
    {
        int now = _match.Session.World.Tick;
        foreach (ReplayBookmark b in Bookmarks)
            if (b.Tick - LeadInTicks > now) { JumpTo(b.Tick); return; }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.N, CtrlPressed: false, AltPressed: false })
        {
            JumpToNext();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (_match.Session.Channel is not ReplayChannel rc) return;
        SimSession s = _match.Session;
        int t = s.World.Tick, total = Math.Max(1, rc.LastTick);
        _progress.Value = (double)t / total;
        _time.Text = $"{ReplayFiles.Clock(t)} / {ReplayFiles.Clock(total)}";
        bool seeking = s.Seeking;
        _seeking.Text = seeking ? $"Seeking to {ReplayFiles.Clock(s.SeekTarget)}..." : "";
        if (_wasSeeking && !seeking) _match.Units.Snapshot(); // land cleanly instead of sliding across the map
        _wasSeeking = seeking;

        // Mark passed bookmarks and say which is next.
        string next = "";
        for (int i = 0; i < _markButtons.Count; i++)
        {
            ReplayBookmark b = Bookmarks[i];
            bool passed = b.Tick <= t;
            _markButtons[i].Modulate = passed ? new Color(1, 1, 1, 0.45f) : Colors.White;
            if (!passed && next.Length == 0) next = $"Next: {ReplayFiles.Clock(b.Tick)} {b.Text}";
        }
        if (next.Length == 0 && Bookmarks.Count > 0) next = "No more bookmarks";
        if (_next.Text != next) _next.Text = next;
    }
}
