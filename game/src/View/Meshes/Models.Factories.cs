using ContingencyZero.Sim.Data;
using Godot;

namespace ContingencyZero.Game.View.Meshes;

/// <summary>
/// Factories: big buildings whose bay opens towards +Z (south, where units roll out), with a
/// visible build pad ringed by faction glow and nanolathe emitters aimed at it.
/// Lattice: bevelled ceramic towers, arches and light. Crucible: steel halls, sawtooth roofs,
/// lintels, stacks and rivets.
/// </summary>
internal static partial class Models
{
    /// <summary>Floor slab, round (Lattice) or square (Crucible) build pad with a glow rim, and a glowing threshold at the door.</summary>
    private static void FactoryFloor(MeshKit b, Style s, float e, float padZ, float padR, float doorHalf)
    {
        if (s.IsLattice)
        {
            Parts.Slab(b, s, V(0, 0.2f, 0), V(e * 2, 0.4f, e * 2), s.HullDark, s.HullDark, 0.04f);
            b.Cylinder(V(0, 0.4f, padZ), padR, padR, 0.12f, 8, s.Mech);
            b.Ring(V(0, 0.52f, padZ), padR + 0.25f, padR - 0.45f, 0.1f, 8, s.Glow);
        }
        else
        {
            b.Box(V(0, 0.25f, 0), V(e * 2, 0.5f, e * 2), s.HullDark, s.Mech);
            b.Box(V(0, 0.56f, padZ), V(padR * 1.8f, 0.12f, padR * 1.8f), s.HullDark);
            foreach (float sd in Sides)
            {
                b.Box(V(0, 0.64f, padZ + sd * padR * 0.9f), V(padR * 1.8f, 0.06f, 0.3f), s.Glow);
                b.Box(V(sd * padR * 0.9f, 0.64f, padZ), V(0.3f, 0.06f, padR * 1.8f), s.Glow);
            }
        }
        b.Box(V(0, 0.45f, e - 0.3f), V(doorHalf * 2, 0.08f, 0.35f), s.Glow);
    }

    // ---- Bot Lab ---------------------------------------------------------------------------------

    public static void FactoryBot(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        float e = rig.Half;
        FactoryFloor(b, s, e, 2.0f, 4.8f, 5.0f);
        if (s.IsLattice)
        {
            foreach (float sx in Sides)
            {
                Parts.Slab(b, s, V(sx * 8.4f, 5.4f, -0.6f), V(5.6f, 10.0f, 21.0f), s.Hull, s.Team, 0.28f);
                b.Box(V(sx * 8.4f, 7.6f, 9.95f), V(5.0f, 1.6f, 0.3f), s.Team);
                b.Box(V(sx * 11.25f, 5.0f, -0.6f), V(0.2f, 6.0f, 1.0f), s.Glow);
                for (int i = 0; i < 2; i++)
                    Parts.NanoArm(b, s, V(sx * 5.6f, 2.4f + i * 3.2f, 0.5f + i * 2.4f), 2.4f, sx * Mathf.Pi / 2);
                b.Rod(V(sx * 8.4f, 10.2f, -6f), V(sx * 8.4f, 13.4f, -6f), 0.18f, 0.12f, 4, s.Metal);
                b.Sphere(V(sx * 8.4f, 13.4f, -6f), V(0.35f, 0.35f, 0.35f), 6, 3, s.Glow);
            }
            Parts.Slab(b, s, V(0, 4.4f, -8.8f), V(11.4f, 8.8f, 4.4f), s.Hull, s.Team, 0.2f);
            b.Box(V(0, 9.3f, -3.0f), V(11.4f, 1.0f, 2.6f), s.HullDark, s.Team);
            b.Box(V(0, 8.75f, -3.0f), V(9.0f, 0.1f, 0.6f), s.Glow);
            b.Box(V(0, 4.4f, -6.55f), V(6.0f, 0.3f, 0.1f), s.Glow);
        }
        else
        {
            foreach (float sx in Sides)
            {
                b.Box(V(sx * 8.5f, 4.25f, 0), V(5.8f, 7.5f, 22.4f), s.Hull, s.HullDark);
                b.Frustum(V(sx * 8.5f, 8.0f, 0), V2(5.8f, 22.4f), V2(2.0f, 22.4f), 1.8f, s.Team, s.Team);
                b.Box(V(sx * 8.5f, 1.2f, 11.25f), V(5.8f, 1.4f, 0.12f), s.Team);
                Parts.Rivets(b, s, V(sx * 5.65f, 8.0f, -10f), V(sx * 5.65f, 8.0f, 10f), 6, 0.35f);
                Parts.NanoArm(b, s, V(sx * 5.5f, 3.4f, 3.0f), 3.0f, sx * Mathf.Pi / 2);
                Parts.Stack(b, s, V(sx * 2.8f, 10.8f, -8.6f), 1.0f, 5.0f);
            }
            b.Box(V(0, 5.4f, -8.6f), V(11.2f, 10.8f, 5.0f), s.Hull, s.HullDark);
            b.Box(V(0, 7.7f, 10.3f), V(11.2f, 1.6f, 1.8f), s.HullDark, s.Team);
            foreach (float x in new[] { -3.5f, 0f, 3.5f })
                b.Box(V(x, 6.85f, 10.3f), V(1.0f, 0.1f, 0.6f), s.Glow);
            b.Box(V(0, 5.0f, -6.05f), V(8.0f, 6.0f, 0.1f), s.Mech);
            b.Box(V(0, 3.6f, -6.0f), V(6.0f, 0.4f, 0.1f), s.GlowDim);
        }
    }

    // ---- Vehicle Plant ---------------------------------------------------------------------------

    public static void FactoryVehicle(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        float e = rig.Half;
        FactoryFloor(b, s, e, 2.5f, 5.0f, 6.0f);
        if (s.IsLattice)
        {
            LatticeHall(b, s, e, 6.2f, 9.2f);
        }
        else
        {
            CrucibleWorks(b, s, e, 8.6f, 3);
            Parts.Stack(b, s, V(8.0f, 9.0f, -9.0f), 1.1f, 6.0f);
        }
    }

    /// <summary>Lattice hall: two low bevelled side wings and a gabled roof over the back, open at the front.</summary>
    private static void LatticeHall(MeshKit b, Style s, float e, float wingH, float roofTop)
    {
        foreach (float sx in Sides)
        {
            Parts.Slab(b, s, V(sx * 8.8f, wingH / 2 + 0.2f, -0.4f), V(4.8f, wingH, 21.6f), s.Hull, s.Team, 0.3f);
            b.Box(V(sx * 6.35f, wingH * 0.55f, 2.0f), V(0.12f, 0.3f, 14.0f), s.Glow);
            Parts.NanoArm(b, s, V(sx * 6.4f, wingH - 0.2f, 3.5f), 2.6f, sx * Mathf.Pi / 2 * 0.9f);
        }
        b.Box(V(0, wingH / 2, -9.6f), V(13.0f, wingH, 3.0f), s.Hull, s.HullDark);
        using (b.Push(V(0, 0, -4.4f), V(0, Mathf.Pi / 2, 0)))
            Parts.Hull(b, new Parts.Section[]
            {
                new(-10.8f, 6.2f, 4.0f, wingH - 0.4f, wingH + 1.2f, roofTop),
                new(10.8f, 6.2f, 4.0f, wingH - 0.4f, wingH + 1.2f, roofTop),
            }, s.Hull, s.Team, s.HullDark, s.Mech);
        b.Box(V(0, wingH + 0.6f, 1.6f), V(14.0f, 0.3f, 0.3f), s.Glow);
    }

    /// <summary>Crucible works: thick side walls, back wall, sawtooth roof with lit windows, front lintel.</summary>
    private static void CrucibleWorks(MeshKit b, Style s, float e, float wallH, int teeth)
    {
        foreach (float sx in Sides)
        {
            b.Box(V(sx * 9.9f, wallH / 2, 0), V(3.2f, wallH, e * 2 - 0.2f), s.Hull, s.Team);
            Parts.Rivets(b, s, V(sx * 9.9f, wallH, -9f), V(sx * 9.9f, wallH, 9f), 6, 0.35f);
            b.Box(V(sx * 9.9f, 1.4f, e - 0.05f), V(3.2f, 1.4f, 0.12f), s.Team);
            Parts.NanoArm(b, s, V(sx * 8.2f, 3.2f, 3.0f), 3.0f, sx * Mathf.Pi / 2);
        }
        b.Box(V(0, wallH / 2, -9.8f), V(16.6f, wallH, 3.4f), s.Hull, s.HullDark);
        b.Box(V(0, wallH + 0.25f, -3.2f), V(16.6f, 0.5f, 16.4f), s.HullDark, s.HullDark);
        float span = 16.4f / teeth;
        for (int i = 0; i < teeth; i++)
        {
            float z0 = -11.4f + i * span, z1 = z0 + span;
            Vector2[] tooth = { new(z0, wallH + 0.5f), new(z1, wallH + 0.5f), new(z1, wallH + 3.0f) };
            b.ExtrudeX(tooth, -8.2f, 8.2f, s.Hull, s.HullDark, j => j switch { 1 => s.GlowDim, 2 => s.Team, _ => null });
        }
        b.Box(V(0, wallH - 0.6f, e - 0.7f), V(16.6f, 1.4f, 1.4f), s.HullDark, s.Team);
        foreach (float x in new[] { -5f, 0f, 5f })
            b.Box(V(x, wallH - 1.35f, e - 0.7f), V(1.2f, 0.1f, 0.6f), s.Glow);
    }

    // ---- Aircraft Plant --------------------------------------------------------------------------

    public static void FactoryAir(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        float e = rig.Half;
        if (s.IsLattice)
        {
            Parts.Slab(b, s, V(0, 0.2f, 0), V(e * 2, 0.4f, e * 2), s.HullDark, s.HullDark, 0.04f);
            b.Cylinder(V(0, 0.4f, 1.6f), 9.6f, 9.0f, 2.6f, 8, s.Hull, s.Hull);
            b.Ring(V(0, 3.05f, 1.6f), 8.4f, 6.6f, 0.1f, 8, s.Team);
            b.Cylinder(V(0, 3.0f, 1.6f), 2.2f, 2.2f, 0.12f, 8, s.Glow);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.Tau / 8 + Mathf.Pi / 8;
                b.Box(V(Mathf.Cos(a) * 8.7f, 3.06f, 1.6f + Mathf.Sin(a) * 8.7f), V(0.5f, 0.12f, 0.5f), s.Glow);
            }
            Vector2[] ramp = { new(8.0f, 0.4f), new(11.2f, 0.4f), new(8.0f, 3.0f) };
            b.ExtrudeX(ramp, -3.4f, 3.4f, s.Hull, s.HullDark, j => j == 2 ? s.Team : null);
            b.Cylinder(V(-8.4f, 0.4f, -8.4f), 2.2f, 1.5f, 9.0f, 6, s.Hull);
            b.Cylinder(V(-8.4f, 9.4f, -8.4f), 2.4f, 2.8f, 1.6f, 8, s.Glass, s.Team);
            b.Cylinder(V(-8.4f, 9.6f, -8.4f), 2.5f, 2.5f, 0.3f, 8, s.Glow);
            Parts.Dish(b, s, V(-8.4f, 12.0f, -8.4f), V(0.4f, 1, 0.4f), 1.2f, s.Hull);
            Parts.Slab(b, s, V(5.0f, 3.2f, -8.8f), V(11.6f, 6.4f, 4.6f), s.Hull, s.Team, 0.2f);
            foreach (float sx in Sides)
                Parts.NanoArm(b, s, V(sx * 6.0f, 3.0f, -5.4f), 2.6f, sx * 0.6f);
        }
        else
        {
            b.Box(V(0, 0.25f, 0), V(e * 2, 0.5f, e * 2), s.HullDark, s.Mech);
            Vector2[] arch = { new(-9.6f, 0.5f), new(9.6f, 0.5f), new(8.6f, 5.4f), new(4.2f, 9.2f), new(-4.2f, 9.2f), new(-8.6f, 5.4f) };
            b.ExtrudeZ(arch, -11.2f, 3.4f, s.Hull, s.HullDark, j => j is 2 or 4 ? s.Team : j == 3 ? s.HullDark : null);
            foreach (float z in new[] { -6f, -1f })
                Parts.Rivets(b, s, V(-3.8f, 9.2f, z), V(3.8f, 9.2f, z), 4, 0.35f);
            b.Box(V(0, 3.6f, 3.45f), V(12.0f, 6.2f, 0.1f), s.Mech);
            b.Box(V(0, 6.9f, 3.5f), V(12.0f, 0.3f, 0.15f), s.Glow);
            b.Box(V(0, 0.56f, 7.4f), V(12.0f, 0.12f, 7.6f), s.HullDark);
            foreach (float x in new[] { -5.6f, 5.6f })
                for (int i = 0; i < 4; i++)
                    b.Box(V(x, 0.66f, 4.6f + i * 1.9f), V(0.4f, 0.1f, 0.4f), s.Glow);
            b.Box(V(9.2f, 4.5f, 8.6f), V(2.6f, 8.0f, 2.6f), s.Hull, s.Team);
            b.Box(V(9.2f, 8.9f, 8.6f), V(3.4f, 1.2f, 3.4f), s.HullDark, s.Hull);
            b.Box(V(9.2f, 8.9f, 10.32f), V(2.8f, 0.4f, 0.06f), s.GlowDim);
            Parts.Stack(b, s, V(-9.4f, 0.5f, 8.6f), 0.8f, 6.0f);
            Parts.NanoArm(b, s, V(6.0f, 0.5f, 5.0f), 2.6f, Mathf.Pi / 2);
        }
    }

    // ---- Shipyard --------------------------------------------------------------------------------

    public static void FactoryShip(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        float e = rig.Half;
        if (s.IsLattice)
        {
            foreach (float sx in Sides)
            {
                Parts.Slab(b, s, V(sx * 8.9f, 1.0f, 0), V(4.4f, 4.0f, e * 2), s.Hull, s.Team, 0.15f);
                for (int i = 0; i < 5; i++)
                    b.Box(V(sx * 6.65f, 2.4f, -8f + i * 4.2f), V(0.12f, 0.3f, 1.2f), s.Glow);
            }
            Parts.Slab(b, s, V(0, 1.0f, -9.6f), V(13.4f, 4.0f, 3.8f), s.Hull, s.HullDark, 0.15f);
            foreach (float z in new[] { -3.5f, 3.5f })
            {
                foreach (float sx in Sides)
                {
                    b.Beam(V(sx * 9.2f, 3.0f, z - 1.2f), V(sx * 8.0f, 9.0f, z), 0.5f, 0.5f, s.Hull);
                    b.Beam(V(sx * 9.2f, 3.0f, z + 1.2f), V(sx * 8.0f, 9.0f, z), 0.5f, 0.5f, s.Hull);
                }
                b.Box(V(0, 9.3f, z), V(17.4f, 0.8f, 1.1f), s.Team, s.Team);
                using (b.Push(V(0, 8.9f, z), V(0, 0, Mathf.Pi), 2.2f))
                    Parts.NanoArm(b, s, Vector3.Zero, 1f);
            }
        }
        else
        {
            foreach (float sx in Sides)
            {
                b.Box(V(sx * 8.9f, 1.0f, 0), V(4.6f, 4.0f, e * 2), s.Hull, s.Team);
                Parts.Rivets(b, s, V(sx * 8.9f, 3.0f, -9f), V(sx * 8.9f, 3.0f, 9f), 6, 0.35f);
                b.Box(V(sx * 8.9f, 3.05f, e - 1.0f), V(4.6f, 0.1f, 1.2f), s.Glow);
            }
            b.Box(V(0, 1.4f, -9.7f), V(13.2f, 4.8f, 4.0f), s.HullDark, s.Hull);
            foreach (float sx in Sides)
                Parts.Stack(b, s, V(sx * 4.0f, 3.8f, -9.7f), 0.9f, 4.0f);
            foreach (float sx in Sides)
            foreach (float z in new[] { -2.5f, 5.5f })
                b.Box(V(sx * 8.4f, 6.0f, z), V(1.2f, 6.0f, 1.2f), s.HullDark);
            foreach (float z in new[] { -2.5f, 5.5f })
            {
                b.Box(V(0, 9.4f, z), V(18.0f, 1.2f, 1.4f), s.Team, s.Team);
                Parts.Rivets(b, s, V(-7f, 10.0f, z), V(7f, 10.0f, z), 5, 0.3f);
            }
            b.Box(V(2.0f, 8.4f, 1.5f), V(2.0f, 1.0f, 9.4f), s.Mech);
            using (b.Push(V(2.0f, 7.9f, 1.5f), V(0, 0, Mathf.Pi), 2.4f))
                Parts.NanoArm(b, s, Vector3.Zero, 1f);
        }
    }

    // ---- Advanced Assembly -----------------------------------------------------------------------

    public static void FactoryAdv(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        float e = rig.Half;
        FactoryFloor(b, s, e, 2.5f, 5.4f, 6.0f);
        if (s.IsLattice)
        {
            LatticeHall(b, s, e, 7.4f, 10.6f);
            Parts.Slab(b, s, V(0, 7.0f, -9.6f), V(6.0f, 14.0f, 3.6f), s.Hull, s.Team, 0.25f);
            b.Ring(V(0, 14.6f, -9.6f), 2.6f, 1.8f, 0.3f, 8, s.Glow);
            b.Box(V(0, 10.4f, 6.8f), V(18.6f, 0.9f, 1.2f), s.Team, s.Team);
            foreach (float sx in Sides)
            {
                b.Beam(V(sx * 9.0f, 7.6f, 6.8f), V(sx * 9.0f, 10.0f, 6.8f), 0.8f, 0.8f, s.Hull);
                using (b.Push(V(sx * 3.0f, 10.0f, 6.8f), V(0, 0, Mathf.Pi), 2.0f))
                    Parts.NanoArm(b, s, Vector3.Zero, 1f);
            }
        }
        else
        {
            CrucibleWorks(b, s, e, 9.6f, 3);
            foreach (float x in new[] { -5.0f, 0f, 5.0f })
                Parts.Stack(b, s, V(x, 10.1f, -9.8f), 1.0f, 6.4f);
            foreach (float sx in Sides)
                b.Frustum(V(sx * 11.0f, 0.5f, 0), V2(1.4f, 18f), V2(0.4f, 14f), 6.0f, s.HullDark, s.Team);
            b.Box(V(0, 11.0f, 7.0f), V(19.6f, 1.0f, 1.4f), s.Team, s.Team);
            foreach (float sx in Sides)
                b.Box(V(sx * 9.9f, 10.3f, 7.0f), V(1.2f, 1.6f, 1.2f), s.HullDark);
            b.Box(V(-2.0f, 10.0f, 7.0f), V(1.6f, 1.2f, 2.0f), s.Mech);
            using (b.Push(V(-2.0f, 9.4f, 7.0f), V(0, 0, Mathf.Pi), 2.2f))
                Parts.NanoArm(b, s, Vector3.Zero, 1f);
        }
    }

    // ---- Experimental Gantry ---------------------------------------------------------------------

    public static void FactoryGantry(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        float e = rig.Half;
        if (s.IsLattice)
        {
            Parts.Slab(b, s, V(0, 0.25f, 0), V(e * 2, 0.5f, e * 2), s.HullDark, s.HullDark, 0.04f);
            b.Cylinder(V(0, 0.5f, 1.5f), 9.4f, 9.4f, 0.14f, 12, s.Mech);
            b.Ring(V(0, 0.68f, 1.5f), 9.8f, 9.0f, 0.12f, 12, s.Glow);
            b.Ring(V(0, 0.68f, 1.5f), 5.8f, 5.2f, 0.12f, 12, s.GlowDim);
            b.Box(V(0, 0.55f, e - 0.4f), V(16f, 0.1f, 0.4f), s.Glow);
            foreach (float sx in Sides)
            {
                Parts.Slab(b, s, V(sx * 12.8f, 8.0f, -1.0f), V(5.0f, 16.0f, 27.0f), s.Hull, s.Team, 0.3f);
                b.Box(V(sx * 10.25f, 7.0f, 3.0f), V(0.12f, 10.0f, 0.6f), s.Glow);
                b.Box(V(sx * 10.25f, 7.0f, -5.0f), V(0.12f, 10.0f, 0.6f), s.Glow);
                b.Box(V(sx * 12.8f, 12.0f, 12.55f), V(4.4f, 2.2f, 0.3f), s.Team);
                b.Rod(V(sx * 12.8f, 16.2f, -8f), V(sx * 12.8f, 20.0f, -8f), 0.25f, 0.15f, 4, s.Metal);
                b.Sphere(V(sx * 12.8f, 20.0f, -8f), V(0.45f, 0.45f, 0.45f), 6, 3, s.Glow);
                Parts.NanoArm(b, s, V(sx * 10.2f, 3.0f, 6.0f), 3.2f, sx * Mathf.Pi / 2);
            }
            Parts.Slab(b, s, V(0, 5.0f, -13.0f), V(20.6f, 10.0f, 4.6f), s.Hull, s.Team, 0.2f);
            b.Box(V(0, 15.0f, -1.5f), V(20.6f, 2.0f, 5.0f), s.HullDark, s.Team);
            b.Box(V(0, 13.95f, -1.5f), V(16f, 0.12f, 1.0f), s.Glow);
            foreach (float x in new[] { -5f, 0f, 5f })
                using (b.Push(V(x, 14.0f, -0.5f), V(0, 0, Mathf.Pi), 3.0f))
                    Parts.NanoArm(b, s, Vector3.Zero, 1f, Mathf.Pi);
        }
        else
        {
            b.Box(V(0, 0.3f, 0), V(e * 2, 0.6f, e * 2), s.HullDark, s.Mech);
            b.Box(V(0, 0.68f, 1.5f), V(17f, 0.16f, 17f), s.HullDark);
            foreach (float sd in Sides)
            {
                b.Box(V(0, 0.8f, 1.5f + sd * 8.3f), V(17f, 0.08f, 0.4f), s.Glow);
                b.Box(V(sd * 8.3f, 0.8f, 1.5f), V(0.4f, 0.08f, 17f), s.Glow);
            }
            b.Box(V(0, 0.65f, e - 0.4f), V(17f, 0.1f, 0.4f), s.Glow);
            foreach (float sx in Sides)
            {
                b.Box(V(sx * 12.9f, 7.5f, -1.0f), V(4.8f, 15.0f, 27.0f), s.Hull, s.HullDark);
                b.Frustum(V(sx * 15.0f, 0.6f, -1.0f), V2(1.2f, 24f), V2(0.4f, 18f), 9.0f, s.HullDark, s.Team);
                b.Box(V(sx * 12.9f, 15.4f, -1.0f), V(4.8f, 0.8f, 27.0f), s.Team, s.Team);
                Parts.Rivets(b, s, V(sx * 12.9f, 15.8f, -12f), V(sx * 12.9f, 15.8f, 10f), 7, 0.4f);
                b.Box(V(sx * 12.9f, 3.0f, 12.55f), V(4.8f, 2.0f, 0.12f), s.Team);
                Parts.NanoArm(b, s, V(sx * 10.4f, 3.0f, 5.0f), 3.4f, sx * Mathf.Pi / 2);
                for (int i = 0; i < 3; i++)
                    b.Box(V(sx * 10.45f, 6.0f, -6f + i * 5f), V(0.1f, 2.0f, 1.4f), s.GlowDim);
            }
            b.Box(V(0, 6.0f, -13.2f), V(20.6f, 12.0f, 4.4f), s.Hull, s.HullDark);
            foreach (float x in new[] { -7.5f, -2.5f, 2.5f, 7.5f })
                Parts.Stack(b, s, V(x, 12.0f, -13.2f), 1.0f, 5.5f);
            foreach (float z in new[] { -4.0f, 1.0f })
                b.Box(V(0, 15.0f, z), V(21f, 1.4f, 1.4f), s.HullDark, s.Team);
            for (int i = 0; i < 5; i++)
                b.Beam(V(-10f + i * 5f, 15.0f, -4.0f), V(-7.5f + i * 5f, 15.0f, 1.0f), 0.4f, 0.4f, s.Mech);
            b.Box(V(0, 14.0f, -1.5f), V(3.0f, 1.6f, 3.0f), s.Mech, s.Team);
            using (b.Push(V(0, 13.2f, -1.5f), V(0, 0, Mathf.Pi), 3.2f))
                Parts.NanoArm(b, s, Vector3.Zero, 1f, Mathf.Pi);
        }
    }
}
