using Godot;

namespace ContingencyZero.Game.App;

/// <summary>
/// The look of every menu and HUD panel: dark gunmetal panels, thin cyan edges, amber accents.
/// Built in code so there are no theme resources to keep in sync.
/// </summary>
public static class UiTheme
{
    public static readonly Color Ink = new(0.86f, 0.9f, 0.95f);
    public static readonly Color Dim = new(0.55f, 0.6f, 0.68f);
    public static readonly Color Accent = new(0.36f, 0.78f, 1.0f);
    public static readonly Color Amber = new(1.0f, 0.68f, 0.22f);
    public static readonly Color Good = new(0.35f, 0.9f, 0.45f);
    public static readonly Color Bad = new(1.0f, 0.32f, 0.28f);
    public static readonly Color Metal = new(0.72f, 0.78f, 0.86f);
    public static readonly Color Energy = new(1.0f, 0.86f, 0.25f);
    public static readonly Color PanelBg = new(0.055f, 0.07f, 0.09f, 0.92f);
    public static readonly Color PanelEdge = new(0.25f, 0.42f, 0.55f, 0.9f);

    public static readonly Color LatticeColour = new(0.33f, 0.62f, 1.0f);
    public static readonly Color CrucibleColour = new(1.0f, 0.5f, 0.15f);

    /// <summary>Team colours for up to eight players.</summary>
    public static readonly Color[] PlayerColours =
    {
        new(0.25f, 0.55f, 1.0f),
        new(1.0f, 0.38f, 0.12f),
        new(0.3f, 0.85f, 0.35f),
        new(0.95f, 0.85f, 0.2f),
        new(0.75f, 0.35f, 0.95f),
        new(0.2f, 0.9f, 0.9f),
        new(0.95f, 0.35f, 0.6f),
        new(0.85f, 0.85f, 0.85f),
    };

    private static Theme? _theme;
    public static Theme Theme => _theme ??= Build();

    public static StyleBoxFlat Box(Color bg, Color edge, int border = 1, int radius = 3, int pad = 8)
    {
        var sb = new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = edge,
            CornerDetail = 4,
        };
        sb.SetBorderWidthAll(border);
        sb.SetCornerRadiusAll(radius);
        sb.SetContentMarginAll(pad);
        return sb;
    }

    private static Theme Build()
    {
        var t = new Theme();
        t.DefaultFontSize = 16;

        t.SetStylebox("panel", "Panel", Box(PanelBg, PanelEdge));
        t.SetStylebox("panel", "PanelContainer", Box(PanelBg, PanelEdge, pad: 10));

        var normal = Box(new Color(0.11f, 0.15f, 0.2f), new Color(0.3f, 0.45f, 0.58f), pad: 6);
        var hover = Box(new Color(0.15f, 0.24f, 0.32f), Accent, pad: 6);
        var pressed = Box(new Color(0.08f, 0.3f, 0.42f), Accent, pad: 6);
        var disabled = Box(new Color(0.08f, 0.09f, 0.11f), new Color(0.18f, 0.2f, 0.24f), pad: 6);
        var focus = Box(new Color(0, 0, 0, 0), new Color(Accent, 0.6f), pad: 6);
        foreach (string type in new[] { "Button", "OptionButton", "CheckButton", "MenuButton" })
        {
            t.SetStylebox("normal", type, normal);
            t.SetStylebox("hover", type, hover);
            t.SetStylebox("pressed", type, pressed);
            t.SetStylebox("disabled", type, disabled);
            t.SetStylebox("focus", type, focus);
            t.SetColor("font_color", type, Ink);
            t.SetColor("font_hover_color", type, Colors.White);
            t.SetColor("font_pressed_color", type, Colors.White);
            t.SetColor("font_disabled_color", type, new Color(0.4f, 0.43f, 0.48f));
        }
        t.SetStylebox("normal", "CheckBox", Box(new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), 0, pad: 4));
        t.SetStylebox("hover", "CheckBox", Box(new Color(1, 1, 1, 0.05f), new Color(0, 0, 0, 0), 0, pad: 4));
        t.SetStylebox("pressed", "CheckBox", Box(new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), 0, pad: 4));
        t.SetColor("font_color", "CheckBox", Ink);

        t.SetColor("font_color", "Label", Ink);
        t.SetColor("default_color", "RichTextLabel", Ink);

        var field = Box(new Color(0.03f, 0.04f, 0.05f), new Color(0.25f, 0.35f, 0.45f), pad: 6);
        t.SetStylebox("normal", "LineEdit", field);
        t.SetStylebox("focus", "LineEdit", Box(new Color(0.03f, 0.04f, 0.05f), Accent, pad: 6));
        t.SetColor("font_color", "LineEdit", Ink);

        t.SetStylebox("panel", "PopupMenu", Box(new Color(0.07f, 0.09f, 0.12f, 0.98f), PanelEdge));
        t.SetStylebox("hover", "PopupMenu", Box(new Color(0.15f, 0.24f, 0.32f), Accent, pad: 4));
        t.SetColor("font_color", "PopupMenu", Ink);
        t.SetColor("font_hover_color", "PopupMenu", Colors.White);

        t.SetStylebox("panel", "TooltipPanel", Box(new Color(0.04f, 0.05f, 0.07f, 0.97f), Accent, pad: 8));
        t.SetColor("font_color", "TooltipLabel", Ink);

        t.SetStylebox("background", "ProgressBar", Box(new Color(0.03f, 0.04f, 0.05f), new Color(0.2f, 0.25f, 0.3f), pad: 0));
        t.SetStylebox("fill", "ProgressBar", Box(Accent, Accent, 0, pad: 0));

        t.SetStylebox("grabber_area", "HSlider", Box(Accent, Accent, 0, 2, 2));
        t.SetStylebox("slider", "HSlider", Box(new Color(0.1f, 0.13f, 0.17f), new Color(0.25f, 0.35f, 0.45f), 1, 2, 2));

        t.SetStylebox("panel", "ItemList", Box(new Color(0.03f, 0.04f, 0.05f, 0.9f), new Color(0.25f, 0.35f, 0.45f)));
        t.SetStylebox("selected", "ItemList", Box(new Color(0.08f, 0.3f, 0.42f), Accent, pad: 2));
        t.SetStylebox("selected_focus", "ItemList", Box(new Color(0.08f, 0.3f, 0.42f), Accent, pad: 2));
        t.SetColor("font_color", "ItemList", Ink);
        t.SetColor("font_selected_color", "ItemList", Colors.White);

        t.SetStylebox("panel", "TabContainer", Box(PanelBg, PanelEdge));
        return t;
    }

    // ---- Small builders used by every screen -------------------------------------------------

    public static Label Label(string text, int size = 16, Color? colour = null, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var l = new Label { Text = text, HorizontalAlignment = align };
        l.AddThemeFontSizeOverride("font_size", size);
        if (colour is { } c) l.AddThemeColorOverride("font_color", c);
        return l;
    }

    public static Label Title(string text, int size = 34) =>
        Label(text, size, Ink, HorizontalAlignment.Center);

    public static Button Button(string text, System.Action onPressed, int minWidth = 0, int fontSize = 17)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(minWidth, 38) };
        b.AddThemeFontSizeOverride("font_size", fontSize);
        b.Pressed += () =>
        {
            Audio.Sfx.Play(Audio.SfxKind.Click);
            onPressed();
        };
        b.MouseEntered += () => Audio.Sfx.Play(Audio.SfxKind.Hover);
        return b;
    }

    public static PanelContainer Panel(Control content, int pad = 14)
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", Box(PanelBg, PanelEdge, 1, 4, pad));
        p.AddChild(content);
        return p;
    }

    public static Color TeamColour(int player) => PlayerColours[((player % 8) + 8) % 8];

    public static Color FactionColour(string faction) => faction == "lattice" ? LatticeColour : CrucibleColour;
}
