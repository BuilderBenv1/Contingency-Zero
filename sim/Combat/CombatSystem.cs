using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.State;

namespace ContingencyZero.Sim.Combat;

/// <summary>
/// Target acquisition and firing. M1 uses instant hits with an accuracy roll; ballistic projectiles
/// with travel time and terrain blocking come in M3.
/// </summary>
internal static class CombatSystem
{
    public static void Tick(World world)
    {
        UnitStore units = world.Units;
        UnitCatalog catalog = world.Catalog;
        world.Grid.Rebuild(units);

        for (int id = 0; id < units.Count; id++)
        {
            if (!units.Alive[id]) continue;
            UnitDef def = catalog[units.TypeIndex[id]];
            if (!def.IsArmed) continue;

            if (units.ReloadLeft[id] > 0) units.ReloadLeft[id]--;

            int team = world.Players[units.Owner[id]].Team;
            int target = PickTarget(world, id, def, team);
            units.WeaponTarget[id] = target;
            if (target == -1 || units.ReloadLeft[id] > 0) continue;

            units.ReloadLeft[id] = def.ReloadTicks;
            if (world.Rng.Chance(def.Accuracy))
                units.Health[target] -= def.WeaponDamage;
        }

        // Resolve deaths after all units have fired this tick, in id order.
        for (int id = 0; id < units.Count; id++)
        {
            if (units.Alive[id] && units.Health[id] <= Fix64.Zero)
                world.Kill(id);
        }
    }

    private static int PickTarget(World world, int id, UnitDef def, int team)
    {
        UnitStore units = world.Units;
        FixVec2 pos = units.Position[id];
        Fix64 rangeSq = def.WeaponRange * def.WeaponRange;

        // A focus-fire order wins whenever its target is alive and in range.
        if (units.Order[id] == OrderType.Attack)
        {
            int focus = units.OrderTarget[id];
            if (units.IsValid(focus) && FixVec2.DistanceSquared(pos, units.Position[focus]) <= rangeSq)
                return focus;
        }

        // Keep the current target while it stays valid, so units don't flicker between targets.
        int current = units.WeaponTarget[id];
        if (units.IsValid(current)
            && world.Players[units.Owner[current]].Team != team
            && FixVec2.DistanceSquared(pos, units.Position[current]) <= rangeSq)
            return current;

        return world.Grid.FindNearestEnemy(units, world.Players, pos, team, def.WeaponRange);
    }
}
