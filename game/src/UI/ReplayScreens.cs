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
        return Save(replay, Path.GetFileNameWithoutExtension(osPath));
    }

    /// <summary>
    /// Writes a replay into the replays folder under a name based on <paramref name="baseName"/>
    /// (numbered on a clash). Re-saving upgrades older formats. Returns the file name.
    /// </summary>
    public static string Save(MatchReplay replay, string baseName)
    {
        DirAccess.MakeDirRecursiveAbsolute(GameData.ReplaysDir);
        baseName = new string(baseName.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ' ' or '(' or ')' ? c : '_').ToArray()).Trim();
        if (baseName.Length == 0) baseName = "replay";
        if (baseName.Length > 60) baseName = baseName[..60];
        string name = baseName + Extension;
        for (int i = 2; FileAccess.FileExists($"{GameData.ReplaysDir}/{name}"); i++) name = $"{baseName} ({i}){Extension}";
        using var ms = new MemoryStream();
        replay.Write(ms);
        using var f = FileAccess.Open($"{GameData.ReplaysDir}/{name}", FileAccess.ModeFlags.Write)
            ?? throw new IOException($"Couldn't write {name}: {FileAccess.GetOpenError()}");
        f.StoreBuffer(ms.ToArray());
        return name;
    }

    /// <summary>Imports a pasted share code into the replays folder. Returns the file name.</summary>
    public static string ImportCode(string code)
    {
        MatchReplay replay = ShareCodes.DecodeReplay(code);
        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        return Save(replay, $"{stamp}_shared_{GameData.SafeFileName(replay.Scenario.Map.Name).Replace(".json", "")}");
    }

    /// <summary>The share code for a saved replay file (its bytes as they are on disk).</summary>
    public static string CodeFor(string fileName)
    {
        byte[] bytes = FileAccess.GetFileAsBytes($"{GameData.ReplaysDir}/{fileName}");
        if (bytes.Length == 0) throw new IOException($"Couldn't read {fileName}");
        return ShareCodes.ReplayCode(bytes);
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
        var highlights = r.Bookmarks.Where(b => b.Kind is ReplayBookmarkKind.CommanderKilled or ReplayBookmarkKind.Wave or ReplayBookmarkKind.PlayerDefeated or ReplayBookmarkKind.BigBattle)
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
        ReplayBookmarkKind.BigBattle => new Color(1f, 0.5f, 0.2f),
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
        var share = UiTheme.Button("Copy share text", CopyShareText, 160);
        share.TooltipText = "Puts a short summary (title, map, players, length, result) on the clipboard to paste next to the file";
        buttons.AddChild(share);
        var code = UiTheme.Button("Copy share code", CopyShareCode, 160);
        code.TooltipText = "Puts the whole replay on the clipboard as a text code (" + ShareCodes.ReplayPrefix + "...) to paste into chat";
        code.Name = "CopyShareCode";
        buttons.AddChild(code);
        var paste = UiTheme.Button("Paste code", PasteCode, 120);
        paste.TooltipText = "Add a replay someone sent you as a share code";
        paste.Name = "PasteCode";
        buttons.AddChild(paste);
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
            int marks = BookmarkCount(f);
            string label = f.Replace(ReplayFiles.Extension, "").Replace('_', ' ');
            _list.AddItem(marks > 0 ? $"{label}   ·  {marks} bookmark{(marks == 1 ? "" : "s")}" : label);
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
        _markCounts.Remove(_files[_currentIndex]);
        Load();
    }

    /// <summary>Bookmark counts per file name, so the list doesn't re-read every replay each time it is shown.</summary>
    private static readonly Dictionary<string, int> _markCounts = new();

    private static int BookmarkCount(string file)
    {
        if (_markCounts.TryGetValue(file, out int n)) return n;
        try { n = ReplayFiles.Load($"{GameData.ReplaysDir}/{file}").Bookmarks.Count; }
        catch (Exception) { n = 0; }
        _markCounts[file] = n;
        return n;
    }

    private void CopyShareCode()
    {
        if (_currentIndex < 0) return;
        try
        {
            string code = ReplayFiles.CodeFor(_files[_currentIndex]);
            AddChild(ShareCodeDialog.ShowCode("SHARE REPLAY", "replay", code));
            _status.Text = $"Share code copied ({ShareCodes.Size(code.Length)}).";
        }
        catch (Exception e)
        {
            _status.Text = $"Couldn't make a code: {e.Message}";
        }
    }

    /// <summary>Opens the paste box; on success the new replay is selected.</summary>
    public void PasteCode()
    {
        string? imported = null;
        AddChild(ShareCodeDialog.Paste("PASTE REPLAY CODE", "replay", ShareCodes.ReplayPrefix, text =>
        {
            imported = ReplayFiles.ImportCode(text);
            return $"Added {imported}";
        }, () =>
        {
            _status.Text = $"Imported {imported} from a share code";
            Load(imported);
        }));
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
/// Playback bar for replays and spectating: pause, speed, whose view to watch and the casting
/// panel. For replays also a timeline with a marker per bookmark (click a marker to jump just
/// before it, click anywhere else to go to that moment), previous / next bookmark buttons
/// (comma / N) and progress while seeking. Jumping forward runs the sim ahead; jumping back
/// rebuilds the match from the replay and runs forward from the start.
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
    private ReplayTimeline? _timeline;
    private bool _wasSeeking;
    private OptionButton? _view;
    private Control _panel = null!;
    private float _castingTop = -1;
    /// <summary>Viewer settings handed from a replay to its rebuilt self when seeking backwards.</summary>
    private static (bool Casting, double Speed, int View)? _carry;
    /// <summary>Tick the seek started from, for the progress readout.</summary>
    private int _seekFrom;

    /// <param name="replay">The replay being watched, or null when spectating a live match (no timeline).</param>
    public ReplayControls(MatchScene match, MatchReplay? replay = null)
    {
        _match = match;
        _replay = replay;
    }

    private IReadOnlyList<ReplayBookmark> Bookmarks => _replay?.Bookmarks ?? (IReadOnlyList<ReplayBookmark>)Array.Empty<ReplayBookmark>();
    private bool IsReplay => _replay is not null;

    /// <summary>The timeline (replays only), for dev automation.</summary>
    public ReplayTimeline? Timeline => _timeline;

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
        _panel = panel;

        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 8);
        rows.AddChild(bar);
        if (!IsReplay)
        {
            var tag = UiTheme.Label("SPECTATING", 15, UiTheme.Amber);
            tag.VerticalAlignment = VerticalAlignment.Center;
            bar.AddChild(tag);
        }
        bool canChangeSpeed = !_match.Session.IsMultiplayer;
        if (canChangeSpeed)
        {
            bar.AddChild(UiTheme.Button("Pause", () => _match.Session.Paused = !_match.Session.Paused, 76, 14));
            foreach (double s in new[] { 0.5, 1, 2, 4, 8 })
                bar.AddChild(UiTheme.Button($"x{s}", () => _match.Session.Speed = s, 48, 14));
        }
        var view = new OptionButton { TooltipText = "Whose eyes to watch through: everything, or one player's vision and fog" };
        view.AddItem("Vision: all");
        var players = _match.Session.World.Players;
        foreach (var p in players) view.AddItem($"Vision: {p.Name}");
        view.ItemSelected += i =>
        {
            _match.SetViewer(i == 0 ? -1 : players[i - 1].Team);
            if (_match.Hud is { } hud) hud.WatchedPlayer = (int)i - 1; // the resource bar follows the chosen player
        };
        view.Name = "Vision";
        bar.AddChild(view);
        _view = view;
        _time = UiTheme.Label("", 14);
        _time.CustomMinimumSize = new Vector2(96, 0);
        _time.VerticalAlignment = VerticalAlignment.Center;
        bar.AddChild(_time);
        if (IsReplay)
        {
            var prev = UiTheme.Button("< Prev", JumpToPrevious, 70, 14);
            prev.TooltipText = "Jump to the previous bookmark (comma)";
            bar.AddChild(prev);
            var next = UiTheme.Button("Next >", JumpToNext, 70, 14);
            next.TooltipText = "Jump to the next bookmark (N)";
            bar.AddChild(next);
        }
        var casting = UiTheme.Button("Casting", () => _match.GetNodeOrNull<CastingOverlay>("Casting")?.Toggle(), 80, 14);
        casting.TooltipText = "Broadcast panel: incomes, army value, losses, kills and graphs for every player (C)";
        bar.AddChild(casting);

        if (IsReplay)
        {
            _timeline = new ReplayTimeline(this, Bookmarks) { CustomMinimumSize = new Vector2(0, 26), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Name = "Timeline" };
            rows.AddChild(_timeline);
        }

        var infoRow = new HBoxContainer();
        infoRow.AddThemeConstantOverride("separation", 10);
        rows.AddChild(infoRow);
        _next = UiTheme.Label("", 13, UiTheme.Dim);
        _next.VerticalAlignment = VerticalAlignment.Center;
        _next.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _next.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        infoRow.AddChild(_next);
        _seeking = UiTheme.Label("", 13, UiTheme.Amber);
        _seeking.VerticalAlignment = VerticalAlignment.Center;
        infoRow.AddChild(_seeking);
        if (!IsReplay) _next.Text = "Watching live. Press C for the casting panel.";

        // Sit the casting panel under this bar, and restore settings carried over a backwards seek.
        var carry = IsReplay ? _carry : null;
        _carry = null;
        Callable.From(() =>
        {
            CastingOverlay? c = _match.GetNodeOrNull<CastingOverlay>("Casting");
            if (carry is { } k)
            {
                _match.Session.Speed = k.Speed;
                if (c is not null) c.Shown = k.Casting;
                if (k.View > 0 && k.View < view.ItemCount)
                {
                    view.Select(k.View);
                    _match.SetViewer(players[k.View - 1].Team);
                    if (_match.Hud is { } hud) hud.WatchedPlayer = k.View - 1;
                }
            }
        }).CallDeferred();
    }

    public int CurrentTick => _match.Session.World.Tick;
    public int TotalTicks => _match.Session.Channel is ReplayChannel rc ? Math.Max(1, rc.LastTick) : Math.Max(1, CurrentTick);
    /// <summary>Where a seek is heading (-1 when not seeking).</summary>
    public int SeekTarget => _match.Session.Seeking ? _match.Session.SeekTarget : -1;

    /// <summary>Starts watching from just before bookmark tick <paramref name="tick"/>.</summary>
    public void JumpTo(int tick) => SeekExact(Math.Max(0, tick - LeadInTicks));

    /// <summary>Goes to exactly <paramref name="target"/> (forward: run ahead; back: rebuild from the replay).</summary>
    public void SeekExact(int target)
    {
        if (_replay is null) return;
        target = Math.Clamp(target, 0, TotalTicks);
        SimSession s = _match.Session;
        if (target >= s.World.Tick)
        {
            _seekFrom = s.World.Tick;
            s.SeekTo(target);
        }
        else
        {
            // Can't step backwards: start the replay again and run forward to the target.
            MatchReplay replay = _replay;
            string title = _match.Title;
            bool casting = _match.GetNodeOrNull<CastingOverlay>("Casting")?.Shown ?? false;
            double speed = s.Speed;
            Callable.From(() =>
            {
                // Keep what the viewer had on (casting panel, speed, vision) across the rebuild.
                _carry = (casting, speed, _view?.Selected ?? 0);
                GameRoot.Instance.StartReplay(replay, title, target);
            }).CallDeferred();
        }
    }

    /// <summary>Jumps to the first bookmark after the current time.</summary>
    public void JumpToNext()
    {
        int now = CurrentTick;
        foreach (ReplayBookmark b in Bookmarks)
            if (b.Tick - LeadInTicks > now) { JumpTo(b.Tick); return; }
    }

    /// <summary>Jumps to the bookmark before the one just passed (so pressing it twice keeps going back).</summary>
    public void JumpToPrevious()
    {
        int now = CurrentTick;
        // A bookmark we just jumped to (we're within its lead-in plus a couple of seconds) doesn't count.
        int slack = LeadInTicks + 2 * ContingencyZero.Sim.World.TicksPerSecond;
        for (int i = Bookmarks.Count - 1; i >= 0; i--)
            if (Bookmarks[i].Tick + slack < now) { JumpTo(Bookmarks[i].Tick); return; }
        SeekExact(0);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!IsReplay || e is not InputEventKey { Pressed: true, Echo: false, CtrlPressed: false, AltPressed: false } k) return;
        if (k.Keycode == Key.N) { JumpToNext(); GetViewport().SetInputAsHandled(); }
        else if (k.Keycode == Key.Comma) { JumpToPrevious(); GetViewport().SetInputAsHandled(); }
    }

    public override void _Process(double delta)
    {
        // Keep the casting panel just under this bar, whatever height the bar ends up.
        float bottom = _panel.GetRect().End.Y + 6;
        if (Math.Abs(bottom - _castingTop) > 0.5f && _match.GetNodeOrNull<CastingOverlay>("Casting") is { } casting)
        {
            casting.SetTop(bottom);
            _castingTop = bottom;
        }
        SimSession s = _match.Session;
        int t = s.World.Tick;
        if (!IsReplay)
        {
            _time.Text = ReplayFiles.Clock(t) + (s.Paused ? "  paused" : s.Speed != 1 ? $"  x{s.Speed}" : "");
            return;
        }
        int total = TotalTicks;
        _time.Text = $"{ReplayFiles.Clock(t)} / {ReplayFiles.Clock(total)}";
        bool seeking = s.Seeking;
        if (seeking)
        {
            int span = Math.Max(1, s.SeekTarget - _seekFrom);
            float pct = Math.Clamp((t - _seekFrom) / (float)span, 0, 1) * 100;
            _seeking.Text = $"Seeking to {ReplayFiles.Clock(s.SeekTarget)}...  {pct:0}%";
        }
        else if (_seeking.Text.Length > 0) _seeking.Text = "";
        if (_wasSeeking && !seeking) _match.Units.Snapshot(); // land cleanly instead of sliding across the map
        _wasSeeking = seeking;
        _timeline?.QueueRedraw();

        string next = "";
        foreach (ReplayBookmark b in Bookmarks)
            if (b.Tick > t) { next = $"Next: {ReplayFiles.Clock(b.Tick)} {b.Text}"; break; }
        if (Bookmarks.Count == 0) next = "No bookmarks in this replay. Click the timeline to skip ahead.";
        else if (next.Length == 0) next = "No more bookmarks";
        if (_next.Text != next) _next.Text = next;
    }
}

/// <summary>
/// The replay's time bar: played part filled, a marker per bookmark coloured by player (tooltip
/// says what happened), the seek target while seeking. Click a marker to jump to it, or anywhere
/// else on the bar to go to that moment.
/// </summary>
public partial class ReplayTimeline : Control
{
    private const float MarkerHit = 7f;
    private readonly ReplayControls _controls;
    private readonly IReadOnlyList<ReplayBookmark> _marks;
    private float _hoverX = -1;

    public ReplayTimeline(ReplayControls controls, IReadOnlyList<ReplayBookmark> marks)
    {
        _controls = controls;
        _marks = marks;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        TooltipText = " "; // non-empty so _GetTooltip is asked
    }

    private Rect2 Track => new(6, Size.Y / 2 - 4, Math.Max(1, Size.X - 12), 8);
    private float XOf(int tick) => Track.Position.X + Track.Size.X * Math.Clamp(tick / (float)_controls.TotalTicks, 0, 1);
    private int TickAt(float x) => (int)Math.Round(Math.Clamp((x - Track.Position.X) / Track.Size.X, 0, 1) * _controls.TotalTicks);

    /// <summary>The bookmark whose marker is under <paramref name="x"/>, or -1.</summary>
    public int MarkerAt(float x)
    {
        int best = -1;
        float bestD = MarkerHit;
        for (int i = 0; i < _marks.Count; i++)
        {
            float d = Math.Abs(XOf(_marks[i].Tick) - x);
            if (d <= bestD) { bestD = d; best = i; }
        }
        return best;
    }

    /// <summary>Screen-space centre of a marker (dev automation clicks it).</summary>
    public Vector2 MarkerPosition(int index) => GlobalPosition + new Vector2(XOf(_marks[index].Tick), Size.Y / 2);

    public override string _GetTooltip(Vector2 atPosition)
    {
        int m = MarkerAt(atPosition.X);
        if (m >= 0) return $"{ReplayFiles.Clock(_marks[m].Tick)}  {_marks[m].Text}\nClick to jump here";
        return $"{ReplayFiles.Clock(TickAt(atPosition.X))}: click to go here";
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion mm) { _hoverX = mm.Position.X; QueueRedraw(); }
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
        {
            Audio.Sfx.Play(Audio.SfxKind.Click);
            int m = MarkerAt(mb.Position.X);
            if (m >= 0) _controls.JumpTo(_marks[m].Tick);
            else _controls.SeekExact(TickAt(mb.Position.X));
            AcceptEvent();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit) { _hoverX = -1; QueueRedraw(); }
    }

    public override void _Draw()
    {
        Rect2 track = Track;
        int now = _controls.CurrentTick;
        DrawRect(track, new Color(0, 0, 0, 0.55f));
        DrawRect(new Rect2(track.Position, new Vector2(XOf(now) - track.Position.X, track.Size.Y)), new Color(UiTheme.Accent, 0.55f));
        DrawRect(track, new Color(UiTheme.PanelEdge, 0.9f), false, 1);
        // Minute ticks.
        int minutes = _controls.TotalTicks / (60 * ContingencyZero.Sim.World.TicksPerSecond);
        int every = minutes > 40 ? 10 : minutes > 15 ? 5 : 1;
        for (int m = every; m <= minutes; m += every)
        {
            float x = XOf(m * 60 * ContingencyZero.Sim.World.TicksPerSecond);
            DrawLine(new Vector2(x, track.End.Y), new Vector2(x, track.End.Y + 3), new Color(UiTheme.Dim, 0.6f), 1);
        }
        int seek = _controls.SeekTarget;
        if (seek >= 0)
        {
            float sx = XOf(seek);
            DrawLine(new Vector2(sx, 1), new Vector2(sx, Size.Y - 1), UiTheme.Amber, 2);
        }
        int hover = _hoverX >= 0 ? MarkerAt(_hoverX) : -1;
        for (int i = 0; i < _marks.Count; i++)
        {
            ReplayBookmark b = _marks[i];
            float x = XOf(b.Tick);
            Color c = ReplayFiles.Colour(b);
            if (b.Tick <= now) c = new Color(c, 0.5f);
            float r = i == hover ? 7 : 5;
            float cy = Size.Y / 2;
            var diamond = new[] { new Vector2(x, cy - r), new Vector2(x + r, cy), new Vector2(x, cy + r), new Vector2(x - r, cy) };
            DrawColoredPolygon(diamond, c);
            DrawPolyline(new[] { diamond[0], diamond[1], diamond[2], diamond[3], diamond[0] }, new Color(0, 0, 0, 0.8f), 1);
        }
        // Playhead.
        float px = XOf(now);
        DrawRect(new Rect2(px - 1.5f, 1, 3, Size.Y - 2), UiTheme.Ink);
        if (_hoverX >= 0 && hover < 0)
            DrawLine(new Vector2(_hoverX, 2), new Vector2(_hoverX, Size.Y - 2), new Color(UiTheme.Ink, 0.35f), 1);
    }
}
