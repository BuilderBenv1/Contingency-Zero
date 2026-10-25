using System.Collections.Generic;
using Godot;

namespace ContingencyZero.Game.View.Meshes;

/// <summary>
/// Desert battlefield clutter built in code (no external assets): broken mud-brick walls, a
/// ruined house, low dry-stone field walls, and abandoned supply dumps. Low-poly boxes and
/// cylinders with per-face vertex colours, the same convention as the hand-built feature models
/// (authored in world units, alpha 1 = plain). Deterministic per-vertex colour jitter so every
/// machine builds the identical mesh.
/// </summary>
internal static class ClutterModels
{
    private static readonly Dictionary<string, ArrayMesh> Cache = new();

    public static ArrayMesh? Get(string name)
    {
        if (Cache.TryGetValue(name, out var hit)) return hit;
        var b = new Builder();
        switch (name)
        {
            case "clutter_wall_ruin": WallRuin(b); break;
            case "clutter_house_ruin": HouseRuin(b); break;
            case "clutter_field_wall": FieldWall(b); break;
            case "clutter_barrels": Barrels(b); break;
            case "clutter_crates": Crates(b); break;
            default: return null;
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

    // Desert materials: sun-dried mud brick, dry stone, old wood, rusted drums.
    private static readonly Color MudBrick = new(0.63f, 0.51f, 0.37f);
    private static readonly Color MudBrickDark = new(0.5f, 0.4f, 0.29f);
    private static readonly Color Stone = new(0.52f, 0.47f, 0.4f);
    private static readonly Color Wood = new(0.45f, 0.35f, 0.24f);
    private static readonly Color Rust = new(0.48f, 0.3f, 0.18f);
    private static readonly Color Dust = new(0.66f, 0.57f, 0.44f);

    /// <summary>A broken mud-brick wall: stepped bays of varying height, one fallen chunk.</summary>
    private static void WallRuin(Builder b)
    {
        float[] bays = { 2.3f, 1.2f, 2.0f, 0f, 1.5f, 2.4f };
        float x = -3.5f;
        uint seed = 11;
        foreach (float h in bays)
        {
            if (h > 0.1f)
                b.Box(new Vector3(x, 0, -0.4f), new Vector3(x + 1.15f, h, 0.4f), Jitter(MudBrick, seed++));
            else
                b.Box(new Vector3(x + 0.1f, 0, -0.35f), new Vector3(x + 1.0f, 0.35f, 0.35f), Jitter(MudBrickDark, seed++));
            x += 1.17f;
        }
        // A fallen slab leaning against the foot of the wall.
        b.Box(new Vector3(-1.6f, 0, 0.7f), new Vector3(-0.2f, 0.28f, 2.4f), Jitter(MudBrickDark, seed));
    }

    /// <summary>A small ruined house: one standing wall with a window, two partial walls, corner pillar.</summary>
    private static void HouseRuin(Builder b)
    {
        uint seed = 31;
        b.Box(new Vector3(-3.2f, -0.06f, -2.6f), new Vector3(3.4f, 0.12f, 2.8f), Jitter(Dust, seed++)); // floor slab
        // Back wall: two parts with a window gap between them.
        b.Box(new Vector3(-3.4f, 0, -3.0f), new Vector3(0.4f, 2.7f, -2.3f), Jitter(MudBrick, seed++));
        b.Box(new Vector3(1.6f, 0, -3.0f), new Vector3(3.4f, 2.7f, -2.3f), Jitter(MudBrick, seed++));
        b.Box(new Vector3(0.4f, 1.8f, -3.0f), new Vector3(1.6f, 2.7f, -2.3f), Jitter(MudBrickDark, seed++)); // lintel
        // Side walls, one taller than the other, both broken.
        b.Box(new Vector3(-3.4f, 0, -2.3f), new Vector3(-2.7f, 2.1f, 0.6f), Jitter(MudBrick, seed++));
        b.Box(new Vector3(2.7f, 0, -2.3f), new Vector3(3.4f, 1.3f, 1.8f), Jitter(MudBrickDark, seed++));
        // Tall corner pillar where the two standing walls meet.
        b.Box(new Vector3(-3.4f, 0, -3.0f), new Vector3(-2.6f, 3.3f, -2.2f), Jitter(MudBrick, seed++));
        // Front stubs flanking the doorway.
        b.Box(new Vector3(-3.3f, 0, 2.1f), new Vector3(-1.9f, 1.2f, 2.8f), Jitter(MudBrickDark, seed++));
        b.Box(new Vector3(2.0f, 0, 2.1f), new Vector3(3.3f, 0.9f, 2.8f), Jitter(MudBrick, seed++));
    }

    /// <summary>A low dry-stone field wall with a pillar at one end.</summary>
    private static void FieldWall(Builder b)
    {
        uint seed = 71;
        float x = -4.5f;
        for (int i = 0; i < 6; i++, x += 1.55f)
        {
            float h = 0.72f + Hash(seed + (uint)i) * 0.3f;
            b.Box(new Vector3(x, 0, -0.38f), new Vector3(x + 1.5f, h, 0.38f), Jitter(Stone, seed + (uint)i));
        }
        b.Box(new Vector3(4.6f, 0, -0.55f), new Vector3(5.4f, 1.15f, 0.55f), Jitter(Stone, seed + 9));
    }

    /// <summary>Abandoned fuel drums, one tipped over, beside a crate.</summary>
    private static void Barrels(Builder b)
    {
        b.Cylinder(new Vector3(-1.1f, 0, -0.4f), 0.55f, 1.5f, Jitter(Rust, 101));
        b.Cylinder(new Vector3(-0.1f, 0, 0.5f), 0.55f, 1.5f, Jitter(Rust, 102));
        // Tipped drum lying on its side.
        b.Cylinder(new Vector3(1.1f, 0.55f, -0.7f), 0.55f, 1.4f, Jitter(Rust, 103), axis: Vector3.Right, roll: 0.5f);
        b.Box(new Vector3(0.7f, 0, 0.9f), new Vector3(1.8f, 1.1f, 1.9f), Jitter(Wood, 104));
    }

    /// <summary>A small stack of supply crates, one broken open on the ground.</summary>
    private static void Crates(Builder b)
    {
        b.Box(new Vector3(-1.2f, 0, -0.9f), new Vector3(-0.1f, 1.05f, 0.2f), Jitter(Wood, 121));
        b.Box(new Vector3(0.1f, 0, -0.8f), new Vector3(1.2f, 1.05f, 0.3f), Jitter(Wood, 122));
        b.Box(new Vector3(-0.6f, 1.05f, -0.6f), new Vector3(0.5f, 2.1f, 0.5f), Jitter(Wood, 123));
        // Fallen side panel.
        b.Box(new Vector3(-0.4f, 0, 0.8f), new Vector3(0.9f, 0.12f, 1.9f), Jitter(Wood, 124));
    }

    private static Color Jitter(Color c, uint seed) => c * (0.9f + Hash(seed) * 0.18f);

    private static float Hash(uint seed)
    {
        unchecked
        {
            uint h = seed * 2654435761u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            return (h & 0xFFFF) / 65535f;
        }
    }

    /// <summary>Accumulates boxes/cylinders with flat-shaded per-face colours into one mesh.</summary>
    private sealed class Builder
    {
        public readonly List<Vector3> Vertices = new();
        public readonly List<Vector3> Normals = new();
        public readonly List<Color> Colours = new();
        public readonly List<int> Indices = new();

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

        public void Box(Vector3 min, Vector3 max, Color col)
        {
            Vector3 c000 = new(min.X, min.Y, min.Z), c100 = new(max.X, min.Y, min.Z);
            Vector3 c110 = new(max.X, min.Y, max.Z), c010 = new(min.X, min.Y, max.Z);
            Vector3 c001 = new(min.X, max.Y, min.Z), c101 = new(max.X, max.Y, min.Z);
            Vector3 c111 = new(max.X, max.Y, max.Z), c011 = new(min.X, max.Y, max.Z);
            Face(c001, c101, c111, c011, col); // top
            Face(c000, c010, c110, c100, col * 0.82f); // bottom
            Face(c000, c100, c101, c001, col * 0.9f); // south
            Face(c100, c110, c111, c101, col * 0.86f); // east
            Face(c110, c010, c011, c111, col * 0.93f); // north
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
                for (int k = 0; k < 4; k++) { Normals.Add(side); Colours.Add(col * 0.92f); }
                Indices.Add(i0); Indices.Add(i0 + 1); Indices.Add(i0 + 2);
                Indices.Add(i0); Indices.Add(i0 + 2); Indices.Add(i0 + 3);
            }
            // Caps: fan of quads around the centre.
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
    }
}
