using System;
using System.Collections.Generic;
using ContingencyZero.Game.App;
using ContingencyZero.Sim.Map;
using Godot;

namespace ContingencyZero.Game.MapEditor;

/// <summary>A labelled slider with its value shown on the right.</summary>
public sealed class SliderRow
{
    public required VBoxContainer Root { get; init; }
    public required HSlider Slider { get; init; }
    public required Label Value { get; init; }
    public required Func<double, string> Format { get; init; }

    public void Set(double v)
    {
        Slider.SetValueNoSignal(v);
        Value.Text = Format(v);
    }
}

/// <summary>
/// The editor's screen furniture: the top bar (file, undo, symmetry, test play), the tool palette
/// and overview map on the left, tool options / map settings / checks on the right, and a help
/// line along the bottom that always says what the mouse will do.
/// </summary>
public partial class EditorHud : CanvasLayer
{
    public const float TopBarHeight = 60;
    public const float LeftWidth = 214;
    public const float RightWidth = 330;
    public const float BottomHeight = 44;

    private MapEditorScene _ed = null!;
    private Control _root = null!;
    private readonly Dictionary<EditorTool, Button> _tools = new();
    private Label _title = null!, _origin = null!;
    private Button _undo = null!, _redo = null!, _save = null!, _pass = null!;
    private OptionButton _symmetry = null!;

    private Label _toolName = null!, _toolSummary = null!, _toolHint = null!;
    private ToolIcon _toolIcon = null!;
    private SliderRow _radius = null!, _strength = null!, _density = null!, _plateau = null!;
    private VBoxContainer _yieldRow = null!;
    private OptionButton _yield = null!;

    private OptionButton _theme = null!;
    private SliderRow _water = null!, _windMin = null!, _windMax = null!;
    private Label _mapInfo = null!;

    private VBoxContainer _checks = null!;
    private Label _checksSummary = null!;
    private EditorMinimap _minimap = null!;

    private Label _help = null!, _cursor = null!;
    private PanelContainer _toastPanel = null!;
    private Label _toast = null!;
    private double _toastLeft;
    private PanelContainer _legend = null!;

    public static readonly string[] Themes = { "temperate", "desert", "lava", "arctic" };
    private static readonly string[] ThemeNames = { "Temperate", "Desert", "Lava", "Arctic" };
    public static readonly float[] Yields = { 2f, 2.5f, 4f };

    public EditorMinimap Minimap => _minimap;

    public void Init(MapEditorScene editor)
    {
        _ed = editor;
        Layer = 5;
        _root = new Control { Theme = UiTheme.Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        BuildTopBar();
        BuildLeft();
        BuildRight();
        BuildBottom();
        BuildToast();
        BuildLegend();
    }

    // ---- Helpers ------------------------------------------------------------------------------

    private static PanelContainer Panel(Control content, int pad = 10)
    {
        PanelContainer p = UiTheme.Panel(content, pad);
        p.MouseFilter = Control.MouseFilterEnum.Stop;
        return p;
    }

    private static Button Btn(string text, Action act, string tip, int width = 0, int font = 16)
    {
        Button b = UiTheme.Button(text, act, width, font);
        b.TooltipText = tip;
        b.FocusMode = Control.FocusModeEnum.None;
        return b;
    }

    private static Label Header(string text) => UiTheme.Label(text, 13, UiTheme.Dim);

    private static Control Gap(float h) => new() { CustomMinimumSize = new Vector2(0, h), MouseFilter = Control.MouseFilterEnum.Ignore };

    private static SliderRow Slider(string name, string tip, double min, double max, double step, Func<double, string> format,
        Action<double> changed, Action? released = null)
    {
        var root = new VBoxContainer { TooltipText = tip, MouseFilter = Control.MouseFilterEnum.Pass };
        root.AddThemeConstantOverride("separation", 2);
        var top = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        Label label = UiTheme.Label(name, 15);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        Label value = UiTheme.Label("", 15, UiTheme.Amber, HorizontalAlignment.Right);
        top.AddChild(label);
        top.AddChild(value);
        root.AddChild(top);
        var slider = new HSlider
        {
            MinValue = min, MaxValue = max, Step = step,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0, 22),
            TooltipText = tip,
        };
        root.AddChild(slider);
        var row = new SliderRow { Root = root, Slider = slider, Value = value, Format = format };
        slider.ValueChanged += v =>
        {
            value.Text = format(v);
            changed(v);
        };
        if (released is not null) slider.DragEnded += _ => released();
        return row;
    }

    // ---- Top bar ------------------------------------------------------------------------------

    private void BuildTopBar()
    {
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 6);
        PanelContainer panel = Panel(bar, 7);
        panel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        panel.OffsetLeft = 8; panel.OffsetRight = -8; panel.OffsetTop = 8; panel.OffsetBottom = TopBarHeight;
        _root.AddChild(panel);

        bar.AddChild(Btn("Menu", _ed.RequestExit, "Back to the main menu (Esc)", 76));
        bar.AddChild(new VSeparator());
        bar.AddChild(Btn("New", _ed.RequestNew, "Start a new map (Ctrl+N)", 64));
        bar.AddChild(Btn("Open", _ed.RequestOpen, "Open one of your maps, or a built-in map as a template (Ctrl+O)", 70));
        _save = Btn("Save", () => _ed.Save(), "Save to your maps (Ctrl+S). Saved maps appear in Skirmish.", 66);
        bar.AddChild(_save);
        bar.AddChild(Btn("Save as", () => _ed.SaveAs(), "Save under a new name (Ctrl+Shift+S)", 84));
        bar.AddChild(new VSeparator());
        _undo = Btn("Undo", () => _ed.Undo(), "Undo the last change (Ctrl+Z)", 66);
        _redo = Btn("Redo", () => _ed.Redo(), "Redo (Ctrl+Y)", 66);
        bar.AddChild(_undo);
        bar.AddChild(_redo);
        bar.AddChild(new VSeparator());

        var titles = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        titles.AddThemeConstantOverride("separation", -2);
        _title = UiTheme.Label("", 18, UiTheme.Ink);
        _title.ClipText = true;
        _title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _origin = UiTheme.Label("", 13, UiTheme.Dim);
        _origin.ClipText = true;
        _origin.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        titles.AddChild(_title);
        titles.AddChild(_origin);
        bar.AddChild(titles);

        bar.AddChild(UiTheme.Label("Symmetry", 15, UiTheme.Dim));
        _symmetry = new OptionButton { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(190, 0) };
        _symmetry.TooltipText = "Mirror every edit so all players get the same ground, metal and trees.\n" +
                                "Rotate suits two players; four-way mirror suits four.";
        foreach (Symmetry s in Enum.GetValues<Symmetry>()) _symmetry.AddItem(ToolInfo.SymmetryName(s));
        _symmetry.ItemSelected += i => _ed.SetSymmetry((Symmetry)(int)i);
        bar.AddChild(_symmetry);

        _pass = Btn("Unit paths", () => _ed.TogglePassability(), "Colour the ground by who can use it: buildable, vehicles, bots only, impassable (Tab)", 108);
        _pass.ToggleMode = true;
        bar.AddChild(_pass);
        bar.AddChild(Btn("Help", () => _ed.ShowHelp(), "Every control and tool explained (F1)", 62));
        Button test = Btn("Test play", () => _ed.TestPlay(), "Play a quick 1v1 on this map against a Normal AI, then come back here (F5)", 112, 17);
        test.AddThemeColorOverride("font_color", UiTheme.Amber);
        bar.AddChild(test);
    }

    // ---- Left: tools and overview ------------------------------------------------------------

    private void BuildLeft()
    {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        col.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
        col.OffsetLeft = 8; col.OffsetRight = 8 + LeftWidth; col.OffsetTop = TopBarHeight + 8; col.OffsetBottom = -BottomHeight - 6;
        col.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(col);

        var tools = new VBoxContainer();
        tools.AddThemeConstantOverride("separation", 3);
        var group = new ButtonGroup();
        tools.AddChild(Header("SHAPE THE GROUND"));
        foreach (EditorTool t in ToolInfo.All)
        {
            if (t == EditorTool.Metal)
            {
                tools.AddChild(Gap(4));
                tools.AddChild(Header("PLACE THINGS"));
            }
            tools.AddChild(ToolButton(t, group));
        }
        col.AddChild(Panel(tools, 8));

        var mini = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        mini.AddThemeConstantOverride("separation", 4);
        mini.AddChild(Header("OVERVIEW · click to look there"));
        _minimap = new EditorMinimap { SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 150) };
        _minimap.Init(_ed);
        mini.AddChild(_minimap);
        PanelContainer miniPanel = Panel(mini, 8);
        miniPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        col.AddChild(miniPanel);
    }

    private Button ToolButton(EditorTool t, ButtonGroup group)
    {
        var b = new Button
        {
            ToggleMode = true,
            ButtonGroup = group,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0, 38),
            TooltipText = $"{ToolInfo.Name(t)}  (key {ToolInfo.HotkeyText(t)})\n{ToolInfo.Summary(t)}",
        };
        b.Pressed += () =>
        {
            Audio.Sfx.Play(Audio.SfxKind.Click);
            _ed.SetTool(t);
        };
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        row.OffsetLeft = 8; row.OffsetRight = -10;
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(new ToolIcon { Tool = t, Tint = ToolInfo.Colour(t), CustomMinimumSize = new Vector2(26, 26), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
        Label name = UiTheme.Label(ToolInfo.Name(t), 16);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.VerticalAlignment = VerticalAlignment.Center;
        name.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(name);
        Label key = UiTheme.Label(ToolInfo.HotkeyText(t), 14, UiTheme.Dim);
        key.VerticalAlignment = VerticalAlignment.Center;
        key.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(key);
        b.AddChild(row);
        _tools[t] = b;
        return b;
    }

    // ---- Right: tool options, map settings, checks -------------------------------------------

    private void BuildRight()
    {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        col.SetAnchorsPreset(Control.LayoutPreset.RightWide);
        col.OffsetLeft = -8 - RightWidth; col.OffsetRight = -8; col.OffsetTop = TopBarHeight + 8; col.OffsetBottom = -BottomHeight - 6;
        col.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(col);

        // Tool options.
        var tool = new VBoxContainer();
        tool.AddThemeConstantOverride("separation", 6);
        var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        head.AddThemeConstantOverride("separation", 8);
        _toolIcon = new ToolIcon { CustomMinimumSize = new Vector2(30, 30) };
        head.AddChild(_toolIcon);
        _toolName = UiTheme.Label("", 20, UiTheme.Ink);
        _toolName.VerticalAlignment = VerticalAlignment.Center;
        head.AddChild(_toolName);
        tool.AddChild(head);
        _toolSummary = UiTheme.Label("", 14, UiTheme.Dim);
        _toolSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _toolSummary.CustomMinimumSize = new Vector2(RightWidth - 24, 0);
        tool.AddChild(_toolSummary);

        _radius = Slider("Brush size", "How wide the brush is, in tiles ([ and ], or Ctrl+mouse wheel)", 1, 30, 1,
            v => $"{v:0} tiles", v => _ed.BrushRadius = (float)v);
        _strength = Slider("Strength", "How fast the brush works (- and =, or Shift+mouse wheel)", 1, 100, 1,
            v => $"{v:0}%", v => _ed.BrushStrength = (float)v / 100f);
        _density = Slider("Density", "How thickly to plant: dragging tops the area up to this density, never more", 5, 100, 5,
            v => $"{v:0}%", v => _ed.FeatureDensity = (float)v / 100f);
        _plateau = Slider("Plateau height", "The height Plateau sets the ground to. Ctrl+click the ground to pick its height.", 0, TerrainBrush.MaxHeight, 1,
            v => $"{v / MapData.HeightStepsPerUnit:0.##}", v => _ed.PlateauLevel = (int)v);
        tool.AddChild(_radius.Root);
        tool.AddChild(_strength.Root);
        tool.AddChild(_density.Root);
        tool.AddChild(_plateau.Root);

        _yieldRow = new VBoxContainer();
        _yieldRow.AddThemeConstantOverride("separation", 2);
        _yieldRow.AddChild(UiTheme.Label("Metal per spot", 15));
        _yield = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        _yield.AddItem("Standard (2 metal/s)");
        _yield.AddItem("Rich (2.5 metal/s)");
        _yield.AddItem("Very rich (4 metal/s)");
        _yield.FitToLongestItem = false;
        _yield.TooltipText = "How much metal an extractor on the spot makes. Put very rich spots in contested ground.";
        _yield.ItemSelected += i => _ed.MetalYield = Yields[(int)i];
        _yieldRow.AddChild(_yield);
        tool.AddChild(_yieldRow);

        _toolHint = UiTheme.Label("", 14, UiTheme.Accent);
        _toolHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _toolHint.CustomMinimumSize = new Vector2(RightWidth - 24, 0);
        tool.AddChild(_toolHint);
        col.AddChild(Panel(tool, 12));

        // Map settings.
        var map = new VBoxContainer();
        map.AddThemeConstantOverride("separation", 6);
        map.AddChild(Header("MAP SETTINGS"));
        var themeRow = new HBoxContainer();
        Label tl = UiTheme.Label("Theme", 15);
        tl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        themeRow.AddChild(tl);
        _theme = new OptionButton { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(170, 0) };
        _theme.TooltipText = "The look of the ground, water and trees. Lava maps have molten lava instead of water.";
        foreach (string n in ThemeNames) _theme.AddItem(n);
        _theme.ItemSelected += i => _ed.SetTheme(Themes[(int)i]);
        themeRow.AddChild(_theme);
        map.AddChild(themeRow);

        _water = Slider("Water level", "Ground below this height is under water (lava on lava maps). Ships need deep water; land units wade only the shallows.",
            0, TerrainBrush.MaxHeight, 1, v => $"{v / MapData.HeightStepsPerUnit:0.##}", v => _ed.SetWaterLevel((int)v), _ed.EndSliderEdit);
        map.AddChild(_water.Root);
        _windMin = Slider("Wind, lowest", "Wind turbines make between the lowest and highest wind in energy per second.",
            0, 40, 1, v => $"{v:0}", v => _ed.SetWind((int)v, true), _ed.EndSliderEdit);
        _windMax = Slider("Wind, highest", "Wind turbines make between the lowest and highest wind in energy per second.",
            0, 40, 1, v => $"{v:0}", v => _ed.SetWind((int)v, false), _ed.EndSliderEdit);
        map.AddChild(_windMin.Root);
        map.AddChild(_windMax.Root);
        _mapInfo = UiTheme.Label("", 14, UiTheme.Dim);
        _mapInfo.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _mapInfo.CustomMinimumSize = new Vector2(RightWidth - 24, 0);
        map.AddChild(_mapInfo);
        col.AddChild(Panel(map, 12));

        // Checks.
        var checks = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        checks.AddThemeConstantOverride("separation", 6);
        var ch = new HBoxContainer();
        Label chl = Header("CHECKS");
        chl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        ch.AddChild(chl);
        _checksSummary = UiTheme.Label("", 13, UiTheme.Dim, HorizontalAlignment.Right);
        ch.AddChild(_checksSummary);
        checks.AddChild(ch);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _checks = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _checks.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_checks);
        checks.AddChild(scroll);
        PanelContainer checksPanel = Panel(checks, 12);
        checksPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        col.AddChild(checksPanel);
    }

    // ---- Bottom: help line and cursor read-out -----------------------------------------------

    private void BuildBottom()
    {
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 16);
        PanelContainer panel = Panel(bar, 8);
        panel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        panel.OffsetLeft = 8; panel.OffsetRight = -8; panel.OffsetTop = -BottomHeight; panel.OffsetBottom = -6;
        _root.AddChild(panel);
        _help = UiTheme.Label("", 15, UiTheme.Ink);
        _help.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _help.ClipText = true;
        _help.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _help.VerticalAlignment = VerticalAlignment.Center;
        bar.AddChild(_help);
        _cursor = UiTheme.Label("", 15, UiTheme.Accent, HorizontalAlignment.Right);
        _cursor.CustomMinimumSize = new Vector2(400, 0);
        _cursor.ClipText = true;
        _cursor.VerticalAlignment = VerticalAlignment.Center;
        bar.AddChild(_cursor);
    }

    private void BuildToast()
    {
        _toast = UiTheme.Label("", 17, UiTheme.Ink, HorizontalAlignment.Center);
        _toastPanel = Panel(_toast, 10);
        _toastPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        _toastPanel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _toastPanel.GrowHorizontal = Control.GrowDirection.Both;
        _toastPanel.OffsetTop = TopBarHeight + 14;
        _toastPanel.Visible = false;
        _root.AddChild(_toastPanel);
    }

    private void BuildLegend()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        void Chip(Color c, string text)
        {
            var h = new HBoxContainer();
            h.AddThemeConstantOverride("separation", 5);
            h.AddChild(new ColorRect { Color = c, CustomMinimumSize = new Vector2(16, 16), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
            h.AddChild(UiTheme.Label(text, 14));
            row.AddChild(h);
        }
        Chip(EditorTerrain.BuildColour, "Buildable");
        Chip(EditorTerrain.VehicleColour, "Vehicles and bots");
        Chip(EditorTerrain.BotColour, "Bots only");
        Chip(EditorTerrain.BlockedColour, "Impassable");
        Chip(EditorTerrain.ShallowColour, "Shallows (wade)");
        Chip(EditorTerrain.DeepColour, "Deep water");
        _legend = Panel(row, 8);
        _legend.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _legend.GrowHorizontal = Control.GrowDirection.Both;
        _legend.OffsetBottom = -BottomHeight - 12;
        _legend.OffsetTop = -BottomHeight - 50;
        _legend.Visible = false;
        _root.AddChild(_legend);
    }

    // ---- Updates from the editor -------------------------------------------------------------

    public void Toast(string text, Color? colour = null, double seconds = 3)
    {
        _toast.Text = text;
        _toast.AddThemeColorOverride("font_color", colour ?? UiTheme.Ink);
        _toastPanel.Visible = true;
        _toastPanel.Modulate = Colors.White;
        _toastPanel.ResetSize();
        _toastLeft = seconds;
    }

    public override void _Process(double delta)
    {
        if (_toastLeft > 0)
        {
            _toastLeft -= delta;
            _toastPanel.Modulate = new Color(1, 1, 1, (float)Math.Clamp(_toastLeft / 0.5, 0, 1));
            if (_toastLeft <= 0) _toastPanel.Visible = false;
        }
    }

    public void SetHelp(string text) => _help.Text = text;
    public void SetCursorInfo(string text, Color colour)
    {
        _cursor.Text = text;
        _cursor.AddThemeColorOverride("font_color", colour);
    }

    /// <summary>Shows the current tool, its options and the map's settings.</summary>
    public void SyncAll()
    {
        EditorDocument? doc = _ed.Document;
        _root.Visible = doc is not null;
        if (doc is null) return;
        SyncTitle();
        SyncTool();
        SyncMap();
    }

    public void SyncTitle()
    {
        EditorDocument? doc = _ed.Document;
        if (doc is null) return;
        _title.Text = (doc.Dirty ? "● " : "") + doc.Map.Name;
        _title.AddThemeColorOverride("font_color", doc.Dirty ? UiTheme.Amber : UiTheme.Ink);
        string where = doc.FileName is null ? doc.Origin : $"{doc.Origin} · {doc.FileName}";
        _origin.Text = $"{doc.Map.TilesX} × {doc.Map.TilesY} tiles · {where}" + (doc.Dirty ? " · unsaved changes" : "");
        _undo.Disabled = !doc.CanUndo;
        _redo.Disabled = !doc.CanRedo;
    }

    public void SyncTool()
    {
        EditorTool t = _ed.Tool;
        if (_tools.TryGetValue(t, out Button? b)) b.ButtonPressed = true; // emits toggled, not pressed, and releases the others
        _toolIcon.Tool = t;
        _toolIcon.Tint = ToolInfo.Colour(t);
        _toolIcon.QueueRedraw();
        _toolName.Text = ToolInfo.Name(t);
        _toolSummary.Text = ToolInfo.Summary(t);
        _radius.Root.Visible = ToolInfo.UsesBrush(t);
        _strength.Root.Visible = ToolInfo.IsTerrain(t);
        _density.Root.Visible = ToolInfo.IsFeature(t);
        _plateau.Root.Visible = t == EditorTool.Plateau;
        _yieldRow.Visible = t == EditorTool.Metal;
        _radius.Set(_ed.BrushRadius);
        _strength.Set(Math.Round(_ed.BrushStrength * 100));
        _density.Set(Math.Round(_ed.FeatureDensity * 100));
        _plateau.Set(_ed.PlateauLevel);
        _yield.Select(Math.Max(0, Array.IndexOf(Yields, _ed.MetalYield)));
        _symmetry.Select((int)_ed.Symmetry);
        _pass.SetPressedNoSignal(_ed.ShowingPassability);
        _legend.Visible = _ed.ShowingPassability;
        _toolHint.Text = t switch
        {
            EditorTool.Flatten => "Tip: flatten a wide area around each start so players can build their base.",
            EditorTool.Smooth => "Tip: vehicles can't climb steep slopes. Turn on Unit paths (Tab) to see where they can go.",
            EditorTool.Lower => "Tip: dig below the water level to make lakes. Land units can wade only the shallows.",
            EditorTool.Plateau => "Tip: keep the edges gentle somewhere (Smooth) so units have a ramp up.",
            EditorTool.Metal => "Tip: give each start 3 to 5 spots close by, and put rich spots in the middle to fight over.",
            EditorTool.Starts => "Tip: with Symmetry on, placing one start places its mirror images too.",
            EditorTool.Trees or EditorTool.Rocks => "Tip: trees and rocks are kept clear of starts and metal spots automatically.",
            _ => "",
        };
        _toolHint.Visible = _toolHint.Text.Length > 0;
        SetHelp(HelpLine());
    }

    public string HelpLine() =>
        $"{ToolInfo.Name(_ed.Tool)}:  {ToolInfo.MouseHelp(_ed.Tool)}       Camera: wheel · middle or Space+drag · Q/E · F1 help";

    public void SyncMap()
    {
        EditorDocument? doc = _ed.Document;
        if (doc is null) return;
        MapData m = doc.Map;
        _theme.Select(Math.Max(0, Array.IndexOf(Themes, m.Theme)));
        _water.Set(m.WaterLevel);
        _windMin.Set(Math.Round(MapEdits.ToFloat(m.WindMin)));
        _windMax.Set(Math.Round(MapEdits.ToFloat(m.WindMax)));
        int under = 0;
        for (int ty = 0; ty < m.TilesY; ty++)
        for (int tx = 0; tx < m.TilesX; tx++)
            if (m.TileDepth(tx, ty) > 0) under++;
        int pct = (int)Math.Round(under * 100.0 / (m.TilesX * m.TilesY));
        _water.Value.Text = $"{m.WaterLevel / (float)MapData.HeightStepsPerUnit:0.##}  ·  {pct}% under";
        int trees = 0, rocks = 0, wrecks = 0;
        foreach (FeaturePlacement f in m.Features)
        {
            if (f.Kind == FeatureKind.Tree) trees++;
            else if (f.Kind == FeatureKind.Rock) rocks++;
            else wrecks++;
        }
        _mapInfo.Text = $"{m.TilesX} × {m.TilesY} tiles ({m.Width} × {m.Height} units)\n" +
                        $"{m.StartPositions.Count} starts · {m.MetalSpots.Count} metal spots · {trees} trees · {rocks} rocks" +
                        (wrecks > 0 ? $" · {wrecks} wrecks" : "");
    }

    /// <summary>Lists the validation results. Clicking one with a location looks at it.</summary>
    public void ShowChecks(List<MapCheck> checks)
    {
        foreach (Node c in _checks.GetChildren()) c.QueueFree();
        int errors = 0, warnings = 0;
        foreach (MapCheck c in checks)
        {
            if (c.Level == CheckLevel.Error) errors++;
            if (c.Level == CheckLevel.Warning) warnings++;
            _checks.AddChild(CheckRow(c));
        }
        if (errors + warnings == 0)
        {
            _checksSummary.Text = "all good";
            _checksSummary.AddThemeColorOverride("font_color", UiTheme.Good);
        }
        else
        {
            var parts = new List<string>();
            if (errors > 0) parts.Add($"{errors} problem{(errors == 1 ? "" : "s")}");
            if (warnings > 0) parts.Add($"{warnings} warning{(warnings == 1 ? "" : "s")}");
            _checksSummary.Text = string.Join(", ", parts);
            _checksSummary.AddThemeColorOverride("font_color", errors > 0 ? UiTheme.Bad : UiTheme.Amber);
        }
    }

    private Control CheckRow(MapCheck c)
    {
        Color colour = c.Level switch
        {
            CheckLevel.Error => UiTheme.Bad,
            CheckLevel.Warning => UiTheme.Amber,
            CheckLevel.Good => UiTheme.Good,
            _ => UiTheme.Dim,
        };
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        row.AddThemeConstantOverride("separation", 8);
        var dot = new Label
        {
            Text = c.Level switch { CheckLevel.Error => "!", CheckLevel.Warning => "!", CheckLevel.Good => "✓", _ => "i" },
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(18, 0),
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        dot.AddThemeFontSizeOverride("font_size", 15);
        dot.AddThemeColorOverride("font_color", colour);
        row.AddChild(dot);
        Label text = UiTheme.Label(c.Text, 14, c.Level == CheckLevel.Info ? UiTheme.Dim : UiTheme.Ink);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        text.CustomMinimumSize = new Vector2(RightWidth - 70, 0);
        row.AddChild(text);
        if (c.Focus is { } focus)
        {
            row.TooltipText = "Click to look at it";
            row.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
            row.MouseEntered += () => text.AddThemeColorOverride("font_color", Colors.White);
            row.MouseExited += () => text.AddThemeColorOverride("font_color", UiTheme.Ink);
            row.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                {
                    Audio.Sfx.Play(Audio.SfxKind.Click);
                    _ed.LookAt(focus);
                }
            };
        }
        return row;
    }
}

/// <summary>The overview map: terrain, metal, starts and where the camera is. Click or drag to move the camera.</summary>
public partial class EditorMinimap : Control
{
    private MapEditorScene _ed = null!;
    private ImageTexture? _texture;
    private bool _dragging;

    public void Init(MapEditorScene ed)
    {
        _ed = ed;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        ClipContents = true;
    }

    public void Refresh(MapData map)
    {
        int w = 200, h = Math.Max(16, 200 * map.TilesY / map.TilesX);
        if (h > 200) { h = 200; w = Math.Max(16, 200 * map.TilesX / map.TilesY); }
        Image img = UI.Minimap.RenderBase(map, w, h);
        if (_texture is null || _texture.GetWidth() != w || _texture.GetHeight() != h) _texture = ImageTexture.CreateFromImage(img);
        else _texture.Update(img);
        QueueRedraw();
    }

    private Rect2 MapRect()
    {
        if (_texture is null) return new Rect2(Vector2.Zero, Size);
        float tw = _texture.GetWidth(), th = _texture.GetHeight();
        float s = MathF.Min(Size.X / tw, Size.Y / th);
        var size = new Vector2(tw * s, th * s);
        return new Rect2((Size - size) / 2, size);
    }

    public override void _Draw()
    {
        if (_texture is null || _ed.Document is null) return;
        MapData map = _ed.Document.Map;
        Rect2 r = MapRect();
        DrawTextureRect(_texture, r, false);
        DrawRect(r, new Color(UiTheme.PanelEdge, 0.9f), false, 1);
        Vector2 ToScreen(Vector2 world) => r.Position + new Vector2(world.X / map.Width * r.Size.X, world.Y / map.Height * r.Size.Y);

        Font font = ThemeDB.FallbackFont;
        for (int i = 0; i < map.StartPositions.Count; i++)
        {
            Vector2 p = ToScreen(MapEdits.ToVec(map.StartPositions[i]));
            DrawCircle(p, 8, new Color(0, 0, 0, 0.6f));
            DrawCircle(p, 6.5f, UiTheme.TeamColour(i));
            string n = (i + 1).ToString();
            Vector2 ts = font.GetStringSize(n, HorizontalAlignment.Left, -1, 11);
            DrawString(font, p + new Vector2(-ts.X / 2, 4), n, HorizontalAlignment.Left, -1, 11, Colors.Black);
        }

        // The camera's view: a cross at the focus.
        Vector2 f = ToScreen(_ed.CameraFocus);
        DrawArc(f, 7, 0, Mathf.Tau, 20, Colors.White, 1.5f, true);
        DrawLine(f + new Vector2(-11, 0), f + new Vector2(-4, 0), Colors.White, 1.5f);
        DrawLine(f + new Vector2(4, 0), f + new Vector2(11, 0), Colors.White, 1.5f);
        DrawLine(f + new Vector2(0, -11), f + new Vector2(0, -4), Colors.White, 1.5f);
        DrawLine(f + new Vector2(0, 4), f + new Vector2(0, 11), Colors.White, 1.5f);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (_ed.Document is null) return;
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
        {
            _dragging = mb.Pressed;
            if (mb.Pressed) Jump(mb.Position);
            AcceptEvent();
        }
        else if (e is InputEventMouseMotion mm && _dragging)
        {
            Jump(mm.Position);
            AcceptEvent();
        }
    }

    private void Jump(Vector2 local)
    {
        MapData map = _ed.Document!.Map;
        Rect2 r = MapRect();
        var uv = (local - r.Position) / r.Size;
        _ed.LookAt(new Vector2(Math.Clamp(uv.X, 0, 1) * map.Width, Math.Clamp(uv.Y, 0, 1) * map.Height), keepZoom: true);
    }

    public override void _Process(double delta) => QueueRedraw();
}
