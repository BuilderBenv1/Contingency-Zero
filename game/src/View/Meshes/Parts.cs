using System;
using Godot;

namespace ContingencyZero.Game.View.Meshes;

/// <summary>
/// Reusable machine parts shared by many models: armour slabs, legs, treads, wheels, barrels,
/// nanolathe arms, missile pods, thrusters, smokestacks, rivets, dishes and lofted hulls.
/// Each takes the faction <see cref="Style"/> so the same call reads as Lattice or Crucible.
/// </summary>
internal static class Parts
{
    private static Vector3 V(float x, float y, float z) => new(x, y, z);

    /// <summary>
    /// An armour block. Lattice: a bevelled slab whose top is inset (angular ceramic panel).
    /// Crucible: a plain square block.
    /// </summary>
    public static void Slab(MeshKit k, Style s, Vector3 center, Vector3 size, Paint side, Paint? top = null, float bevel = 0.18f)
    {
        if (!s.IsLattice)
        {
            k.Box(center, size, side, top);
            return;
        }
        float inset = Mathf.Min(size.X, size.Z) * bevel;
        float capH = Mathf.Min(size.Y * 0.45f, inset * 1.2f);
        k.Box(center - V(0, capH / 2, 0), V(size.X, size.Y - capH, size.Z), side);
        k.Frustum(center + V(0, size.Y / 2 - capH, 0), new Vector2(size.X, size.Z),
            new Vector2(size.X - 2 * inset, size.Z - 2 * inset), capH, side, top);
    }

    /// <summary>A row of rivet studs (small pyramids) from a to b, sitting on a surface facing up.</summary>
    public static void Rivets(MeshKit k, Style s, Vector3 a, Vector3 b, int count, float size)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 p = count == 1 ? (a + b) / 2 : a.Lerp(b, i / (float)(count - 1));
            k.Frustum(p, new Vector2(size, size), new Vector2(size * 0.4f, size * 0.4f), size * 0.5f, s.Metal);
        }
    }

    /// <summary>
    /// A two-segment leg from hip to foot. The knee is pushed along <paramref name="kneeDir"/>
    /// (Lattice bots use reverse "chicken" knees pointing back, Crucible forward knees).
    /// </summary>
    public static void Leg(MeshKit k, Style s, Vector3 hip, Vector3 foot, Vector3 kneeDir, float bend, float thick, Paint armour)
    {
        Vector3 knee = (hip + foot) / 2 + kneeDir.Normalized() * bend;
        Vector3 ankle = foot + V(0, thick * 0.45f, 0);
        k.Beam(hip, knee, thick * 0.8f, thick * 0.9f, s.Mech);
        k.Beam(hip.Lerp(knee, 0.12f), hip.Lerp(knee, 0.88f), thick * 1.25f, thick * 1.45f, armour);
        k.Beam(knee, ankle, thick * 0.7f, thick * 0.8f, s.Metal);
        k.Beam(knee.Lerp(ankle, 0.08f), knee.Lerp(ankle, 0.62f), thick * 1.05f, thick * 1.2f, s.IsLattice ? s.Hull : s.HullDark);
        k.Box(knee, V(thick * 1.2f, thick * 1.2f, thick * 1.2f), s.Mech);
        k.Box(hip, V(thick * 1.1f, thick * 1.1f, thick * 1.1f), s.Mech);
        if (s.IsLattice)
            k.Frustum(foot, new Vector2(thick * 1.7f, thick * 3.0f), new Vector2(thick * 0.8f, thick * 1.4f), thick * 0.55f, s.HullDark,
                shift: new Vector2(0, thick * 0.2f));
        else
            k.Box(foot + V(0, thick * 0.32f, -thick * 0.25f), V(thick * 2.0f, thick * 0.64f, thick * 2.9f), s.HullDark, s.Mech);
    }

    /// <summary>
    /// A track unit centred on <paramref name="x"/>, running z0..z1 (z0 is the front), with road
    /// wheels on the outer face and an optional armoured skirt over its top half.
    /// </summary>
    public static void Tread(MeshKit k, Style s, float x, float width, float z0, float z1, float height, Paint? skirt = null)
    {
        float h = height, r = h * 0.45f;
        Vector2[] profile =
        {
            new(z0, h * 0.5f), new(z0 + r, 0), new(z1 - r, 0), new(z1, h * 0.5f), new(z1 - r * 0.6f, h), new(z0 + r * 0.6f, h),
        };
        k.ExtrudeX(profile, x - width / 2, x + width / 2, s.Tread, s.Mech);
        float side = Mathf.Sign(x == 0 ? 1 : x);
        float outer = x + side * width / 2;
        int wheels = Math.Max(3, (int)((z1 - z0) / (h * 0.85f)));
        for (int i = 0; i < wheels; i++)
        {
            float z = Mathf.Lerp(z0 + r * 0.9f, z1 - r * 0.9f, i / (float)(wheels - 1));
            k.Rod(V(outer - side * 0.01f * h, h * 0.42f, z), V(outer + side * 0.12f * h, h * 0.42f, z), h * 0.3f, h * 0.3f, 6, s.Metal, s.Mech);
        }
        if (skirt is { } sk)
            k.Box(V(x + side * width * 0.06f, h * 0.84f, (z0 + z1) / 2), V(width * 1.14f, h * 0.36f, (z1 - z0) * 0.9f), sk);
    }

    /// <summary>
    /// A smooth Lattice track pod: a faired, sloped-ended runner with a glowing strip along its outer face.
    /// </summary>
    public static void SkidPod(MeshKit k, Style s, float x, float width, float z0, float z1, float height, Paint top)
    {
        float h = height;
        Vector2[] profile =
        {
            new(z0, h * 0.55f), new(z0 + h * 0.6f, 0), new(z1 - h * 0.5f, 0), new(z1, h * 0.5f), new(z1 - h * 0.15f, h), new(z0 + h * 0.5f, h),
        };
        // Edge 4 of the profile ring is the top run.
        k.ExtrudeX(profile, x - width / 2, x + width / 2, s.HullDark, s.Hull, i => i == 4 ? top : i == 1 ? s.Tread : null);
        float side = Mathf.Sign(x == 0 ? 1 : x);
        k.Box(V(x + side * width / 2, h * 0.45f, (z0 + z1) / 2 + h * 0.05f), V(0.04f * h + 0.01f, h * 0.12f, (z1 - z0) * 0.55f), s.Glow);
    }

    /// <summary>A wheel on an axle along X; <paramref name="outward"/> is +1 or -1 (which face gets the hub).</summary>
    public static void Wheel(MeshKit k, Style s, Vector3 center, float radius, float width, float outward)
    {
        Vector3 a = center - V(outward * width / 2, 0, 0), b = center + V(outward * width / 2, 0, 0);
        k.Rod(a, b, radius, radius, s.IsLattice ? 8 : 6, s.Tread, s.Mech);
        k.Rod(b - V(outward * 0.01f, 0, 0), b + V(outward * width * 0.18f, 0, 0), radius * 0.45f, radius * 0.38f, 6, s.Metal);
    }

    /// <summary>A gun barrel from <paramref name="from"/> to the muzzle, with an optional muzzle brake.</summary>
    public static void Barrel(MeshKit k, Style s, Vector3 from, Vector3 to, float radius, bool brake = true, Paint? paint = null)
    {
        k.Rod(from, to, radius, radius * 0.85f, 6, paint ?? s.Metal, s.Mech);
        if (!brake) return;
        Vector3 d = (to - from).Normalized();
        k.Rod(to - d * radius * 2.6f, to + d * radius * 0.2f, radius * 1.45f, radius * 1.35f, 6, s.Mech, s.Mech);
    }

    /// <summary>A beam weapon emitter: barrel with glowing lens ring and tip.</summary>
    public static void Emitter(MeshKit k, Style s, Vector3 from, Vector3 to, float radius)
    {
        Vector3 d = (to - from).Normalized();
        k.Rod(from, to - d * radius * 1.2f, radius, radius * 0.8f, 6, s.Metal);
        k.Rod(to - d * radius * 1.4f, to, radius * 1.3f, radius * 0.9f, 6, s.Mech, s.Glow);
    }

    /// <summary>
    /// A nanolathe crane: swivel base, raised upper arm, forearm reaching forward (-Z), and a
    /// glowing emitter tip. Authored ~1 unit tall at size 1.
    /// </summary>
    public static void NanoArm(MeshKit k, Style s, Vector3 basePos, float size, float yaw = 0, float reach = 1f)
    {
        using (k.Push(basePos, V(0, yaw, 0), size))
        {
            float t = s.IsLattice ? 1f : 1.35f;
            k.Cylinder(Vector3.Zero, 0.24f * t, 0.2f * t, 0.14f, s.Round, s.Mech);
            Vector3 shoulder = V(0, 0.14f, 0.06f), elbow = V(0, 0.78f, 0.22f), wrist = V(0, 0.62f, -0.5f * reach);
            k.Beam(shoulder, elbow, 0.15f * t, 0.19f * t, s.Team);
            k.Beam(elbow, wrist, 0.11f * t, 0.13f * t, s.IsLattice ? s.Hull : s.Accent);
            k.Box(elbow, V(0.2f * t, 0.2f * t, 0.2f * t), s.Mech);
            if (!s.IsLattice)
                k.Beam(shoulder + V(0, 0.05f, -0.1f), elbow.Lerp(wrist, 0.35f), 0.05f, 0.05f, s.Metal); // hydraulic ram
            Vector3 tipDir = V(0, -0.5f, -1).Normalized();
            k.Rod(wrist, wrist + tipDir * 0.22f, 0.1f * t, 0.07f * t, 6, s.Metal);
            k.Sphere(wrist + tipDir * 0.3f, V(0.09f, 0.09f, 0.09f) * t, 6, 3, s.Glow);
        }
    }

    /// <summary>
    /// A missile pod pitched up by <paramref name="pitchDeg"/>, with a grid of warhead tips poking
    /// out of its front (-Z) face.
    /// </summary>
    public static void MissilePod(MeshKit k, Style s, Vector3 center, Vector3 size, float pitchDeg, int cols, int rows, Paint body, float yaw = 0)
    {
        using (k.Push(center, V(Mathf.DegToRad(pitchDeg), yaw, 0)))
        {
            k.Box(Vector3.Zero, size, body);
            k.Box(V(0, 0, -size.Z / 2 - 0.004f), V(size.X * 0.9f, size.Y * 0.88f, 0.01f), s.Mech);
            float cw = size.X / cols, rh = size.Y / rows;
            float rad = Mathf.Min(cw, rh) * 0.32f;
            for (int c = 0; c < cols; c++)
            for (int r = 0; r < rows; r++)
            {
                Vector3 p = V(-size.X / 2 + cw * (c + 0.5f), -size.Y / 2 + rh * (r + 0.5f), -size.Z / 2);
                k.Rod(p + V(0, 0, 0.02f), p - V(0, 0, rad * 2.4f), rad, 0, 4, s.IsLattice ? s.Hull : s.Accent);
            }
        }
    }

    /// <summary>An engine nozzle from front to back with a glowing exhaust disc at the back.</summary>
    public static void Thruster(MeshKit k, Style s, Vector3 front, Vector3 back, float radius)
    {
        k.Rod(front, back, radius * 0.85f, radius, s.Round, s.Mech, s.Glow);
    }

    /// <summary>A smokestack with a dark rim; hot stacks glow at the mouth.</summary>
    public static void Stack(MeshKit k, Style s, Vector3 basePos, float radius, float height, bool hot = true, Paint? band = null)
    {
        k.Cylinder(basePos, radius, radius * 0.86f, height, 6, s.Metal);
        k.Cylinder(basePos + V(0, height - radius * 0.55f, 0), radius * 1.08f, radius * 1.02f, radius * 0.6f, 6, s.Mech, hot ? s.Glow : s.Mech);
        if (band is { } b)
            k.Cylinder(basePos + V(0, height * 0.35f, 0), radius * 1.06f, radius * 1.02f, height * 0.18f, 6, b);
    }

    /// <summary>A shallow radar dish facing <paramref name="facing"/>, with a feed horn and glowing tip.</summary>
    public static void Dish(MeshKit k, Style s, Vector3 center, Vector3 facing, float radius, Paint face)
    {
        Vector3 f = facing.Normalized();
        k.Rod(center - f * radius * 0.3f, center, radius * 0.25f, radius, s.Round, s.Metal, face);
        k.Rod(center, center + f * radius * 0.55f, radius * 0.05f, radius * 0.04f, 4, s.Mech);
        k.Sphere(center + f * radius * 0.6f, V(radius, radius, radius) * 0.09f, 6, 3, s.Glow);
    }

    /// <summary>A glowing visor / eye slit as a thin box.</summary>
    public static void Eye(MeshKit k, Style s, Vector3 center, Vector3 size) => k.Box(center, size, s.Glow);

    /// <summary>A hull cross-section for <see cref="Hull"/>.</summary>
    public readonly record struct Section(float Z, float HalfWidth, float HalfTop, float Y0, float Y1, float Y2);

    /// <summary>
    /// Lofts a closed hull through cross-sections (front to back). Each ring is a bevelled
    /// hexagon: bottom, sides up to <c>Y1</c>, then sloping in to a <c>HalfTop</c>-wide top at <c>Y2</c>.
    /// </summary>
    public static void Hull(MeshKit k, Section[] sections, Paint side, Paint top, Paint? bevel = null, Paint? bottom = null)
    {
        var rings = new Vector3[sections.Length][];
        for (int i = 0; i < sections.Length; i++)
        {
            Section c = sections[i];
            rings[i] = new[]
            {
                V(-c.HalfWidth * 0.8f, c.Y0, c.Z), V(c.HalfWidth * 0.8f, c.Y0, c.Z), V(c.HalfWidth, c.Y1, c.Z),
                V(c.HalfTop, c.Y2, c.Z), V(-c.HalfTop, c.Y2, c.Z), V(-c.HalfWidth, c.Y1, c.Z),
            };
        }
        k.Loft(rings, side, side, i => i switch
        {
            0 => bottom ?? side,
            3 => top,
            2 or 4 => bevel ?? side,
            _ => null,
        });
    }
}
