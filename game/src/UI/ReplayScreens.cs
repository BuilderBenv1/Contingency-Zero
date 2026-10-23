using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Match;
using ContingencyZero.Sim.Replay;
using Godot;
using FileAccess = Godot.FileAccess;

namespace ContingencyZero.Game.UI;

/// <summary>Lists saved replays (newest first) and plays the chosen one.</summary>
public partial class ReplayBrowser : CanvasLayer
{
    private ItemList _list = null!;
    private Label _info = null!;
    private readonly List<string> _files = new();

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
        outer.AddChild(UiTheme.Label("Every match you finish (or quit) is saved here automatically.", 15, UiTheme.Dim));

        _list = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _list.AddThemeFontSizeOverride("font_size", 16);
        _list.ItemSelected += i => Describe((int)i);
        _list.ItemActivated += i => Play((int)i);
        outer.AddChild(_list);
        _info = UiTheme.Label("", 15);
        outer.AddChild(_info);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddThemeConstantOverride("separation", 12);
        buttons.AddChild(UiTheme.Button("Back", GameRoot.Instance.ShowMainMenu, 140));
        buttons.AddChild(UiTheme.Button("Open folder", () => OS.ShellOpen(ProjectSettings.GlobalizePath(GameData.ReplaysDir)), 160));
        buttons.AddChild(UiTheme.Button("Delete", Delete, 120));
        buttons.AddChild(UiTheme.Button("Watch", () => { if (_list.GetSelectedItems().Length > 0) Play(_list.GetSelectedItems()[0]); }, 160));
        outer.AddChild(buttons);
        Load();
    }

    private void Load()
    {
        _list.Clear();
        _files.Clear();
        DirAccess.MakeDirRecursiveAbsolute(GameData.ReplaysDir);
        using var dir = DirAccess.Open(GameData.ReplaysDir);
        if (dir is null) return;
        foreach (string f in dir.GetFiles().Where(f => f.EndsWith(".czr")).OrderByDescending(f => f, StringComparer.Ordinal))
        {
            _files.Add(f);
            _list.AddItem(f.Replace(".czr", "").Replace('_', ' '));
        }
        if (_files.Count == 0) _info.Text = "No replays yet. Finish a match and it will appear here.";
    }

    private MatchReplay? Read(int i)
    {
        try
        {
            byte[] bytes = FileAccess.GetFileAsBytes($"{GameData.ReplaysDir}/{_files[i]}");
            using var ms = new MemoryStream(bytes);
            return MatchReplay.Read(ms);
        }
        catch (Exception e)
        {
            _info.Text = $"Couldn't read that replay: {e.Message}";
            return null;
        }
    }

    private void Describe(int i)
    {
        MatchReplay? r = Read(i);
        if (r is null) return;
        int secs = r.TickCount / ContingencyZero.Sim.World.TicksPerSecond;
        string players = string.Join(", ", r.Scenario.Players.Select(p => $"{p.Name} (team {p.Team + 1})"));
        _info.Text = $"{r.Title}\n{r.Scenario.Map.Name} · {secs / 60}:{secs % 60:D2} · {players}";
        if (r.CatalogFingerprint != 0 && r.CatalogFingerprint != GameData.Catalog.Fingerprint)
            _info.Text += "\nThis replay was recorded with different unit stats and may not play back correctly.";
    }

    private void Play(int i)
    {
        MatchReplay? r = Read(i);
        if (r is null) return;
        GameRoot.Instance.StartReplay(r, r.Title);
    }

    private void Delete()
    {
        if (_list.GetSelectedItems().Length == 0) return;
        int i = _list.GetSelectedItems()[0];
        DirAccess.RemoveAbsolute($"{GameData.ReplaysDir}/{_files[i]}");
        Load();
    }
}

/// <summary>Playback bar for replays: pause, speed, and whose view to watch.</summary>
public partial class ReplayControls : CanvasLayer
{
    private readonly MatchScene _match;
    private Label _time = null!;
    private ProgressBar _progress = null!;

    public ReplayControls(MatchScene match) { _match = match; }

    public override void _Ready()
    {
        Layer = 15;
        var root = new Control { Theme = UiTheme.Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 8);
        var panel = UiTheme.Panel(bar, 8);
        panel.AnchorLeft = 0.5f; panel.AnchorRight = 0.5f;
        panel.OffsetLeft = -380; panel.OffsetRight = 380; panel.OffsetTop = 58; panel.OffsetBottom = 110;
        root.AddChild(panel);

        bar.AddChild(UiTheme.Button("Pause", () => _match.Session.Paused = !_match.Session.Paused, 80, 14));
        foreach (double s in new[] { 0.5, 1, 2, 4, 8 })
            bar.AddChild(UiTheme.Button($"x{s}", () => _match.Session.Speed = s, 50, 14));
        var view = new OptionButton();
        view.AddItem("Everyone");
        var players = _match.Session.World.Players;
        foreach (var p in players) view.AddItem(p.Name);
        view.ItemSelected += i => _match.SetViewer(i == 0 ? -1 : players[i - 1].Team);
        bar.AddChild(view);
        _progress = new ProgressBar { CustomMinimumSize = new Vector2(120, 10), MaxValue = 1, ShowPercentage = false, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        bar.AddChild(_progress);
        _time = UiTheme.Label("", 14);
        bar.AddChild(_time);
    }

    public override void _Process(double delta)
    {
        if (_match.Session.Channel is not ReplayChannel rc) return;
        int t = _match.Session.World.Tick, total = Math.Max(1, rc.LastTick);
        _progress.Value = (double)t / total;
        _time.Text = $"{t / 15 / 60}:{t / 15 % 60:D2} / {total / 15 / 60}:{total / 15 % 60:D2}";
    }
}
