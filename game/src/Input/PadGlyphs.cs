using System.Collections.Generic;
using ContingencyZero.Game.App;
using Godot;

namespace ContingencyZero.Game.Controls;

/// <summary>
/// Controller button prompts drawn as simple badges (no image assets): coloured discs for the
/// face buttons (Xbox layout, which is also how the Steam Deck labels them), rounded tabs for the
/// rest. Names: A B X Y LB RB LT RT LS RS DPAD START VIEW.
/// </summary>
public static class PadGlyphs
{
    public const int FontSize = 14;
    private const float Height = 24;

    private static Font Font => UiTheme.Theme.DefaultFont ?? ThemeDB.FallbackFont;

    private static Color FaceColour(string b) => b switch
    {
        "A" => new Color(0.36f, 0.72f, 0.3f),
        "B" => new Color(0.86f, 0.3f, 0.24f),
        "X" => new Color(0.3f, 0.52f, 0.9f),
        "Y" => new Color(0.92f, 0.76f, 0.22f),
        _ => new Color(0.36f, 0.36f, 0.34f),
    };

    private static string Caption(string b) => b switch
    {
        "DPAD" => "D-pad",
        "START" => "Menu",
        "VIEW" => "View",
        _ => b,
    };

    /// <summary>Width of one badge.</summary>
    public static float BadgeWidth(string button)
    {
        if (button.Length == 1) return Height;
        return Font.GetStringSize(Caption(button), HorizontalAlignment.Left, -1, FontSize - 2).X + 14;
    }

    /// <summary>Draws one badge with its left-middle at <paramref name="at"/>; returns its width.</summary>
    public static float Badge(CanvasItem ci, string button, Vector2 at)
    {
        Font font = Font;
        float w = BadgeWidth(button);
        Color c = FaceColour(button);
        if (button.Length == 1)
        {
            Vector2 centre = at + new Vector2(Height / 2, 0);
            ci.DrawCircle(centre, Height / 2 + 1.5f, new Color(0, 0, 0, 0.75f));
            ci.DrawCircle(centre, Height / 2, c);
            Vector2 sz = font.GetStringSize(button, HorizontalAlignment.Left, -1, FontSize);
            ci.DrawString(font, centre + new Vector2(-sz.X / 2, FontSize * 0.36f), button, HorizontalAlignment.Left, -1, FontSize, Colors.Black);
        }
        else
        {
            var r = new Rect2(at - new Vector2(0, Height / 2), new Vector2(w, Height));
            var box = UiTheme.Box(c, new Color(0.75f, 0.75f, 0.7f), 1, 6, 0);
            ci.DrawStyleBox(box, r);
            string cap = Caption(button);
            Vector2 sz = font.GetStringSize(cap, HorizontalAlignment.Left, -1, FontSize - 2);
            ci.DrawString(font, at + new Vector2((w - sz.X) / 2, (FontSize - 2) * 0.36f), cap, HorizontalAlignment.Left, -1, FontSize - 2, Colors.White);
        }
        return w;
    }

    /// <summary>Measures a strip of badge + label pairs.</summary>
    public static float StripWidth(IReadOnlyList<(string Button, string Label)> items)
    {
        float w = 0;
        foreach (var (b, l) in items)
            w += BadgeWidth(b) + 6 + Font.GetStringSize(l, HorizontalAlignment.Left, -1, FontSize).X + 18;
        return w > 0 ? w - 18 : 0;
    }

    /// <summary>Draws "[A] Select  [B] Back ..." on a dark backing, centred on or starting at <paramref name="at"/> (vertical middle).</summary>
    public static Rect2 Strip(CanvasItem ci, IReadOnlyList<(string Button, string Label)> items, Vector2 at, bool centred)
    {
        Font font = Font;
        float width = StripWidth(items);
        Vector2 p = centred ? at - new Vector2(width / 2, 0) : at;
        var back = new Rect2(p - new Vector2(10, Height / 2 + 5), new Vector2(width + 20, Height + 10));
        ci.DrawRect(back, new Color(0, 0, 0, 0.62f));
        foreach (var (b, l) in items)
        {
            p.X += Badge(ci, b, p) + 6;
            ci.DrawString(font, p + new Vector2(0, FontSize * 0.36f), l, HorizontalAlignment.Left, -1, FontSize, UiTheme.Ink);
            p.X += font.GetStringSize(l, HorizontalAlignment.Left, -1, FontSize).X + 18;
        }
        return back;
    }
}
