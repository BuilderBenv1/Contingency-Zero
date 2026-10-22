using ContingencyZero.Sim.Data;
using Godot;

namespace ContingencyZero.Game.View.Meshes;

/// <summary>
/// Economy, defence and intel structures, authored in world units on a footprint × 8 square
/// (half-extent <see cref="ModelRig.Half"/>). Spinning parts (extractor heads, turbine blades,
/// radar dishes, fusion rings) are rotors; gun heads are turrets.
/// </summary>
internal static partial class Models
{
    // ---- Extraction ------------------------------------------------------------------------------

    public static void MetalExtractor(ModelRig rig, Style s, UnitDef def) => Extractor(rig, s, deep: false);

    public static void DeepExtractor(ModelRig rig, Style s, UnitDef def) => Extractor(rig, s, deep: true);

    /// <summary>Drill tower over a glowing bore with a spinning head; the deep version is taller and heavier.</summary>
    private static void Extractor(ModelRig rig, Style s, bool deep)
    {
        MeshKit b = rig.Body;
        float e = rig.Half;
        if (s.IsLattice)
        {
            b.Cylinder(V(0, 0, 0), e * 0.98f, e * 0.86f, 0.5f, 6, s.Hull, s.HullDark, phase: 0);
            b.Ring(V(0, 0.56f, 0), e * 0.82f, e * 0.6f, 0.12f, 6, s.Team);
            b.Cylinder(V(0, 0.5f, 0), 1.1f, 1.1f, 0.08f, 6, s.Glow, phase: 0);
            float colH = deep ? 6.0f : 4.0f;
            b.Cylinder(V(0, 0.5f, 0), 0.62f, 0.42f, colH, 6, s.Metal);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.Tau / 3 + Mathf.Pi / 6;
                Vector3 d = V(Mathf.Cos(a), 0, Mathf.Sin(a));
                b.Beam(d * e * 0.74f + V(0, 0.4f, 0), d * 0.5f + V(0, colH * 0.84f, 0), 0.42f, 0.32f, s.Hull);
                b.Beam(d * e * 0.66f + V(0, 1.1f, 0), d * 0.9f + V(0, colH * 0.7f, 0), 0.12f, 0.5f, s.Team);
                b.Box(d * e * 0.74f + V(0, 0.55f, 0), V(0.8f, 0.3f, 0.8f), s.Mech);
            }
            b.Cylinder(V(0, colH * 0.8f, 0), 1.05f, 0.85f, 0.7f, 6, s.Hull, s.Team);
            if (deep)
            {
                b.Ring(V(0, colH * 0.45f, 0), 1.6f, 0.7f, 0.3f, 6, s.HullDark, s.Team);
                b.Cylinder(V(0, 0.5f, 0), 0.32f, 0.32f, colH * 0.9f, 6, s.Glow);
            }
            MeshKit r = rig.AddRotor(V(0, colH + 0.5f, 0), Vector3.Up);
            r.Cylinder(V(0, 0, 0), 0.6f, 0.42f, 0.55f, 6, s.Mech, s.Glow);
            float reach = deep ? 2.7f : 2.2f;
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.Tau / 3;
                Vector3 d = V(Mathf.Cos(a), 0, Mathf.Sin(a));
                r.Beam(d * 0.4f + V(0, 0.3f, 0), d * reach + V(0, -0.1f, 0), 0.36f, 0.24f, s.Team);
                r.Sphere(d * reach + V(0, -0.1f, 0), V(0.22f, 0.22f, 0.22f), 6, 3, s.Glow);
            }
            if (deep) r.Ring(V(0, -0.1f, 0), reach + 0.15f, reach - 0.15f, 0.16f, 9, s.Hull);
        }
        else
        {
            b.Box(V(0, 0.3f, 0), V(e * 1.92f, 0.6f, e * 1.92f), s.Hull, s.HullDark);
            foreach (float sx in Sides)
            foreach (float sz in Sides)
                b.Box(V(sx * e * 0.78f, 0.72f, sz * e * 0.78f), V(1.2f, 0.24f, 1.2f), s.Team, s.Team);
            Parts.Rivets(b, s, V(-e * 0.5f, 0.6f, e * 0.9f), V(e * 0.5f, 0.6f, e * 0.9f), 4, 0.25f);
            b.Cylinder(V(0, 0.6f, 0), 1.25f, 1.25f, 0.06f, 6, s.Glow);
            float towerH = deep ? 6.0f : 4.0f;
            b.Cylinder(V(0, 0.6f, 0), 0.5f, 0.5f, towerH, 6, s.Metal);
            foreach (float sx in Sides)
            foreach (float sz in Sides)
                b.Beam(V(sx * e * 0.62f, 0.6f, sz * e * 0.62f), V(sx * 0.9f, towerH, sz * 0.9f), 0.45f, 0.45f, s.HullDark);
            foreach (float sx in Sides)
                b.Beam(V(sx * e * 0.62f, 0.8f, e * 0.62f), V(-sx * 1.1f, towerH * 0.75f, 1.1f), 0.2f, 0.2f, s.Mech);
            b.Box(V(0, towerH + 0.3f, 0), V(2.4f, 0.7f, 2.4f), s.Hull, s.Team);
            Parts.Stack(b, s, V(e * 0.62f, 0.6f, -e * 0.62f), 0.36f, deep ? 4.6f : 3.2f);
            if (deep) Parts.Stack(b, s, V(-e * 0.62f, 0.6f, -e * 0.62f), 0.36f, 4.0f);
            MeshKit r = rig.AddRotor(V(0, towerH + 0.65f, 0), Vector3.Up);
            r.Cylinder(V(0, 0, 0), 0.6f, 0.55f, 0.5f, 6, s.Mech);
            float arm = deep ? 4.2f : 3.4f;
            r.Box(V(0, 0.32f, 0), V(arm, 0.5f, 0.9f), s.Team, s.Team);
            foreach (float sx in Sides)
                r.Box(V(sx * arm / 2, 0.32f, 0), V(0.8f, 0.8f, 1.2f), s.HullDark, s.Mech);
            if (deep) r.Box(V(0, 0.32f, 0), V(0.9f, 0.5f, arm * 0.8f), s.Team, s.Team);
        }
    }

    // ---- Energy ----------------------------------------------------------------------------------

    /// <summary>Solar: Lattice opens four tilted petals around a hub; Crucible racks three rows of panels facing south.</summary>
    public static void SolarCollector(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        float e = rig.Half;
        if (s.IsLattice)
        {
            b.Cylinder(V(0, 0, 0), 2.4f, 2.0f, 1.5f, 8, s.Hull, s.Team);
            b.Cylinder(V(0, 1.5f, 0), 0.9f, 0.7f, 0.5f, 8, s.Mech, s.Glow);
            for (int i = 0; i < 4; i++)
                using (b.Push(V(0, 1.2f, 0), V(0, i * Mathf.Pi / 2 + Mathf.Pi / 4, 0)))
                {
                    b.Beam(V(0, -1.2f, -3.2f), V(0, 0.9f, -3.4f), 0.3f, 0.3f, s.Metal);
                    using (b.Push(V(0, 0, -1.5f), V(Rad(28), 0, 0)))
                    {
                        b.Box(V(0, 0, -2.6f), V(4.4f, 0.28f, 5.2f), s.Team);
                        b.Box(V(0, 0.16f, -2.6f), V(3.9f, 0.06f, 4.7f), s.Solar);
                        b.Box(V(0, 0.2f, -2.6f), V(0.1f, 0.03f, 4.7f), s.Metal);
                        b.Box(V(0, 0.2f, -2.6f), V(3.9f, 0.03f, 0.1f), s.Metal);
                    }
                }
        }
        else
        {
            b.Box(V(0, 0.25f, 0), V(e * 1.94f, 0.5f, e * 1.94f), s.HullDark, s.Hull);
            foreach (float z in new[] { -4.7f, 0f, 4.7f })
            {
                foreach (float x in new[] { -5.6f, 0f, 5.6f })
                {
                    b.Box(V(x, 1.0f, z + 1.0f), V(0.4f, 1.0f, 0.4f), s.Mech);
                    b.Box(V(x, 1.7f, z - 1.0f), V(0.4f, 2.4f, 0.4f), s.Mech);
                }
                using (b.Push(V(0, 2.2f, z), V(Rad(28), 0, 0)))
                {
                    b.Box(Vector3.Zero, V(13.8f, 0.34f, 4.0f), s.Team);
                    b.Box(V(0, 0.19f, 0), V(13.2f, 0.06f, 3.5f), s.Solar);
                    foreach (float x in new[] { -4.4f, 0f, 4.4f })
                        b.Box(V(x, 0.23f, 0), V(0.16f, 0.04f, 3.5f), s.Metal);
                }
            }
            b.Box(V(-6.0f, 1.5f, 6.6f), V(2.2f, 2.0f, 1.2f), s.Hull, s.Team);
            Parts.Stack(b, s, V(-6.5f, 2.5f, 6.6f), 0.3f, 1.5f, hot: false);
        }
    }

    /// <summary>Wind turbine: tall mast, nacelle and a three-bladed rotor spinning about Z.</summary>
    public static void WindTurbine(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        float e = rig.Half;
        MeshKit r;
        if (s.IsLattice)
        {
            b.Cylinder(V(0, 0, 0), e * 0.75f, e * 0.6f, 0.8f, 6, s.Hull, s.Team);
            b.Cylinder(V(0, 0.8f, 0), 0.55f, 0.28f, 10.6f, 6, s.Hull);
            b.Cylinder(V(0, 5.0f, 0), 0.46f, 0.43f, 1.4f, 6, s.Team);
            b.Cylinder(V(0, 8.6f, 0), 0.36f, 0.34f, 0.3f, 6, s.Glow);
            Parts.Hull(b, new Parts.Section[]
            {
                new(-0.9f, 0.32f, 0.2f, 10.9f, 11.3f, 11.6f),
                new(-0.4f, 0.5f, 0.32f, 10.8f, 11.45f, 11.95f),
                new(0.9f, 0.42f, 0.26f, 10.9f, 11.4f, 11.8f),
                new(1.6f, 0.12f, 0.06f, 11.2f, 11.45f, 11.6f),
            }, s.Hull, s.Team, s.Hull, s.HullDark);
            r = rig.AddRotor(V(0, 11.4f, -1.05f), Vector3.Back);
            r.Rod(V(0, 0, 0.3f), V(0, 0, -0.8f), 0.48f, 0.06f, 8, s.Hull);
            for (int i = 0; i < 3; i++)
                using (r.Push(Vector3.Zero, V(0, 0, i * Mathf.Tau / 3)))
                using (r.Push(Vector3.Zero, V(0, 0.35f, 0)))
                {
                    r.Frustum(V(0, 0.3f, 0), V2(0.8f, 0.14f), V2(0.36f, 0.08f), 3.1f, s.Hull, s.Hull);
                    r.Frustum(V(0, 3.4f, 0), V2(0.36f, 0.08f), V2(0.16f, 0.05f), 1.0f, s.Team, s.Team);
                }
        }
        else
        {
            b.Box(V(0, 0.5f, 0), V(e * 1.5f, 1.0f, e * 1.5f), s.HullDark, s.Team);
            Parts.Rivets(b, s, V(-e * 0.6f, 1.0f, e * 0.66f), V(e * 0.6f, 1.0f, e * 0.66f), 4, 0.25f);
            b.Frustum(V(0, 1.0f, 0), V2(1.4f, 1.4f), V2(0.7f, 0.7f), 9.8f, s.Hull, s.Hull);
            foreach (float y in new[] { 3.4f, 6.6f })
                b.Box(V(0, y, 0), V(1.32f - y * 0.07f, 0.35f, 1.32f - y * 0.07f), s.Mech);
            b.Box(V(0, 5.0f, 0), V(1.2f, 1.0f, 1.2f), s.Team);
            b.Box(V(0, 11.3f, 0.2f), V(1.3f, 1.2f, 2.8f), s.Hull, s.Team);
            b.Box(V(0, 11.3f, 1.7f), V(0.9f, 0.8f, 0.3f), s.Mech);
            r = rig.AddRotor(V(0, 11.3f, -1.4f), Vector3.Back);
            r.Box(V(0, 0, -0.1f), V(0.9f, 0.9f, 0.8f), s.Mech);
            for (int i = 0; i < 3; i++)
                using (r.Push(Vector3.Zero, V(0, 0, i * Mathf.Tau / 3)))
                using (r.Push(Vector3.Zero, V(0, 0.3f, 0)))
                {
                    r.Frustum(V(0, 0.4f, 0), V2(1.15f, 0.18f), V2(0.75f, 0.12f), 3.2f, s.HullDark, s.HullDark);
                    r.Frustum(V(0, 3.6f, 0), V2(0.75f, 0.12f), V2(0.5f, 0.08f), 0.8f, s.Team, s.Team);
                }
        }
    }

    // ---- Storage and conversion ------------------------------------------------------------------

    /// <summary>Metal storage: a squat domed tank (Lattice) or four banded tanks (Crucible).</summary>
    public static void MetalStorage(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        float e = rig.Half;
        if (s.IsLattice)
        {
            b.Cylinder(V(0, 0, 0), e * 0.97f, e * 0.9f, 0.6f, 8, s.HullDark, s.Hull);
            b.Cylinder(V(0, 0.6f, 0), 5.6f, 5.4f, 2.4f, 8, s.Hull);
            b.Dome(V(0, 3.0f, 0), 5.4f, 1.8f, 8, 2, s.Team);
            b.Cylinder(V(0, 4.6f, 0), 1.2f, 1.0f, 0.4f, 8, s.Mech, s.Hull);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.Pi / 2 + Mathf.Pi / 8;
                Vector3 d = V(Mathf.Cos(a), 0, Mathf.Sin(a));
                b.Box(d * 5.6f + V(0, 1.8f, 0), V(0.7f, 2.6f, 0.7f), s.HullDark, s.Team);
            }
            b.Box(V(0, 1.2f, 5.6f), V(2.0f, 1.2f, 0.4f), s.Mech);
        }
        else
        {
            b.Box(V(0, 0.3f, 0), V(e * 1.94f, 0.6f, e * 1.94f), s.HullDark, s.Hull);
            foreach (float sx in Sides)
            foreach (float sz in Sides)
            {
                Vector3 c = V(sx * 3.5f, 0.6f, sz * 3.5f);
                b.Cylinder(c, 3.0f, 2.9f, 3.2f, 6, s.Hull, s.Team);
                b.Cylinder(c + V(0, 0.6f, 0), 3.08f, 3.08f, 0.4f, 6, s.Mech);
                b.Cylinder(c + V(0, 2.2f, 0), 3.0f, 3.0f, 0.35f, 6, s.Mech);
                Parts.Rivets(b, s, c + V(-1.2f, 3.2f, 0), c + V(1.2f, 3.2f, 0), 3, 0.3f);
            }
            b.Rod(V(-3.5f, 2.8f, 0), V(3.5f, 2.8f, 0), 0.4f, 0.4f, 6, s.Metal);
            b.Rod(V(0, 2.8f, -3.5f), V(0, 2.8f, 3.5f), 0.4f, 0.4f, 6, s.Metal);
            b.Box(V(0, 2.8f, 0), V(1.4f, 1.2f, 1.4f), s.Accent);
        }
    }

    /// <summary>Energy storage: a capacitor tower with glowing rings (Lattice) or three glowing battery cells (Crucible).</summary>
    public static void EnergyStorage(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        float e = rig.Half;
        if (s.IsLattice)
        {
            b.Cylinder(V(0, 0, 0), e * 0.9f, e * 0.8f, 0.7f, 6, s.HullDark, s.Hull, phase: 0);
            b.Cylinder(V(0, 0.7f, 0), 3.6f, 3.2f, 5.6f, 8, s.Hull);
            foreach (float y in new[] { 1.8f, 3.4f, 5.0f })
                b.Ring(V(0, y, 0), 4.0f - y * 0.08f, 3.2f, 0.35f, 8, s.Glow);
            b.Dome(V(0, 6.3f, 0), 3.2f, 1.2f, 8, 2, s.Team);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.Tau / 3 + Mathf.Pi / 2;
                Vector3 d = V(Mathf.Cos(a), 0, Mathf.Sin(a));
                b.Frustum(d * 5.4f + V(0, 0.7f, 0), V2(1.4f, 1.4f), V2(0.8f, 0.8f), 3.6f, s.Team, s.Team);
                b.Beam(d * 5.2f + V(0, 3.2f, 0), d * 3.4f + V(0, 4.0f, 0), 0.3f, 0.3f, s.Metal);
            }
        }
        else
        {
            b.Box(V(0, 0.3f, 0), V(e * 1.94f, 0.6f, e * 1.94f), s.HullDark, s.Hull);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.Tau / 3 - Mathf.Pi / 2;
                Vector3 c = V(Mathf.Cos(a) * 3.6f, 0.6f, Mathf.Sin(a) * 3.6f);
                b.Cylinder(c, 2.5f, 2.4f, 4.6f, 6, s.Hull, s.Team);
                b.Cylinder(c + V(0, 3.4f, 0), 2.56f, 2.56f, 0.5f, 6, s.Glow);
                b.Cylinder(c + V(0, 1.0f, 0), 2.56f, 2.56f, 0.4f, 6, s.Mech);
                b.Cylinder(c + V(0, 4.6f, 0), 0.6f, 0.5f, 0.8f, 6, s.Metal, s.Glow);
                b.Rod(c + V(0, 4.0f, 0), V(0, 4.4f, 0), 0.25f, 0.25f, 4, s.Accent);
            }
            b.Cylinder(V(0, 0.6f, 0), 1.0f, 0.9f, 4.0f, 6, s.HullDark, s.Glow);
        }
    }

    /// <summary>Metal maker: a floating glowing crystal in a cradle (Lattice) or a molten crucible pot (Crucible).</summary>
    public static void MetalMaker(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        float e = rig.Half;
        if (s.IsLattice)
        {
            Parts.Slab(b, s, V(0, 0.5f, 0), V(e * 1.8f, 1.0f, e * 1.8f), s.Hull, s.HullDark, 0.08f);
            b.Ring(V(0, 1.05f, 0), 3.6f, 2.6f, 0.1f, 8, s.Glow);
            foreach (float sx in Sides)
            foreach (float sz in Sides)
            {
                b.Beam(V(sx * 5.0f, 1.0f, sz * 5.0f), V(sx * 2.4f, 6.4f, sz * 2.4f), 0.9f, 0.9f, s.Team);
                b.Box(V(sx * 2.4f, 6.4f, sz * 2.4f), V(0.8f, 0.5f, 0.8f), s.Mech);
            }
            b.Sphere(V(0, 4.0f, 0), V(2.0f, 2.8f, 2.0f), 4, 2, s.Glow);
            b.Ring(V(0, 4.0f, 0), 3.3f, 2.7f, 0.3f, 8, s.Hull);
        }
        else
        {
            b.Box(V(0, 0.4f, 0), V(e * 1.9f, 0.8f, e * 1.9f), s.HullDark, s.Hull);
            b.Cylinder(V(0, 0.8f, 0), 4.0f, 5.0f, 3.6f, 6, s.HullDark, s.Glow);
            b.Cylinder(V(0, 1.6f, 0), 4.3f, 4.5f, 1.2f, 6, s.Team);
            b.Ring(V(0, 4.45f, 0), 5.2f, 4.3f, 0.3f, 6, s.Mech);
            foreach (float sx in Sides)
                b.Box(V(sx * 5.2f, 3.0f, 0), V(0.8f, 1.2f, 1.2f), s.Metal);
            Parts.Stack(b, s, V(5.4f, 0.8f, -5.4f), 0.6f, 5.0f);
            b.Box(V(-5.2f, 1.5f, 5.2f), V(2.4f, 1.4f, 2.4f), s.Hull, s.Team);
        }
    }

    /// <summary>
    /// Fusion: Lattice dome with a glowing crown core and two spinning core rings (rotor);
    /// Crucible hexagonal furnace block with four hot cooling stacks and a spinning glowing band.
    /// </summary>
    public static void FusionReactor(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        float e = rig.Half;
        MeshKit r;
        if (s.IsLattice)
        {
            b.Cylinder(V(0, 0, 0), e * 0.98f, e * 0.92f, 1.0f, 8, s.HullDark, s.Hull);
            b.Ring(V(0, 1.06f, 0), e * 0.9f, e * 0.76f, 0.12f, 8, s.Team);
            b.Dome(V(0, 1.0f, 0), 7.4f, 7.2f, 12, 4, s.Hull, s.Team);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.Pi / 2 + Mathf.Pi / 4;
                Vector3 d = V(Mathf.Cos(a), 0, Mathf.Sin(a));
                b.Frustum(d * 9.4f + V(0, 1.0f, 0), V2(1.8f, 1.8f), V2(0.9f, 0.9f), 6.0f, s.Hull, s.Team);
                b.Sphere(d * 9.4f + V(0, 7.3f, 0), V(0.5f, 0.5f, 0.5f), 6, 3, s.Glow);
                b.Beam(d * 9.0f + V(0, 6.2f, 0), d * 3.0f + V(0, 7.9f, 0), 0.3f, 0.3f, s.Metal);
            }
            foreach (float a in new[] { 0f, Mathf.Pi / 2 })
                using (b.Push(V(0, 7.6f, 0), V(0, a, 0)))
                    b.Beam(V(-2.4f, 0, 0), V(-1.0f, 3.2f, 0), 0.5f, 0.6f, s.Team);
            b.Sphere(V(0, 9.0f, 0), V(1.9f, 1.9f, 1.9f), 8, 4, s.Glow);
            foreach (float a in new[] { Mathf.Pi / 4, 3 * Mathf.Pi / 4 })
                using (b.Push(V(0, 7.6f, 0), V(0, a, 0)))
                    b.Beam(V(2.4f, 0, 0), V(1.0f, 3.2f, 0), 0.5f, 0.6f, s.Team);
            r = rig.AddRotor(V(0, 1.0f, 0), Vector3.Up);
            foreach (var (y, rin) in new[] { (2.2f, 7.4f), (4.6f, 6.0f) })
            {
                r.Ring(V(0, y, 0), rin + 0.7f, rin + 0.1f, 0.4f, 12, s.Glow);
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.Pi / 2;
                    r.Box(V(Mathf.Cos(a) * (rin + 0.4f), y, Mathf.Sin(a) * (rin + 0.4f)), V(1.0f, 0.7f, 1.0f), s.Mech);
                }
            }
        }
        else
        {
            b.Box(V(0, 0.4f, 0), V(e * 1.96f, 0.8f, e * 1.96f), s.HullDark, s.Hull);
            b.Cylinder(V(0, 0.8f, 0), 9.4f, 8.6f, 3.6f, 6, s.Hull, s.HullDark);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.Tau / 6;
                Vector3 d = V(Mathf.Cos(a), 0, Mathf.Sin(a));
                b.Box(d * 8.75f + V(0, 2.4f, 0), V(1.2f, 0.6f, 1.2f), s.Glow);
            }
            b.Cylinder(V(0, 4.4f, 0), 6.2f, 5.4f, 3.0f, 6, s.Hull, s.Team);
            b.Dome(V(0, 7.4f, 0), 3.6f, 2.6f, 6, 2, s.HullDark);
            b.Cylinder(V(0, 9.8f, 0), 1.0f, 0.8f, 0.6f, 6, s.Mech, s.Glow);
            foreach (float sx in Sides)
            foreach (float sz in Sides)
            {
                Parts.Stack(b, s, V(sx * 9.2f, 0.8f, sz * 9.2f), 1.4f, 9.6f, band: s.Team);
                b.Beam(V(sx * 8.6f, 4.0f, sz * 8.6f), V(sx * 5.2f, 5.8f, sz * 5.2f), 0.6f, 0.6f, s.Metal);
            }
            Parts.Rivets(b, s, V(-5f, 4.4f, 7.8f), V(5f, 4.4f, 7.8f), 5, 0.35f);
            r = rig.AddRotor(V(0, 4.4f, 0), Vector3.Up);
            r.Ring(V(0, 1.5f, 0), 7.0f, 6.0f, 0.9f, 6, s.Glow);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.Tau / 3;
                r.Box(V(Mathf.Cos(a) * 6.6f, 1.5f, Mathf.Sin(a) * 6.6f), V(1.6f, 1.4f, 1.6f), s.HullDark);
            }
        }
    }

    // ---- Defence ---------------------------------------------------------------------------------

    /// <summary>Light laser tower: Lattice "Sentinel" pillar with an angular head; Crucible "Brander" bunker with twin barrels.</summary>
    public static void LightTurret(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        float e = rig.Half;
        MeshKit t;
        if (s.IsLattice)
        {
            b.Cylinder(V(0, 0, 0), e * 0.85f, e * 0.62f, 1.4f, 6, s.Hull, s.Team, phase: 0);
            b.Cylinder(V(0, 1.4f, 0), 1.3f, 1.0f, 2.4f, 6, s.Hull, s.Mech, phase: 0);
            b.Cylinder(V(0, 2.6f, 0), 1.1f, 1.1f, 0.25f, 6, s.Glow, phase: 0);
            t = rig.AddTurret(V(0, 3.8f, 0));
            LatticeTurret(t, s, 1.15f, 1.4f, 1.3f, 1.2f);
            Parts.Emitter(t, s, V(0, 0.6f, -0.9f), V(0, 0.6f, -3.3f), 0.17f);
            Parts.Eye(t, s, V(0.6f, 0.8f, -0.95f), V(0.3f, 0.16f, 0.1f));
        }
        else
        {
            b.Frustum(V(0, 0, 0), V2(e * 1.9f, e * 1.9f), V2(e * 1.45f, e * 1.45f), 3.0f, s.Hull, s.HullDark);
            foreach (float sz in Sides)
                Parts.Rivets(b, s, V(-e * 0.55f, 3.0f, sz * e * 0.6f), V(e * 0.55f, 3.0f, sz * e * 0.6f), 4, 0.24f);
            b.Box(V(0, 1.4f, e * 0.89f), V(e * 1.4f, 0.5f, 0.2f), s.Team);
            t = rig.AddTurret(V(0, 3.0f, 0));
            t.Box(V(0, 0.75f, 0.1f), V(2.8f, 1.5f, 3.0f), s.Hull, s.Team);
            t.Box(V(0, 0.7f, -1.45f), V(1.8f, 1.0f, 0.3f), s.Mech);
            foreach (float sx in Sides)
                Parts.Emitter(t, s, V(sx * 0.45f, 0.7f, -1.4f), V(sx * 0.45f, 0.7f, -3.0f), 0.2f);
            Parts.Rivets(t, s, V(-1.1f, 1.5f, -1.2f), V(1.1f, 1.5f, -1.2f), 4, 0.22f);
        }
    }

    /// <summary>AA tower: Lattice "Spire" with twin pods on a tall needle; Crucible "Flak Nest" walled pit with a big launcher.</summary>
    public static void AaTurret(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        float e = rig.Half;
        MeshKit t;
        if (s.IsLattice)
        {
            b.Cylinder(V(0, 0, 0), e * 0.8f, e * 0.62f, 0.9f, 6, s.Hull, s.Team, phase: 0);
            b.Cylinder(V(0, 0.9f, 0), 1.3f, 0.55f, 4.4f, 6, s.Hull, phase: 0);
            b.Cylinder(V(0, 2.6f, 0), 1.0f, 0.95f, 0.8f, 6, s.Team, phase: 0);
            b.Cylinder(V(0, 4.6f, 0), 0.62f, 0.62f, 0.2f, 6, s.Glow, phase: 0);
            t = rig.AddTurret(V(0, 5.3f, 0));
            t.Cylinder(V(0, 0, 0), 0.8f, 0.6f, 0.5f, 6, s.Mech);
            t.Box(V(0, 0.55f, 0), V(1.7f, 0.3f, 0.5f), s.Hull);
            foreach (float sx in Sides)
                Parts.MissilePod(t, s, V(sx * 1.15f, 0.9f, 0.1f), V(0.9f, 1.0f, 1.9f), 55, 2, 2, s.Team);
        }
        else
        {
            b.Ring(V(0, 0.9f, 0), e * 0.98f, e * 0.6f, 1.8f, 6, s.Hull, s.Team);
            b.Cylinder(V(0, 0, 0), e * 0.62f, e * 0.62f, 0.4f, 6, s.Mech);
            Parts.Rivets(b, s, V(-1.4f, 1.8f, e * 0.82f), V(1.4f, 1.8f, e * 0.82f), 3, 0.26f);
            t = rig.AddTurret(V(0, 0.4f, 0));
            t.Box(V(0, 0.6f, 0), V(1.4f, 1.2f, 1.4f), s.Mech);
            foreach (float sx in Sides)
                t.Box(V(sx * 1.45f, 1.6f, 0.2f), V(0.3f, 1.8f, 1.4f), s.HullDark, s.Hull);
            Parts.MissilePod(t, s, V(0, 2.4f, 0.2f), V(2.5f, 1.4f, 2.2f), 52, 3, 2, s.Team);
        }
    }

    /// <summary>Heavy cannon emplacement: Lattice "Bastion" octagonal keep; Crucible "Rampart" slab-sided bunker.</summary>
    public static void HeavyTurret(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        float e = rig.Half;
        MeshKit t;
        if (s.IsLattice)
        {
            b.Cylinder(V(0, 0, 0), e * 0.98f, e * 0.76f, 3.0f, 8, s.Hull, s.HullDark);
            b.Ring(V(0, 3.0f, 0), e * 0.76f, e * 0.56f, 0.16f, 8, s.Team);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.Tau / 8 + Mathf.Pi / 8;
                b.Box(V(Mathf.Cos(a) * e * 0.88f, 1.5f, Mathf.Sin(a) * e * 0.88f), V(0.5f, 0.3f, 0.5f), s.Glow);
            }
            t = rig.AddTurret(V(0, 3.0f, 0));
            t.Cylinder(V(0, 0, 0), 3.0f, 2.8f, 0.4f, 8, s.Mech);
            LatticeTurret(t, s, 2.5f, 2.6f, 2.6f, 2.4f);
            Parts.Barrel(t, s, V(0, 1.2f, -2.0f), V(0, 1.25f, -7.0f), 0.36f);
            t.Rod(V(0, 1.2f, -2.2f), V(0, 1.2f, -3.4f), 0.5f, 0.48f, 6, s.Team);
            Parts.Eye(t, s, V(1.4f, 1.6f, -2.0f), V(0.6f, 0.25f, 0.2f));
        }
        else
        {
            b.Frustum(V(0, 0, 0), V2(e * 1.94f, e * 1.94f), V2(e * 1.5f, e * 1.5f), 3.4f, s.Hull, s.HullDark);
            foreach (float sz in Sides)
                Parts.Rivets(b, s, V(-e * 0.6f, 3.4f, sz * e * 0.66f), V(e * 0.6f, 3.4f, sz * e * 0.66f), 5, 0.35f);
            b.Box(V(0, 1.4f, e * 0.88f), V(e * 1.5f, 0.8f, 0.3f), s.Team);
            t = rig.AddTurret(V(0, 3.4f, 0));
            t.Box(V(0, 1.4f, 0.4f), V(5.6f, 2.8f, 6.0f), s.Hull, s.Team);
            t.Box(V(0, 1.3f, -2.7f), V(2.6f, 2.0f, 0.6f), s.Mech);
            Parts.Barrel(t, s, V(0, 1.3f, -2.8f), V(0, 1.3f, -7.2f), 0.52f);
            Parts.Rivets(t, s, V(-2.4f, 2.8f, -2.2f), V(2.4f, 2.8f, -2.2f), 5, 0.3f);
            Parts.Stack(t, s, V(1.8f, 2.8f, 2.6f), 0.4f, 1.6f);
        }
    }

    /// <summary>Strategic artillery: a very long barrel elevated ~48° on a traversing yoke.</summary>
    public static void PlasmaBattery(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        float e = rig.Half;
        MeshKit t;
        if (s.IsLattice)
        {
            b.Cylinder(V(0, 0, 0), e * 0.96f, e * 0.82f, 1.6f, 8, s.Hull, s.HullDark);
            b.Ring(V(0, 1.6f, 0), e * 0.82f, e * 0.62f, 0.14f, 8, s.Team);
            t = rig.AddTurret(V(0, 1.6f, 0));
            t.Cylinder(V(0, 0, 0), 4.2f, 3.8f, 1.0f, 8, s.Hull, s.Team);
            foreach (float sx in Sides)
                t.Frustum(V(sx * 1.4f, 1.0f, 0.4f), V2(0.6f, 4.2f), V2(0.6f, 1.6f), 3.0f, s.Hull, s.Team, V2(0, -0.6f));
            t.Box(V(0, 2.4f, 2.4f), V(2.0f, 1.8f, 1.8f), s.HullDark, s.Team);
            Vector3 root = V(0, 3.0f, 0.6f), dir = Elevated(48);
            t.Rod(root - dir * 1.4f, root + dir * 9.6f, 0.5f, 0.4f, 8, s.Metal);
            foreach (float d in new[] { 1.8f, 3.6f, 5.4f, 7.2f })
                t.Rod(root + dir * d, root + dir * (d + 0.4f), 0.66f, 0.66f, 8, s.Glow);
            t.Rod(root + dir * 9.4f, root + dir * 10.0f, 0.6f, 0.5f, 8, s.Mech, s.Glow);
        }
        else
        {
            b.Frustum(V(0, 0, 0), V2(e * 1.94f, e * 1.94f), V2(e * 1.6f, e * 1.6f), 2.0f, s.Hull, s.HullDark);
            Parts.Rivets(b, s, V(-e * 0.6f, 2.0f, e * 0.72f), V(e * 0.6f, 2.0f, e * 0.72f), 5, 0.35f);
            t = rig.AddTurret(V(0, 2.0f, 0));
            t.Cylinder(V(0, 0, 0), 4.6f, 4.4f, 0.8f, 6, s.Mech);
            t.Box(V(0, 2.0f, 1.2f), V(4.2f, 2.6f, 4.6f), s.Hull, s.Team);
            Vector3 root = V(0, 3.0f, -0.2f), dir = Elevated(45);
            Parts.Barrel(t, s, root, root + dir * 9.0f, 0.75f);
            t.Rod(root + dir * 8.6f, root + dir * 9.3f, 1.12f, 1.0f, 6, s.Mech, s.Glow);
            t.Rod(root + dir * 0.6f, root + dir * 2.4f, 1.0f, 0.95f, 6, s.TeamDark);
            foreach (float sx in Sides)
                Parts.Stack(t, s, V(sx * 1.4f, 3.3f, 3.0f), 0.45f, 2.2f);
        }
    }

    // ---- Intel -----------------------------------------------------------------------------------

    /// <summary>Radar: a tall mast with a spinning dish (Lattice) or a truss tower with a spinning array (Crucible).</summary>
    public static void RadarTower(ModelRig rig, Style s, UnitDef def)
    {
        MeshKit b = rig.Body;
        float e = rig.Half;
        MeshKit r;
        if (s.IsLattice)
        {
            b.Cylinder(V(0, 0, 0), e * 0.7f, e * 0.55f, 0.7f, 6, s.Hull, s.Team, phase: 0);
            b.Cylinder(V(0, 0.7f, 0), 0.5f, 0.26f, 8.6f, 6, s.Hull);
            foreach (float y in new[] { 3.0f, 5.6f })
                b.Cylinder(V(0, y, 0), 0.48f - y * 0.025f, 0.46f - y * 0.025f, 0.3f, 6, s.Glow);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.Tau / 3 + Mathf.Pi / 2;
                Vector3 d = V(Mathf.Cos(a), 0, Mathf.Sin(a));
                b.Beam(d * 2.4f + V(0, 0.6f, 0), d * 0.3f + V(0, 4.6f, 0), 0.22f, 0.22f, s.Team);
            }
            r = rig.AddRotor(V(0, 9.2f, 0), Vector3.Up);
            r.Cylinder(V(0, -0.2f, 0), 0.4f, 0.32f, 0.4f, 6, s.Mech);
            r.Beam(V(0, 0.1f, 0), V(0, 0.6f, -0.6f), 0.2f, 0.2f, s.Metal);
            Parts.Dish(r, s, V(0, 0.8f, -0.8f), V(0, 0.45f, -1), 2.0f, s.Team);
            r.Box(V(0, 0.2f, 0.6f), V(0.5f, 0.5f, 0.6f), s.HullDark);
        }
        else
        {
            foreach (float sx in Sides)
            foreach (float sz in Sides)
                b.Beam(V(sx * 2.4f, 0, sz * 2.4f), V(sx * 0.8f, 7.2f, sz * 0.8f), 0.36f, 0.36f, s.HullDark);
            foreach (float y in new[] { 2.2f, 4.8f })
            {
                float w = 2.4f - (2.4f - 0.8f) * y / 7.2f;
                foreach (float sz in Sides)
                    b.Box(V(0, y, sz * w), V(w * 2, 0.24f, 0.24f), s.Mech);
                foreach (float sx in Sides)
                    b.Box(V(sx * w, y, 0), V(0.24f, 0.24f, w * 2), s.Mech);
            }
            foreach (float sx in Sides)
                b.Beam(V(sx * 2.2f, 0.2f, 2.2f), V(-sx * 1.4f, 4.6f, 1.4f), 0.16f, 0.16f, s.Metal);
            b.Box(V(0, 0.3f, 0), V(e * 1.6f, 0.6f, e * 1.6f), s.Hull, s.Team);
            b.Box(V(0, 7.4f, 0), V(2.8f, 0.5f, 2.8f), s.Hull, s.Team);
            b.Box(V(0.9f, 8.0f, 0.9f), V(0.8f, 0.7f, 0.8f), s.HullDark, s.Glow);
            r = rig.AddRotor(V(0, 7.65f, 0), Vector3.Up);
            r.Cylinder(V(0, 0, 0), 0.4f, 0.35f, 1.0f, 6, s.Metal);
            using (r.Push(V(0, 1.6f, 0), V(Rad(15), 0, 0)))
            {
                r.Box(Vector3.Zero, V(4.6f, 1.6f, 0.3f), s.Team);
                r.Box(V(0, 0, 0.25f), V(4.8f, 0.3f, 0.3f), s.Mech);
                r.Box(V(0, 0, -0.18f), V(4.2f, 0.12f, 0.06f), s.GlowDim);
            }
        }
    }
}
