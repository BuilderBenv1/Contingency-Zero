using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;

namespace ContingencyZero.Sim.Combat;

/// <summary>
/// How blasts reshape the ground. Depth is in height steps (four to a world unit); a crater deep
/// enough makes slopes vehicles can't climb, while bots still walk through. Nothing here reads
/// unit stats directly: craters scale with a weapon's splash radius and a death blast's radius.
/// </summary>
public static class Craters
{
    /// <summary>A death or reactor blast: a wide bowl, deep enough to block vehicles in the middle.</summary>
    public static void FromExplosion(World world, FixVec2 centre, Fix64 radius)
    {
        if (radius < Fix64.FromInt(12)) return;
        int r = radius.ToIntFloor() * 85 / 100;
        // A Commander (blast radius 26) leaves a 22-unit bowl about 7 units deep: too steep for
        // vehicles across the middle, fine for bots. A fusion reactor's is wider and deeper.
        world.DeformTerrain(centre, Fix64.FromInt(r), r * 135 / 100);
    }

    /// <summary>A shell, bomb or Breach Cannon bolt landing.</summary>
    public static void FromImpact(World world, FixVec2 centre, WeaponKind kind, Fix64 splash)
    {
        int s = splash.ToIntFloor();
        switch (kind)
        {
            // Ground corners sit on an 8-unit lattice, so anything much smaller than a tile
            // doesn't register; small hits are left to the renderer's scorch marks.
            case WeaponKind.Artillery:
                if (s >= 7) world.DeformTerrain(centre, Fix64.FromInt(s + 6), (s + 6) * 7 / 10); // heavy guns: shallow pockmarks vehicles can cross
                break;
            case WeaponKind.Bomb:
                world.DeformTerrain(centre, Fix64.FromInt(s + 8), (s + 8) * 12 / 10);          // a dimple that bombing runs dig into a hole
                break;
            case WeaponKind.DGun:
                world.DeformTerrain(centre, Fix64.FromInt(12), 14);                             // a glassy scar
                break;
        }
    }
}
