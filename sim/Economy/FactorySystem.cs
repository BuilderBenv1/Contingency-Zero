using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.State;

namespace ContingencyZero.Sim.Economy;

/// <summary>
/// Starts the next unit in each factory's queue, and releases finished units out of the door
/// toward the rally point. Build work itself is paid for in <see cref="EconomySystem"/>.
/// </summary>
internal static class FactorySystem
{
    public static void Tick(World world)
    {
        UnitStore units = world.Units;
        int count = units.Count; // units spawned this tick are handled next tick
        for (int id = 0; id < count; id++)
        {
            if (!units.Alive[id] || !units.Complete[id]) continue;
            UnitDef def = world.Catalog[units.TypeIndex[id]];
            if (!def.IsFactory) continue;

            int current = units.FactoryCurrent[id];
            if (current >= 0)
            {
                if (!units.IsValid(current))
                {
                    units.FactoryCurrent[id] = -1; // destroyed on the pad
                }
                else if (units.Complete[current])
                {
                    Release(world, id, current);
                    var queue = units.FactoryQueue[id];
                    if (queue.Count > 0)
                    {
                        int built = queue[0];
                        queue.RemoveAt(0);
                        if (units.FactoryRepeat[id]) queue.Add(built);
                    }
                    units.FactoryCurrent[id] = -1;
                    world.Players[units.Owner[id]].UnitsBuilt++;
                }
                else
                {
                    continue; // still building
                }
            }

            if (units.FactoryQueue[id].Count == 0) continue;
            int type = units.FactoryQueue[id][0];
            int frame = world.SpawnUnit(type, units.Owner[id], units.Position[id], complete: false);
            units.FactoryCurrent[id] = frame;
            world.Events.Add(new GameEvent(GameEventType.BuildStarted, frame, id, Position: units.Position[frame]));
        }
    }

    /// <summary>Sends a finished unit out of the factory's front (south side) and on to the rally point.</summary>
    private static void Release(World world, int factory, int unit)
    {
        UnitStore units = world.Units;
        UnitDef fdef = world.Catalog[units.TypeIndex[factory]];
        UnitDef udef = world.Catalog[units.TypeIndex[unit]];
        if (!udef.CanMove) return;

        Fix64 half = Fix64.FromInt(fdef.Footprint * Map.MapData.TileSize / 2 + 4);
        FixVec2 door = world.ClampToMap(units.Position[factory] + new FixVec2(Fix64.Zero, half));
        if (!udef.IsAir) door = world.Pathfinder.NearestPassable(udef.MoveClass, door);

        units.ClearOrders(unit);
        units.Orders[unit].Add(new Order(OrderType.Move, door));
        // Spread finished units over the rally points: in turn, or every Nth one to the next extra stop.
        if (!units.HasRally[factory]) return;
        var extra = units.RallyExtra[factory];
        int n = units.RallyCount[factory]++;
        FixVec2 goal = units.Rally[factory];
        if (extra.Count > 0)
        {
            int every = units.RallyEvery[factory];
            if (every <= 0)
            {
                int k = n % (extra.Count + 1);
                if (k > 0) goal = extra[k - 1];
            }
            else if ((n + 1) % every == 0)
            {
                goal = extra[((n + 1) / every - 1) % extra.Count];
            }
        }
        units.Orders[unit].Add(new Order(OrderType.Move, goal));
    }
}
