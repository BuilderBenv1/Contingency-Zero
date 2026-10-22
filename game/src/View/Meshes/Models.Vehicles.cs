using ContingencyZero.Sim.Data;
using Godot;

namespace ContingencyZero.Game.View.Meshes;

/// <summary>
/// Ground vehicles. Lattice vehicles ride on two faired skid pods under a bevelled wedge hull;
/// Crucible vehicles sit on skirted treads (or big wheels) under square riveted hulls with stacks.
/// </summary>
internal static partial class Models
{
    // ---- Chassis ---------------------------------------------------------------------------------

    /// <summary>Lattice chassis: two skid pods with a bevelled wedge hull between them. Returns the hull's top height.</summary>
    private static float LatticeChassis(MeshKit b, Style s, float halfW, float z0, float z1, float podH, float top, float podW = 0.3f)
    {
        foreach (float sx in Sides)
            Parts.SkidPod(b, s, sx * (halfW - podW / 2), podW, z0, z1, podH, s.Team);
        float hw = halfW - podW + 0.04f;
        Parts.Hull(b, new Parts.Section[]
        {
            new(z0 + 0.04f, hw * 0.7f, hw * 0.45f, podH * 0.35f, podH * 0.75f, podH * 0.95f),
            new(z0 + 0.45f, hw, hw * 0.78f, podH * 0.3f, top * 0.86f, top),
            new(z1 - 0.18f, hw, hw * 0.78f, podH * 0.3f, top * 0.86f, top),
            new(z1, hw * 0.9f, hw * 0.62f, podH * 0.4f, top * 0.76f, top * 0.9f),
        }, s.Hull, s.Hull, s.HullDark, s.Mech);
        Parts.Eye(b, s, V(0, top * 0.8f, z1 + 0.005f), V(hw * 0.9f, 0.04f, 0.02f));
        return top;
    }

    /// <summary>Crucible chassis: two skirted treads with a square hull and sloped glacis between them.</summary>
    private static float CrucibleChassis(MeshKit b, Style s, float halfW, float z0, float z1, float treadH, float top, float treadW = 0.34f)
    {
        foreach (float sx in Sides)
            Parts.Tread(b, s, sx * (halfW - treadW / 2), treadW, z0, z1, treadH, s.Team);
        float hw = halfW - treadW + 0.03f;
        Vector2[] profile =
        {
            new(z0 + 0.06f, treadH * 0.35f), new(z0 + 0.06f, treadH * 0.8f), new(z0 + 0.38f, top),
            new(z1 - 0.1f, top), new(z1 - 0.02f, top * 0.86f), new(z1 - 0.02f, treadH * 0.35f),
        };
        b.ExtrudeX(profile, -hw, hw, s.Hull, s.HullDark, i => i == 1 ? s.HullDark : null);
        Parts.Rivets(b, s, V(-hw * 0.8f, top, z0 + 0.46f), V(hw * 0.8f, top, z0 + 0.46f), 4, 0.05f);
        b.Box(V(0, top * 0.7f, z1 - 0.01f), V(hw * 1.2f, top * 0.25f, 0.04f), s.Mech);
        Parts.Eye(b, s, V(0, top * 0.7f, z1 + 0.012f), V(hw * 0.9f, 0.035f, 0.01f));
        return top;
    }

    // ---- Vehicle Plant ---------------------------------------------------------------------------

    /// <summary>Main battle tank: hull plus a turret with the cannon.</summary>
    public static void Tank(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        if (s.IsLattice)
        {
            float top = LatticeChassis(b, s, 0.76f, -1.15f, 1.05f, 0.34f, 0.46f);
            MeshKit t = rig.AddTurret(V(0, top, 0.12f));
            LatticeTurret(t, s, 0.36f, 0.42f, 0.44f, 0.24f);
            Parts.Barrel(t, s, V(0, 0.12f, -0.3f), V(0, 0.13f, -1.32f), 0.055f);
            t.Rod(V(0, 0.12f, -0.36f), V(0, 0.12f, -0.6f), 0.075f, 0.07f, 6, s.Team);
            Parts.Eye(t, s, V(0.2f, 0.18f, -0.33f), V(0.08f, 0.04f, 0.04f));
        }
        else
        {
            float top = CrucibleChassis(b, s, 0.8f, -1.1f, 1.05f, 0.42f, 0.56f);
            Parts.Stack(b, s, V(0.28f, top, 0.78f), 0.07f, 0.3f);
            MeshKit t = rig.AddTurret(V(0, top, 0.08f));
            t.Box(V(0, 0.15f, 0.06f), V(0.84f, 0.3f, 0.84f), s.Hull, s.Team);
            t.Box(V(0, 0.14f, -0.38f), V(0.42f, 0.24f, 0.1f), s.Mech);
            Parts.Rivets(t, s, V(-0.34f, 0.3f, -0.3f), V(0.34f, 0.3f, -0.3f), 4, 0.05f);
            t.Cylinder(V(-0.18f, 0.3f, 0.18f), 0.12f, 0.12f, 0.06f, 6, s.HullDark);
            Parts.Barrel(t, s, V(0, 0.15f, -0.4f), V(0, 0.15f, -1.12f), 0.1f);
        }
    }

    /// <summary>Flak vehicle: twin or quad barrels pointing almost straight up.</summary>
    public static void FlakVehicle(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        MeshKit t;
        if (s.IsLattice)
        {
            float top = LatticeChassis(b, s, 0.76f, -1.12f, 1.05f, 0.34f, 0.44f);
            t = rig.AddTurret(V(0, top, 0.12f));
            t.Cylinder(V(0, 0, 0), 0.34f, 0.3f, 0.08f, 8, s.Mech);
            // Prism housing: a wide triangular block with team flanks and a glowing crystal ridge.
            Vector2[] tri = { new(-0.44f, 0.06f), new(0.4f, 0.06f), new(0.12f, 0.46f) };
            t.ExtrudeX(tri, -0.26f, 0.26f, s.Hull, s.Team);
            t.Beam(V(0, 0.46f, 0.12f), V(0, 0.46f, -0.2f), 0.08f, 0.1f, s.Glow);
            foreach (float sx in Sides)
            {
                t.Box(V(sx * 0.33f, 0.26f, -0.02f), V(0.14f, 0.2f, 0.3f), s.HullDark, s.Team);
                Vector3 root = V(sx * 0.33f, 0.32f, -0.1f), dir = Elevated(68);
                Parts.Barrel(t, s, root, root + dir * 0.78f, 0.045f);
            }
        }
        else
        {
            foreach (float sx in Sides)
            foreach (float z in new[] { -0.72f, 0.12f, 0.72f })
                Parts.Wheel(b, s, V(sx * 0.62f, 0.26f, z), 0.26f, 0.2f, sx);
            b.Box(V(0, 0.4f, 0), V(0.96f, 0.16f, 2.1f), s.Mech);
            b.Box(V(0, 0.68f, -0.76f), V(1.04f, 0.46f, 0.56f), s.Hull, s.Team);
            b.Box(V(0, 0.76f, -1.045f), V(0.86f, 0.16f, 0.02f), s.Glass);
            foreach (float sx in Sides)
                b.Box(V(sx * 0.62f, 0.56f, 0.12f), V(0.22f, 0.06f, 1.9f), s.Team);
            b.Box(V(0, 0.52f, 0.36f), V(1.06f, 0.12f, 1.36f), s.HullDark, s.Hull);
            Parts.Stack(b, s, V(-0.42f, 0.58f, -0.4f), 0.06f, 0.5f);
            t = rig.AddTurret(V(0, 0.58f, 0.36f));
            t.Cylinder(V(0, 0, 0), 0.38f, 0.36f, 0.1f, 6, s.Mech);
            t.Box(V(0, 0.24f, 0.06f), V(0.54f, 0.3f, 0.42f), s.Team, s.Team);
            foreach (float sx in Sides)
            foreach (float off in new[] { -0.05f, 0.05f })
            {
                Vector3 root = V(sx * 0.16f + off * sx, 0.3f, -0.08f), dir = Elevated(65);
                Parts.Barrel(t, s, root, root + dir * 0.7f, 0.035f);
            }
        }
    }

    /// <summary>Artillery vehicle: a long barrel angled up on a traversing mount.</summary>
    public static void ArtilleryVehicle(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        if (s.IsLattice)
        {
            float top = LatticeChassis(b, s, 0.72f, -1.1f, 1.12f, 0.34f, 0.44f);
            MeshKit t = rig.AddTurret(V(0, top, 0.38f));
            Parts.Slab(t, s, V(0, 0.08f, 0.04f), V(0.62f, 0.16f, 0.66f), s.Hull, s.Team);
            foreach (float sx in Sides)
                t.Frustum(V(sx * 0.16f, 0.14f, 0), V2(0.06f, 0.42f), V2(0.06f, 0.18f), 0.3f, s.Hull, s.Team, V2(0, -0.06f));
            Vector3 root = V(0, 0.32f, 0.08f), dir = Elevated(30);
            t.Rod(root - dir * 0.28f, root + dir * 1.72f, 0.065f, 0.055f, 6, s.Metal);
            foreach (float d in new[] { 0.35f, 0.75f, 1.15f })
                t.Rod(root + dir * d, root + dir * (d + 0.1f), 0.09f, 0.09f, 6, s.Team);
            t.Rod(root + dir * 1.66f, root + dir * 1.78f, 0.08f, 0.07f, 6, s.Mech, s.Glow);
        }
        else
        {
            foreach (float sx in Sides)
            foreach (float z in new[] { -0.66f, 0.56f })
                Parts.Wheel(b, s, V(sx * 0.6f, 0.34f, z), 0.34f, 0.22f, sx);
            b.Box(V(0, 0.52f, -0.05f), V(0.86f, 0.28f, 1.9f), s.Hull, s.Team);
            Parts.Rivets(b, s, V(-0.36f, 0.66f, -0.9f), V(0.36f, 0.66f, -0.9f), 4, 0.05f);
            b.Beam(V(0, 0.48f, 0.88f), V(0, 0.08f, 1.18f), 0.5f, 0.08f, s.Metal);
            b.Box(V(0, 0.06f, 1.18f), V(0.58f, 0.12f, 0.14f), s.Mech);
            Parts.Stack(b, s, V(-0.3f, 0.66f, -0.75f), 0.06f, 0.3f);
            MeshKit t = rig.AddTurret(V(0, 0.66f, 0.22f));
            t.Box(V(0, 0.1f, 0.08f), V(0.66f, 0.2f, 0.7f), s.HullDark, s.Team);
            foreach (float sx in Sides)
                t.Box(V(sx * 0.22f, 0.28f, 0.02f), V(0.08f, 0.3f, 0.4f), s.Hull, s.Team);
            Vector3 root = V(0, 0.32f, 0.06f), dir = Elevated(32);
            Parts.Barrel(t, s, root - dir * 0.3f, root + dir * 1.62f, 0.09f);
            t.Rod(root - dir * 0.2f, root + dir * 0.5f, 0.07f, 0.07f, 6, s.Mech);
            t.Box(root - dir * 0.36f + V(0, -0.04f, 0), V(0.24f, 0.24f, 0.22f), s.Mech);
        }
    }

    /// <summary>Salvager: pincers or a grinder drum at the front, scrap hopper behind.</summary>
    public static void Salvager(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        if (s.IsLattice)
        {
            float top = LatticeChassis(b, s, 0.7f, -1.0f, 1.1f, 0.32f, 0.44f);
            foreach (float sx in Sides)
            {
                b.Beam(V(sx * 0.3f, 0.4f, -0.62f), V(sx * 0.26f, 0.32f, -1.2f), 0.09f, 0.1f, s.Team);
                b.Beam(V(sx * 0.26f, 0.32f, -1.18f), V(sx * 0.07f, 0.28f, -1.3f), 0.07f, 0.08f, s.Hull);
            }
            Parts.Dish(b, s, V(0, 0.6f, -0.62f), V(0, -0.35f, -1), 0.22f, s.GlowDim);
            b.Box(V(0, top + 0.16f, 0.5f), V(0.84f, 0.34f, 0.76f), s.Hull, s.Team);
            b.Box(V(0, top + 0.33f, 0.5f), V(0.64f, 0.02f, 0.56f), s.Mech);
            b.Box(V(0.12f, top + 0.36f, 0.44f), V(0.2f, 0.1f, 0.16f), s.Metal);
        }
        else
        {
            float top = CrucibleChassis(b, s, 0.78f, -0.92f, 1.05f, 0.4f, 0.54f);
            Vector3 l = V(-0.64f, 0.34f, -1.08f), r = V(0.64f, 0.34f, -1.08f);
            b.Rod(l, r, 0.24f, 0.24f, 6, s.Metal, s.Mech);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.Tau / 6 + 0.25f;
                foreach (float x in new[] { -0.4f, 0f, 0.4f })
                    using (b.Push(V(x + (i % 2) * 0.1f, 0.34f, -1.08f), V(a, 0, 0)))
                        b.Frustum(V(0, 0.22f, 0), V2(0.12f, 0.08f), V2(0.02f, 0.04f), 0.1f, s.Accent);
            }
            foreach (float sx in Sides)
                b.Beam(V(sx * 0.5f, 0.42f, -0.62f), V(sx * 0.56f, 0.36f, -1.06f), 0.1f, 0.12f, s.HullDark);
            b.Box(V(0, top + 0.24f, 0.42f), V(0.98f, 0.5f, 0.96f), s.Team, s.Mech);
            b.Box(V(0, top + 0.5f, 0.42f), V(0.92f, 0.04f, 0.9f), s.Mech);
            foreach (var (x, z, rot) in new[] { (-0.2f, 0.3f, 0.4f), (0.18f, 0.5f, -0.7f), (0.05f, 0.25f, 1.2f) })
                using (b.Push(V(x, top + 0.52f, z), V(0.3f, rot, 0.4f)))
                    b.Box(Vector3.Zero, V(0.24f, 0.1f, 0.18f), s.Metal);
            Parts.Stack(b, s, V(-0.34f, top, -0.3f), 0.07f, 0.5f);
        }
    }

    /// <summary>Builder vehicle: nanolathe crane on a utility hull (Crucible adds a dozer blade).</summary>
    public static void BuilderVehicle(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        if (s.IsLattice)
        {
            float top = LatticeChassis(b, s, 0.74f, -1.12f, 1.05f, 0.32f, 0.46f);
            Parts.Slab(b, s, V(0, top + 0.06f, -0.55f), V(0.56f, 0.14f, 0.42f), s.Hull, s.Team);
            b.Box(V(0, top + 0.07f, -0.77f), V(0.4f, 0.08f, 0.02f), s.Glass);
            Parts.NanoArm(b, s, V(0, top, 0.32f), 1.0f);
            b.Rod(V(0.36f, top, 0.7f), V(0.38f, top + 0.5f, 0.74f), 0.02f, 0.015f, 4, s.Metal);
            b.Sphere(V(0.38f, top + 0.5f, 0.74f), V(0.04f, 0.04f, 0.04f), 6, 3, s.Glow);
        }
        else
        {
            float top = CrucibleChassis(b, s, 0.8f, -0.92f, 1.05f, 0.42f, 0.58f);
            using (b.Push(V(0, 0.3f, -1.12f), V(Rad(-14), 0, 0)))
            {
                b.Box(Vector3.Zero, V(1.72f, 0.52f, 0.08f), s.Team);
                b.Box(V(0, -0.24f, -0.05f), V(1.72f, 0.06f, 0.06f), s.Metal);
            }
            foreach (float sx in Sides)
                b.Beam(V(sx * 0.5f, 0.32f, -1.08f), V(sx * 0.5f, 0.44f, -0.6f), 0.1f, 0.1f, s.Metal);
            b.Box(V(-0.22f, top + 0.12f, 0.6f), V(0.4f, 0.24f, 0.5f), s.Hull, s.Team);
            Parts.Stack(b, s, V(0.34f, top, 0.72f), 0.07f, 0.36f);
            Parts.NanoArm(b, s, V(0.05f, top, -0.05f), 0.92f);
        }
    }

    // ---- Advanced Assembly -----------------------------------------------------------------------

    /// <summary>Advanced builder: bigger hull with two nanolathe cranes.</summary>
    public static void BuilderAdv(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        if (s.IsLattice)
        {
            float top = LatticeChassis(b, s, 0.82f, -1.15f, 1.1f, 0.36f, 0.5f, 0.32f);
            Parts.Slab(b, s, V(0, top + 0.12f, 0.42f), V(0.66f, 0.24f, 0.62f), s.Hull, s.Team);
            b.Ring(V(0, top + 0.3f, 0.42f), 0.24f, 0.18f, 0.04f, 8, s.Glow);
            foreach (float sx in Sides)
                Parts.NanoArm(b, s, V(sx * 0.3f, top, -0.2f), 0.85f, sx * -0.3f);
            Parts.Dish(b, s, V(0, top + 0.58f, 0.62f), V(0, 1, 0.6f), 0.18f, s.Team);
            b.Rod(V(0, top + 0.24f, 0.62f), V(0, top + 0.52f, 0.62f), 0.03f, 0.03f, 4, s.Metal);
        }
        else
        {
            float top = CrucibleChassis(b, s, 0.88f, -1.08f, 1.1f, 0.46f, 0.62f, 0.36f);
            b.Box(V(0, top + 0.18f, 0.5f), V(0.84f, 0.36f, 0.7f), s.Hull, s.Team);
            Parts.Rivets(b, s, V(-0.36f, top + 0.36f, 0.2f), V(0.36f, top + 0.36f, 0.2f), 4, 0.05f);
            foreach (float sx in Sides)
            {
                Parts.NanoArm(b, s, V(sx * 0.3f, top, -0.35f), 0.82f, sx * -0.25f);
                Parts.Stack(b, s, V(sx * 0.28f, top + 0.36f, 0.72f), 0.07f, 0.34f);
            }
        }
    }

    /// <summary>Heavy assault tank: big, blocky, twin barrels and a coaxial laser.</summary>
    public static void HeavyAssault(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        if (s.IsLattice)
        {
            float top = LatticeChassis(b, s, 0.86f, -1.16f, 1.1f, 0.42f, 0.56f, 0.36f);
            foreach (float sx in Sides)
                b.Frustum(V(sx * 0.62f, 0.42f, 0), V2(0.1f, 1.7f), V2(0.04f, 1.3f), 0.16f, s.Hull, s.Team);
            MeshKit t = rig.AddTurret(V(0, top, 0.14f));
            LatticeTurret(t, s, 0.46f, 0.5f, 0.52f, 0.28f);
            foreach (float sx in Sides)
                Parts.Barrel(t, s, V(sx * 0.11f, 0.14f, -0.36f), V(sx * 0.11f, 0.15f, -1.3f), 0.055f);
            Parts.Emitter(t, s, V(0.34f, 0.24f, -0.3f), V(0.34f, 0.24f, -0.7f), 0.035f);
        }
        else
        {
            float top = CrucibleChassis(b, s, 0.92f, -1.12f, 1.1f, 0.5f, 0.68f, 0.38f);
            b.Frustum(V(0, 0.3f, -1.02f), V2(1.2f, 0.12f), V2(1.0f, 0.1f), 0.36f, s.HullDark, s.Team, V2(0, 0.08f));
            foreach (float sx in Sides)
                Parts.Stack(b, s, V(sx * 0.3f, top, 0.86f), 0.08f, 0.32f);
            MeshKit t = rig.AddTurret(V(0, top, 0.1f));
            t.Box(V(0, 0.18f, 0.06f), V(0.96f, 0.36f, 0.96f), s.Hull, s.Team);
            t.Box(V(0, 0.17f, -0.44f), V(0.56f, 0.28f, 0.12f), s.Mech);
            Parts.Rivets(t, s, V(-0.4f, 0.36f, -0.36f), V(0.4f, 0.36f, -0.36f), 5, 0.05f);
            Parts.Rivets(t, s, V(-0.4f, 0.36f, 0.48f), V(0.4f, 0.36f, 0.48f), 5, 0.05f);
            foreach (float sx in Sides)
                Parts.Barrel(t, s, V(sx * 0.14f, 0.18f, -0.46f), V(sx * 0.14f, 0.18f, -1.18f), 0.09f);
            Parts.Emitter(t, s, V(0.4f, 0.4f, -0.1f), V(0.4f, 0.4f, -0.52f), 0.04f);
            t.Box(V(0.4f, 0.4f, 0.0f), V(0.14f, 0.12f, 0.24f), s.HullDark);
        }
    }

    /// <summary>Long-range artillery: a very long barrel angled up, braced by outriggers or a spade.</summary>
    public static void LongArtillery(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        if (s.IsLattice)
        {
            float top = LatticeChassis(b, s, 0.72f, -1.0f, 1.15f, 0.34f, 0.44f);
            foreach (float sx in Sides)
            foreach (float z in new[] { -0.7f, 0.85f })
            {
                b.Beam(V(sx * 0.6f, 0.3f, z), V(sx * 0.98f, 0.04f, z + 0.1f), 0.08f, 0.1f, s.Hull);
                b.Box(V(sx * 0.98f, 0.03f, z + 0.1f), V(0.18f, 0.06f, 0.2f), s.Mech);
            }
            MeshKit t = rig.AddTurret(V(0, top, 0.42f));
            Parts.Slab(t, s, V(0, 0.08f, 0.1f), V(0.64f, 0.16f, 0.8f), s.Hull, s.Team);
            t.Box(V(0, 0.2f, 0.42f), V(0.4f, 0.22f, 0.24f), s.HullDark, s.Team);
            foreach (float sx in Sides)
                t.Frustum(V(sx * 0.15f, 0.14f, 0), V2(0.06f, 0.5f), V2(0.06f, 0.2f), 0.34f, s.Hull, s.Team, V2(0, -0.08f));
            Vector3 root = V(0, 0.36f, 0.08f), dir = Elevated(32);
            t.Rod(root - dir * 0.3f, root + dir * 2.06f, 0.072f, 0.058f, 6, s.Metal);
            foreach (float d in new[] { 0.3f, 0.7f, 1.1f, 1.5f })
                t.Rod(root + dir * d, root + dir * (d + 0.1f), 0.1f, 0.095f, 6, s.Team);
            t.Rod(root + dir * 1.98f, root + dir * 2.12f, 0.085f, 0.075f, 6, s.Mech, s.Glow);
        }
        else
        {
            float top = CrucibleChassis(b, s, 0.86f, -1.04f, 1.1f, 0.46f, 0.6f);
            b.Beam(V(0, 0.42f, 1.0f), V(0, 0.06f, 1.24f), 0.7f, 0.1f, s.Metal);
            b.Box(V(0, 0.05f, 1.24f), V(0.8f, 0.1f, 0.16f), s.Mech);
            Parts.Stack(b, s, V(-0.32f, top, -0.66f), 0.07f, 0.3f);
            MeshKit t = rig.AddTurret(V(0, top, 0.36f));
            t.Box(V(0, 0.2f, 0.12f), V(0.74f, 0.4f, 0.84f), s.Hull, s.Team);
            Parts.Rivets(t, s, V(-0.3f, 0.4f, 0.48f), V(0.3f, 0.4f, 0.48f), 4, 0.05f);
            Vector3 root = V(0, 0.36f, -0.12f), dir = Elevated(30);
            Parts.Barrel(t, s, root - dir * 0.1f, root + dir * 1.95f, 0.12f);
            foreach (float sx in Sides)
                t.Rod(root + V(sx * 0.13f, -0.06f, 0.06f), root + V(sx * 0.13f, -0.06f, 0.06f) + dir * 0.7f, 0.045f, 0.045f, 6, s.Mech);
            t.Rod(root + dir * 0.2f, root + dir * 0.42f, 0.16f, 0.16f, 6, s.Team);
        }
    }

    /// <summary>Lattice angular turret shell (lofted, team top) around the turret origin.</summary>
    private static void LatticeTurret(MeshKit t, Style s, float hw, float zFront, float zBack, float h)
    {
        Parts.Hull(t, new Parts.Section[]
        {
            new(-zFront, hw * 0.5f, hw * 0.3f, 0, h * 0.42f, h * 0.66f),
            new(-zFront * 0.45f, hw, hw * 0.68f, 0, h * 0.58f, h),
            new(zBack * 0.75f, hw, hw * 0.68f, 0, h * 0.58f, h),
            new(zBack, hw * 0.72f, hw * 0.44f, 0, h * 0.5f, h * 0.84f),
        }, s.Hull, s.Team, s.Hull, s.Mech);
    }
}
