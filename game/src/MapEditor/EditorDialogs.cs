using System;
using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Game.App;
using ContingencyZero.Game.UI;
using ContingencyZero.Sim.Map;
using Godot;
using FileAccess = Godot.FileAccess;

namespace ContingencyZero.Game.MapEditor;

/// <summary>A centred dialog over a dimmed screen; Esc runs <see cref="OnCancel"/>.</summary>
public partial class EditorDialog : ModalLayer
{
    /// <summary>What Esc does. Null means Esc does nothing.</summary>
    protected Action? OnCancel { get; set; }

    protected void Setup(string title, float width, string? subtitle = null)
    {
        Build(width, 0.6f);
        Body.AddChild(UiTheme.Title(title, 28));
        if (subtitle is not null) Body.AddChild(UiTheme.Label(subtitle, 15, UiTheme.Dim, HorizontalAlignment.Center));
        Body.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && OnCancel is not null)
        {
            GetViewport().SetInputAsHandled();
            OnCancel();
        }
    }

    protected static Button Btn(string text, Action act, int width = 140, bool accent = false)
    {
        Button b = UiTheme.Button(text, act, width, accent ? 18 : 17);
        if (accent) b.AddThemeColorOverride("font_color", UiTheme.Amber);
        return b;
    }

    protected static HBoxContainer ButtonRow(BoxContainer.AlignmentMode align = BoxContainer.AlignmentMode.End)
    {
        var row = new HBoxContainer { Alignment = align };
        row.AddThemeConstantOverride("separation", 10);
        return row;
    }
}

/// <summary>A question with a few answers ("Save changes?").</summary>
public partial class ConfirmDialog : EditorDialog
{
    public static ConfirmDialog Create(string title, string message, (string Text, Action Act, bool Accent)[] buttons, Action? onEsc)
    {
        var d = new ConfirmDialog();
        d.Setup(title, 520);
        Label text = UiTheme.Label(message, 16, UiTheme.Ink, HorizontalAlignment.Center);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.CustomMinimumSize = new Vector2(500, 0);
        d.Body.AddChild(text);
        d.Body.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        HBoxContainer row = ButtonRow(BoxContainer.AlignmentMode.Center);
        foreach (var (t, act, accent) in buttons)
        {
            Action run = act;
            row.AddChild(Btn(t, () => { d.QueueFree(); run(); }, 150, accent));
        }
        d.Body.AddChild(row);
        d.OnCancel = onEsc is null ? null : () => { d.QueueFree(); onEsc(); };
        return d;
    }
}

/// <summary>Name the map and save it to the player's maps (asks before replacing another file).</summary>
public partial class SaveDialog : EditorDialog
{
    private LineEdit _name = null!;
    private LineEdit _desc = null!;
    private Label _file = null!;
    private Label _warn = null!;
    private Button _save = null!;
    private string? _currentFile;
    private Action<string, string, string> _onSave = null!;

    /// <param name="onSave">Called with (name, description, file name) when the player confirms.</param>
    public static SaveDialog Create(MapData map, string? currentFile, Action<string, string, string> onSave, Action onCancel)
    {
        var d = new SaveDialog { _currentFile = currentFile, _onSave = onSave };
        d.Setup("SAVE MAP", 560, "Saved maps appear in the Skirmish map list.");
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 10);
        grid.AddChild(UiTheme.Label("Name", 16));
        d._name = new LineEdit { Text = map.Name, CustomMinimumSize = new Vector2(400, 0), MaxLength = 48, SelectAllOnFocus = true };
        d._name.TextChanged += _ => d.Refresh();
        d._name.TextSubmitted += _ => d.Confirm();
        grid.AddChild(d._name);
        grid.AddChild(UiTheme.Label("Description", 16));
        d._desc = new LineEdit { Text = map.Description, PlaceholderText = "One line for the Skirmish screen (optional)", MaxLength = 140 };
        d._desc.TextSubmitted += _ => d.Confirm();
        grid.AddChild(d._desc);
        d.Body.AddChild(grid);
        d._file = UiTheme.Label("", 14, UiTheme.Dim);
        d.Body.AddChild(d._file);
        d._warn = UiTheme.Label("", 15, UiTheme.Amber);
        d._warn.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        d._warn.CustomMinimumSize = new Vector2(520, 0);
        d.Body.AddChild(d._warn);
        HBoxContainer row = ButtonRow();
        row.AddChild(Btn("Cancel", () => { d.QueueFree(); onCancel(); }));
        d._save = Btn("Save", d.Confirm, 160, true);
        row.AddChild(d._save);
        d.Body.AddChild(row);
        d.OnCancel = () => { d.QueueFree(); onCancel(); };
        d.Refresh();
        return d;
    }

    public override void _Ready() => _name.CallDeferred(Control.MethodName.GrabFocus);

    private string FileName => GameData.SafeFileName(_name.Text.Trim().Length == 0 ? "Untitled" : _name.Text.Trim());

    public static bool UserMapExists(string file) => FileAccess.FileExists($"{GameData.UserMapsDir}/{file}");

    private void Refresh()
    {
        string file = FileName;
        _file.Text = $"File: your maps / {file}";
        bool clash = file != _currentFile && UserMapExists(file);
        _warn.Text = clash ? $"You already have a map saved as {file}. Saving will replace it." : "";
        _warn.Visible = clash;
        _save.Text = clash ? "Replace" : "Save";
    }

    private void Confirm()
    {
        string name = _name.Text.Trim();
        if (name.Length == 0) name = "Untitled";
        QueueFree();
        _onSave(name, _desc.Text.Trim(), FileName);
    }
}

/// <summary>The editor's front page: start a new map, or open an existing one.</summary>
public partial class StartDialog : EditorDialog
{
    /// <summary>Settings for a new map.</summary>
    public sealed record NewMapSpec(string Name, int TilesX, int TilesY, string Theme, int BaseHeight, int Players);

    private static readonly (string Label, int Tiles, string Note)[] Sizes =
    {
        ("Small", 64, "quick 1v1"),
        ("Medium", 96, "1v1 or 2v2"),
        ("Large", 128, "up to 4 players"),
        ("Huge", 160, "big team games"),
    };

    private Action<NewMapSpec> _onNew = null!;
    private Action<MapEntry> _onOpen = null!;
    private LineEdit _name = null!;
    private int _sizeIndex = 1;
    private SpinBox _customW = null!, _customH = null!;
    private HBoxContainer _customRow = null!;
    private Label _sizeNote = null!;
    private string _theme = "temperate";
    private int _players = 2;
    private HSlider _height = null!;
    private Label _heightValue = null!;

    private List<MapEntry> _maps = new();
    private ItemList _list = null!;
    private readonly List<int> _rowToMap = new();
    private TextureRect _preview = null!;
    private Label _info = null!;
    private Button _open = null!, _delete = null!;

    public static StartDialog Create(bool canCancel, Action<NewMapSpec> onNew, Action<MapEntry> onOpen, Action onCancel, string cancelText)
    {
        var d = new StartDialog { _onNew = onNew, _onOpen = onOpen };
        d.Setup("MAP EDITOR", 1060, "Make a new battlefield, or open a map to change it.");
        var cols = new HBoxContainer();
        cols.AddThemeConstantOverride("separation", 18);
        d.Body.AddChild(cols);
        cols.AddChild(d.BuildNew());
        cols.AddChild(d.BuildOpen());
        HBoxContainer row = ButtonRow(BoxContainer.AlignmentMode.Begin);
        row.AddChild(Btn(cancelText, () => { d.QueueFree(); onCancel(); }, 180));
        d.Body.AddChild(row);
        // Esc does what the bottom button does: close this, or (with no map open) leave the editor.
        d.OnCancel = () => { d.QueueFree(); onCancel(); };
        return d;
    }

    private static PanelContainer Section(Control content) =>
        UiTheme.Panel(content, 14);

    private Control BuildNew()
    {
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(470, 0) };
        box.AddThemeConstantOverride("separation", 9);
        box.AddChild(UiTheme.Label("NEW MAP", 15, UiTheme.Dim));

        box.AddChild(UiTheme.Label("Name", 15));
        _name = new LineEdit { Text = "My map", MaxLength = 48, SelectAllOnFocus = true };
        box.AddChild(_name);

        box.AddChild(UiTheme.Label("Size", 15));
        var sizes = new HBoxContainer();
        sizes.AddThemeConstantOverride("separation", 6);
        var sizeGroup = new ButtonGroup();
        for (int i = 0; i <= Sizes.Length; i++)
        {
            int index = i;
            string text = i < Sizes.Length ? $"{Sizes[i].Label}\n{Sizes[i].Tiles}" : "Custom\nW × H";
            var b = new Button { Text = text, ToggleMode = true, ButtonGroup = sizeGroup, CustomMinimumSize = new Vector2(84, 52), FocusMode = Control.FocusModeEnum.None };
            b.AddThemeFontSizeOverride("font_size", 15);
            b.ButtonPressed = i == _sizeIndex;
            b.Pressed += () => { Audio.Sfx.Play(Audio.SfxKind.Click); _sizeIndex = index; RefreshSize(); };
            sizes.AddChild(b);
        }
        box.AddChild(sizes);
        _customRow = new HBoxContainer();
        _customRow.AddThemeConstantOverride("separation", 8);
        _customW = new SpinBox { MinValue = 32, MaxValue = 256, Step = 32, Value = 128, Suffix = "wide", CustomMinimumSize = new Vector2(130, 0) };
        _customH = new SpinBox { MinValue = 32, MaxValue = 256, Step = 32, Value = 96, Suffix = "deep", CustomMinimumSize = new Vector2(130, 0) };
        _customW.ValueChanged += _ => RefreshSize();
        _customH.ValueChanged += _ => RefreshSize();
        _customRow.AddChild(UiTheme.Label("Tiles", 15));
        _customRow.AddChild(_customW);
        _customRow.AddChild(UiTheme.Label("×", 15));
        _customRow.AddChild(_customH);
        box.AddChild(_customRow);
        _sizeNote = UiTheme.Label("", 14, UiTheme.Dim);
        box.AddChild(_sizeNote);

        box.AddChild(UiTheme.Label("Theme", 15));
        var themes = new HBoxContainer();
        themes.AddThemeConstantOverride("separation", 6);
        var themeGroup = new ButtonGroup();
        string[] names = { "Temperate", "Desert", "Lava", "Arctic" };
        for (int i = 0; i < EditorHud.Themes.Length; i++)
        {
            string theme = EditorHud.Themes[i];
            var b = new Button { Text = names[i], ToggleMode = true, ButtonGroup = themeGroup, CustomMinimumSize = new Vector2(108, 40), FocusMode = Control.FocusModeEnum.None };
            b.AddThemeFontSizeOverride("font_size", 15);
            var pal = View.TerrainView.PaletteFor(theme);
            var chip = new ColorRect { Color = pal.Low, CustomMinimumSize = new Vector2(0, 5), MouseFilter = Control.MouseFilterEnum.Ignore };
            chip.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
            chip.OffsetTop = -7; chip.OffsetBottom = -3; chip.OffsetLeft = 8; chip.OffsetRight = -8;
            b.AddChild(chip);
            b.ButtonPressed = theme == _theme;
            b.Pressed += () => { Audio.Sfx.Play(Audio.SfxKind.Click); _theme = theme; };
            themes.AddChild(b);
        }
        box.AddChild(themes);

        box.AddChild(UiTheme.Label("Players", 15));
        var players = new HBoxContainer();
        players.AddThemeConstantOverride("separation", 6);
        var playerGroup = new ButtonGroup();
        foreach (var (label, n, tip) in new[]
                 {
                     ("2 players", 2, "Two starts in opposite corners, with half-turn symmetry switched on"),
                     ("4 players", 4, "Four starts in the corners, with four-way mirror symmetry switched on"),
                     ("Blank", 0, "No starts and no symmetry: place everything yourself"),
                 })
        {
            int count = n;
            var b = new Button { Text = label, ToggleMode = true, ButtonGroup = playerGroup, CustomMinimumSize = new Vector2(144, 40), TooltipText = tip, FocusMode = Control.FocusModeEnum.None };
            b.AddThemeFontSizeOverride("font_size", 15);
            b.ButtonPressed = n == _players;
            b.Pressed += () => { Audio.Sfx.Play(Audio.SfxKind.Click); _players = count; };
            players.AddChild(b);
        }
        box.AddChild(players);

        var hRow = new HBoxContainer();
        Label hl = UiTheme.Label("Ground height", 15);
        hl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hRow.AddChild(hl);
        _heightValue = UiTheme.Label("", 15, UiTheme.Amber);
        hRow.AddChild(_heightValue);
        box.AddChild(hRow);
        _height = new HSlider { MinValue = 0, MaxValue = 240, Step = 4, Value = 80, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 22) };
        _height.TooltipText = "How high the flat ground starts. The water line starts 10 below it, so you can dig lakes straight away.";
        _height.ValueChanged += _ => RefreshHeight();
        box.AddChild(_height);
        box.AddChild(UiTheme.Label("The water line starts 10 below the ground: dig down to make lakes.", 14, UiTheme.Dim));
        RefreshHeight();

        box.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        box.AddChild(Btn("Create map", Create, 0, true));
        RefreshSize();
        return Section(box);
    }

    private void RefreshHeight() => _heightValue.Text = $"{_height.Value / MapData.HeightStepsPerUnit:0}";

    private (int W, int H) ChosenSize() => _sizeIndex < Sizes.Length
        ? (Sizes[_sizeIndex].Tiles, Sizes[_sizeIndex].Tiles)
        : ((int)_customW.Value, (int)_customH.Value);

    private void RefreshSize()
    {
        _customRow.Visible = _sizeIndex == Sizes.Length;
        var (w, h) = ChosenSize();
        string note = _sizeIndex < Sizes.Length ? Sizes[_sizeIndex].Note : (w * h <= 96 * 96 ? "small to medium" : w * h <= 128 * 128 ? "medium to large" : "very large");
        _sizeNote.Text = $"{w} × {h} tiles ({w * MapData.TileSize} × {h * MapData.TileSize} units): {note}";
    }

    private void Create()
    {
        var (w, h) = ChosenSize();
        string name = _name.Text.Trim().Length == 0 ? "My map" : _name.Text.Trim();
        QueueFree();
        _onNew(new NewMapSpec(name, w, h, _theme, (int)_height.Value, _players));
    }

    private Control BuildOpen()
    {
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(520, 0) };
        box.AddThemeConstantOverride("separation", 9);
        box.AddChild(UiTheme.Label("OPEN A MAP", 15, UiTheme.Dim));
        _list = new ItemList { CustomMinimumSize = new Vector2(0, 250), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _list.AddThemeFontSizeOverride("font_size", 16);
        _list.ItemSelected += i => Select((int)i);
        _list.ItemActivated += i => { Select((int)i); OpenSelected(); };
        box.AddChild(_list);

        var detail = new HBoxContainer();
        detail.AddThemeConstantOverride("separation", 12);
        _preview = new TextureRect
        {
            CustomMinimumSize = new Vector2(150, 150),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        detail.AddChild(_preview);
        _info = UiTheme.Label("Pick a map to see it here.", 14, UiTheme.Ink);
        _info.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _info.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _info.CustomMinimumSize = new Vector2(320, 0);
        detail.AddChild(_info);
        box.AddChild(detail);

        HBoxContainer row = ButtonRow();
        _delete = Btn("Delete", AskDelete, 120);
        _delete.TooltipText = "Delete this map from your maps (built-in maps can't be deleted)";
        _open = Btn("Open", OpenSelected, 160, true);
        row.AddChild(_delete);
        row.AddChild(_open);
        box.AddChild(row);
        FillList();
        return Section(box);
    }

    private void FillList()
    {
        _maps = GameData.Maps();
        _list.Clear();
        _rowToMap.Clear();
        void Header(string text)
        {
            int i = _list.AddItem(text, null, false);
            _list.SetItemCustomFgColor(i, UiTheme.Dim);
            _list.SetItemDisabled(i, true);
            _rowToMap.Add(-1);
        }
        Header("YOUR MAPS");
        bool any = false;
        for (int m = 0; m < _maps.Count; m++)
        {
            if (_maps[m].BuiltIn) continue;
            _list.AddItem("   " + _maps[m].Name.Replace("  (yours)", ""));
            _rowToMap.Add(m);
            any = true;
        }
        if (!any)
        {
            int i = _list.AddItem("   (none yet: make one, or start from a built-in map)", null, false);
            _list.SetItemCustomFgColor(i, UiTheme.Dim);
            _rowToMap.Add(-1);
        }
        Header("BUILT-IN MAPS (open as templates)");
        for (int m = 0; m < _maps.Count; m++)
        {
            if (!_maps[m].BuiltIn) continue;
            _list.AddItem("   " + _maps[m].Name);
            _rowToMap.Add(m);
        }
        _open.Disabled = true;
        _delete.Disabled = true;
        _preview.Texture = null;
        _info.Text = "Pick a map to see it here.";
        // Start with a map picked, so there's something to look at: your first map, or the first built-in.
        int first = _rowToMap.FindIndex(m => m >= 0);
        if (first >= 0)
        {
            _list.Select(first);
            Select(first);
        }
    }

    /// <summary>For the dev script: picks a size button (4 is custom) and a list row.</summary>
    internal void DevPick(int sizeIndex, int row)
    {
        _sizeIndex = sizeIndex;
        RefreshSize();
        if (row >= 0 && row < _rowToMap.Count && _rowToMap[row] >= 0) { _list.Select(row); Select(row); }
    }

    private MapEntry? Selected()
    {
        int[] sel = _list.GetSelectedItems();
        if (sel.Length == 0) return null;
        int m = _rowToMap[sel[0]];
        return m < 0 ? null : _maps[m];
    }

    private void Select(int row)
    {
        int m = _rowToMap[row];
        if (m < 0) return;
        MapEntry entry = _maps[m];
        try
        {
            MapData map = entry.Load();
            Image img = Minimap.RenderBase(map, 160, Math.Max(8, 160 * map.TilesY / map.TilesX));
            for (int i = 0; i < map.StartPositions.Count; i++)
            {
                var s = MapEdits.ToVec(map.StartPositions[i]);
                int x = (int)(s.X / map.Width * img.GetWidth()), y = (int)(s.Y / map.Height * img.GetHeight());
                for (int dy = -3; dy <= 3; dy++)
                for (int dx = -3; dx <= 3; dx++)
                    if (dx * dx + dy * dy <= 9 && x + dx >= 0 && y + dy >= 0 && x + dx < img.GetWidth() && y + dy < img.GetHeight())
                        img.SetPixel(x + dx, y + dy, UiTheme.TeamColour(i));
            }
            _preview.Texture = ImageTexture.CreateFromImage(img);
            string kind = entry.BuiltIn ? "Built-in map. Saving makes a copy in your maps." : $"Your map ({entry.Key["user:".Length..]}).";
            _info.Text = $"{map.Name}\n{(map.Description.Length > 0 ? map.Description + "\n" : "")}" +
                         $"{map.TilesX} × {map.TilesY} tiles · {map.StartPositions.Count} players · {map.MetalSpots.Count} metal spots · {Capitalise(map.Theme)}\n\n{kind}";
            _open.Disabled = false;
            _delete.Disabled = entry.BuiltIn;
        }
        catch (Exception e)
        {
            _info.Text = $"This map couldn't be read: {e.Message}";
            _open.Disabled = true;
            _delete.Disabled = entry.BuiltIn;
        }
    }

    private static string Capitalise(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private void OpenSelected()
    {
        if (Selected() is not { } entry || _open.Disabled) return;
        QueueFree();
        _onOpen(entry);
    }

    private void AskDelete()
    {
        if (Selected() is not { BuiltIn: false } entry) return;
        string file = entry.Key["user:".Length..];
        var confirm = ConfirmDialog.Create("DELETE MAP", $"Delete \"{entry.Name.Replace("  (yours)", "")}\" ({file}) from your maps? This can't be undone.",
            new (string, Action, bool)[]
            {
                ("Cancel", () => { }, false),
                ("Delete", () =>
                {
                    DirAccess.RemoveAbsolute($"{GameData.UserMapsDir}/{file}");
                    FillList();
                }, true),
            }, () => { });
        confirm.Layer = Layer + 1;
        GetParent().AddChild(confirm);
    }
}

/// <summary>Every control and tool on one page.</summary>
public partial class HelpDialog : EditorDialog
{
    private const string Text =
@"[b]Tools[/b]  (pick with the number keys)
[color=#9fd8ff]1 Raise · 2 Lower[/color]  build hills or dig valleys. Right mouse does the opposite.
[color=#9fd8ff]3 Smooth[/color]  softens slopes so vehicles can climb them.   [color=#9fd8ff]4 Flatten[/color]  levels to the height where you start dragging.
[color=#9fd8ff]5 Plateau[/color]  sets ground to an exact height with a sharp edge (Ctrl+click picks a height).   [color=#9fd8ff]6 Roughen[/color]  adds natural bumps.
[color=#9fd8ff]7 Metal spots[/color]  click to place, right click to remove. They snap to tiles.
[color=#9fd8ff]8 Trees · 9 Rocks[/color]  drag to paint, right drag to clear (Shift+right drag clears everything).
[color=#9fd8ff]0 Start positions[/color]  click to add (up to 8), drag a marker to move it, right click a marker to remove it.

[b]Brush[/b]   Size: [ and ] keys, or Ctrl+mouse wheel.   Strength (density for trees and rocks): - and = keys, or Shift+mouse wheel.
[b]Camera[/b]   Mouse wheel zooms · Middle-drag or Space+left drag pans · Arrow keys scroll · Q / E turn · Home shows the whole map · Click the overview map to jump
[b]Editing[/b]   Ctrl+Z undo · Ctrl+Y redo · Ctrl+S save · Ctrl+Shift+S save as · Ctrl+N new · Ctrl+O open
[b]Views[/b]   Tab shows where units can go: green buildable, yellow vehicles and bots, orange bots only, red impassable, blue water
[b]Symmetry[/b]   mirrors every edit (ground, metal, trees, new starts) so the map is fair. Half-turn suits 1v1; four-way suits four players.
[b]Testing[/b]   F5 plays a quick 1v1 against a Normal AI on the map as it is now; leave the match to come back.
The checks panel lists problems (starts that can't reach each other, too little metal, steep or flooded bases). Click one to look at it.

[b]Good maps[/b]   Flat ground around every start · 3 to 5 metal spots near each start · routes between starts that vehicles can drive · something worth fighting over in the middle.";

    public static HelpDialog Create(Action onClose)
    {
        var d = new HelpDialog();
        d.Setup("MAP EDITOR HELP", 980);
        var text = new RichTextLabel { BbcodeEnabled = true, Text = Text, FitContent = true, CustomMinimumSize = new Vector2(940, 0), ScrollActive = false };
        text.AddThemeFontSizeOverride("normal_font_size", 15);
        text.AddThemeFontSizeOverride("bold_font_size", 15);
        text.AddThemeColorOverride("default_color", UiTheme.Ink);
        d.Body.AddChild(text);
        HBoxContainer row = ButtonRow(BoxContainer.AlignmentMode.Center);
        row.AddChild(Btn("Close", () => { d.QueueFree(); onClose(); }, 160));
        d.Body.AddChild(row);
        d.OnCancel = () => { d.QueueFree(); onClose(); };
        return d;
    }
}
