using System;
using Godot;

namespace ContingencyZero.Game.MapEditor;

/// <summary>A small drawn pictogram for a tool (there are no icon files: everything is drawn in code).</summary>
public partial class ToolIcon : Control
{
    public EditorTool Tool { get; set; }
    public Color Tint { get; set; } = Colors.White;

    public ToolIcon()
    {
        CustomMinimumSize = new Vector2(28, 28);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        // Draw in a 28x28 design box, centred in whatever size we're given.
        float s = MathF.Min(Size.X, Size.Y) / 28f;
        Vector2 o = (Size - new Vector2(28, 28) * s) / 2;
        Vector2 P(float x, float y) => o + new Vector2(x, y) * s;
        Color c = Tint;
        Color dim = new(c, 0.35f);
        float w = 2f * s;

        Vector2[] Pts(params float[] xy)
        {
            var pts = new Vector2[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = P(xy[i * 2], xy[i * 2 + 1]);
            return pts;
        }
        void Arrow(float x, float y0, float y1)
        {
            DrawLine(P(x, y0), P(x, y1), c, w, true);
            float d = y1 > y0 ? -4 : 4;
            DrawPolyline(Pts(x - 4, y1 + d, x, y1, x + 4, y1 + d), c, w, true);
        }

        switch (Tool)
        {
            case EditorTool.Raise:
                DrawColoredPolygon(Pts(2, 26, 9, 17, 14, 14, 19, 17, 26, 26), dim);
                Arrow(14, 13, 2);
                break;
            case EditorTool.Lower:
                DrawColoredPolygon(Pts(2, 16, 8, 16, 11, 23, 17, 23, 20, 16, 26, 16, 26, 26, 2, 26), dim);
                Arrow(14, 2, 13);
                break;
            case EditorTool.Smooth:
            {
                var pts = new Vector2[15];
                for (int i = 0; i < pts.Length; i++)
                {
                    float x = 2 + i * 24f / (pts.Length - 1);
                    pts[i] = P(x, 15 + MathF.Sin(i / (float)(pts.Length - 1) * MathF.Tau) * 5);
                }
                DrawPolyline(pts, c, w, true);
                DrawLine(P(2, 24), P(26, 24), dim, w, true);
                break;
            }
            case EditorTool.Flatten:
                DrawColoredPolygon(Pts(2, 18, 26, 18, 26, 26, 2, 26), dim);
                DrawLine(P(2, 18), P(26, 18), c, w, true);
                Arrow(8, 3, 14);
                Arrow(20, 3, 14);
                break;
            case EditorTool.Plateau:
                DrawColoredPolygon(Pts(2, 26, 7, 10, 21, 10, 26, 26), dim);
                DrawPolyline(Pts(2, 26, 7, 10, 21, 10, 26, 26), c, w, true);
                DrawLine(P(4, 5), P(24, 5), new Color(c, 0.7f), 1.5f * s, true);
                break;
            case EditorTool.Roughen:
                DrawColoredPolygon(Pts(2, 20, 5, 14, 8, 18, 11, 9, 14, 16, 17, 11, 20, 17, 23, 12, 26, 19, 26, 26, 2, 26), dim);
                DrawPolyline(Pts(2, 20, 5, 14, 8, 18, 11, 9, 14, 16, 17, 11, 20, 17, 23, 12, 26, 19), c, w, true);
                break;
            case EditorTool.Metal:
                DrawCircle(P(14, 15), 10 * s, new Color(0.12f, 0.14f, 0.17f));
                DrawArc(P(14, 15), 9 * s, 0, MathF.Tau, 28, c, w, true);
                DrawCircle(P(14, 15), 4 * s, new Color(c, 0.6f));
                break;
            case EditorTool.Trees:
                DrawColoredPolygon(Pts(14, 2, 23, 15, 18, 15, 25, 23, 3, 23, 10, 15, 5, 15), new Color(c, 0.85f));
                DrawRect(new Rect2(P(12, 23), new Vector2(4, 4) * s), new Color(0.55f, 0.4f, 0.25f));
                break;
            case EditorTool.Rocks:
                DrawColoredPolygon(Pts(3, 24, 5, 14, 11, 8, 18, 9, 24, 15, 25, 24), new Color(c, 0.8f));
                DrawPolyline(Pts(3, 24, 5, 14, 11, 8, 18, 9, 24, 15, 25, 24, 3, 24), new Color(c.Darkened(0.4f), 1f), 1.5f * s, true);
                DrawLine(P(11, 8), P(13, 17), new Color(c.Darkened(0.35f), 1f), 1.2f * s, true);
                break;
            default:
                DrawLine(P(8, 3), P(8, 26), c, w, true);
                DrawColoredPolygon(Pts(9, 4, 23, 8, 9, 13), new Color(c, 0.9f));
                DrawArc(P(8, 25), 5 * s, MathF.PI, MathF.Tau, 10, dim, w, true);
                break;
        }
    }
}
