using System;
using Godot;

namespace ContingencyZero.Game.View.Meshes;

/// <summary>
/// Map features: wreck debris piles, fallen-structure foundations and rubble, and themed trees and rocks. Plain and glow materials only
/// (no team colour). Shapes are jittered with a small seeded generator, so a given
/// (kind, theme, variant) or wreck size always produces the same mesh.
/// </summary>
internal static class Features
{
    private static Vector3 V(float x, float y, float z) => new(x, y, z);
    private static Paint P(float r, float g, float b) => Paint.Plain(r, g, b);

    /// <summary>Tiny deterministic xorshift generator (visual jitter only; never used by the sim).</summary>
    private sealed class Jitter
    {
        private uint _s;
        public Jitter(int seed) => _s = (uint)seed * 2654435761u ^ 0x9E3779B9u | 1u;
        public float Next()
        {
            _s ^= _s << 13;
            _s ^= _s >> 17;
            _s ^= _s << 5;
            return (_s & 0xFFFFFF) / 16777216f;
        }
        public float Range(float a, float b) => a + (b - a) * Next();
    }

    private static int Seed(string text, int variant)
    {
        int h = 17;
        foreach (char c in text) h = h * 31 + c;
        return h * 7 + variant * 1013;
    }

    // ---- Wrecks ----------------------------------------------------------------------------------

    /// <summary>A scorched debris pile about <paramref name="size"/> units across: hull chunk, plates, a broken barrel, embers.</summary>
    public static ArrayMesh Wreck(int size)
    {
        var rng = new Jitter(size * 7919 + 17);
        float r = size / 2f;
        var k = new MeshKit(new Transform3D(Basis.Identity.Scaled(V(r, r, r)), Vector3.Zero));
        Paint scorch = P(0.13f, 0.12f, 0.11f), metal = P(0.36f, 0.35f, 0.33f), rust = P(0.34f, 0.22f, 0.14f), dark = P(0.2f, 0.19f, 0.18f);
        Paint ember = Paint.Glow(0.75f, 0.24f, 0.05f);
        Paint[] chunkPaints = { metal, rust, dark, metal, dark };

        k.Cylinder(V(0, 0, 0), 0.98f, 0.86f, 0.06f, 7, scorch, phase: rng.Range(0, 1));
        using (k.Push(V(rng.Range(-0.1f, 0.1f), 0.06f, rng.Range(-0.1f, 0.1f)), V(rng.Range(-0.25f, 0.25f), rng.Range(0, Mathf.Tau), rng.Range(-0.25f, 0.25f))))
            k.Frustum(Vector3.Zero, new Vector2(0.8f, 0.6f), new Vector2(0.5f, 0.36f), 0.32f, dark, metal);
        int chunks = Math.Clamp(5 + size / 3, 5, 11);
        for (int i = 0; i < chunks; i++)
        {
            float a = rng.Range(0, Mathf.Tau), d = rng.Range(0.3f, 0.78f);
            var pos = V(Mathf.Cos(a) * d, rng.Range(0.06f, 0.14f), Mathf.Sin(a) * d);
            using (k.Push(pos, V(rng.Range(-0.6f, 0.6f), rng.Range(0, Mathf.Tau), rng.Range(-0.6f, 0.6f))))
                k.Box(Vector3.Zero, V(rng.Range(0.12f, 0.34f), rng.Range(0.07f, 0.2f), rng.Range(0.14f, 0.4f)), chunkPaints[i % chunkPaints.Length]);
        }
        using (k.Push(V(rng.Range(-0.4f, 0.4f), 0.2f, rng.Range(-0.4f, 0.4f)), V(rng.Range(0.5f, 0.9f), rng.Range(0, Mathf.Tau), 0)))
            k.Box(Vector3.Zero, V(0.5f, 0.03f, 0.36f), rust);
        float ba = rng.Range(0, Mathf.Tau);
        k.Rod(V(Mathf.Cos(ba) * 0.1f, 0.24f, Mathf.Sin(ba) * 0.1f), V(Mathf.Cos(ba) * 0.85f, 0.08f, Mathf.Sin(ba) * 0.85f), 0.05f, 0.045f, 5, metal, scorch);
        for (int i = 0; i < 2; i++)
        {
            float a = rng.Range(0, Mathf.Tau), d = rng.Range(0.2f, 0.6f);
            k.Box(V(Mathf.Cos(a) * d, 0.07f, Mathf.Sin(a) * d), V(0.12f, 0.04f, 0.1f), ember);
        }
        return k.ToMesh();
    }

    // ---- Fallen structures ------------------------------------------------------------------------

    /// <summary>
    /// What's left of a structure: its cracked, scorched foundation slab broken into tilted plates,
    /// stumps of wall round the edge, twisted rebar and a few embers. About
    /// <paramref name="width"/> units square (the footprint), centred on the origin.
    /// </summary>
    public static ArrayMesh Foundation(int width, int variant)
    {
        var rng = new Jitter(width * 4241 + variant * 613 + 5);
        var k = new MeshKit();
        Paint concrete = P(0.42f, 0.41f, 0.38f), cracked = P(0.3f, 0.29f, 0.27f), soot = P(0.12f, 0.115f, 0.11f);
        Paint wall = P(0.36f, 0.35f, 0.33f), rebar = P(0.33f, 0.2f, 0.12f), steel = P(0.27f, 0.27f, 0.28f);
        Paint ember = Paint.Glow(0.7f, 0.22f, 0.04f);
        float half = width * 0.46f;

        // The slab, split into plates that cracked and settled at slightly different angles.
        int cells = width >= 24 ? 3 : 2;
        float cell = half * 2 / cells;
        for (int cy = 0; cy < cells; cy++)
        for (int cx = 0; cx < cells; cx++)
        {
            var centre = V(-half + cell * (cx + 0.5f), 0.22f + rng.Range(-0.08f, 0.1f), -half + cell * (cy + 0.5f));
            var tilt = V(rng.Range(-0.06f, 0.06f), rng.Range(-0.03f, 0.03f), rng.Range(-0.06f, 0.06f));
            float gap = rng.Range(0.25f, 0.6f);
            Paint face = rng.Next() < 0.35f ? soot : rng.Next() < 0.5f ? cracked : concrete;
            using (k.Push(centre, tilt))
                k.Box(Vector3.Zero, V(cell - gap, 0.5f, cell - gap), cracked, face);
        }
        // A blackened burn across the middle.
        k.Cylinder(V(rng.Range(-1f, 1f), 0.48f, rng.Range(-1f, 1f)), half * 0.55f, half * 0.5f, 0.04f, 9, soot, phase: rng.Range(0, 1));

        // Wall stumps round the edge: broken to different heights, some knocked over.
        int perSide = Math.Clamp(width / 6, 2, 5);
        for (int side = 0; side < 4; side++)
        for (int i = 0; i < perSide; i++)
        {
            if (rng.Next() < 0.3f) continue; // a gap blown through
            float along = -half + (i + 0.5f) * (half * 2 / perSide);
            float h = rng.Range(0.6f, 2.6f) * Math.Min(1.6f, width / 16f + 0.4f);
            float len = half * 2 / perSide * rng.Range(0.55f, 0.95f);
            Vector3 pos = side switch
            {
                0 => V(along, 0.45f + h / 2, -half),
                1 => V(along, 0.45f + h / 2, half),
                2 => V(-half, 0.45f + h / 2, along),
                _ => V(half, 0.45f + h / 2, along),
            };
            bool alongX = side < 2;
            Vector3 lean = V(alongX ? rng.Range(-0.25f, 0.25f) : 0, 0, alongX ? 0 : rng.Range(-0.25f, 0.25f));
            using (k.Push(pos, lean))
                k.Box(Vector3.Zero, alongX ? V(len, h, 0.7f) : V(0.7f, h, len), wall, rng.Next() < 0.5f ? soot : cracked);
        }

        // Twisted rebar and a buckled girder.
        int rods = Math.Clamp(width / 3, 3, 10);
        for (int i = 0; i < rods; i++)
        {
            var a = V(rng.Range(-half, half), 0.45f, rng.Range(-half, half));
            var b = a + V(rng.Range(-1.4f, 1.4f), rng.Range(1.2f, 3.2f), rng.Range(-1.4f, 1.4f));
            k.Rod(a, b, 0.09f, 0.07f, 4, rebar);
        }
        var g0 = V(rng.Range(-half, 0), 0.6f, rng.Range(-half, half) * 0.5f);
        k.Beam(g0, g0 + V(half * rng.Range(0.6f, 1.1f), rng.Range(0.6f, 1.8f), rng.Range(-2f, 2f)), 0.5f, 0.35f, steel, soot);

        // Loose blocks and a few embers still glowing in the ash.
        for (int i = 0; i < Math.Clamp(width / 3, 4, 12); i++)
        {
            var pos = V(rng.Range(-half, half), 0.55f, rng.Range(-half, half));
            using (k.Push(pos, V(rng.Range(-0.5f, 0.5f), rng.Range(0, Mathf.Tau), rng.Range(-0.5f, 0.5f))))
                k.Box(Vector3.Zero, V(rng.Range(0.4f, 1.1f), rng.Range(0.3f, 0.7f), rng.Range(0.4f, 1.1f)), i % 3 == 0 ? soot : concrete);
        }
        for (int i = 0; i < 3; i++)
            k.Box(V(rng.Range(-half, half) * 0.6f, 0.5f, rng.Range(-half, half) * 0.6f), V(0.35f, 0.08f, 0.3f), ember);
        return k.ToMesh();
    }

    /// <summary>A small heap of broken concrete and rebar, about three units across (scattered round a fallen structure).</summary>
    public static ArrayMesh Rubble(int variant)
    {
        var rng = new Jitter(variant * 977 + 31);
        var k = new MeshKit();
        Paint concrete = P(0.4f, 0.39f, 0.36f), dusty = P(0.47f, 0.45f, 0.4f), soot = P(0.15f, 0.14f, 0.13f), rebar = P(0.33f, 0.2f, 0.12f);
        k.Cylinder(V(0, 0, 0), 1.6f, 1.2f, 0.18f, 6, soot, phase: rng.Range(0, 1));
        int blocks = 5 + variant;
        for (int i = 0; i < blocks; i++)
        {
            float a = rng.Range(0, Mathf.Tau), d = rng.Range(0f, 1.1f);
            var pos = V(Mathf.Cos(a) * d, 0.25f + rng.Range(0, 0.35f) * (1.2f - d), Mathf.Sin(a) * d);
            using (k.Push(pos, V(rng.Range(-0.7f, 0.7f), rng.Range(0, Mathf.Tau), rng.Range(-0.7f, 0.7f))))
                k.Box(Vector3.Zero, V(rng.Range(0.35f, 0.8f), rng.Range(0.25f, 0.55f), rng.Range(0.35f, 0.8f)), i % 3 == 0 ? dusty : concrete, i % 4 == 1 ? soot : null);
        }
        var r0 = V(rng.Range(-0.4f, 0.4f), 0.4f, rng.Range(-0.4f, 0.4f));
        k.Rod(r0, r0 + V(rng.Range(-0.8f, 0.8f), rng.Range(0.8f, 1.5f), rng.Range(-0.8f, 0.8f)), 0.06f, 0.05f, 4, rebar);
        return k.ToMesh();
    }

    // ---- Trees -----------------------------------------------------------------------------------

    /// <summary>
    /// Temperate: tiered conifer. Arctic: conifer with snow caps. Desert: dusty juniper / cypress.
    /// Lava: charred dead snag with embers at the roots. Variant 0 standard, 1 squat, 2 slender.
    /// </summary>
    public static ArrayMesh Tree(string theme, int variant)
    {
        var rng = new Jitter(Seed(theme, variant));
        var k = new MeshKit();
        Paint bark = P(0.3f, 0.2f, 0.12f);
        switch (theme)
        {
            case "lava":
            {
                Paint charred = P(0.1f, 0.09f, 0.085f), ash = P(0.24f, 0.22f, 0.21f);
                float h = variant switch { 1 => 4.2f, 2 => 6.6f, _ => 5.4f };
                k.Cylinder(V(0, 0, 0), 0.42f, 0.1f, h, 6, charred, phase: rng.Range(0, 1));
                int branches = 3 + variant;
                for (int i = 0; i < branches; i++)
                {
                    float y = h * rng.Range(0.35f, 0.8f), a = rng.Range(0, Mathf.Tau), len = rng.Range(1.0f, 1.9f);
                    var from = V(0, y, 0);
                    var to = from + V(Mathf.Cos(a) * len, len * rng.Range(0.5f, 0.9f), Mathf.Sin(a) * len);
                    k.Rod(from, to, 0.12f, 0.02f, 4, i % 2 == 0 ? charred : ash);
                }
                k.Ring(V(0, 0.05f, 0), 0.9f, 0.42f, 0.1f, 6, ash);
                for (int i = 0; i < 3; i++)
                {
                    float a = i * Mathf.Tau / 3 + rng.Range(0, 1);
                    k.Box(V(Mathf.Cos(a) * 0.5f, 0.12f, Mathf.Sin(a) * 0.5f), V(0.22f, 0.08f, 0.16f), Paint.Glow(0.8f, 0.26f, 0.04f));
                }
                break;
            }
            case "desert":
            {
                Paint olive = P(0.42f, 0.44f, 0.24f), oliveDark = P(0.33f, 0.35f, 0.19f);
                bark = P(0.4f, 0.3f, 0.2f);
                if (variant == 2)
                {
                    k.Cylinder(V(0, 0, 0), 0.24f, 0.18f, 1.0f, 5, bark);
                    k.Cylinder(V(0, 0.8f, 0), 1.0f, 0.0f, 6.0f, 6, oliveDark, phase: rng.Range(0, 1));
                    k.Cylinder(V(0, 1.6f, 0), 0.8f, 0.0f, 4.6f, 6, olive, phase: rng.Range(0, 1));
                }
                else
                {
                    float h = variant == 1 ? 1.6f : 2.4f;
                    k.Rod(V(0, 0, 0), V(rng.Range(-0.4f, 0.4f), h, rng.Range(-0.4f, 0.4f)), 0.3f, 0.16f, 5, bark);
                    int puffs = variant == 1 ? 3 : 2;
                    for (int i = 0; i < puffs; i++)
                    {
                        float a = rng.Range(0, Mathf.Tau);
                        var c = V(Mathf.Cos(a) * 0.7f, h + rng.Range(-0.3f, 0.6f), Mathf.Sin(a) * 0.7f);
                        k.Sphere(c, V(rng.Range(1.3f, 1.8f), rng.Range(0.7f, 1.0f), rng.Range(1.3f, 1.8f)), 6, 3, i % 2 == 0 ? olive : oliveDark);
                    }
                }
                break;
            }
            default:
            {
                bool snowy = theme == "arctic";
                Paint leaf = snowy ? P(0.12f, 0.26f, 0.2f) : P(0.17f, 0.37f, 0.15f);
                Paint leafDark = snowy ? P(0.09f, 0.2f, 0.16f) : P(0.12f, 0.29f, 0.12f);
                Paint snow = P(0.9f, 0.93f, 0.96f);
                int tiers = variant switch { 1 => 2, 2 => 4, _ => 3 };
                float radius = variant switch { 1 => 2.3f, 2 => 1.4f, _ => 1.9f };
                float tierH = variant switch { 1 => 2.4f, 2 => 2.0f, _ => 2.4f };
                k.Cylinder(V(0, 0, 0), 0.3f, 0.22f, 1.6f, 5, bark);
                for (int i = 0; i < tiers; i++)
                {
                    float y = 1.0f + i * tierH * 0.62f;
                    float rr = radius * (1f - i * 0.6f / tiers);
                    var c = V(rng.Range(-0.12f, 0.12f), y, rng.Range(-0.12f, 0.12f));
                    float ph = rng.Range(0, 1);
                    Paint p = i % 2 == 0 ? leafDark : leaf;
                    if (snowy)
                    {
                        k.Cylinder(c, rr, rr * 0.42f, tierH * 0.58f, 7, p, phase: ph);
                        k.Cylinder(c + V(0, tierH * 0.58f, 0), rr * 0.42f, 0, tierH * 0.42f, 7, snow, phase: ph);
                    }
                    else
                    {
                        k.Cylinder(c, rr, 0, tierH, 7, p, phase: ph);
                    }
                }
                if (snowy) k.Ring(V(0, 0.04f, 0), radius * 0.9f, 0.3f, 0.08f, 7, snow);
                break;
            }
        }
        return k.ToMesh();
    }

    // ---- Rocks -----------------------------------------------------------------------------------

    /// <summary>A boulder (variant 0), a flat slab with a stone (1) or a cluster of three (2), coloured for the theme.</summary>
    public static ArrayMesh Rock(string theme, int variant)
    {
        var rng = new Jitter(Seed(theme, variant) + 77);
        var k = new MeshKit();
        (Paint rock, Paint rockDark, Paint? top, Paint? crack) = theme switch
        {
            "desert" => (P(0.62f, 0.47f, 0.32f), P(0.52f, 0.38f, 0.26f), (Paint?)P(0.74f, 0.62f, 0.44f), (Paint?)null),
            "lava" => (P(0.17f, 0.16f, 0.16f), P(0.11f, 0.1f, 0.1f), null, Paint.Glow(0.85f, 0.28f, 0.04f)),
            "arctic" => (P(0.46f, 0.5f, 0.56f), P(0.36f, 0.4f, 0.46f), P(0.92f, 0.95f, 0.98f), null),
            _ => (P(0.46f, 0.46f, 0.44f), P(0.37f, 0.37f, 0.36f), (Paint?)null, (Paint?)null),
        };
        Paint ore = P(0.58f, 0.62f, 0.68f);
        switch (variant)
        {
            case 1:
                Boulder(k, V(0, 0, 0), 2.6f, 0.45f, rng, rock, rockDark, top, crack);
                Boulder(k, V(1.6f, 0.5f, 0.8f), 1.0f, 0.9f, rng, rockDark, rock, top, crack);
                break;
            case 2:
                Boulder(k, V(-0.9f, 0, -0.5f), 1.5f, 0.8f, rng, rock, rockDark, top, crack);
                Boulder(k, V(1.0f, 0, -0.3f), 1.2f, 0.9f, rng, rockDark, rock, top, crack);
                Boulder(k, V(0.1f, 0, 1.1f), 1.0f, 0.75f, rng, rock, rockDark, top, crack);
                break;
            default:
                Boulder(k, V(0, 0, 0), 2.2f, 0.72f, rng, rock, rockDark, top, crack);
                break;
        }
        // Metal ore crystals: the reason builders reclaim rocks.
        for (int i = 0; i < 2 + variant % 2; i++)
        {
            float a = rng.Range(0, Mathf.Tau), d = rng.Range(0.9f, 1.6f);
            var from = V(Mathf.Cos(a) * d, 0.1f, Mathf.Sin(a) * d);
            var to = from + V(Mathf.Cos(a) * 0.4f, rng.Range(0.6f, 1.0f), Mathf.Sin(a) * 0.4f);
            k.Rod(from, to, 0.2f, 0, 4, ore);
        }
        return k.ToMesh();
    }

    /// <summary>A jittered low-poly boulder, partly sunk into the ground; faces facing up take the top paint (sand / snow).</summary>
    private static void Boulder(MeshKit k, Vector3 c, float r, float hs, Jitter rng, Paint rock, Paint rockDark, Paint? top, Paint? crack)
    {
        const int seg = 7, rings = 3;
        var grid = new Vector3[rings + 1][];
        for (int ring = 0; ring <= rings; ring++)
        {
            float lat = Mathf.Lerp(-0.3f, 1.25f, ring / (float)rings);
            grid[ring] = new Vector3[seg];
            for (int sIdx = 0; sIdx < seg; sIdx++)
            {
                float lon = sIdx * Mathf.Tau / seg + rng.Range(-0.2f, 0.2f);
                float rad = r * rng.Range(0.82f, 1.12f);
                grid[ring][sIdx] = c + V(Mathf.Cos(lat) * Mathf.Cos(lon) * rad, Mathf.Sin(lat) * rad * hs, Mathf.Cos(lat) * Mathf.Sin(lon) * rad);
            }
        }
        Vector3 apex = c + V(rng.Range(-0.2f, 0.2f) * r, r * hs * rng.Range(1.0f, 1.12f), rng.Range(-0.2f, 0.2f) * r);
        Vector3 inside = c + V(0, r * hs * 0.3f, 0);

        void Face(Vector3 a, Vector3 b, Vector3 d, int index)
        {
            Vector3 n = (b - a).Cross(d - a);
            if (n.Dot((a + b + d) / 3 - inside) < 0) n = -n;
            n = n.Normalized();
            Paint p = index % 3 == 0 ? rockDark : rock;
            if (top is { } tp && n.Y > 0.72f) p = tp;
            else if (crack is { } cp && n.Y < 0.35f && index % 5 == 1) p = cp;
            k.Tri(a, b, d, p, inside);
        }

        int f = 0;
        for (int ring = 0; ring < rings; ring++)
        for (int sIdx = 0; sIdx < seg; sIdx++)
        {
            int s1 = (sIdx + 1) % seg;
            Face(grid[ring][sIdx], grid[ring][s1], grid[ring + 1][s1], f++);
            Face(grid[ring][sIdx], grid[ring + 1][s1], grid[ring + 1][sIdx], f++);
        }
        for (int sIdx = 0; sIdx < seg; sIdx++)
            Face(grid[rings][sIdx], grid[rings][(sIdx + 1) % seg], apex, f++);
        k.Poly(grid[0], rockDark, inside);
    }
}
