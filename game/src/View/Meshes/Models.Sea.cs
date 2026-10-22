using ContingencyZero.Sim.Data;
using Godot;

namespace ContingencyZero.Game.View.Meshes;

/// <summary>
/// Ships. The waterline is y = 0: hulls rise a little above it and dip a little below.
/// Lattice hulls are sleek with a needle bow and tiered ceramic superstructures; Crucible hulls
/// are blunt, boxy and riveted with smokestacks. A team stripe runs along every hull's shoulder.
/// The main gun is the turret part; secondary guns are fixed to the body.
/// </summary>
internal static partial class Models
{
    /// <summary>A ship hull from bow (-Z) to stern, deck at <paramref name="deck"/>, keel at <paramref name="keel"/> (negative).</summary>
    private static void ShipHull(MeshKit b, Style s, float hw, float deck, float keel, float bowZ = -1.2f, float sternZ = 1.15f)
    {
        if (s.IsLattice)
        {
            Parts.Hull(b, new Parts.Section[]
            {
                new(bowZ, 0.02f, 0.015f, deck * 0.5f, deck + 0.05f, deck + 0.07f),
                new(bowZ + 0.4f, hw * 0.72f, hw * 0.64f, keel, deck * 0.6f, deck + 0.02f),
                new(bowZ + 0.95f, hw, hw * 0.9f, keel, deck * 0.6f, deck),
                new(sternZ - 0.2f, hw, hw * 0.9f, keel, deck * 0.6f, deck),
                new(sternZ, hw * 0.86f, hw * 0.76f, keel * 0.4f, deck * 0.6f, deck),
            }, s.Hull, s.HullDark, s.Team, s.Mech);
        }
        else
        {
            Parts.Hull(b, new Parts.Section[]
            {
                new(bowZ, hw * 0.42f, hw * 0.36f, keel * 0.3f, deck + 0.02f, deck + 0.06f),
                new(bowZ + 0.3f, hw * 0.92f, hw * 0.86f, keel, deck * 0.66f, deck + 0.02f),
                new(sternZ - 0.1f, hw, hw * 0.92f, keel, deck * 0.66f, deck),
                new(sternZ, hw, hw * 0.92f, keel * 0.6f, deck * 0.66f, deck),
            }, s.Hull, s.HullDark, s.Team, s.Mech);
            Parts.Rivets(b, s, V(-hw * 0.8f, deck, bowZ + 0.45f), V(-hw * 0.8f, deck, sternZ - 0.1f), 6, 0.035f);
            Parts.Rivets(b, s, V(hw * 0.8f, deck, bowZ + 0.45f), V(hw * 0.8f, deck, sternZ - 0.1f), 6, 0.035f);
        }
    }

    /// <summary>Patrol boat: Lattice "Skiff", Crucible "Cutter". Gun turret forward, AA pod aft.</summary>
    public static void PatrolBoat(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        MeshKit t;
        if (s.IsLattice)
        {
            ShipHull(b, s, 0.4f, 0.24f, -0.16f);
            Parts.Slab(b, s, V(0, 0.38f, 0.12f), V(0.42f, 0.28f, 0.52f), s.Hull, s.Team);
            b.Box(V(0, 0.42f, -0.145f), V(0.32f, 0.07f, 0.02f), s.Glass);
            b.Rod(V(0, 0.5f, 0.24f), V(0, 0.98f, 0.28f), 0.022f, 0.016f, 4, s.Metal);
            b.Box(V(0, 0.82f, 0.27f), V(0.3f, 0.025f, 0.03f), s.Metal);
            b.Sphere(V(0, 0.98f, 0.28f), V(0.035f, 0.035f, 0.035f), 6, 3, s.Glow);
            Parts.MissilePod(b, s, V(0, 0.36f, 0.74f), V(0.28f, 0.16f, 0.26f), 45, 2, 1, s.Team, Mathf.Pi);
            t = rig.AddTurret(V(0, 0.24f, -0.55f));
            LatticeTurret(t, s, 0.17f, 0.2f, 0.2f, 0.14f);
            Parts.Barrel(t, s, V(0, 0.08f, -0.14f), V(0, 0.08f, -0.62f), 0.032f);
        }
        else
        {
            ShipHull(b, s, 0.44f, 0.26f, -0.16f);
            b.Box(V(0, 0.42f, 0.14f), V(0.5f, 0.32f, 0.52f), s.Hull, s.Team);
            b.Box(V(0, 0.48f, -0.125f), V(0.4f, 0.06f, 0.02f), s.GlowDim);
            Parts.Stack(b, s, V(0.12f, 0.58f, 0.32f), 0.06f, 0.3f);
            Parts.MissilePod(b, s, V(0, 0.38f, 0.8f), V(0.36f, 0.16f, 0.24f), 40, 3, 1, s.Team, Mathf.Pi);
            t = rig.AddTurret(V(0, 0.26f, -0.58f));
            t.Box(V(0, 0.08f, 0.02f), V(0.34f, 0.16f, 0.36f), s.Hull, s.Team);
            Parts.Barrel(t, s, V(0, 0.08f, -0.16f), V(0, 0.08f, -0.62f), 0.045f);
        }
    }

    /// <summary>Destroyer: Lattice "Reef", Crucible "Hammerhead". Long hull, big forward gun.</summary>
    public static void Destroyer(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        MeshKit t;
        if (s.IsLattice)
        {
            ShipHull(b, s, 0.32f, 0.18f, -0.12f);
            Parts.Slab(b, s, V(0, 0.28f, 0.18f), V(0.38f, 0.2f, 0.74f), s.Hull, s.Team);
            Parts.Slab(b, s, V(0, 0.43f, 0.06f), V(0.28f, 0.12f, 0.36f), s.Hull, s.Team);
            b.Box(V(0, 0.45f, -0.125f), V(0.22f, 0.05f, 0.02f), s.Glass);
            b.Rod(V(0, 0.49f, 0.14f), V(0, 0.86f, 0.18f), 0.018f, 0.012f, 4, s.Metal);
            Parts.Dish(b, s, V(0, 0.68f, 0.12f), V(0, 0.3f, -1), 0.08f, s.Team);
            Parts.MissilePod(b, s, V(0, 0.27f, 0.8f), V(0.22f, 0.12f, 0.2f), 45, 2, 1, s.Team, Mathf.Pi);
            foreach (float sx in Sides)
                b.Box(V(sx * 0.2f, 0.24f, 0.18f), V(0.02f, 0.03f, 0.5f), s.Glow);
            t = rig.AddTurret(V(0, 0.18f, -0.58f));
            LatticeTurret(t, s, 0.16f, 0.18f, 0.2f, 0.12f);
            Parts.Barrel(t, s, V(0, 0.07f, -0.14f), V(0, 0.08f, -0.66f), 0.03f);
        }
        else
        {
            ShipHull(b, s, 0.34f, 0.2f, -0.12f);
            b.Box(V(0, 0.32f, 0.2f), V(0.42f, 0.24f, 0.62f), s.Hull, s.Team);
            b.Box(V(0, 0.49f, 0.06f), V(0.3f, 0.1f, 0.3f), s.HullDark, s.Hull);
            b.Box(V(0, 0.5f, -0.095f), V(0.24f, 0.04f, 0.02f), s.GlowDim);
            foreach (float z in new[] { 0.34f, 0.48f })
                Parts.Stack(b, s, V(0, 0.44f, z), 0.05f, 0.24f);
            b.Box(V(0, 0.26f, 0.82f), V(0.2f, 0.1f, 0.2f), s.HullDark, s.Team);
            Parts.Barrel(b, s, V(0, 0.27f, 0.9f), V(0, 0.27f, 1.12f), 0.025f, brake: false);
            t = rig.AddTurret(V(0, 0.2f, -0.56f));
            t.Box(V(0, 0.07f, 0.02f), V(0.3f, 0.14f, 0.34f), s.Hull, s.Team);
            Parts.Rivets(t, s, V(-0.1f, 0.14f, -0.08f), V(0.1f, 0.14f, -0.08f), 3, 0.03f);
            Parts.Barrel(t, s, V(0, 0.07f, -0.15f), V(0, 0.07f, -0.66f), 0.045f);
        }
    }

    /// <summary>Battleship: Lattice "Monolith", Crucible "Leviathan". Triple-gun main turret, fixed aft turret, tall superstructure.</summary>
    public static void Battleship(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        MeshKit t;
        if (s.IsLattice)
        {
            ShipHull(b, s, 0.3f, 0.14f, -0.1f);
            Parts.Slab(b, s, V(0, 0.22f, 0.12f), V(0.36f, 0.16f, 0.64f), s.Hull, s.Team);
            Parts.Slab(b, s, V(0, 0.36f, 0.06f), V(0.26f, 0.14f, 0.36f), s.Hull, s.Team);
            Parts.Slab(b, s, V(0, 0.48f, 0.02f), V(0.16f, 0.1f, 0.18f), s.Hull, s.Hull);
            b.Box(V(0, 0.49f, -0.072f), V(0.12f, 0.03f, 0.01f), s.Glass);
            b.Rod(V(0, 0.53f, 0.04f), V(0, 0.86f, 0.06f), 0.014f, 0.01f, 4, s.Metal);
            b.Sphere(V(0, 0.86f, 0.06f), V(0.025f, 0.025f, 0.025f), 6, 3, s.Glow);
            foreach (float sx in Sides)
            {
                b.Box(V(sx * 0.2f, 0.2f, 0.1f), V(0.02f, 0.025f, 0.5f), s.Glow);
                Parts.Emitter(b, s, V(sx * 0.2f, 0.26f, -0.1f), V(sx * 0.24f, 0.26f, -0.3f), 0.018f);
            }
            using (b.Push(V(0, 0.14f, 0.66f), V(0, Mathf.Pi, 0)))
            {
                LatticeTurret(b, s, 0.15f, 0.16f, 0.18f, 0.1f);
                foreach (float x in new[] { -0.05f, 0.05f })
                    b.Rod(V(x, 0.06f, -0.12f), V(x, 0.06f, -0.42f), 0.018f, 0.016f, 6, s.Metal, s.Mech);
            }
            t = rig.AddTurret(V(0, 0.14f, -0.5f));
            LatticeTurret(t, s, 0.18f, 0.2f, 0.22f, 0.12f);
            foreach (float x in new[] { -0.065f, 0f, 0.065f })
                Parts.Barrel(t, s, V(x, 0.07f, -0.16f), V(x, 0.08f, -0.64f), 0.022f);
        }
        else
        {
            ShipHull(b, s, 0.34f, 0.15f, -0.1f);
            b.Box(V(0, 0.25f, 0.12f), V(0.42f, 0.2f, 0.62f), s.Hull, s.Team);
            b.Box(V(0, 0.41f, 0.02f), V(0.3f, 0.12f, 0.36f), s.HullDark, s.Team);
            b.Box(V(0, 0.42f, -0.162f), V(0.24f, 0.035f, 0.02f), s.GlowDim);
            foreach (float z in new[] { 0.28f, 0.4f })
                Parts.Stack(b, s, V(0, 0.35f, z), 0.055f, 0.3f);
            foreach (float sx in Sides)
                Parts.Emitter(b, s, V(sx * 0.24f, 0.22f, -0.1f), V(sx * 0.27f, 0.22f, -0.3f), 0.02f);
            using (b.Push(V(0, 0.15f, 0.68f), V(0, Mathf.Pi, 0)))
            {
                b.Box(V(0, 0.06f, 0.02f), V(0.26f, 0.12f, 0.28f), s.Hull, s.Team);
                foreach (float x in new[] { -0.05f, 0.05f })
                    b.Rod(V(x, 0.06f, -0.12f), V(x, 0.06f, -0.4f), 0.022f, 0.02f, 6, s.Metal, s.Mech);
            }
            t = rig.AddTurret(V(0, 0.15f, -0.5f));
            t.Box(V(0, 0.07f, 0.02f), V(0.32f, 0.14f, 0.36f), s.Hull, s.Team);
            Parts.Rivets(t, s, V(-0.12f, 0.14f, 0.14f), V(0.12f, 0.14f, 0.14f), 3, 0.03f);
            foreach (float x in new[] { -0.075f, 0f, 0.075f })
                Parts.Barrel(t, s, V(x, 0.07f, -0.16f), V(x, 0.07f, -0.62f), 0.026f);
        }
    }

    /// <summary>Naval constructor: Lattice "Tender" with a midships nanolathe; Crucible "Dredger" barge with a bucket ladder.</summary>
    public static void BuilderSea(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        if (s.IsLattice)
        {
            ShipHull(b, s, 0.4f, 0.22f, -0.14f);
            Parts.Slab(b, s, V(0, 0.38f, 0.6f), V(0.42f, 0.32f, 0.4f), s.Hull, s.Team);
            b.Box(V(0, 0.44f, 0.395f), V(0.32f, 0.07f, 0.02f), s.Glass);
            b.Ring(V(0, 0.24f, -0.12f), 0.24f, 0.18f, 0.04f, 8, s.Glow);
            Parts.NanoArm(b, s, V(0, 0.22f, -0.12f), 0.62f);
            Parts.Slab(b, s, V(0, 0.25f, -0.66f), V(0.34f, 0.06f, 0.3f), s.Hull, s.Team);
        }
        else
        {
            ShipHull(b, s, 0.44f, 0.24f, -0.14f);
            b.Box(V(0, 0.4f, 0.64f), V(0.48f, 0.32f, 0.42f), s.Hull, s.Team);
            b.Box(V(0, 0.46f, 0.425f), V(0.38f, 0.05f, 0.02f), s.GlowDim);
            Parts.Stack(b, s, V(-0.14f, 0.56f, 0.76f), 0.05f, 0.26f);
            // Bucket ladder over the bow.
            b.Beam(V(0, 0.34f, -0.55f), V(0, -0.1f, -1.25f), 0.16f, 0.08f, s.Metal);
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = V(0, 0.34f, -0.55f).Lerp(V(0, -0.1f, -1.25f), 0.15f + i * 0.22f);
                b.Box(p + V(0, 0.06f, 0), V(0.22f, 0.07f, 0.09f), s.Accent);
            }
            foreach (float sx in Sides)
                b.Beam(V(sx * 0.3f, 0.24f, -0.7f), V(0, 0.52f, -0.62f), 0.04f, 0.04f, s.HullDark);
            b.Box(V(0, 0.3f, -0.1f), V(0.5f, 0.12f, 0.4f), s.Team, s.Mech);
            Parts.NanoArm(b, s, V(0.15f, 0.24f, 0.28f), 0.6f);
        }
    }
}
