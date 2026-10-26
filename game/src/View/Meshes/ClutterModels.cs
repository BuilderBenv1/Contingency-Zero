using System;
using System.Collections.Generic;
using Godot;

namespace ContingencyZero.Game.View.Meshes;

/// <summary>
/// Desert battlefield clutter built in code (no external assets): ruined mud-brick villages,
/// field walls, a well, supply dumps, palms and oasis pools. Walls are laid as stacked
/// individual bricks with per-brick colour jitter, missing bricks and broken top courses;
/// rubble grounds each piece. Vertex colours only, the same convention as the hand-built
/// feature models (authored in world units, alpha 1 = plain, alpha 0.5 = glow - unused here).
/// Deterministic per-vertex colour jitter so every machine builds the identical mesh.
/// </summary>
internal static class ClutterModels
{
    private static readonly Dictionary<string, ArrayMesh> Cache = new();

    /// <summary>Ground footprint (x, z) of each model, for the fake contact shadow.</summary>
    public static Vector2 Footprint(string name) => name switch
    {
        "clutter_wall_ruin" => new Vector2(8.4f, 3f),
        "clutter_house_ruin" or "clutter_farm_ruin" => new Vector2(9.5f, 8.5f),
        "clutter_field_wall" or "clutter_stone_wall" => new Vector2(11f, 2.6f),
        "clutter_well" => new Vector2(3.4f, 3.4f),
        "clutter_barrels" => new Vector2(4.2f, 3.6f),
        "clutter_crates" => new Vector2(3.6f, 3.2f),
        "clutter_palm" => new Vector2(2.4f, 2.4f),
        _ => new Vector2(3f, 3f),
    };

    public static ArrayMesh? Get(string name)
    {
        if (Cache.TryGetValue(name, out var hit)) return hit;
        var b = new Builder();
        switch (name)
        {
            case "clutter_wall_ruin": WallRuin(b); break;
            case "clutter_house_ruin": HouseRuin(b); break;
            case "clutter_farm_ruin": HouseRuin(b); break;
            case "clutter_field_wall": FieldWall(b); break;
            case "clutter_stone_wall": FieldWall(b); break;
            case "clutter_well": Well(b); break;
            case "clutter_barrels": Barrels(b); break;
            case "clutter_crates": Crates(b); break;
            case "clutter_palm": Palm(b); break;
            case "clutter_reeds": Reeds(b); break;
            default: return null;
        }
        if (name is "clutter_farm_ruin" or "clutter_stone_wall")
        {
            // The same brick construction reads as weathered grey fieldstone on temperate maps.
            var grey = new Color(0.42f, 0.44f, 0.38f);
            for (int i = 0; i < b.Colours.Count; i++) b.Colours[i] = b.Colours[i].Lerp(grey, 0.65f);
        }
        var mesh = new ArrayMesh();
        using Variant v = Variant.CreateFrom(b.Vertices.ToArray()), n = Variant.CreateFrom(b.Normals.ToArray()),
            c = Variant.CreateFrom(b.Colours.ToArray()), ix = Variant.CreateFrom(b.Indices.ToArray());
        using var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = v;
        arrays[(int)Mesh.ArrayType.Normal] = n;
        arrays[(int)Mesh.ArrayType.Color] = c;
        arrays[(int)Mesh.ArrayType.Index] = ix;
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        Cache[name] = mesh;
        return mesh;
    }

    // Desert materials: sun-dried mud brick, dry stone, palm greens, old wood, rusted drums.
    private static readonly Color MudBrick = new(0.66f, 0.54f, 0.39f);
    private static readonly Color MudBrickDark = new(0.5f, 0.4f, 0.29f);
    private static readonly Color Mortar = new(0.55f, 0.47f, 0.36f);
    private static readonly Color Stone = new(0.55f, 0.5f, 0.43f);
    private static readonly Color StoneDark = new(0.4f, 0.37f, 0.33f);
    private static readonly Color FloorEarth = new(0.33f, 0.27f, 0.21f);
    private static readonly Color Trunk = new(0.47f, 0.38f, 0.28f);
    private static readonly Color Frond = new(0.3f, 0.46f, 0.2f);
    private static readonly Color FrondTip = new(0.52f, 0.62f, 0.28f);
    private static readonly Color FrondDead = new(0.52f, 0.42f, 0.26f);
    private static readonly Color Wood = new(0.46f, 0.36f, 0.25f);
    private static readonly Color Rust = new(0.5f, 0.31f, 0.19f);
    private static readonly Color DampSand = new(0.42f, 0.35f, 0.26f);

    public static Color DampSandColour => DampSand;

    /// <summary>A broken mud-brick wall: stacked bricks, missing courses at the top, rubble at the foot.</summary>
    private static void WallRuin(Builder b)
    {
        uint seed = 11;
        // Height profile along the wall: standing bays separated by collapsed stretches.
        float[] profile = { 2.5f, 2.2f, 1.1f, 1.9f, 0.45f, 2.3f };
        const float L = 7f, T = 0.78f;
        BrickRun(b, seed, -L / 2, L / 2, T, profile, doorwayAt: 99f, seedStep: 1);
        // Fallen bricks scattered at the foot.
        for (int i = 0; i < 7; i++)
        {
            float x = -3f + Hash(seed + 40 + (uint)i) * 6f;
            float z = T * 0.5f + 0.3f + Hash(seed + 50 + (uint)i) * 1.1f;
            float s = 0.24f + Hash(seed + 60 + (uint)i) * 0.12f;
            b.Box(new Vector3(x - s, 0, z - s * 0.7f), new Vector3(x + s, s * 0.8f, z + s * 0.7f), Jitter(MudBrickDark, seed + 70 + (uint)i), yaw: Hash(seed + 80 + (uint)i) * 0.9f);
        }
        Rubble(b, seed + 90, L * 0.85f, 1.4f);
    }

    /// <summary>A small ruined house: standing brick walls with a doorway and window, a dark
    /// interior floor, broken top courses and rubble.</summary>
    private static void HouseRuin(Builder b)
    {
        uint seed = 211;
        const float W = 7f, D = 5.8f, T = 0.6f, H = 2.6f;
        // Packed-earth floor, darker than the ground so the shell reads as an interior.
        b.Box(new Vector3(-W / 2 + 0.2f, -0.05f, -D / 2 + 0.2f), new Vector3(W / 2 - 0.2f, 0.1f, D / 2 - 0.2f), FloorEarth);
        // Back wall (solid, window hole high up), side walls partial, front with the doorway.
        float[] back = { H, H, H, H };
        BrickRun(b, seed, -W / 2, W / 2, T, back, 99f, 1, z: -D / 2, windowAt: 1.2f);
        float[] sideA = { 2.2f, 2.2f, 1.6f, 0.9f };
        BrickRun(b, seed + 7, -D / 2, D / 2, T, sideA, 99f, 1, x: -W / 2, alongZ: true);
        float[] sideB = { 1.5f, 1.8f, 1.8f, 0.6f };
        BrickRun(b, seed + 13, -D / 2, D / 2, T, sideB, 99f, 1, x: W / 2, alongZ: true);
        // Front stubs with a doorway between them.
        float[] frontA = { 1.4f, 0.9f, 0.3f, 0.15f };
        BrickRun(b, seed + 19, -W / 2, -0.7f, T, frontA, 99f, 1, z: D / 2);
        float[] frontB = { 1.1f, 1.3f, 0.5f, 0.2f };
        BrickRun(b, seed + 23, 0.9f, W / 2, T, frontB, 99f, 1, z: D / 2);
        // Taller corner pier where the standing walls meet.
        BrickPier(b, seed + 29, new Vector3(-W / 2, 0, -D / 2), 3.1f, T * 1.7f);
        Rubble(b, seed + 31, 8.5f, 3.4f);
        // A couple of fallen bricks inside.
        b.Box(new Vector3(-1.6f, 0.1f, 0.6f), new Vector3(-0.9f, 0.32f, 1.7f), Jitter(MudBrickDark, seed + 33), yaw: 0.7f);
        b.Box(new Vector3(0.8f, 0.1f, -0.9f), new Vector3(1.5f, 0.3f, -0.2f), Jitter(MudBrick, seed + 35), yaw: -0.4f);
    }

    /// <summary>A low dry-stone field wall: rough stacked stones, one taller end pillar.</summary>
    private static void FieldWall(Builder b)
    {
        uint seed = 71;
        const float L = 9.4f, T = 0.62f;
        float x = -L / 2;
        int i = 0;
        while (x < L / 2 - 0.5f)
        {
            float len = 0.9f + Hash(seed + (uint)i) * 0.7f;
            float h = 0.62f + Hash(seed + 20 + (uint)i) * 0.3f;
            b.Box(new Vector3(x, 0, -T / 2), new Vector3(x + len, h, T / 2), Jitter(Hash(seed + 40 + (uint)i) < 0.5f ? Stone : StoneDark, seed + (uint)i), yaw: (Hash(seed + 60 + (uint)i) - 0.5f) * 0.06f);
            x += len + 0.06f;
            i++;
        }
        // Pillar at one end, two rough stones stacked.
        b.Box(new Vector3(L / 2 - 0.2f, 0, -0.5f), new Vector3(L / 2 + 0.7f, 0.8f, 0.5f), Jitter(StoneDark, seed + 90));
        b.Box(new Vector3(L / 2 - 0.05f, 0.8f, -0.4f), new Vector3(L / 2 + 0.55f, 1.35f, 0.4f), Jitter(Stone, seed + 91), yaw: 0.15f);
        Rubble(b, seed + 95, L * 0.7f, 1.1f);
    }

    /// <summary>A stone well: ring of blocks, two posts, a crossbar and a little pitched roof.</summary>
    private static void Well(Builder b)
    {
        uint seed = 301;
        // Ring: two slightly offset stone courses with a dark hollow inside.
        b.Cylinder(new Vector3(0, 0, 0), 1.05f, 0.45f, Jitter(StoneDark, seed), sides: 12);
        b.Cylinder(new Vector3(0, 0.45f, 0), 1.0f, 0.4f, Jitter(Stone, seed + 1), sides: 12);
        b.Cylinder(new Vector3(0, 0.12f, 0), 0.78f, 0.6f, new Color(0.08f, 0.09f, 0.1f), sides: 10); // the shaft
        // Posts and crossbar.
        b.Box(new Vector3(-1.15f, 0.75f, -0.09f), new Vector3(-0.95f, 2.5f, 0.09f), Jitter(Wood, seed + 2));
        b.Box(new Vector3(0.95f, 0.75f, -0.09f), new Vector3(1.15f, 2.5f, 0.09f), Jitter(Wood, seed + 3));
        b.Box(new Vector3(-1.2f, 2.15f, -0.07f), new Vector3(1.2f, 2.32f, 0.07f), Jitter(Wood, seed + 4));
        // Small pitched roof of two slabs.
        b.Box(new Vector3(-1.35f, 2.72f, -0.85f), new Vector3(1.35f, 2.86f, 0.05f), Jitter(MudBrickDark, seed + 5), pitch: 0.42f);
        b.Box(new Vector3(-1.35f, 2.72f, -0.05f), new Vector3(1.35f, 2.86f, 0.85f), Jitter(MudBrickDark, seed + 6), pitch: -0.42f);
        Rubble(b, seed + 8, 3.4f, 0.9f);
    }

    /// <summary>Abandoned fuel drums with rim bands, one tipped over, beside a crate.</summary>
    private static void Barrels(Builder b)
    {
        uint seed = 101;
        b.Cylinder(new Vector3(-1.1f, 0, -0.4f), 0.55f, 1.5f, Jitter(Rust, seed), sides: 12);
        b.Cylinder(new Vector3(-1.1f, 0.3f, -0.4f), 0.57f, 0.09f, Jitter(Rust * 0.75f, seed + 1), sides: 12);
        b.Cylinder(new Vector3(-1.1f, 1.1f, -0.4f), 0.57f, 0.09f, Jitter(Rust * 0.75f, seed + 2), sides: 12);
        b.Cylinder(new Vector3(-0.05f, 0, 0.55f), 0.55f, 1.5f, Jitter(Rust * 1.08f, seed + 3), sides: 12);
        b.Cylinder(new Vector3(-0.05f, 0.3f, 0.55f), 0.57f, 0.09f, Jitter(Rust * 0.8f, seed + 4), sides: 12);
        // Tipped drum lying on its side.
        b.Cylinder(new Vector3(1.15f, 0.55f, -0.75f), 0.55f, 1.4f, Jitter(Rust * 0.9f, seed + 5), sides: 12, axis: Vector3.Right, roll: 0.5f);
        b.Cylinder(new Vector3(1.5f, 0.55f, -0.75f), 0.57f, 0.09f, Jitter(Rust * 0.7f, seed + 6), sides: 12, axis: Vector3.Right, roll: 0.5f);
        Crate(b, seed + 7, new Vector3(0.7f, 0, 0.95f), 1.05f);
        Rubble(b, seed + 9, 3.6f, 0.7f);
    }

    /// <summary>A small stack of supply crates, one broken open on the ground.</summary>
    private static void Crates(Builder b)
    {
        uint seed = 121;
        Crate(b, seed, new Vector3(-1.2f, 0, -0.9f), 1.05f);
        Crate(b, seed + 1, new Vector3(0.05f, 0, -0.8f), 1.05f);
        Crate(b, seed + 2, new Vector3(-0.6f, 1.05f, -0.75f), 1.05f, yaw: 0.12f);
        // Fallen side panel and a few planks.
        b.Box(new Vector3(-0.5f, 0.02f, 0.7f), new Vector3(0.85f, 0.14f, 1.85f), Jitter(Wood * 0.85f, seed + 3), yaw: 0.35f);
        b.Box(new Vector3(1.0f, 0.02f, 0.6f), new Vector3(1.9f, 0.1f, 0.78f), Jitter(Wood, seed + 4), yaw: -0.5f);
        Rubble(b, seed + 6, 3.2f, 0.8f);
    }

    private static void Crate(Builder b, uint seed, Vector3 at, float s, float yaw = 0f)
    {
        b.Box(at, at + new Vector3(s, s, s), Jitter(Wood, seed), yaw: yaw);
        // Corner posts and a mid band so the crate reads as planking.
        b.Box(at + new Vector3(0, s * 0.42f, 0), at + new Vector3(s, s * 0.58f, s), Jitter(Wood * 0.8f, seed + 1), yaw: yaw);
    }

    /// <summary>A date palm: curved segmented trunk, a crown of bent fronds, dead skirt, coconuts.</summary>
    private static void Palm(Builder b)
    {
        uint seed = 401;
        Vector3 lean = new Vector3(0.09f, 0, 0.05f);
        Vector3 c = new Vector3(0, 0, 0);
        float r = 0.24f;
        for (int i = 0; i < 6; i++)
        {
            b.Cylinder(c, r, 0.58f, Jitter(Trunk * (i % 2 == 0 ? 0.88f : 1f), seed + (uint)i), sides: 7);
            c += new Vector3(lean.X * (0.4f + i * 0.22f), 0.56f, lean.Z * (0.4f + i * 0.22f));
            r *= 0.88f;
        }
        Vector3 crown = c + new Vector3(0, 0.15f, 0);
        // Coconuts at the crown base.
        for (int i = 0; i < 3; i++)
        {
            float a = i / 3f * Mathf.Tau;
            b.Box(crown + new Vector3(Mathf.Cos(a) * 0.28f, -0.12f, Mathf.Sin(a) * 0.28f) - new Vector3(0.11f, 0.11f, 0.11f),
                  crown + new Vector3(Mathf.Cos(a) * 0.28f, -0.12f, Mathf.Sin(a) * 0.28f) + new Vector3(0.11f, 0.11f, 0.11f),
                  Jitter(new Color(0.4f, 0.33f, 0.2f), seed + 20 + (uint)i));
        }
        // Fronds: bent strips radiating outward and drooping.
        int fronds = 9;
        for (int i = 0; i < fronds; i++)
        {
            float a = i / (float)fronds * Mathf.Tau + Hash(seed + 30 + (uint)i) * 0.4f;
            float droop = 0.55f + Hash(seed + 40 + (uint)i) * 0.5f;
            Color col = Jitter(Frond, seed + 50 + (uint)i);
            b.Frond(crown, a, 2.3f + Hash(seed + 60 + (uint)i) * 0.7f, droop, col, FrondTip);
        }
        // Dead skirt of brown fronds hanging below the crown.
        for (int i = 0; i < 4; i++)
        {
            float a = i / 4f * Mathf.Tau + 0.4f;
            b.Frond(crown + new Vector3(0, -0.18f, 0), a, 1.5f, 1.5f, Jitter(FrondDead, seed + 70 + (uint)i), FrondDead * 0.8f);
        }
    }

    /// <summary>One brick wall run: courses of individual bricks. <paramref name="profile"/>
    /// gives the broken top height at stations along the run; bricks above the local height
    /// are missing. A doorway or window leaves a gap below the given offset.</summary>
    private static void BrickRun(Builder b, uint seed, float from, float to, float t, float[] profile, float doorwayAt, uint seedStep,
        float x = 0, float z = 0, bool alongZ = false, float windowAt = 99f)
    {
        const float courseH = 0.3f, brickL = 0.82f, gap = 0.035f;
        float len = to - from;
        int courses = 12;
        for (int c = 0; c < courses; c++)
        {
            float y = c * courseH;
            float offset = (c % 2) * (brickL + gap) * 0.5f;
            for (float u = from - offset; u < to; u += brickL + gap)
            {
                float u0 = Mathf.Max(u, from), u1 = Mathf.Min(u + brickL, to);
                if (u1 - u0 < 0.2f) continue;
                // Broken top: local height from the profile, stations across the run.
                float f = Mathf.Clamp(((u0 + u1) * 0.5f - from) / len, 0, 1);
                int station = Math.Min((int)(f * profile.Length), profile.Length - 1);
                float frac = f * profile.Length - station;
                float h = Mathf.Lerp(profile[station], profile[Math.Min(station + 1, profile.Length - 1)], frac);
                if (y + courseH > h + 0.01f) continue;
                // Doorway / window gaps.
                float mid = (u0 + u1) * 0.5f - from;
                if (Mathf.Abs(mid - doorwayAt) < 0.55f && c < 6) continue;
                if (Mathf.Abs(mid - windowAt) < 0.6f && c is >= 5 and <= 7) continue;
                uint s = seed + (uint)(c * 37) + (uint)((u0 - from) * 7);
                if (Hash(s + 5) < 0.045f && c > 2) continue; // an odd brick fallen out
                Color col = Jitter(MudBrick, s);
                if (c == 0) col = Jitter(MudBrickDark, s); // damp foundation course
                else if (c % 3 == 2) col = col.Lerp(Mortar, 0.25f);
                if (alongZ) b.Box(new Vector3(x - t / 2, y, u0), new Vector3(x + t / 2, y + courseH - 0.02f, u1), col);
                else b.Box(new Vector3(u0, y, z - t / 2), new Vector3(u1, y + courseH - 0.02f, z + t / 2), col);
            }
        }
    }

    private static void BrickPier(Builder b, uint seed, Vector3 at, float h, float w)
    {
        const float courseH = 0.3f;
        for (float y = 0; y < h; y += courseH)
            b.Box(at + new Vector3(-w / 2, y, -w / 2), at + new Vector3(w / 2, y + courseH - 0.02f, w / 2), Jitter(MudBrick, seed + (uint)(y * 10)));
    }

    /// <summary>A scatter of small stones and broken brick at the foot of a piece.</summary>
    private static void Rubble(Builder b, uint seed, float span, float depth)
    {
        for (int i = 0; i < 10; i++)
        {
            float u = (Hash(seed + (uint)i) - 0.5f) * span;
            float v = (Hash(seed + 20 + (uint)i) - 0.5f) * depth;
            float s = 0.1f + Hash(seed + 40 + (uint)i) * 0.22f;
            Color col = Hash(seed + 60 + (uint)i) < 0.6f ? Jitter(MudBrickDark, seed + (uint)i) : Jitter(StoneDark, seed + (uint)i);
            b.Box(new Vector3(u - s, 0, v - s * 0.8f), new Vector3(u + s, s * (0.5f + Hash(seed + 80 + (uint)i)), v + s * 0.8f), col, yaw: Hash(seed + 100 + (uint)i) * Mathf.Tau);
        }
    }

    /// <summary>A few sparse reed stems and seed heads for an oasis bank.</summary>
    private static void Reeds(Builder b)
    {
        Color stem = new(0.32f, 0.4f, 0.2f);
        Color head = new(0.4f, 0.3f, 0.17f);
        for (int i = 0; i < 9; i++)
        {
            float a = i * 2.39996f;
            float r = 0.15f + (i % 3) * 0.16f;
            float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
            float h = 0.85f + (i % 4) * 0.18f;
            b.Box(new Vector3(x - 0.025f, 0, z - 0.025f), new Vector3(x + 0.025f, h, z + 0.025f), Jitter(stem, (uint)(700 + i)));
            if (i % 2 == 0)
                b.Box(new Vector3(x - 0.055f, h - 0.08f, z - 0.055f), new Vector3(x + 0.055f, h + 0.18f, z + 0.055f), head);
        }
    }

    /// <summary>A disc draped over the terrain in a grid (so bumps never clip through it),
    /// coloured in radial rings: still water at the centre, damp sand at the rim.</summary>
    public static ArrayMesh DrapedDisc(float radius, float lift, (float r, Color c)[] rings, System.Func<float, float, float> heightAt)
    {
        const int n = 6;
        var grid = new System.Collections.Generic.List<Vector3>();
        int Row(int i) => i * (2 * n + 1);
        Color At(float d)
        {
            for (int k = 0; k < rings.Length; k++)
                if (d <= rings[k].r) return rings[k].c;
            return rings[^1].c;
        }
        var b = new Builder();
        // The grid in world space, then one flat-lit quad per cell (normals up, colours by ring).
        for (int gz = -n; gz <= n; gz++)
        for (int gx = -n; gx <= n; gx++)
        {
            float x = gx / (float)n * radius, z = gz / (float)n * radius;
            float d = Mathf.Sqrt(x * x + z * z);
            float edge = radius * (0.86f + 0.16f * Mathf.Sin(Mathf.Atan2(z, x) * 5f + radius));
            grid.Add(d <= edge * 0.94f ? new Vector3(x, heightAt(x, z) + lift, z) : new Vector3(x, -1000f, z));
        }
        for (int gz = -n; gz < n; gz++)
        for (int gx = -n; gx < n; gx++)
        {
            int o = Row(gz + n) + gx + n;
            Vector3 a = grid[o], c = grid[o + 1], d2 = grid[o + 2 * n + 2], e = grid[o + 2 * n + 1];
            if (a.Y < -100f || c.Y < -100f || d2.Y < -100f || e.Y < -100f) continue;
            b.FaceA(a, e, d2, c, At(Mathf.Sqrt((a.X + e.X) * (a.X + e.X) * 0.25f + (a.Z + e.Z) * (a.Z + e.Z) * 0.25f)));
        }
        var mesh = new ArrayMesh();
        using Variant v = Variant.CreateFrom(b.Vertices.ToArray()), nv = Variant.CreateFrom(b.Normals.ToArray()),
            cc = Variant.CreateFrom(b.Colours.ToArray()), ii = Variant.CreateFrom(b.Indices.ToArray());
        using var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = v;
        arrays[(int)Mesh.ArrayType.Normal] = nv;
        arrays[(int)Mesh.ArrayType.Color] = cc;
        arrays[(int)Mesh.ArrayType.Index] = ii;
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private static Color Jitter(Color c, uint seed) => c * (0.88f + Hash(seed) * 0.22f);

    private static float Hash(uint seed)
    {
        unchecked
        {
            uint h = seed * 2654435761u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            return (h & 0xFFFF) / 65535f;
        }
    }

    /// <summary>Accumulates boxes, cylinders, fronds and caps with flat-shaded per-face colours.</summary>
    private sealed class Builder
    {
        public readonly List<Vector3> Vertices = new();
        public readonly List<Vector3> Normals = new();
        public readonly List<Color> Colours = new();
        public readonly List<int> Indices = new();

        public void FaceA(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col) => Face(a, b, c, d, col);

        private void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            // Godot front faces wind clockwise (normal toward the viewer), so the geometric
            // normal is the reversed right-hand cross.
            Vector3 n = (c - a).Cross(b - a).Normalized();
            int i = Vertices.Count;
            Vertices.Add(a); Vertices.Add(b); Vertices.Add(c); Vertices.Add(d);
            for (int k = 0; k < 4; k++) { Normals.Add(n); Colours.Add(col); }
            Indices.Add(i); Indices.Add(i + 1); Indices.Add(i + 2);
            Indices.Add(i); Indices.Add(i + 2); Indices.Add(i + 3);
        }

        /// <summary>An axis box. <paramref name="yaw"/> rotates it about the Y axis (at its centre);
        /// <paramref name="pitch"/> tilts it about the Z axis (for roof slabs).</summary>
        public void Box(Vector3 min, Vector3 max, Color col, float yaw = 0f, float pitch = 0f)
        {
            Vector3 c000 = new(min.X, min.Y, min.Z), c100 = new(max.X, min.Y, min.Z);
            Vector3 c110 = new(max.X, min.Y, max.Z), c010 = new(min.X, min.Y, max.Z);
            Vector3 c001 = new(min.X, max.Y, min.Z), c101 = new(max.X, max.Y, min.Z);
            Vector3 c111 = new(max.X, max.Y, max.Z), c011 = new(min.X, max.Y, max.Z);
            if (yaw != 0f || pitch != 0f)
            {
                var rot = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, pitch);
                Vector3 mid = (min + max) / 2f;
                Vector3 T(Vector3 v) => mid + rot * (v - mid);
                c000 = T(c000); c100 = T(c100); c110 = T(c110); c010 = T(c010);
                c001 = T(c001); c101 = T(c101); c111 = T(c111); c011 = T(c011);
            }
            Face(c001, c101, c111, c011, col); // top
            Face(c000, c010, c110, c100, col * 0.8f); // bottom
            Face(c000, c100, c101, c001, col * 0.92f); // south
            Face(c100, c110, c111, c101, col * 0.86f); // east
            Face(c110, c010, c011, c111, col * 0.95f); // north
            Face(c010, c000, c001, c011, col * 0.88f); // west
        }

        /// <summary>A capped n-gonal drum. Upright by default; <paramref name="axis"/> lays it on its side.</summary>
        public void Cylinder(Vector3 baseCentre, float radius, float height, Color col, int sides = 10, Vector3? axis = null, float roll = 0f)
        {
            var basis = axis.HasValue
                ? new Basis(axis.Value.Normalized(), roll) * new Basis(Vector3.Right, Mathf.Pi / 2f)
                : Basis.Identity;
            Vector3 up = basis.Y;
            Vector3 Centre(float h) => baseCentre + up * h;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.Tau, a1 = (i + 1) / (float)sides * Mathf.Tau;
                Vector3 r0 = basis.X * Mathf.Cos(a0) * radius + basis.Z * Mathf.Sin(a0) * radius;
                Vector3 r1 = basis.X * Mathf.Cos(a1) * radius + basis.Z * Mathf.Sin(a1) * radius;
                int i0 = Vertices.Count;
                Vertices.Add(Centre(0) + r0); Vertices.Add(Centre(0) + r1);
                Vertices.Add(Centre(height) + r1); Vertices.Add(Centre(height) + r0);
                Vector3 side = (r0 + r1).Normalized(); // radially outward, in the ring plane
                for (int k = 0; k < 4; k++) { Normals.Add(side); Colours.Add(col * 0.94f); }
                Indices.Add(i0); Indices.Add(i0 + 1); Indices.Add(i0 + 2);
                Indices.Add(i0); Indices.Add(i0 + 2); Indices.Add(i0 + 3);
            }
            foreach (bool top in new[] { false, true })
            {
                Vector3 c = Centre(top ? height : 0f);
                Vector3 n = top ? up : -up;
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i / (float)sides * Mathf.Tau, a1 = (i + 1) / (float)sides * Mathf.Tau;
                    Vector3 r0 = basis.X * Mathf.Cos(a0) * radius * 0.98f + basis.Z * Mathf.Sin(a0) * radius * 0.98f;
                    Vector3 r1 = basis.X * Mathf.Cos(a1) * radius * 0.98f + basis.Z * Mathf.Sin(a1) * radius * 0.98f;
                    int i0 = Vertices.Count;
                    Vertices.Add(c); Vertices.Add(c + r0); Vertices.Add(c + r1);
                    for (int k = 0; k < 3; k++) { Normals.Add(n); Colours.Add(col); }
                    Indices.Add(i0); Indices.Add(i0 + 1); Indices.Add(i0 + 2);
                }
            }
        }

        /// <summary>A palm frond: three quads in a strip that rises, reaches out, then droops.</summary>
        public void Frond(Vector3 crown, float angle, float length, float droop, Color baseCol, Color tipCol)
        {
            var dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var side = new Vector3(-dir.Z, 0, dir.X) * 0.16f;
            // Three control points along the frond's arc.
            Vector3 p0 = crown;
            Vector3 p1 = crown + dir * length * 0.4f + Vector3.Up * length * 0.22f;
            Vector3 p2 = crown + dir * length * 0.85f + Vector3.Up * length * (0.22f - droop * 0.28f);
            Vector3 p3 = crown + dir * length + Vector3.Up * length * (0.1f - droop * 0.5f);
            Vector3[] pts = { p0, p1, p2, p3 };
            for (int s = 0; s < 3; s++)
            {
                Color ca = baseCol.Lerp(tipCol, s / 3f), cb = baseCol.Lerp(tipCol, (s + 1) / 3f);
                Face(pts[s] - side, pts[s] + side, pts[s + 1] + side, pts[s + 1] - side,
                    (ca + cb) * 0.5f);
                // Colour the two ends of the segment by their own tones.
                int i = Vertices.Count - 4;
                Colours[i] = ca; Colours[i + 3] = ca;
                Colours[i + 1] = cb; Colours[i + 2] = cb;
            }
        }
    }
}
