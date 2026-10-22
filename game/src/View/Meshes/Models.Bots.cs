using ContingencyZero.Sim.Data;
using Godot;

namespace ContingencyZero.Game.View.Meshes;

/// <summary>
/// Role builders. Each takes a rig, the faction style and the unit definition, and composes the
/// body (plus optional turret / rotor). Mobile units are authored at radius 1, nose at z = -1.2.
/// This file: Commanders, walking bots and the amphibious walkers.
/// </summary>
internal static partial class Models
{
    private static Vector3 V(float x, float y, float z) => new(x, y, z);
    private static Vector2 V2(float x, float y) => new(x, y);
    private static readonly float[] Sides = MeshKit.Sides;
    private static float Rad(float deg) => Mathf.DegToRad(deg);

    /// <summary>Unit direction pitched up from -Z by <paramref name="deg"/> degrees.</summary>
    private static Vector3 Elevated(float deg) => V(0, Mathf.Sin(Rad(deg)), -Mathf.Cos(Rad(deg)));

    /// <summary>Fallback for roles without a dedicated model: a team-roofed block.</summary>
    public static void Generic(ModelRig rig, Style s, UnitDef def)
    {
        if (rig.IsStructure)
        {
            float e = rig.Half;
            Parts.Slab(rig.Body, s, V(0, 1.5f, 0), V(e * 2, 3, e * 2), s.Hull, s.Team);
            return;
        }
        Parts.Slab(rig.Body, s, V(0, 0.45f, 0), V(1.4f, 0.7f, 2.2f), s.Hull, s.Team);
        Parts.Eye(rig.Body, s, V(0, 0.55f, -1.11f), V(0.5f, 0.08f, 0.02f));
    }

    // ---- Commander -----------------------------------------------------------------------------

    /// <summary>The Commander: a big humanoid mech whose upper body (the turret) twists to aim the laser arm and Breach Cannon.</summary>
    public static void Commander(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        if (s.IsLattice)
        {
            foreach (float sx in Sides)
                Parts.Leg(b, s, V(sx * 0.24f, 0.8f, 0.04f), V(sx * 0.32f, 0, 0.02f), V(0, 0, -1), 0.13f, 0.15f, s.Team);
            Parts.Slab(b, s, V(0, 0.82f, 0.04f), V(0.6f, 0.2f, 0.42f), s.HullDark, s.Mech);

            MeshKit t = rig.AddTurret(V(0, 0.88f, 0.04f));
            t.Cylinder(V(0, -0.02f, 0), 0.2f, 0.17f, 0.14f, 8, s.Mech);
            t.Frustum(V(0, 0.1f, 0), V2(0.46f, 0.34f), V2(0.76f, 0.46f), 0.36f, s.Hull, s.Hull);
            t.Frustum(V(0, 0.16f, -0.215f), V2(0.3f, 0.06f), V2(0.5f, 0.06f), 0.26f, s.Team, s.Team);
            // Head with visor and a crest fin.
            Parts.Slab(t, s, V(0, 0.56f, -0.02f), V(0.2f, 0.16f, 0.24f), s.Hull, s.Hull);
            Parts.Eye(t, s, V(0, 0.57f, -0.14f), V(0.15f, 0.045f, 0.02f));
            t.Beam(V(0, 0.6f, 0.02f), V(0, 0.72f, 0.12f), 0.03f, 0.16f, s.Team);
            foreach (float sx in Sides)
            {
                // Shoulder pads and upper arms.
                t.Frustum(V(sx * 0.47f, 0.32f, 0.01f), V2(0.28f, 0.44f), V2(0.2f, 0.34f), 0.2f, s.Team, s.Team, V2(sx * 0.03f, 0));
                t.Beam(V(sx * 0.48f, 0.34f, 0.02f), V(sx * 0.5f, 0.08f, 0.04f), 0.11f, 0.12f, s.Mech);
                t.Beam(V(sx * 0.48f, 0.3f, 0.02f), V(sx * 0.5f, 0.14f, 0.04f), 0.15f, 0.16f, s.Hull);
                // Backpack antennae.
                t.Rod(V(sx * 0.17f, 0.46f, 0.3f), V(sx * 0.21f, 0.8f, 0.36f), 0.016f, 0.012f, 4, s.Metal);
                t.Sphere(V(sx * 0.21f, 0.8f, 0.36f), V(0.035f, 0.035f, 0.035f), 6, 3, s.Glow);
            }
            Parts.Slab(t, s, V(0, 0.3f, 0.27f), V(0.5f, 0.36f, 0.18f), s.HullDark, s.Team);
            Parts.Eye(t, s, V(0, 0.24f, 0.365f), V(0.3f, 0.05f, 0.02f));
            // Right arm: Breach Cannon.
            t.Box(V(0.5f, 0.04f, -0.12f), V(0.18f, 0.18f, 0.44f), s.HullDark, s.Team);
            t.Rod(V(0.5f, 0.04f, -0.3f), V(0.5f, 0.04f, -0.5f), 0.07f, 0.07f, 8, s.Metal);
            t.Rod(V(0.5f, 0.04f, -0.49f), V(0.5f, 0.04f, -0.56f), 0.09f, 0.09f, 8, s.Mech, s.Glow);
            // Left arm: laser and nanolathe.
            t.Box(V(-0.5f, 0.05f, -0.1f), V(0.14f, 0.14f, 0.36f), s.Hull, s.Hull);
            Parts.Emitter(t, s, V(-0.5f, 0.05f, -0.26f), V(-0.5f, 0.05f, -0.56f), 0.035f);
            t.Sphere(V(-0.5f, 0.15f, -0.18f), V(0.045f, 0.045f, 0.045f), 6, 3, s.Glow);
        }
        else
        {
            foreach (float sx in Sides)
                Parts.Leg(b, s, V(sx * 0.3f, 0.64f, 0.05f), V(sx * 0.37f, 0, 0.02f), V(0, 0, -1), 0.1f, 0.2f, s.Hull);
            b.Box(V(0, 0.67f, 0.05f), V(0.72f, 0.2f, 0.46f), s.HullDark, s.Mech);
            b.Box(V(0, 0.6f, -0.2f), V(0.38f, 0.28f, 0.06f), s.Team);

            MeshKit t = rig.AddTurret(V(0, 0.74f, 0.05f));
            t.Cylinder(V(0, -0.03f, 0), 0.26f, 0.24f, 0.12f, 6, s.Mech);
            t.Frustum(V(0, 0.07f, 0), V2(0.72f, 0.5f), V2(0.92f, 0.58f), 0.42f, s.Hull, s.Team, V2(0, -0.03f));
            Parts.Rivets(t, s, V(-0.38f, 0.5f, -0.3f), V(0.38f, 0.5f, -0.3f), 5, 0.04f);
            t.Box(V(0, 0.54f, -0.14f), V(0.28f, 0.14f, 0.24f), s.HullDark, s.Hull);
            Parts.Eye(t, s, V(0, 0.55f, -0.265f), V(0.2f, 0.035f, 0.02f));
            foreach (float sx in Sides)
            {
                // Massive pauldrons.
                t.Box(V(sx * 0.55f, 0.43f, 0), V(0.34f, 0.24f, 0.54f), s.Team, s.Team);
                t.Box(V(sx * 0.55f, 0.29f, 0), V(0.3f, 0.06f, 0.5f), s.HullDark);
                Parts.Rivets(t, s, V(sx * 0.55f, 0.55f, -0.2f), V(sx * 0.55f, 0.55f, 0.2f), 3, 0.045f);
                t.Beam(V(sx * 0.56f, 0.34f, 0.02f), V(sx * 0.57f, 0.1f, 0.03f), 0.16f, 0.17f, s.Mech);
                Parts.Stack(t, s, V(sx * 0.2f, 0.45f, 0.27f), 0.075f, 0.5f);
            }
            t.Box(V(0, 0.27f, 0.32f), V(0.58f, 0.38f, 0.16f), s.HullDark, s.Accent);
            Parts.Eye(t, s, V(0, 0.2f, 0.405f), V(0.34f, 0.08f, 0.02f));
            // Right arm: Breach Cannon.
            t.Box(V(0.57f, 0.02f, -0.14f), V(0.26f, 0.26f, 0.52f), s.HullDark, s.Team);
            Parts.Barrel(t, s, V(0.57f, 0.02f, -0.38f), V(0.57f, 0.02f, -0.58f), 0.09f, brake: false);
            t.Rod(V(0.57f, 0.02f, -0.57f), V(0.57f, 0.02f, -0.63f), 0.115f, 0.115f, 6, s.Mech, s.Glow);
            // Left arm: claw nanolathe.
            t.Box(V(-0.57f, 0.04f, -0.1f), V(0.22f, 0.22f, 0.4f), s.HullDark, s.Hull);
            foreach (float sy in Sides)
                t.Beam(V(-0.57f, 0.04f + sy * 0.07f, -0.28f), V(-0.57f, 0.04f + sy * 0.03f, -0.48f), 0.07f, 0.06f, s.Metal);
            t.Sphere(V(-0.57f, 0.04f, -0.42f), V(0.05f, 0.05f, 0.05f), 6, 3, s.Glow);
        }
    }

    // ---- Bot Lab ---------------------------------------------------------------------------------

    /// <summary>Walking constructor: a pod on two legs with a nanolathe crane on its back.</summary>
    public static void BuilderBot(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        if (s.IsLattice)
        {
            foreach (float sx in Sides)
                Parts.Leg(b, s, V(sx * 0.24f, 0.62f, 0.06f), V(sx * 0.32f, 0, -0.02f), V(0, 0, 1), 0.17f, 0.11f, s.Team);
            Parts.Hull(b, new Parts.Section[]
            {
                new(-0.6f, 0.12f, 0.08f, 0.62f, 0.72f, 0.78f),
                new(-0.32f, 0.32f, 0.22f, 0.56f, 0.8f, 0.92f),
                new(0.28f, 0.34f, 0.24f, 0.56f, 0.82f, 0.94f),
                new(0.5f, 0.22f, 0.14f, 0.62f, 0.78f, 0.86f),
            }, s.Hull, s.Team, s.Hull, s.HullDark);
            Parts.Eye(b, s, V(0, 0.73f, -0.56f), V(0.16f, 0.04f, 0.06f));
            foreach (float sx in Sides)
                b.Box(V(sx * 0.37f, 0.7f, 0.1f), V(0.08f, 0.16f, 0.4f), s.HullDark, s.Team);
            Parts.NanoArm(b, s, V(0, 0.92f, 0.16f), 0.78f);
        }
        else
        {
            foreach (float sx in Sides)
                Parts.Leg(b, s, V(sx * 0.3f, 0.5f, 0.05f), V(sx * 0.36f, 0, 0), V(0, 0, -1), 0.08f, 0.16f, s.HullDark);
            b.Box(V(0, 0.68f, 0.06f), V(0.76f, 0.38f, 0.84f), s.Hull, s.Team);
            b.Box(V(0, 0.64f, -0.38f), V(0.5f, 0.2f, 0.04f), s.Mech);
            foreach (float sy in new[] { 0.6f, 0.68f })
                Parts.Eye(b, s, V(0, sy, -0.405f), V(0.4f, 0.025f, 0.02f));
            Parts.Rivets(b, s, V(-0.34f, 0.87f, -0.33f), V(0.34f, 0.87f, -0.33f), 4, 0.05f);
            b.Box(V(0.22f, 0.93f, 0.25f), V(0.24f, 0.12f, 0.3f), s.Accent);
            Parts.Stack(b, s, V(0.26f, 0.87f, -0.1f), 0.065f, 0.34f);
            Parts.NanoArm(b, s, V(-0.14f, 0.87f, 0.12f), 0.72f);
        }
    }

    /// <summary>Scout: tiny, light, long-legged (Lattice) or a scuttling lamp-eyed runner (Crucible).</summary>
    public static void ScoutBot(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        if (s.IsLattice)
        {
            foreach (float sx in Sides)
                Parts.Leg(b, s, V(sx * 0.15f, 0.7f, 0.08f), V(sx * 0.21f, 0, 0), V(0, 0, 1), 0.2f, 0.075f, s.Team);
            Parts.Hull(b, new Parts.Section[]
            {
                new(-0.56f, 0.05f, 0.03f, 0.72f, 0.77f, 0.8f),
                new(-0.26f, 0.22f, 0.14f, 0.62f, 0.8f, 0.9f),
                new(0.24f, 0.2f, 0.12f, 0.64f, 0.8f, 0.88f),
                new(0.44f, 0.1f, 0.06f, 0.7f, 0.78f, 0.82f),
            }, s.Hull, s.Team, s.Hull, s.HullDark);
            b.Sphere(V(0, 0.76f, -0.36f), V(0.07f, 0.07f, 0.07f), 6, 3, s.Glow);
            b.Rod(V(0.07f, 0.86f, 0.2f), V(0.12f, 1.25f, 0.36f), 0.014f, 0.01f, 4, s.Metal);
            Parts.Emitter(b, s, V(0, 0.64f, -0.2f), V(0, 0.64f, -0.62f), 0.028f);
        }
        else
        {
            foreach (float sx in Sides)
                Parts.Leg(b, s, V(sx * 0.21f, 0.5f, 0.06f), V(sx * 0.25f, 0, 0), V(0, 0, -1), 0.08f, 0.11f, s.HullDark);
            b.Box(V(0, 0.62f, 0.04f), V(0.46f, 0.28f, 0.62f), s.Hull, s.Team);
            b.Box(V(0, 0.61f, -0.29f), V(0.3f, 0.18f, 0.06f), s.Mech);
            Parts.Eye(b, s, V(0, 0.62f, -0.325f), V(0.18f, 0.08f, 0.02f));
            Parts.Stack(b, s, V(-0.13f, 0.76f, 0.24f), 0.05f, 0.24f);
            b.Rod(V(0.14f, 0.76f, 0.24f), V(0.18f, 1.1f, 0.38f), 0.014f, 0.01f, 4, s.Metal);
            Parts.Barrel(b, s, V(0.2f, 0.56f, -0.2f), V(0.2f, 0.56f, -0.5f), 0.035f, brake: false);
        }
    }

    /// <summary>Assault bot: laser infantry. The upper body is the turret.</summary>
    public static void AssaultBot(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        if (s.IsLattice)
        {
            LatticeBotLegs(b, s);
            MeshKit t = rig.AddTurret(V(0, 0.72f, 0.04f));
            LatticeBotTorso(t, s);
            foreach (float sx in Sides)
                t.Frustum(V(sx * 0.3f, 0.2f, 0), V2(0.2f, 0.3f), V2(0.14f, 0.22f), 0.14f, s.Team, s.Team, V2(sx * 0.02f, 0));
            // Long laser rifle in the right arm, braced by the left.
            t.Beam(V(0.3f, 0.22f, 0.02f), V(0.32f, 0.04f, -0.02f), 0.08f, 0.09f, s.Mech);
            t.Beam(V(-0.3f, 0.22f, 0.02f), V(-0.12f, 0.04f, -0.24f), 0.07f, 0.08f, s.Mech);
            t.Box(V(0.26f, 0.05f, -0.2f), V(0.1f, 0.11f, 0.44f), s.HullDark, s.Team);
            Parts.Emitter(t, s, V(0.26f, 0.06f, -0.4f), V(0.26f, 0.06f, -0.78f), 0.032f);
            t.Beam(V(0, 0.22f, 0.12f), V(0, 0.5f, 0.24f), 0.03f, 0.12f, s.Team);
        }
        else
        {
            CrucibleBotLegs(b, s, 0.52f);
            MeshKit t = rig.AddTurret(V(0, 0.58f, 0.05f));
            CrucibleBotTorso(t, s);
            foreach (float sx in Sides)
            {
                t.Box(V(sx * 0.4f, 0.28f, 0), V(0.2f, 0.2f, 0.32f), s.Team, s.Team);
                t.Box(V(sx * 0.41f, 0.08f, -0.14f), V(0.16f, 0.16f, 0.38f), s.HullDark, s.Hull);
                Parts.Barrel(t, s, V(sx * 0.41f, 0.08f, -0.32f), V(sx * 0.41f, 0.08f, -0.6f), 0.045f);
            }
            Parts.Stack(t, s, V(0.15f, 0.3f, 0.22f), 0.05f, 0.28f);
        }
    }

    /// <summary>AA bot: missile pods angled steeply up. Lattice wears a glowing halo.</summary>
    public static void AaBot(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        if (s.IsLattice)
        {
            LatticeBotLegs(b, s);
            MeshKit t = rig.AddTurret(V(0, 0.72f, 0.04f));
            LatticeBotTorso(t, s);
            foreach (float sx in Sides)
            {
                t.Box(V(sx * 0.27f, 0.22f, 0.02f), V(0.08f, 0.1f, 0.12f), s.Mech);
                Parts.MissilePod(t, s, V(sx * 0.36f, 0.32f, 0.04f), V(0.18f, 0.2f, 0.38f), 58, 1, 2, s.Team);
            }
            t.Ring(V(0, 0.58f, 0.0f), 0.2f, 0.15f, 0.035f, 8, s.Glow);
        }
        else
        {
            CrucibleBotLegs(b, s, 0.52f);
            MeshKit t = rig.AddTurret(V(0, 0.58f, 0.05f));
            CrucibleBotTorso(t, s);
            Parts.MissilePod(t, s, V(0, 0.5f, 0.12f), V(0.6f, 0.3f, 0.44f), 52, 3, 2, s.Team);
            foreach (float sx in Sides)
                t.Box(V(sx * 0.36f, 0.16f, -0.04f), V(0.14f, 0.24f, 0.26f), s.HullDark, s.Hull);
        }
    }

    /// <summary>Mortar bot: a stubby fat tube angled steeply up. Lattice walks on four legs.</summary>
    public static void MortarBot(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        MeshKit t;
        if (s.IsLattice)
        {
            foreach (float sx in Sides)
            foreach (float sz in Sides)
                Parts.Leg(b, s, V(sx * 0.26f, 0.52f, sz * 0.24f), V(sx * 0.52f, 0, sz * 0.46f), V(sx * 0.7f, 1, 0), 0.14f, 0.09f, s.Team);
            Parts.Hull(b, new Parts.Section[]
            {
                new(-0.5f, 0.2f, 0.12f, 0.42f, 0.56f, 0.62f),
                new(-0.24f, 0.36f, 0.26f, 0.4f, 0.6f, 0.7f),
                new(0.3f, 0.36f, 0.26f, 0.4f, 0.6f, 0.7f),
                new(0.48f, 0.24f, 0.16f, 0.44f, 0.56f, 0.62f),
            }, s.Hull, s.Team, s.Hull, s.HullDark);
            Parts.Eye(b, s, V(0, 0.53f, -0.47f), V(0.18f, 0.04f, 0.06f));
            t = rig.AddTurret(V(0, 0.68f, 0.06f));
            t.Cylinder(V(0, 0, 0), 0.24f, 0.2f, 0.1f, 8, s.Mech);
            foreach (float sx in Sides)
                t.Box(V(sx * 0.16f, 0.16f, 0.04f), V(0.05f, 0.24f, 0.3f), s.Hull, s.Team);
        }
        else
        {
            CrucibleBotLegs(b, s, 0.5f);
            b.Box(V(0, 0.66f, 0.06f), V(0.68f, 0.34f, 0.66f), s.Hull, s.Team);
            b.Box(V(0, 0.62f, -0.29f), V(0.4f, 0.16f, 0.06f), s.Mech);
            Parts.Eye(b, s, V(0, 0.63f, -0.325f), V(0.24f, 0.05f, 0.02f));
            Parts.Stack(b, s, V(-0.24f, 0.83f, 0.28f), 0.06f, 0.3f);
            t = rig.AddTurret(V(0, 0.83f, 0.06f));
            t.Box(V(0, 0.05f, 0.04f), V(0.42f, 0.1f, 0.42f), s.Mech);
            foreach (float sx in Sides)
                t.Box(V(sx * 0.18f, 0.18f, 0.04f), V(0.07f, 0.26f, 0.34f), s.HullDark, s.Team);
        }
        // The tube: short, fat and steep.
        Vector3 root = V(0, 0.14f, 0.12f), dir = Elevated(60);
        float r = s.IsLattice ? 0.1f : 0.13f;
        t.Rod(root - dir * 0.12f, root + dir * 0.52f, r * 1.05f, r, 6, s.Metal, s.Mech);
        t.Rod(root + dir * 0.06f, root + dir * 0.22f, r * 1.25f, r * 1.25f, 6, s.Team);
        t.Rod(root + dir * 0.46f, root + dir * 0.6f, r * 1.3f, r * 1.2f, 6, s.Mech, s.Mech);
    }

    /// <summary>
    /// Amphibious T2: Lattice "Wader" is a four-legged sealed-hull walker; Crucible "Mudskipper"
    /// is a tracked boat-hulled crawler with a snorkel stack.
    /// </summary>
    public static void Amphibious(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        MeshKit t;
        if (s.IsLattice)
        {
            foreach (float sx in Sides)
            foreach (float sz in Sides)
                Parts.Leg(b, s, V(sx * 0.32f, 0.7f, sz * 0.3f), V(sx * 0.66f, 0, sz * 0.66f), V(sx, 1.3f, sz * 0.3f), 0.22f, 0.11f, s.Team);
            Parts.Hull(b, new Parts.Section[]
            {
                new(-0.7f, 0.12f, 0.08f, 0.7f, 0.82f, 0.88f),
                new(-0.4f, 0.46f, 0.32f, 0.54f, 0.86f, 1.04f),
                new(0.34f, 0.46f, 0.32f, 0.54f, 0.86f, 1.04f),
                new(0.62f, 0.22f, 0.14f, 0.64f, 0.82f, 0.9f),
            }, s.Hull, s.Team, s.Hull, s.HullDark);
            foreach (float sx in Sides)
                b.Box(V(sx * 0.465f, 0.8f, -0.03f), V(0.02f, 0.05f, 0.62f), s.Glow);
            Parts.Eye(b, s, V(0, 0.82f, -0.66f), V(0.16f, 0.05f, 0.06f));
            b.Rod(V(-0.18f, 1.0f, 0.36f), V(-0.2f, 1.42f, 0.42f), 0.03f, 0.02f, 4, s.Metal);
            b.Sphere(V(-0.2f, 1.42f, 0.42f), V(0.04f, 0.04f, 0.04f), 6, 3, s.Glow);
            t = rig.AddTurret(V(0, 1.04f, -0.08f));
            t.Cylinder(V(0, 0, 0), 0.24f, 0.18f, 0.14f, 8, s.Hull, s.Team);
            Parts.Emitter(t, s, V(0, 0.08f, -0.12f), V(0, 0.08f, -0.68f), 0.045f);
        }
        else
        {
            foreach (float sx in Sides)
                Parts.Tread(b, s, sx * 0.56f, 0.3f, -0.98f, 0.98f, 0.44f, s.Team);
            Parts.Hull(b, new Parts.Section[]
            {
                new(-1.18f, 0.3f, 0.24f, 0.34f, 0.5f, 0.6f),
                new(-0.82f, 0.44f, 0.38f, 0.18f, 0.52f, 0.72f),
                new(0.9f, 0.44f, 0.38f, 0.2f, 0.52f, 0.74f),
                new(1.06f, 0.4f, 0.32f, 0.3f, 0.52f, 0.68f),
            }, s.Hull, s.Hull, s.HullDark, s.HullDark);
            Parts.Rivets(b, s, V(-0.36f, 0.73f, 0.85f), V(0.36f, 0.73f, 0.85f), 4, 0.05f);
            Parts.Rivets(b, s, V(-0.3f, 0.6f, -1.0f), V(0.3f, 0.6f, -1.0f), 4, 0.05f);
            Parts.Stack(b, s, V(0.24f, 0.7f, 0.6f), 0.07f, 0.62f, hot: false, band: s.Team);
            b.Box(V(-0.18f, 0.78f, 0.55f), V(0.3f, 0.12f, 0.36f), s.Team, s.Team);
            t = rig.AddTurret(V(0, 0.73f, -0.12f));
            t.Box(V(0, 0.09f, 0.02f), V(0.46f, 0.18f, 0.5f), s.HullDark, s.Team);
            foreach (float sx in Sides)
                Parts.Emitter(t, s, V(sx * 0.1f, 0.09f, -0.2f), V(sx * 0.1f, 0.09f, -0.62f), 0.04f);
        }
    }

    // ---- Shared bot bodies -----------------------------------------------------------------------

    /// <summary>Slim reverse-jointed legs and pelvis used by Lattice infantry.</summary>
    private static void LatticeBotLegs(MeshKit b, Style s)
    {
        foreach (float sx in Sides)
            Parts.Leg(b, s, V(sx * 0.17f, 0.66f, 0.04f), V(sx * 0.23f, 0, 0), V(0, 0, 1), 0.16f, 0.1f, s.Team);
        b.Box(V(0, 0.68f, 0.04f), V(0.38f, 0.12f, 0.24f), s.Mech);
    }

    /// <summary>Lattice infantry chest and head, around the turret origin at the waist.</summary>
    private static void LatticeBotTorso(MeshKit t, Style s)
    {
        t.Cylinder(V(0, -0.02f, 0), 0.12f, 0.1f, 0.08f, 6, s.Mech);
        t.Frustum(V(0, 0.04f, 0), V2(0.3f, 0.24f), V2(0.5f, 0.32f), 0.3f, s.Hull, s.Team);
        Parts.Slab(t, s, V(0, 0.42f, -0.03f), V(0.16f, 0.13f, 0.18f), s.Hull, s.Hull);
        Parts.Eye(t, s, V(0, 0.43f, -0.125f), V(0.12f, 0.035f, 0.02f));
    }

    /// <summary>Short, thick forward-kneed legs and hip block for Crucible infantry.</summary>
    private static void CrucibleBotLegs(MeshKit b, Style s, float hipY)
    {
        foreach (float sx in Sides)
            Parts.Leg(b, s, V(sx * 0.25f, hipY, 0.05f), V(sx * 0.31f, 0, 0.02f), V(0, 0, -1), 0.07f, 0.15f, s.HullDark);
        b.Box(V(0, hipY + 0.02f, 0.05f), V(0.5f, 0.14f, 0.3f), s.Mech);
    }

    /// <summary>Crucible infantry: a hunched block of a torso with a low head.</summary>
    private static void CrucibleBotTorso(MeshKit t, Style s)
    {
        t.Cylinder(V(0, -0.02f, 0), 0.16f, 0.14f, 0.08f, 6, s.Mech);
        t.Frustum(V(0, 0.04f, 0.02f), V2(0.54f, 0.4f), V2(0.64f, 0.46f), 0.34f, s.Hull, s.Team, V2(0, -0.05f));
        Parts.Rivets(t, s, V(-0.26f, 0.38f, -0.18f), V(0.26f, 0.38f, -0.18f), 4, 0.04f);
        t.Box(V(0, 0.3f, -0.24f), V(0.22f, 0.12f, 0.12f), s.HullDark);
        Parts.Eye(t, s, V(0, 0.31f, -0.305f), V(0.16f, 0.03f, 0.02f));
    }
}
