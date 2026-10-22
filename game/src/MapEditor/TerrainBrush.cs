using System;
using System.Collections.Generic;
using ContingencyZero.Sim.Map;

namespace ContingencyZero.Game.MapEditor;

/// <summary>What a terrain brush does to the ground under it.</summary>
public enum BrushOp { Raise, Lower, Smooth, Flatten, Plateau, Roughen }

/// <summary>
/// Sculpts a map's corner heights. Heights are whole quarter-unit steps, so a stroke works on a
/// float copy and writes rounded values back; that way a gentle brush still moves the ground.
/// Editor code, not sim code: floats are fine here because the saved map is plain integers.
/// </summary>
public sealed class TerrainBrush
{
    /// <summary>Lowest and highest corner height the editor allows, in height steps.</summary>
    public const int MinHeight = 0;
    public const int MaxHeight = 440;

    // Full-strength rates, per second at the centre of the brush.
    private const float RaiseRate = 90f;      // height steps per second
    private const float SmoothRate = 10f;     // blend speed
    private const float FlattenRate = 7f;     // blend speed
    private const float PlateauRate = 30f;    // blend speed (near-instant at full strength)
    private const float RoughenRate = 70f;    // height steps per second of noise amplitude

    private readonly MapData _map;
    private readonly float[] _work;
    private float[] _scratch = Array.Empty<float>();
    private int _seed;

    /// <summary>Corner rectangles (inclusive) changed since <see cref="TakeDirty"/> was last called: one per brush application, so mirrored strokes stay apart.</summary>
    private readonly List<(int X0, int Y0, int X1, int Y1)> _dirty = new();

    public TerrainBrush(MapData map)
    {
        _map = map;
        _work = new float[map.Heights.Length];
    }

    /// <summary>Starts a stroke: copies the current heights into the working buffer.</summary>
    public void BeginStroke(int seed)
    {
        for (int i = 0; i < _work.Length; i++) _work[i] = _map.Heights[i];
        _seed = seed;
    }

    /// <summary>Moves the corner rectangles changed since the last call into <paramref name="into"/>. Returns false if nothing changed.</summary>
    public bool TakeDirty(List<(int X0, int Y0, int X1, int Y1)> into)
    {
        into.AddRange(_dirty);
        bool any = _dirty.Count > 0;
        _dirty.Clear();
        return any;
    }

    /// <summary>Smooth bell falloff: 1 at the centre, 0 at the rim.</summary>
    private static float Falloff(float t) => t >= 1f ? 0f : 0.5f + 0.5f * MathF.Cos(t * MathF.PI);

    /// <summary>
    /// Applies one step of a brush centred on world point (<paramref name="cx"/>, <paramref name="cz"/>).
    /// <paramref name="target"/> is the height (in steps) that Flatten and Plateau aim for.
    /// </summary>
    /// <param name="flipX">This application is a mirror image across x (Roughen then mirrors its bumps too).</param>
    /// <param name="flipY">This application is a mirror image across y.</param>
    public void Apply(BrushOp op, float cx, float cz, float radius, float strength, float dt, float target, bool flipX = false, bool flipY = false)
    {
        int vw = _map.TilesX + 1, vh = _map.TilesY + 1;
        const float ts = MapData.TileSize;
        int x0 = Math.Max(0, (int)MathF.Floor((cx - radius) / ts));
        int x1 = Math.Min(vw - 1, (int)MathF.Ceiling((cx + radius) / ts));
        int y0 = Math.Max(0, (int)MathF.Floor((cz - radius) / ts));
        int y1 = Math.Min(vh - 1, (int)MathF.Ceiling((cz + radius) / ts));
        if (x0 > x1 || y0 > y1) return;
        strength = Math.Clamp(strength, 0.01f, 1f);

        if (op == BrushOp.Smooth)
        {
            // Read neighbours from an untouched copy so the result doesn't depend on loop order.
            int bw = x1 - x0 + 3, bh = y1 - y0 + 3;
            if (_scratch.Length < bw * bh) _scratch = new float[bw * bh];
            for (int y = 0; y < bh; y++)
            for (int x = 0; x < bw; x++)
            {
                int sx = Math.Clamp(x0 - 1 + x, 0, vw - 1), sy = Math.Clamp(y0 - 1 + y, 0, vh - 1);
                _scratch[y * bw + x] = _work[sy * vw + sx];
            }
        }

        float k = op switch
        {
            BrushOp.Smooth => 1f - MathF.Exp(-SmoothRate * strength * dt),
            BrushOp.Flatten => 1f - MathF.Exp(-FlattenRate * strength * dt),
            BrushOp.Plateau => 1f - MathF.Exp(-PlateauRate * strength * dt),
            _ => 0f,
        };

        bool changed = false;
        for (int vy = y0; vy <= y1; vy++)
        for (int vx = x0; vx <= x1; vx++)
        {
            float dx = vx * ts - cx, dz = vy * ts - cz;
            float t = MathF.Sqrt(dx * dx + dz * dz) / radius;
            if (t >= 1f) continue;
            int i = vy * vw + vx;
            float h = _work[i];
            float f = Falloff(t);
            switch (op)
            {
                case BrushOp.Raise:
                    h += RaiseRate * strength * dt * f;
                    break;
                case BrushOp.Lower:
                    h -= RaiseRate * strength * dt * f;
                    break;
                case BrushOp.Smooth:
                {
                    int bw = x1 - x0 + 3;
                    int sx = vx - x0 + 1, sy = vy - y0 + 1;
                    float sum = 0;
                    // 3x3 tent filter: centre 4, edges 2, corners 1.
                    for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        float w = (ox == 0 ? 2 : 1) * (oy == 0 ? 2 : 1);
                        sum += _scratch[(sy + oy) * bw + sx + ox] * w;
                    }
                    float avg = sum / 16f;
                    h += (avg - h) * k * f;
                    break;
                }
                case BrushOp.Flatten:
                    h += (target - h) * k * f;
                    break;
                case BrushOp.Plateau:
                    // Hard edge: the whole disc moves together, so its rim becomes a cliff.
                    h += (target - h) * k;
                    break;
                case BrushOp.Roughen:
                    h += Noise(flipX ? _map.TilesX - vx : vx, flipY ? _map.TilesY - vy : vy) * RoughenRate * strength * dt * f;
                    break;
            }
            h = Math.Clamp(h, MinHeight, MaxHeight);
            _work[i] = h;
            int rounded = (int)MathF.Round(h);
            if (_map.Heights[i] != rounded)
            {
                _map.Heights[i] = rounded;
                changed = true;
            }
        }

        if (changed) _dirty.Add((x0, y0, x1, y1));
    }

    /// <summary>Value noise in -1..1 over corners, about two tiles across, different for each stroke.</summary>
    private float Noise(int vx, int vy)
    {
        const float scale = 0.55f;
        float fx = vx * scale, fy = vy * scale;
        int ix = (int)MathF.Floor(fx), iy = (int)MathF.Floor(fy);
        float ux = fx - ix, uy = fy - iy;
        ux = ux * ux * (3 - 2 * ux);
        uy = uy * uy * (3 - 2 * uy);
        float a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
        float top = a + (b - a) * ux, bottom = c + (d - c) * ux;
        // Add a finer octave so the bumps aren't too regular.
        float fine = Hash(vx * 7 + 3, vy * 5 + 11) * 0.35f;
        return (top + (bottom - top) * uy) * 0.85f + fine;
    }

    private float Hash(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + _seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFF) / 32767.5f - 1f;
        }
    }

    /// <summary>Ground height in steps at a world point, bilinear between corners (float, editor only).</summary>
    public static float HeightStepsAt(MapData map, float x, float z)
    {
        float fx = Math.Clamp(x / MapData.TileSize, 0, map.TilesX - 0.0001f);
        float fz = Math.Clamp(z / MapData.TileSize, 0, map.TilesY - 0.0001f);
        int tx = (int)fx, tz = (int)fz;
        float ux = fx - tx, uz = fz - tz;
        int w = map.TilesX + 1;
        float h00 = map.Heights[tz * w + tx], h10 = map.Heights[tz * w + tx + 1];
        float h01 = map.Heights[(tz + 1) * w + tx], h11 = map.Heights[(tz + 1) * w + tx + 1];
        float top = h00 + (h10 - h00) * ux, bottom = h01 + (h11 - h01) * ux;
        return top + (bottom - top) * uz;
    }
}
