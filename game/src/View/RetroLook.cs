using System;
using System.Collections.Generic;
using ContingencyZero.Game.App;
using Godot;

namespace ContingencyZero.Game.View;

/// <summary>
/// The old-school look: the 3D world is drawn in chunky pixels and squeezed into a fixed
/// 8-bit-style palette with ordered dithering, like a 1997 RTS running at 640x480. The HUD
/// sits above it and stays sharp.
/// </summary>
/// <remarks>
/// The palette lookup is a 32x32x32 table (built once on the CPU) so the shader does one texture
/// read per pixel rather than searching the palette.
/// </remarks>
public static class RetroLook
{
    /// <summary>On when the setting is on, or forced with --dev-retro.</summary>
    public static bool Enabled => Forced ?? Settings.Current.RetroLook;

    /// <summary>Set by dev automation to force the look on or off regardless of the setting.</summary>
    public static bool? Forced { get; set; }

    /// <summary>Screen pixels per world pixel. 2 at 1080p gives about a 960x540 picture.</summary>
    public static int PixelSize => Math.Max(1, (int)Math.Round(DisplayServer.WindowGetSize().Y / 540f));

    /// <summary>Gives a unit-shader material the hull detail texture, when the old-school look is on.</summary>
    public static void ApplyHullTexture(ShaderMaterial mat)
    {
        if (!Enabled || ResourceLoader.Load<Texture2D>("res://textures/hull_detail.png") is not { } tex) return;
        mat.SetShaderParameter("hull_tex", tex);
        Color a = TextureSet.HullAverage;
        mat.SetShaderParameter("hull_avg", new Vector3(a.R, a.G, a.B));
        mat.SetShaderParameter("hull_textured", true);
    }

    private static readonly Dictionary<string, ImageTexture> Luts = new();

    /// <summary>A canvas layer that pixelates and palettises everything drawn beneath it (the 3D world).</summary>
    public static CanvasLayer MakeLayer(string theme = "")
    {
        var layer = new CanvasLayer { Layer = -5, Name = "Retro" };
        var mat = new ShaderMaterial { Shader = new Shader { Code = PostShader } };
        mat.SetShaderParameter("lut", Lut(theme));
        mat.SetShaderParameter("pixel", (float)PixelSize);
        var rect = new ColorRect { Material = mat, MouseFilter = Control.MouseFilterEnum.Ignore };
        rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(rect);
        return layer;
    }

    /// <summary>
    /// A 1997-ish palette: ramps of greys, earth, olive, sand, rust, sea and fire, plus the team
    /// colours. Darker shades drift cooler and lighter ones warmer, as hand-made ramps do.
    /// </summary>
    public static List<Color> Palette()
    {
        var p = new List<Color>();
        void Ramp(float hue, float sat, int n, float lo = 0.06f, float hi = 0.95f, float shift = 0.04f)
        {
            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 0.5f : i / (float)(n - 1);
                float h = hue + (t - 0.5f) * shift * -1f;
                float s = sat * (1f - 0.35f * t * t);
                p.Add(Color.FromHsv(((h % 1f) + 1f) % 1f, Mathf.Clamp(s, 0, 1), Mathf.Lerp(lo, hi, t)));
            }
        }
        Ramp(0.60f, 0.06f, 14, 0.02f, 0.97f, 0);  // cold grey (metal)
        Ramp(0.08f, 0.14f, 10, 0.05f, 0.85f);      // warm grey
        Ramp(0.08f, 0.40f, 10, 0.06f, 0.80f);      // earth brown
        Ramp(0.11f, 0.35f, 8, 0.25f, 0.92f);       // sand
        Ramp(0.20f, 0.45f, 10, 0.06f, 0.75f);      // olive
        Ramp(0.28f, 0.45f, 8, 0.06f, 0.70f);       // grass
        Ramp(0.03f, 0.60f, 8, 0.08f, 0.75f);       // rust
        Ramp(0.60f, 0.55f, 10, 0.10f, 0.95f);      // team blue
        Ramp(0.59f, 0.32f, 8, 0.15f, 0.85f);       // steel blue (worn blue paint)
        Ramp(0.00f, 0.70f, 8, 0.10f, 0.95f);       // team red
        Ramp(0.33f, 0.55f, 5, 0.15f, 0.90f);       // team green
        Ramp(0.14f, 0.75f, 6, 0.30f, 1.00f);       // yellow
        Ramp(0.07f, 0.85f, 6, 0.30f, 1.00f);       // fire orange
        Ramp(0.52f, 0.55f, 6, 0.15f, 1.00f);       // cyan glow / ice
        Ramp(0.78f, 0.45f, 5, 0.15f, 0.85f);       // purple
        Ramp(0.57f, 0.50f, 6, 0.05f, 0.45f);       // deep water
        return p;
    }

    /// <summary>
    /// The palette lookup table for a theme. Each theme's actual ground tones (from
    /// TextureSet) are added as shaded ramp entries so its terrain quantises to its own
    /// colours - without them, warm mid-tones (like the desert patch layer) snap to the
    /// generic saturated yellow ramp and read as big mustard blotches.
    /// </summary>
    private static ImageTexture Lut(string theme)
    {
        if (Luts.TryGetValue(theme, out var cached)) return cached;
        List<Color> pal = Palette();
        if (theme == "desert")
            for (int i = 0; i < pal.Count; i++)
            {
                // Sandstone and brick must not fall into saturated UI/explosion warm ramps.
                // Neutralise rust, orange and yellow highlights; pure team red stays intact.
                Color c = pal[i];
                c.ToHsv(out float h, out float s, out float v);
                if (h is > 0.025f and < 0.18f && s > 0.38f)
                    pal[i] = Color.FromHsv(h, s * 0.28f, v);
            }
        if (theme.Length > 0 && TextureSet.GroundAverages.TryGetValue(theme, out Color[]? avgs))
        {
            // Ground sources are art-directed in the shader. Seed retro with those displayed
            // bands rather than raw photo averages, which can snap to mustard or cold grey.
            if (theme is "temperate" or "arctic" or "lava")
            {
                TerrainPalette p = TerrainView.PaletteFor(theme);
                avgs = new[] { p.Shore, p.Low, p.High, p.Peak, p.Rock };
            }
            foreach (Color a in avgs)
                foreach (float v in new[] { 0.7f, 0.85f, 1f, 1.15f, 1.3f, 1.45f, 1.6f })
                    pal.Add(new Color(Mathf.Clamp(a.R * v, 0, 1), Mathf.Clamp(a.G * v, 0, 1), Mathf.Clamp(a.B * v, 0, 1)));
            if (theme == "temperate")
            {
                // Tree crowns and hedge leaves occupy different greens from the meadow. Without
                // these displayed foliage tones their lit faces snap to the generic yellow ramp.
                Color[] foliage =
                {
                    new(0.12f, 0.21f, 0.08f), new(0.21f, 0.32f, 0.11f),
                    new(0.34f, 0.44f, 0.16f), new(0.24f, 0.36f, 0.16f),
                };
                foreach (Color a in foliage)
                foreach (float v in new[] { 0.65f, 0.85f, 1f, 1.2f, 1.45f, 1.7f })
                    pal.Add(new Color(Mathf.Clamp(a.R * v, 0, 1), Mathf.Clamp(a.G * v, 0, 1), Mathf.Clamp(a.B * v, 0, 1)));
            }
            if (theme == "arctic")
            {
                // Preserve snow, blue-green needles, exposed stone and weathered wreck steel.
                Color[] featureColours =
                {
                    new(0.88f, 0.89f, 0.89f), new(0.76f, 0.77f, 0.77f), new(0.62f, 0.65f, 0.69f),
                    new(0.07f, 0.15f, 0.14f), new(0.12f, 0.23f, 0.21f), new(0.20f, 0.32f, 0.29f),
                    new(0.30f, 0.32f, 0.35f), new(0.34f, 0.33f, 0.31f),
                };
                foreach (Color a in featureColours)
                foreach (float v in new[] { 0.65f, 0.85f, 1f, 1.2f, 1.45f })
                    pal.Add(new Color(Mathf.Clamp(a.R * v, 0, 1), Mathf.Clamp(a.G * v, 0, 1), Mathf.Clamp(a.B * v, 0, 1)));
            }
            if (theme == "lava")
            {
                // Keep charcoal ruins, basalt and burnt wood neutral; only actual lava uses ember colours.
                Color[] featureColours =
                {
                    new(0.10f, 0.095f, 0.09f), new(0.17f, 0.155f, 0.14f), new(0.25f, 0.225f, 0.20f),
                    new(0.34f, 0.30f, 0.27f), new(0.65f, 0.18f, 0.03f), new(0.95f, 0.38f, 0.05f),
                };
                foreach (Color a in featureColours)
                foreach (float v in new[] { 0.65f, 0.85f, 1f, 1.2f, 1.45f })
                    pal.Add(new Color(Mathf.Clamp(a.R * v, 0, 1), Mathf.Clamp(a.G * v, 0, 1), Mathf.Clamp(a.B * v, 0, 1)));
            }
        }
        // Keep desert gently warm like the unquantised scene. Temperate and arctic use their
        // displayed terrain bands directly: warming snaps meadow to mustard and snow to cyan.
        if (theme == "desert")
            for (int i = 0; i < pal.Count; i++)
            {
                Color c = pal[i];
                pal[i] = new Color(Mathf.Clamp(c.R * 1.025f + 0.004f, 0, 1),
                    Mathf.Clamp(c.G * 1.01f + 0.002f, 0, 1), Mathf.Clamp(c.B * 0.975f, 0, 1), c.A);
            }
        const int N = 32;
        var img = Image.CreateEmpty(N * N, N, false, Image.Format.Rgb8);
        for (int b = 0; b < N; b++)
        for (int g = 0; g < N; g++)
        for (int r = 0; r < N; r++)
        {
            float cr = r / (N - 1f), cg = g / (N - 1f), cb = b / (N - 1f);
            Color best = pal[0];
            float bestD = float.MaxValue;
            foreach (Color c in pal)
            {
                // "Redmean" weighting: closer to how far apart colours look than plain RGB distance.
                float rm = (cr + c.R) * 0.5f;
                float dr = cr - c.R, dg = cg - c.G, db = cb - c.B;
                float d = (2 + rm) * dr * dr + 4 * dg * dg + (3 - rm) * db * db;
                if (d < bestD) { bestD = d; best = c; }
            }
            img.SetPixel(r + b * N, g, best);
        }
        var tex = ImageTexture.CreateFromImage(img);
        Luts[theme] = tex;
        return tex;
    }

    private const string PostShader = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_nearest;
uniform sampler2D lut : filter_nearest;
uniform float pixel = 2.0;
const float BAYER[16] = float[](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
void fragment() {
    vec2 cell = floor(FRAGCOORD.xy / pixel);
    vec2 uv = (cell * pixel + 0.5 * pixel) * SCREEN_PIXEL_SIZE;
    vec3 c = texture(screen_tex, uv).rgb;
    int bi = int(mod(cell.y, 4.0)) * 4 + int(mod(cell.x, 4.0));
    c += (BAYER[bi] / 16.0 - 0.47) * (1.0 / 18.0);
    vec3 q = floor(clamp(c, 0.0, 1.0) * 31.0 + 0.5);
    vec2 at = vec2(q.r + q.b * 32.0 + 0.5, q.g + 0.5) / vec2(1024.0, 32.0);
    COLOR = vec4(texture(lut, at).rgb, 1.0);
}";
}
