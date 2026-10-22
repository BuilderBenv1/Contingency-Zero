using ContingencyZero.Sim.Data;
using Godot;

namespace ContingencyZero.Game.View.Meshes;

/// <summary>
/// Aircraft, modelled around y = 0 (belly near 0); the renderer lifts them to flight altitude.
/// Lattice: needle fuselages, swept deltas and flying wings, blue engine glow.
/// Crucible: fat fuselages, straight stubby wings, twin engines and rotors, orange glow.
/// Wings are the team-colour carriers since the camera mostly sees their tops.
/// </summary>
internal static partial class Models
{
    /// <summary>A mirrored pair of wings from a right-hand (x &gt; 0) planform outline, between y0 and y1.</summary>
    private static void Wings(MeshKit k, Vector2[] rightPlanform, float y0, float y1, Paint side, Paint top)
    {
        k.ExtrudeY(rightPlanform, y0, y1, side, top);
        var left = new Vector2[rightPlanform.Length];
        for (int i = 0; i < left.Length; i++) left[i] = new Vector2(-rightPlanform[i].X, rightPlanform[i].Y);
        k.ExtrudeY(left, y0, y1, side, top);
    }

    /// <summary>A vertical tail fin with a swept (z, y) profile at x, canted outwards by <paramref name="cant"/> radians.</summary>
    private static void Fin(MeshKit k, float x, float thick, Vector2[] zy, float cant, Paint p, Paint faces)
    {
        using (k.Push(V(x, 0, 0), V(0, 0, -cant)))
            k.ExtrudeX(zy, -thick / 2, thick / 2, p, faces);
    }

    /// <summary>Fighter: Lattice "Swift" needle delta; Crucible "Hornet" stubby twin-engine jet.</summary>
    public static void Fighter(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        if (s.IsLattice)
        {
            Parts.Hull(b, new Parts.Section[]
            {
                new(-1.22f, 0.02f, 0.01f, 0.14f, 0.16f, 0.17f),
                new(-0.6f, 0.14f, 0.09f, 0.03f, 0.18f, 0.3f),
                new(0.3f, 0.2f, 0.12f, 0.0f, 0.2f, 0.32f),
                new(0.9f, 0.15f, 0.1f, 0.04f, 0.18f, 0.28f),
            }, s.Hull, s.Hull, s.HullDark, s.HullDark);
            Parts.Hull(b, new Parts.Section[]
            {
                new(-0.66f, 0.02f, 0.01f, 0.26f, 0.27f, 0.28f),
                new(-0.42f, 0.09f, 0.05f, 0.24f, 0.32f, 0.38f),
                new(-0.12f, 0.08f, 0.04f, 0.26f, 0.32f, 0.35f),
            }, s.Glass, s.Glass);
            Wings(b, new[] { V2(0.12f, -0.36f), V2(0.98f, 0.55f), V2(0.98f, 0.76f), V2(0.12f, 0.78f) }, 0.12f, 0.17f, s.HullDark, s.Team);
            Wings(b, new[] { V2(0.1f, -0.82f), V2(0.36f, -0.6f), V2(0.36f, -0.52f), V2(0.1f, -0.56f) }, 0.15f, 0.18f, s.HullDark, s.Team);
            foreach (float sx in Sides)
            {
                Fin(b, sx * 0.15f, 0.04f, new[] { V2(0.42f, 0.24f), V2(0.86f, 0.64f), V2(1.0f, 0.64f), V2(0.95f, 0.24f) }, sx * 0.25f, s.Hull, s.Team);
                b.Rod(V(sx * 0.98f, 0.12f, 0.2f), V(sx * 0.98f, 0.12f, 0.82f), 0.035f, 0.035f, 4, s.Metal);
            }
            Parts.Thruster(b, s, V(0, 0.17f, 0.82f), V(0, 0.17f, 1.08f), 0.13f);
        }
        else
        {
            Parts.Hull(b, new Parts.Section[]
            {
                new(-1.15f, 0.08f, 0.05f, 0.08f, 0.15f, 0.2f),
                new(-0.7f, 0.22f, 0.18f, 0.0f, 0.2f, 0.34f),
                new(0.5f, 0.28f, 0.22f, 0.0f, 0.22f, 0.38f),
                new(0.86f, 0.26f, 0.2f, 0.02f, 0.2f, 0.34f),
            }, s.Hull, s.Hull, s.HullDark, s.HullDark);
            b.Box(V(0, 0.38f, -0.48f), V(0.22f, 0.1f, 0.34f), s.Glass);
            Wings(b, new[] { V2(0.24f, -0.18f), V2(0.98f, 0.04f), V2(0.98f, 0.4f), V2(0.24f, 0.46f) }, 0.12f, 0.2f, s.HullDark, s.Team);
            foreach (float sx in Sides)
            {
                b.Box(V(sx * 0.32f, 0.16f, -0.3f), V(0.14f, 0.2f, 0.4f), s.HullDark, s.Mech);
                Parts.Thruster(b, s, V(sx * 0.17f, 0.18f, 0.7f), V(sx * 0.17f, 0.18f, 1.14f), 0.15f);
                foreach (float x in new[] { 0.55f, 0.8f })
                    b.Rod(V(sx * x, 0.06f, -0.12f), V(sx * x, 0.06f, 0.42f), 0.04f, 0.04f, 4, s.Accent);
            }
            Fin(b, 0, 0.06f, new[] { V2(0.4f, 0.34f), V2(0.84f, 0.74f), V2(1.0f, 0.74f), V2(1.0f, 0.34f) }, 0, s.Hull, s.Team);
            Parts.Rivets(b, s, V(-0.15f, 0.38f, 0.1f), V(0.15f, 0.38f, 0.1f), 3, 0.04f);
        }
    }

    /// <summary>Bomber: Lattice "Lancet" flying wing; Crucible "Anvilfall" four-engined heavy.</summary>
    public static void Bomber(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        if (s.IsLattice)
        {
            b.ExtrudeY(new[] { V2(0, -1.2f), V2(1.08f, 0.6f), V2(0.92f, 0.84f), V2(-0.92f, 0.84f), V2(-1.08f, 0.6f) }, 0.08f, 0.16f, s.HullDark, s.Hull);
            foreach (float sx in Sides)
                b.ExtrudeY(new[] { V2(sx * 0.12f, -0.7f), V2(sx * 0.98f, 0.48f), V2(sx * 0.9f, 0.6f), V2(sx * 0.12f, -0.38f) }, 0.16f, 0.18f, s.Team, s.Team);
            Parts.Hull(b, new Parts.Section[]
            {
                new(-1.0f, 0.04f, 0.02f, 0.12f, 0.17f, 0.2f),
                new(-0.5f, 0.24f, 0.14f, 0.08f, 0.24f, 0.36f),
                new(0.5f, 0.28f, 0.16f, 0.08f, 0.24f, 0.34f),
                new(0.8f, 0.2f, 0.1f, 0.1f, 0.2f, 0.26f),
            }, s.Hull, s.Team, s.Hull, s.HullDark);
            b.Box(V(0, 0.33f, -0.5f), V(0.16f, 0.06f, 0.22f), s.Glass);
            foreach (float sx in Sides)
                Parts.Thruster(b, s, V(sx * 0.34f, 0.16f, 0.52f), V(sx * 0.34f, 0.16f, 0.9f), 0.12f);
        }
        else
        {
            Parts.Hull(b, new Parts.Section[]
            {
                new(-1.2f, 0.12f, 0.08f, 0.05f, 0.2f, 0.28f),
                new(-0.85f, 0.26f, 0.2f, -0.04f, 0.26f, 0.42f),
                new(0.55f, 0.28f, 0.22f, -0.04f, 0.26f, 0.42f),
                new(1.1f, 0.12f, 0.08f, 0.14f, 0.26f, 0.32f),
            }, s.Hull, s.Team, s.HullDark, s.HullDark);
            b.Box(V(0, 0.42f, -0.82f), V(0.3f, 0.1f, 0.24f), s.Glass);
            b.Box(V(0, -0.03f, 0), V(0.3f, 0.04f, 0.7f), s.Mech);
            Wings(b, new[] { V2(0.26f, -0.32f), V2(1.22f, -0.18f), V2(1.22f, 0.18f), V2(0.26f, 0.3f) }, 0.16f, 0.24f, s.HullDark, s.Team);
            Wings(b, new[] { V2(0.1f, 0.85f), V2(0.6f, 0.92f), V2(0.6f, 1.08f), V2(0.1f, 1.1f) }, 0.24f, 0.28f, s.HullDark, s.Hull);
            foreach (float sx in Sides)
            {
                foreach (float x in new[] { 0.52f, 0.92f })
                {
                    b.Rod(V(sx * x, 0.12f, -0.52f), V(sx * x, 0.12f, -0.36f), 0.1f, 0.12f, 6, s.Mech);
                    Parts.Thruster(b, s, V(sx * x, 0.12f, -0.38f), V(sx * x, 0.12f, 0.36f), 0.12f);
                }
                Fin(b, sx * 0.55f, 0.05f, new[] { V2(0.86f, 0.26f), V2(1.0f, 0.62f), V2(1.1f, 0.62f), V2(1.1f, 0.26f) }, 0, s.Hull, s.Team);
            }
            Parts.Rivets(b, s, V(0, 0.42f, -0.4f), V(0, 0.42f, 0.4f), 4, 0.05f);
        }
    }

    /// <summary>Gunship: Lattice "Seraph" twin-duct VTOL; Crucible "Buzzsaw" helicopter (main rotor is the rotor part).</summary>
    public static void Gunship(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        if (s.IsLattice)
        {
            Parts.Hull(b, new Parts.Section[]
            {
                new(-1.0f, 0.06f, 0.04f, 0.08f, 0.16f, 0.2f),
                new(-0.6f, 0.26f, 0.16f, -0.04f, 0.24f, 0.4f),
                new(0.3f, 0.26f, 0.16f, -0.02f, 0.24f, 0.4f),
                new(1.15f, 0.06f, 0.04f, 0.18f, 0.24f, 0.28f),
            }, s.Hull, s.Team, s.Hull, s.HullDark);
            b.Box(V(0, 0.36f, -0.62f), V(0.2f, 0.08f, 0.3f), s.Glass);
            Wings(b, new[] { V2(0.2f, -0.12f), V2(0.62f, -0.06f), V2(0.62f, 0.22f), V2(0.2f, 0.26f) }, 0.14f, 0.2f, s.HullDark, s.Team);
            foreach (float sx in Sides)
            {
                Vector3 c = V(sx * 0.82f, 0.1f, 0.06f);
                b.Ring(c, 0.36f, 0.27f, 0.24f, 8, s.Hull, s.Team);
                b.Cylinder(c - V(0, 0.03f, 0), 0.27f, 0.27f, 0.04f, 8, s.Mech, s.GlowDim);
                b.Box(c + V(0, 0.04f, 0), V(0.5f, 0.03f, 0.06f), s.Metal);
                Fin(b, sx * 0.1f, 0.03f, new[] { V2(0.7f, 0.28f), V2(1.0f, 0.56f), V2(1.12f, 0.56f), V2(1.1f, 0.26f) }, sx * 0.5f, s.Hull, s.Team);
            }
            Parts.Emitter(b, s, V(0, 0.0f, -0.62f), V(0, -0.02f, -1.18f), 0.05f);
            b.Sphere(V(0, 0.02f, -0.62f), V(0.1f, 0.1f, 0.1f), 6, 3, s.Mech);
        }
        else
        {
            Parts.Hull(b, new Parts.Section[]
            {
                new(-1.05f, 0.1f, 0.06f, 0.1f, 0.22f, 0.3f),
                new(-0.62f, 0.28f, 0.22f, 0.02f, 0.3f, 0.5f),
                new(0.3f, 0.28f, 0.22f, 0.02f, 0.3f, 0.5f),
                new(0.55f, 0.14f, 0.1f, 0.18f, 0.3f, 0.38f),
            }, s.Hull, s.Team, s.HullDark, s.HullDark);
            b.Box(V(0, 0.4f, -0.8f), V(0.32f, 0.12f, 0.2f), s.Glass);
            b.Beam(V(0, 0.3f, 0.5f), V(0, 0.36f, 1.18f), 0.1f, 0.12f, s.Hull);
            Fin(b, 0.05f, 0.04f, new[] { V2(1.0f, 0.32f), V2(1.12f, 0.6f), V2(1.22f, 0.6f), V2(1.2f, 0.3f) }, 0, s.Hull, s.Team);
            b.Cylinder(V(0.09f, 0.46f, 1.12f), 0.02f, 0.02f, 0.02f, 4, s.Mech);
            Wings(b, new[] { V2(0.24f, -0.1f), V2(0.64f, -0.08f), V2(0.64f, 0.14f), V2(0.24f, 0.18f) }, 0.12f, 0.18f, s.HullDark, s.Team);
            foreach (float sx in Sides)
            {
                b.Box(V(sx * 0.6f, 0.06f, -0.02f), V(0.12f, 0.12f, 0.36f), s.HullDark);
                Parts.Barrel(b, s, V(sx * 0.6f, 0.06f, -0.2f), V(sx * 0.6f, 0.06f, -0.52f), 0.035f, brake: false);
                b.Beam(V(sx * 0.2f, 0.04f, -0.3f), V(sx * 0.28f, -0.14f, -0.3f), 0.04f, 0.04f, s.Metal);
                b.Beam(V(sx * 0.2f, 0.04f, 0.3f), V(sx * 0.28f, -0.14f, 0.3f), 0.04f, 0.04f, s.Metal);
                b.Box(V(sx * 0.28f, -0.15f, 0), V(0.05f, 0.04f, 0.9f), s.Metal);
                Parts.Thruster(b, s, V(sx * 0.18f, 0.42f, 0.1f), V(sx * 0.18f, 0.42f, 0.42f), 0.08f);
            }
            b.Cylinder(V(0, 0.5f, -0.1f), 0.1f, 0.08f, 0.14f, 6, s.Mech);
            MeshKit r = rig.AddRotor(V(0, 0.64f, -0.1f), Vector3.Up);
            r.Cylinder(V(0, -0.02f, 0), 0.1f, 0.08f, 0.08f, 6, s.Metal);
            for (int i = 0; i < 4; i++)
                using (r.Push(Vector3.Zero, V(0, i * Mathf.Pi / 2 + 0.4f, 0)))
                {
                    r.Box(V(0, 0.02f, -0.66f), V(0.12f, 0.025f, 1.2f), s.Mech);
                    r.Box(V(0, 0.02f, -1.18f), V(0.13f, 0.03f, 0.18f), s.Team);
                }
        }
    }

    /// <summary>Flying constructor: Lattice "Kite" diamond VTOL; Crucible "Hauler" rotor lifter with a hanging crane.</summary>
    public static void BuilderAir(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        if (s.IsLattice)
        {
            b.ExtrudeY(new[] { V2(0, -1.1f), V2(0.82f, 0.05f), V2(0, 0.95f), V2(-0.82f, 0.05f) }, 0.08f, 0.16f, s.HullDark, s.Team);
            Parts.Hull(b, new Parts.Section[]
            {
                new(-0.8f, 0.05f, 0.03f, 0.1f, 0.16f, 0.2f),
                new(-0.4f, 0.22f, 0.14f, 0.0f, 0.22f, 0.34f),
                new(0.4f, 0.22f, 0.14f, 0.0f, 0.22f, 0.34f),
                new(0.75f, 0.08f, 0.05f, 0.08f, 0.16f, 0.2f),
            }, s.Hull, s.Hull, s.HullDark, s.HullDark);
            b.Box(V(0, 0.3f, -0.4f), V(0.16f, 0.06f, 0.2f), s.Glass);
            foreach (float sx in Sides)
                Parts.Thruster(b, s, V(sx * 0.42f, 0.12f, 0.1f), V(sx * 0.42f, 0.12f, 0.5f), 0.1f);
            using (b.Push(V(0, 0.0f, -0.2f), V(0, 0, Mathf.Pi), 0.7f))
                Parts.NanoArm(b, s, Vector3.Zero, 1f);
        }
        else
        {
            b.Box(V(0, 0.22f, 0.05f), V(0.7f, 0.44f, 1.5f), s.Hull, s.Team);
            b.Frustum(V(0, 0.0f, -0.78f), V2(0.7f, 0.12f), V2(0.5f, 0.06f), 0.4f, s.HullDark, s.Hull, V2(0, -0.08f));
            b.Box(V(0, 0.32f, -0.72f), V(0.5f, 0.14f, 0.04f), s.Glass);
            foreach (float sx in Sides)
            {
                b.Box(V(sx * 0.4f, 0.2f, 0.45f), V(0.1f, 0.3f, 0.5f), s.Team);
                Parts.Thruster(b, s, V(sx * 0.2f, 0.3f, 0.8f), V(sx * 0.2f, 0.3f, 1.05f), 0.1f);
            }
            Parts.Rivets(b, s, V(-0.25f, 0.44f, -0.5f), V(-0.25f, 0.44f, 0.6f), 4, 0.05f);
            b.Cylinder(V(0, 0.44f, 0), 0.12f, 0.1f, 0.14f, 6, s.Mech);
            using (b.Push(V(0, 0.0f, 0.2f), V(0, 0, Mathf.Pi), 0.7f))
                Parts.NanoArm(b, s, Vector3.Zero, 1f);
            MeshKit r = rig.AddRotor(V(0, 0.6f, 0), Vector3.Up);
            r.Cylinder(V(0, -0.02f, 0), 0.12f, 0.1f, 0.08f, 6, s.Metal);
            for (int i = 0; i < 3; i++)
                using (r.Push(Vector3.Zero, V(0, i * Mathf.Tau / 3, 0)))
                {
                    r.Box(V(0, 0.02f, -0.62f), V(0.16f, 0.03f, 1.14f), s.Mech);
                    r.Box(V(0, 0.02f, -1.12f), V(0.17f, 0.035f, 0.16f), s.Team);
                }
        }
    }
}
