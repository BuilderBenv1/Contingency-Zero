using System;
using System.Collections.Generic;
using Godot;

namespace ContingencyZero.Game.View.Meshes;

/// <summary>
/// A surface colour plus the material code the unit shader reads from vertex alpha:
/// 1 = plain surface, 0 = team colour (RGB brightness modulates the player colour), 0.5 = glow.
/// </summary>
internal readonly record struct Paint(Color Rgb, float Alpha)
{
    public static Paint Plain(float r, float g, float b) => new(new Color(r, g, b), 1f);

    /// <summary>A team-colour area; brightness ~0.85 for main panels, darker for shadowed bands.</summary>
    public static Paint Team(float brightness = 0.85f) => new(new Color(brightness, brightness, brightness), 0f);

    public static Paint Glow(float r, float g, float b) => new(new Color(r, g, b), 0.5f);

    /// <summary>Same material, RGB scaled (for darker/lighter variants).</summary>
    public Paint Shade(float k) => this with { Rgb = new Color(Rgb.R * k, Rgb.G * k, Rgb.B * k) };

    public Color Vertex => new(Rgb.R, Rgb.G, Rgb.B, Alpha);
}

/// <summary>
/// Low-poly mesh builder: a small kit of solid primitives (box, frustum, prism/cone, sphere,
/// dome, loft, extrusions, rods and beams) placed through a transform stack and baked into one
/// flat-shaded <see cref="ArrayMesh"/> surface with per-face normals and vertex colours.
///
/// Every face is oriented against a point known to lie inside its primitive, after the current
/// transform is applied, so primitives can be mirrored or scaled freely and still come out
/// facing outward. Faces are emitted in Godot's clockwise front-face order.
/// </summary>
internal sealed class MeshKit
{
    public static readonly float[] Sides = { -1f, 1f };

    private readonly List<Vector3> _verts = new(1024);
    private readonly List<Vector3> _normals = new(1024);
    private readonly List<Color> _colors = new(1024);
    private readonly Stack<Transform3D> _stack = new();
    private Transform3D _xf = Transform3D.Identity;
    private Vector3 _min = new(float.MaxValue, float.MaxValue, float.MaxValue);
    private Vector3 _max = new(float.MinValue, float.MinValue, float.MinValue);

    public MeshKit() { }

    /// <summary>Starts with a root transform (e.g. a uniform scale so a model can be authored at radius 1).</summary>
    public MeshKit(Transform3D root) => _xf = root;

    public int Triangles => _verts.Count / 3;
    public bool IsEmpty => _verts.Count == 0;
    public Vector3 Min => IsEmpty ? Vector3.Zero : _min;
    public Vector3 Max => IsEmpty ? Vector3.Zero : _max;

    /// <summary>Largest distance of any vertex from <paramref name="pivot"/> (used for rotor sweep heights).</summary>
    public float MaxDistanceFrom(Vector3 pivot)
    {
        float best = 0;
        foreach (Vector3 v in _verts) best = Math.Max(best, v.DistanceTo(pivot));
        return best;
    }

    // ---- Transform stack ---------------------------------------------------------------------

    /// <summary>Applies <paramref name="local"/> on top of the current transform until the scope is disposed.</summary>
    public Scope Push(Transform3D local)
    {
        _stack.Push(_xf);
        _xf = _xf * local;
        return new Scope(this);
    }

    public Scope Push(Vector3 offset) => Push(new Transform3D(Basis.Identity, offset));

    public Scope Push(Vector3 offset, Basis basis) => Push(new Transform3D(basis, offset));

    /// <summary>Translate, then rotate (radians, YXZ Euler) and optionally scale.</summary>
    public Scope Push(Vector3 offset, Vector3 euler, float scale = 1f) =>
        Push(new Transform3D(Basis.FromEuler(euler).Scaled(new Vector3(scale, scale, scale)), offset));

    public readonly struct Scope : IDisposable
    {
        private readonly MeshKit _kit;
        public Scope(MeshKit kit) => _kit = kit;
        public void Dispose() => _kit._xf = _kit._stack.Pop();
    }

    // ---- Faces -------------------------------------------------------------------------------

    /// <summary>One triangle, oriented to face away from <paramref name="inside"/> (all in local space).</summary>
    public void Tri(Vector3 a, Vector3 b, Vector3 c, Paint p, Vector3 inside)
    {
        a = _xf * a;
        b = _xf * b;
        c = _xf * c;
        inside = _xf * inside;
        Vector3 n = (b - a).Cross(c - a);
        float len = n.Length();
        if (len < 1e-7f) return; // degenerate (collapsed edge of a wedge or cone)
        n /= len;
        if (n.Dot((a + b + c) / 3f - inside) < 0)
        {
            (b, c) = (c, b);
            n = -n;
        }
        // Outward normal by the right-hand rule is (a, b, c); Godot's front faces are clockwise.
        Emit(a, n, p);
        Emit(c, n, p);
        Emit(b, n, p);
    }

    /// <summary>A planar quad a-b-c-d (in order around its edge).</summary>
    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Paint p, Vector3 inside)
    {
        Tri(a, b, c, p, inside);
        Tri(a, c, d, p, inside);
    }

    /// <summary>A convex polygon (vertices in order around its edge), fanned from its first vertex.</summary>
    public void Poly(IReadOnlyList<Vector3> ring, Paint p, Vector3 inside)
    {
        for (int i = 1; i + 1 < ring.Count; i++) Tri(ring[0], ring[i], ring[i + 1], p, inside);
    }

    private void Emit(Vector3 v, Vector3 n, Paint p)
    {
        _verts.Add(v);
        _normals.Add(n);
        _colors.Add(p.Vertex);
        _min = _min.Min(v);
        _max = _max.Max(v);
    }

    // ---- Solids ------------------------------------------------------------------------------

    /// <summary>Axis-aligned box around <paramref name="center"/>. <paramref name="top"/> paints the +Y face.</summary>
    public void Box(Vector3 center, Vector3 size, Paint p, Paint? top = null)
    {
        Vector3 h = size / 2f;
        Vector3 C(float x, float y, float z) => center + new Vector3(h.X * x, h.Y * y, h.Z * z);
        Paint t = top ?? p;
        Quad(C(-1, 1, -1), C(1, 1, -1), C(1, 1, 1), C(-1, 1, 1), t, center);       // top
        Quad(C(-1, -1, -1), C(1, -1, -1), C(1, -1, 1), C(-1, -1, 1), p, center);   // bottom
        Quad(C(-1, -1, 1), C(1, -1, 1), C(1, 1, 1), C(-1, 1, 1), p, center);       // +Z
        Quad(C(-1, -1, -1), C(1, -1, -1), C(1, 1, -1), C(-1, 1, -1), p, center);   // -Z
        Quad(C(1, -1, -1), C(1, -1, 1), C(1, 1, 1), C(1, 1, -1), p, center);       // +X
        Quad(C(-1, -1, -1), C(-1, -1, 1), C(-1, 1, 1), C(-1, 1, -1), p, center);   // -X
    }

    /// <summary>
    /// Tapered box standing on <paramref name="baseCenter"/>: a <paramref name="bottom"/> (x, z)
    /// rectangle rising <paramref name="height"/> to a <paramref name="top"/> rectangle shifted by
    /// <paramref name="shift"/> (x, z). A zero top dimension makes a wedge; zero both makes a pyramid.
    /// </summary>
    public void Frustum(Vector3 baseCenter, Vector2 bottom, Vector2 top, float height, Paint p,
        Paint? topPaint = null, Vector2 shift = default)
    {
        var b = new Vector3[4];
        var t = new Vector3[4];
        float[] sx = { -1, 1, 1, -1 };
        float[] sz = { -1, -1, 1, 1 };
        for (int i = 0; i < 4; i++)
        {
            b[i] = baseCenter + new Vector3(sx[i] * bottom.X / 2, 0, sz[i] * bottom.Y / 2);
            t[i] = baseCenter + new Vector3(shift.X + sx[i] * top.X / 2, height, shift.Y + sz[i] * top.Y / 2);
        }
        Vector3 inside = Centroid(b, t);
        Quad(t[0], t[1], t[2], t[3], topPaint ?? p, inside);
        Quad(b[0], b[1], b[2], b[3], p, inside);
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            Quad(b[i], b[j], t[j], t[i], p, inside);
        }
    }

    /// <summary>
    /// N-sided prism / truncated cone along +Y from <paramref name="baseCenter"/>. A zero top
    /// radius makes a cone. The default phase puts a flat side facing -Z.
    /// </summary>
    public void Cylinder(Vector3 baseCenter, float rBottom, float rTop, float height, int sides, Paint p,
        Paint? cap = null, float phase = float.NaN, bool bottomCap = true)
    {
        if (float.IsNaN(phase)) phase = Mathf.Pi / sides + Mathf.Pi / 2;
        var b = new Vector3[sides];
        var t = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = phase + i * Mathf.Tau / sides;
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            b[i] = baseCenter + dir * rBottom;
            t[i] = baseCenter + dir * rTop + new Vector3(0, height, 0);
        }
        Vector3 inside = baseCenter + new Vector3(0, height * (rTop > 0 ? 0.5f : 0.25f), 0);
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            Quad(b[i], b[j], t[j], t[i], p, inside);
        }
        if (rTop > 0) Poly(t, cap ?? p, inside);
        if (rBottom > 0 && bottomCap) Poly(b, p, inside);
    }

    /// <summary>Cylinder (or cone, if <paramref name="rb"/> is 0) running from point a to point b.</summary>
    public void Rod(Vector3 a, Vector3 b, float ra, float rb, int sides, Paint p, Paint? cap = null)
    {
        Vector3 d = b - a;
        float len = d.Length();
        if (len < 1e-5f) return;
        using (Push(new Transform3D(BasisAlong(d / len), a)))
            Cylinder(Vector3.Zero, ra, rb, len, sides, p, cap);
    }

    /// <summary>Box running from point a to point b, <paramref name="width"/> across and <paramref name="depth"/> thick.</summary>
    public void Beam(Vector3 a, Vector3 b, float width, float depth, Paint p, Paint? end = null)
    {
        Vector3 d = b - a;
        float len = d.Length();
        if (len < 1e-5f) return;
        using (Push(new Transform3D(BasisAlong(d / len), a)))
        {
            Box(new Vector3(0, len / 2, 0), new Vector3(width, len, depth), p, end);
        }
    }

    /// <summary>A basis whose Y axis points along <paramref name="dir"/>, X kept as horizontal as possible.</summary>
    public static Basis BasisAlong(Vector3 dir)
    {
        Vector3 y = dir.Normalized();
        Vector3 hint = Mathf.Abs(y.X) > 0.9f ? Vector3.Forward : Vector3.Right;
        Vector3 z = hint.Cross(y).Normalized();
        Vector3 x = y.Cross(z).Normalized();
        return new Basis(x, y, z);
    }

    /// <summary>Low-poly ellipsoid.</summary>
    public void Sphere(Vector3 center, Vector3 radii, int segments, int rings, Paint p)
    {
        var grid = new Vector3[rings + 1][];
        for (int r = 0; r <= rings; r++)
        {
            float lat = -Mathf.Pi / 2 + Mathf.Pi * r / rings;
            grid[r] = new Vector3[segments];
            for (int s = 0; s < segments; s++)
            {
                float lon = s * Mathf.Tau / segments + (r % 2) * Mathf.Pi / segments;
                grid[r][s] = center + new Vector3(
                    Mathf.Cos(lat) * Mathf.Cos(lon) * radii.X, Mathf.Sin(lat) * radii.Y, Mathf.Cos(lat) * Mathf.Sin(lon) * radii.Z);
            }
        }
        for (int r = 0; r < rings; r++)
        for (int s = 0; s < segments; s++)
        {
            int s1 = (s + 1) % segments;
            Quad(grid[r][s], grid[r][s1], grid[r + 1][s1], grid[r + 1][s], p, center);
        }
    }

    /// <summary>Dome (upper half-ellipsoid) on a flat base, <paramref name="radius"/> wide and <paramref name="height"/> tall.</summary>
    public void Dome(Vector3 baseCenter, float radius, float height, int segments, int rings, Paint p, Paint? band = null)
    {
        var grid = new Vector3[rings + 1][];
        for (int r = 0; r <= rings; r++)
        {
            float lat = Mathf.Pi / 2 * r / rings;
            grid[r] = new Vector3[segments];
            for (int s = 0; s < segments; s++)
            {
                float lon = s * Mathf.Tau / segments;
                grid[r][s] = baseCenter + new Vector3(
                    Mathf.Cos(lat) * Mathf.Cos(lon) * radius, Mathf.Sin(lat) * height, Mathf.Cos(lat) * Mathf.Sin(lon) * radius);
            }
        }
        Vector3 inside = baseCenter + new Vector3(0, height * 0.3f, 0);
        for (int r = 0; r < rings; r++)
        for (int s = 0; s < segments; s++)
        {
            int s1 = (s + 1) % segments;
            Paint paint = r == 0 && band is { } bp ? bp : p;
            Quad(grid[r][s], grid[r][s1], grid[r + 1][s1], grid[r + 1][s], paint, inside);
        }
        Poly(grid[0], p, inside);
    }

    /// <summary>
    /// Skins a sequence of convex rings (same vertex count each) into a closed solid.
    /// <paramref name="edgePaint"/> optionally paints ring edge i (from vertex i to i+1) differently.
    /// </summary>
    public void Loft(IReadOnlyList<Vector3[]> rings, Paint p, Paint? cap = null, Func<int, Paint?>? edgePaint = null)
    {
        int n = rings[0].Length;
        for (int r = 0; r + 1 < rings.Count; r++)
        {
            Vector3 inside = Centroid(rings[r], rings[r + 1]);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Paint paint = edgePaint?.Invoke(i) ?? p;
                Quad(rings[r][i], rings[r][j], rings[r + 1][j], rings[r + 1][i], paint, inside);
            }
        }
        Poly(rings[0], cap ?? p, Centroid(rings[1]));
        Poly(rings[^1], cap ?? p, Centroid(rings[^2]));
    }

    /// <summary>Convex (z, y) side profile extruded along X from x0 to x1 (treads, armour plates, fins).</summary>
    public void ExtrudeX(IReadOnlyList<Vector2> zy, float x0, float x1, Paint p, Paint? faces = null, Func<int, Paint?>? edgePaint = null)
    {
        var a = new Vector3[zy.Count];
        var b = new Vector3[zy.Count];
        for (int i = 0; i < zy.Count; i++)
        {
            a[i] = new Vector3(x0, zy[i].Y, zy[i].X);
            b[i] = new Vector3(x1, zy[i].Y, zy[i].X);
        }
        Loft(new[] { a, b }, p, faces, edgePaint);
    }

    /// <summary>Convex (x, z) top-view outline extruded up from y0 to y1 (wings, decks, pads).</summary>
    public void ExtrudeY(IReadOnlyList<Vector2> xz, float y0, float y1, Paint p, Paint? top = null)
    {
        var a = new Vector3[xz.Count];
        var b = new Vector3[xz.Count];
        for (int i = 0; i < xz.Count; i++)
        {
            a[i] = new Vector3(xz[i].X, y0, xz[i].Y);
            b[i] = new Vector3(xz[i].X, y1, xz[i].Y);
        }
        Vector3 inside = Centroid(a, b);
        int n = xz.Count;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Quad(a[i], a[j], b[j], b[i], p, inside);
        }
        Poly(b, top ?? p, inside);
        Poly(a, p, inside);
    }

    /// <summary>Convex (x, y) front profile extruded along Z from z0 to z1 (hull sections, blades).</summary>
    public void ExtrudeZ(IReadOnlyList<Vector2> xy, float z0, float z1, Paint p, Paint? faces = null, Func<int, Paint?>? edgePaint = null)
    {
        var a = new Vector3[xy.Count];
        var b = new Vector3[xy.Count];
        for (int i = 0; i < xy.Count; i++)
        {
            a[i] = new Vector3(xy[i].X, xy[i].Y, z0);
            b[i] = new Vector3(xy[i].X, xy[i].Y, z1);
        }
        Loft(new[] { a, b }, p, faces, edgePaint);
    }

    /// <summary>Flat ring (washer) lying in XZ at <paramref name="center"/>, given thickness.</summary>
    public void Ring(Vector3 center, float rOuter, float rInner, float thickness, int sides, Paint p, Paint? top = null)
    {
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.Tau / sides, a1 = (i + 1) * Mathf.Tau / sides;
            var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
            var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            var up = new Vector3(0, thickness / 2, 0);
            Vector3 o0 = center + d0 * rOuter, o1 = center + d1 * rOuter, i0 = center + d0 * rInner, i1 = center + d1 * rInner;
            // Between the inner and outer chord midpoints (not at mid-radius: with few sides the
            // outer chord dips inside the mid-radius circle and the outer wall would flip).
            Vector3 inside = center + (d0 + d1) / 2 * ((rOuter + rInner) / 2);
            Quad(o0 + up, o1 + up, i1 + up, i0 + up, top ?? p, inside);
            Quad(o0 - up, o1 - up, i1 - up, i0 - up, p, inside);
            Quad(o0 - up, o1 - up, o1 + up, o0 + up, p, inside);
            Quad(i0 - up, i1 - up, i1 + up, i0 + up, p, inside);
        }
    }

    // ---- Output ------------------------------------------------------------------------------

    /// <summary>Bakes everything added so far into a single-surface mesh (no material).</summary>
    public ArrayMesh ToMesh()
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = _verts.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = _normals.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = _colors.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private static Vector3 Centroid(params Vector3[][] sets)
    {
        Vector3 sum = Vector3.Zero;
        int count = 0;
        foreach (Vector3[] set in sets)
        foreach (Vector3 v in set)
        {
            sum += v;
            count++;
        }
        return count == 0 ? Vector3.Zero : sum / count;
    }
}
