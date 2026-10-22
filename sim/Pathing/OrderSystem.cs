using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.State;

namespace ContingencyZero.Sim.Pathing;

/// <summary>
/// Carries out each unit's current order: walking to a point, closing on a target, or walking up
/// to a build site or wreck. M1 moves in straight lines; flow-field pathfinding replaces this in M3.
/// </summary>
internal static class OrderSystem
{
    public static void Tick(World world)
    {
        UnitStore units = world.Units;
        UnitCatalog catalog = world.Catalog;

        for (int id = 0; id < units.Count; id++)
        {
            if (!units.Alive[id]) continue;
            UnitDef def = catalog[units.TypeIndex[id]];

            switch (units.Order[id])
            {
                case OrderType.Move:
                    Step(world, id, def, units.OrderPosition[id]);
                    if (units.Position[id] == units.OrderPosition[id]) units.ClearOrder(id);
                    break;

                case OrderType.Attack:
                {
                    int target = units.OrderTarget[id];
                    if (!units.IsValid(target)) { units.ClearOrder(id); break; }
                    if (!InRange(units.Position[id], units.Position[target], def.WeaponRange))
                        Step(world, id, def, units.Position[target]);
                    break;
                }

                case OrderType.Build:
                {
                    BuildJob job = world.Jobs[units.OrderTarget[id]];
                    if (job.Done) { units.ClearOrder(id); break; }
                    if (!InRange(units.Position[id], job.Position, def.BuildRange))
                        Step(world, id, def, job.Position);
                    break;
                }

                case OrderType.Reclaim:
                {
                    Wreck wreck = world.Wrecks[units.OrderTarget[id]];
                    if (wreck.Gone) { units.ClearOrder(id); break; }
                    if (!InRange(units.Position[id], wreck.Position, def.BuildRange))
                        Step(world, id, def, wreck.Position);
                    break;
                }
            }
        }
    }

    public static bool InRange(FixVec2 a, FixVec2 b, Fix64 range) =>
        FixVec2.DistanceSquared(a, b) <= range * range;

    private static void Step(World world, int id, UnitDef def, FixVec2 destination)
    {
        if (!def.CanMove) return;
        world.Units.Position[id] = world.ClampToMap(
            FixVec2.MoveTowards(world.Units.Position[id], destination, def.SpeedPerTick));
    }
}
