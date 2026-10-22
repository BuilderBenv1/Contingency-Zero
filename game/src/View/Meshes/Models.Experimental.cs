using ContingencyZero.Sim.Data;
using Godot;

namespace ContingencyZero.Game.View.Meshes;

/// <summary>
/// The experimentals (radius 8, so 3–4× a tank):
/// Lattice "Skywarden": a towering three-jointed walker whose upper body carries a huge beam
/// emitter with glowing focus coils, plus shoulder AA pods.
/// Crucible "Mantle Crawler": a four-tracked smelter with a glowing furnace mouth, ore hopper,
/// smokestacks and a slag cannon.
/// </summary>
internal static partial class Models
{
    public static void Experimental(ModelRig rig, Style s, UnitDef def)
    {
        if (s.IsLattice) Skywarden(rig, s);
        else MantleCrawler(rig, s);
    }

    private static void Skywarden(ModelRig rig, Style s)
    {
        MeshKit b = rig.Body;
        foreach (float sx in Sides)
        {
            Vector3 hip = V(sx * 0.34f, 1.3f, 0.12f), knee = V(sx * 0.42f, 0.86f, -0.24f);
            Vector3 ankle = V(sx * 0.44f, 0.4f, 0.28f), foot = V(sx * 0.46f, 0.04f, 0.04f);
            b.Box(hip, V(0.2f, 0.2f, 0.2f), s.Mech);
            b.Beam(hip, knee, 0.11f, 0.13f, s.Mech);
            b.Beam(hip.Lerp(knee, 0.08f), hip.Lerp(knee, 0.86f), 0.18f, 0.22f, s.Team);
            b.Box(knee, V(0.17f, 0.17f, 0.17f), s.Mech);
            b.Beam(knee, ankle, 0.09f, 0.11f, s.Metal);
            b.Beam(knee.Lerp(ankle, 0.1f), knee.Lerp(ankle, 0.72f), 0.14f, 0.16f, s.Hull);
            b.Box(ankle, V(0.13f, 0.13f, 0.13f), s.Mech);
            b.Beam(ankle, foot + V(0, 0.04f, 0), 0.08f, 0.1f, s.Metal);
            b.Beam(ankle.Lerp(foot, 0.1f), ankle.Lerp(foot, 0.6f), 0.12f, 0.13f, s.Hull);
            foreach (float a in new[] { -0.55f, 0f, 0.55f })
                using (b.Push(foot, V(0, a, 0)))
                    b.Frustum(V(0, 0, -0.12f), V2(0.09f, 0.3f), V2(0.04f, 0.14f), 0.08f, s.HullDark, s.Hull, V2(0, -0.06f));
            b.Frustum(foot + V(0, 0, 0.12f), V2(0.08f, 0.14f), V2(0.03f, 0.06f), 0.07f, s.HullDark, s.HullDark, V2(0, 0.03f));
            b.Box(knee + V(0, 0, -0.1f), V(0.06f, 0.08f, 0.06f), s.Glow);
        }
        Parts.Slab(b, s, V(0, 1.32f, 0.1f), V(0.62f, 0.2f, 0.44f), s.HullDark, s.Mech);

        MeshKit t = rig.AddTurret(V(0, 1.4f, 0.08f));
        t.Cylinder(V(0, -0.04f, 0), 0.26f, 0.22f, 0.1f, 8, s.Mech);
        Parts.Hull(t, new Parts.Section[]
        {
            new(-0.46f, 0.26f, 0.16f, 0.02f, 0.18f, 0.28f),
            new(-0.26f, 0.46f, 0.34f, -0.02f, 0.3f, 0.46f),
            new(0.3f, 0.46f, 0.34f, -0.02f, 0.3f, 0.46f),
            new(0.5f, 0.3f, 0.2f, 0.02f, 0.24f, 0.36f),
        }, s.Hull, s.Team, s.Hull, s.Mech);
        Parts.Eye(t, s, V(0, 0.18f, -0.465f), V(0.3f, 0.05f, 0.02f));
        // The beam emitter: a long lance along the spine with glowing focus coils and lens.
        Parts.Slab(t, s, V(0, 0.54f, 0.12f), V(0.3f, 0.16f, 0.66f), s.Hull, s.Team);
        t.Rod(V(0, 0.56f, 0.2f), V(0, 0.56f, -1.08f), 0.09f, 0.075f, 8, s.Metal);
        foreach (float z in new[] { -0.32f, -0.56f, -0.8f })
            t.Rod(V(0, 0.56f, z + 0.05f), V(0, 0.56f, z - 0.05f), 0.12f, 0.12f, 8, s.Glow);
        t.Rod(V(0, 0.56f, -1.02f), V(0, 0.56f, -1.2f), 0.13f, 0.1f, 8, s.Mech, s.Glow);
        t.Sphere(V(0, 0.56f, -1.22f), V(0.07f, 0.07f, 0.04f), 8, 3, s.Glow);
        foreach (float sx in Sides)
        {
            t.Frustum(V(sx * 0.5f, 0.2f, 0.06f), V2(0.24f, 0.5f), V2(0.18f, 0.38f), 0.14f, s.Team, s.Team);
            Parts.MissilePod(t, s, V(sx * 0.54f, 0.44f, 0.12f), V(0.22f, 0.24f, 0.42f), 50, 2, 2, s.Hull);
            t.Beam(V(sx * 0.2f, 0.44f, 0.36f), V(sx * 0.3f, 0.86f, 0.54f), 0.035f, 0.22f, s.Team);
            t.Sphere(V(sx * 0.3f, 0.88f, 0.55f), V(0.035f, 0.035f, 0.035f), 6, 3, s.Glow);
        }
    }

    private static void MantleCrawler(ModelRig rig, Style s)
    {
        MeshKit b = rig.Body;
        foreach (float sx in Sides)
        foreach (var (z0, z1) in new[] { (-1.18f, -0.08f), (0.08f, 1.18f) })
            Parts.Tread(b, s, sx * 0.76f, 0.38f, z0, z1, 0.4f, s.Team);
        Vector2[] profile =
        {
            new(-1.06f, 0.2f), new(-1.06f, 0.42f), new(-0.8f, 0.78f), new(0.96f, 0.78f), new(1.1f, 0.62f), new(1.1f, 0.2f),
        };
        b.ExtrudeX(profile, -0.6f, 0.6f, s.Hull, s.HullDark, i => i == 1 ? s.HullDark : null);
        foreach (float sx in Sides)
        {
            b.Box(V(sx * 0.76f, 0.5f, 0), V(0.42f, 0.2f, 2.3f), s.HullDark, s.Hull);
            for (int i = 0; i < 4; i++)
                b.Box(V(sx * 0.605f, 0.5f, -0.6f + i * 0.4f), V(0.02f, 0.06f, 0.2f), s.GlowDim);
            Parts.Rivets(b, s, V(sx * 0.76f, 0.6f, -1.05f), V(sx * 0.76f, 0.6f, 1.05f), 7, 0.045f);
        }
        // Furnace mouth on the glacis: a glowing slot behind grille bars.
        using (b.Push(V(0, 0.58f, -0.93f), V(Rad(-54), 0, 0)))
        {
            b.Box(Vector3.Zero, V(0.74f, 0.04f, 0.32f), s.Mech);
            b.Box(V(0, 0.02f, 0), V(0.62f, 0.04f, 0.22f), s.Glow);
            for (int i = 0; i < 5; i++)
                b.Box(V(-0.24f + i * 0.12f, 0.045f, 0), V(0.035f, 0.03f, 0.26f), s.Mech);
        }
        // Ore hopper with scrap, forward on the deck.
        b.Box(V(0, 0.9f, -0.5f), V(0.86f, 0.24f, 0.46f), s.Team, s.Mech);
        foreach (var (x, z, r) in new[] { (-0.2f, -0.55f, 0.5f), (0.18f, -0.45f, -0.4f), (0.02f, -0.6f, 1.1f) })
            using (b.Push(V(x, 1.02f, z), V(0.4f, r, 0.3f)))
                b.Box(Vector3.Zero, V(0.16f, 0.07f, 0.12f), s.Metal);
        // Smokestacks at the back.
        foreach (var (x, z, h) in new[] { (-0.4f, 0.72f, 0.62f), (0.4f, 0.72f, 0.62f), (-0.2f, 0.94f, 0.48f), (0.2f, 0.94f, 0.48f) })
            Parts.Stack(b, s, V(x, 0.78f, z), 0.075f, h, band: s.TeamDark);
        Parts.Rivets(b, s, V(-0.5f, 0.78f, 0.45f), V(0.5f, 0.78f, 0.45f), 6, 0.045f);
        // Fixed point-defence lasers at the front corners.
        foreach (float sx in Sides)
        {
            b.Box(V(sx * 0.5f, 0.84f, -0.72f), V(0.14f, 0.12f, 0.16f), s.HullDark, s.Team);
            Parts.Emitter(b, s, V(sx * 0.5f, 0.85f, -0.8f), V(sx * 0.5f, 0.85f, -1.04f), 0.025f);
        }

        // Slag cannon.
        MeshKit t = rig.AddTurret(V(0, 0.78f, 0.12f));
        t.Cylinder(V(0, 0, 0), 0.36f, 0.32f, 0.1f, 6, s.Mech);
        t.Box(V(0, 0.2f, 0.06f), V(0.52f, 0.28f, 0.6f), s.Hull, s.Team);
        Parts.Rivets(t, s, V(-0.2f, 0.34f, 0.3f), V(0.2f, 0.34f, 0.3f), 3, 0.045f);
        Vector3 root = V(0, 0.24f, -0.2f), dir = Elevated(16);
        t.Rod(root, root + dir * 0.98f, 0.11f, 0.095f, 6, s.Metal);
        t.Rod(root + dir * 0.1f, root + dir * 0.3f, 0.14f, 0.14f, 6, s.TeamDark);
        t.Rod(root + dir * 0.9f, root + dir * 1.06f, 0.14f, 0.13f, 6, s.Mech, s.Glow);
        t.Beam(root + dir * 0.32f + V(0, 0.1f, 0), root + dir * 0.86f + V(0, 0.09f, 0), 0.05f, 0.02f, s.GlowDim);
    }
}
