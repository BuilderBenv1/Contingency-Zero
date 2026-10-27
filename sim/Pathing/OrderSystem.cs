using System.Collections.Generic;
using ContingencyZero.Sim.Combat;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Economy;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.State;

namespace ContingencyZero.Sim.Pathing;

/// <summary>
/// Carries out each unit's current order and moves it: walking a path, closing on a target,
/// walking up to a build site or wreck, patrolling. Then pushes overlapping ground units apart.
/// Idle armed units chase enemies that come into view, within a leash of where they were parked.
/// </summary>
internal static class OrderSystem
{
    /// <summary>How far an idle unit will chase before going back to its post.</summary>
    private static readonly Fix64 Leash = Fix64.FromInt(40);
    private static readonly Fix64 MaxPushPerTick = Fix64.Half;

    public static void Tick(World world)
    {
        UnitStore units = world.Units;
        int count = units.Count;
        var before = new FixVec2[count];
        for (int id = 0; id < count; id++) before[id] = units.Position[id];

        ServePathRequests(world);

        // Alternate the order units move in, tick by tick, so no player slot always moves first.
        bool reverse = (world.Tick & 1) == 1;
        for (int k = 0; k < count; k++)
        {
            int id = reverse ? count - 1 - k : k;
            if (!units.Alive[id] || !units.Complete[id]) continue;
            UnitDef def = world.Catalog[units.TypeIndex[id]];
            if (units.Orders[id].Count == 0)
                Idle(world, id, def);
            else
                Execute(world, id, def);
        }

        world.Grid.Rebuild(units);
        Separate(world);

        for (int id = 0; id < count; id++)
        {
            units.Velocity[id] = units.Position[id] - before[id];
            // Stuck in a crowd: following a path but going nowhere after being jostled.
            if (units.Path[id] is not null && units.Alive[id])
            {
                UnitDef def = world.Catalog[units.TypeIndex[id]];
                Fix64 slow = def.SpeedPerTick / 5;
                if (units.Velocity[id].LengthSquared() < slow * slow) units.StuckTicks[id]++;
            }
        }
    }

    // ---- Orders --------------------------------------------------------------------------------

    private static void Execute(World world, int id, UnitDef def)
    {
        UnitStore units = world.Units;
        Order order = units.Orders[id][0];

        switch (order.Type)
        {
            case OrderType.Move:
                if (!def.CanMove || MoveTo(world, id, def, order.Position, Fix64.FromInt(2)))
                    units.PopOrder(id);
                break;

            case OrderType.Attack:
            {
                int target = order.Target;
                if (!units.IsValid(target)) { units.PopOrder(id); break; }
                FixVec2 tpos = units.Position[target];
                order.Position = tpos;
                units.Orders[id][0] = order;
                if (!def.CanMove) break;
                Fix64 range = CombatSystem.BestRangeAgainst(world, def, target);
                if (range <= Fix64.Zero) { units.PopOrder(id); break; } // nothing that can hit it
                bool bomber = HasBomb(def);
                Fix64 stop = bomber ? Fix64.One : range * Fix64.Parse("0.9");
                if (FixVec2.DistanceSquared(units.Position[id], tpos) > stop * stop || bomber)
                    MoveTo(world, id, def, tpos, stop);
                else
                    units.ClearPath(id);
                break;
            }

            case OrderType.AttackGround:
            {
                if (!def.CanMove) break;
                Fix64 range = CombatSystem.BestGroundRange(def);
                if (range <= Fix64.Zero) { units.PopOrder(id); break; }
                Fix64 stop = HasBomb(def) ? Fix64.One : range * Fix64.Parse("0.9");
                if (FixVec2.DistanceSquared(units.Position[id], order.Position) > stop * stop)
                    MoveTo(world, id, def, order.Position, stop);
                else
                    units.ClearPath(id);
                break;
            }

            case OrderType.Build:
                ExecuteBuild(world, id, def, order);
                break;

            case OrderType.Reclaim:
            {
                if (order.Target < 0 || order.Target >= world.Features.Count || world.Features[order.Target].Gone)
                {
                    units.PopOrder(id);
                    break;
                }
                Feature f = world.Features[order.Target];
                Fix64 reach = def.BuildRange + f.Radius - Fix64.One;
                if (FixVec2.DistanceSquared(units.Position[id], f.Position) > reach * reach)
                    MoveTo(world, id, def, f.Position, reach);
                else
                    units.ClearPath(id);
                break;
            }

            case OrderType.Assist:
                ExecuteAssist(world, id, def, order);
                break;

            case OrderType.ReclaimArea:
            {
                // Work through the wrecks, rocks and trees inside the circle, nearest first.
                if (order.Target < 0 || order.Target >= world.Features.Count || world.Features[order.Target].Gone)
                {
                    int next = NearestFeatureIn(world, order.Position, Fix64.FromInt(order.TypeIndex), units.Position[id]);
                    if (next < 0) { units.PopOrder(id); break; }
                    order.Target = next;
                    units.Orders[id][0] = order;
                }
                Feature f = world.Features[order.Target];
                Fix64 reach = def.BuildRange + f.Radius - Fix64.One;
                if (FixVec2.DistanceSquared(units.Position[id], f.Position) > reach * reach)
                    MoveTo(world, id, def, f.Position, reach);
                else
                    units.ClearPath(id);
                break;
            }

            case OrderType.RepairArea:
            {
                // Help with every frame and damaged friendly unit inside the circle, nearest first.
                if (!units.IsValid(order.Target) || !NeedsRepair(world, order.Target))
                {
                    int next = NearestRepairJobIn(world, id, order.Position, Fix64.FromInt(order.TypeIndex));
                    if (next < 0) { units.PopOrder(id); break; }
                    order.Target = next;
                    units.Orders[id][0] = order;
                }
                int target = order.Target;
                if (!EconomySystem.InBuildRange(world, id, def, target))
                {
                    UnitDef tdef = world.Catalog[units.TypeIndex[target]];
                    Fix64 half = tdef.IsStructure ? Fix64.FromInt(tdef.Footprint * MapData.TileSize / 2) : tdef.Radius;
                    MoveTo(world, id, def, units.Position[target], def.BuildRange + half - Fix64.One);
                }
                else
                {
                    units.ClearPath(id);
                }
                break;
            }

            case OrderType.AttackArea:
                ExecuteAttackArea(world, id, def, order);
                break;

            case OrderType.Patrol:
                if (MoveTo(world, id, def, order.Position, Fix64.FromInt(3)))
                {
                    (order.Position, order.Origin) = (order.Origin, order.Position);
                    units.Orders[id][0] = order;
                    units.ClearPath(id);
                }
                break;

            case OrderType.Fight:
                if (def.IsArmed && units.Fire[id] != FireState.HoldFire && Engage(world, id, def, Fix64.Zero))
                    break;
                if (MoveTo(world, id, def, order.Position, Fix64.FromInt(4)))
                    units.PopOrder(id);
                break;

            case OrderType.DGun:
            {
                int slot = CombatSystem.DGunSlot(def);
                if (slot < 0) { units.PopOrder(id); break; }
                WeaponDef w = def.Weapons[slot];
                Fix64 range = w.Range;
                if (FixVec2.DistanceSquared(units.Position[id], order.Position) > range * range)
                {
                    MoveTo(world, id, def, order.Position, range - Fix64.One);
                    break;
                }
                units.ClearPath(id);
                if (CombatSystem.ReloadOf(units, id, slot) > 0) break;
                PlayerState owner = world.Players[units.Owner[id]];
                if (owner.Energy < w.EnergyPerShot)
                {
                    world.Events.Add(new GameEvent(GameEventType.OrderFailed, id, units.Owner[id], 1));
                    units.PopOrder(id);
                    break;
                }
                owner.Energy -= w.EnergyPerShot;
                CombatSystem.FireDGun(world, id, slot, order.Position);
                units.PopOrder(id);
                break;
            }

            default:
                units.PopOrder(id);
                break;
        }
    }

    private static void ExecuteBuild(World world, int id, UnitDef def, Order order)
    {
        UnitStore units = world.Units;
        UnitDef tdef = world.Catalog[order.TypeIndex];
        Fix64 half = Fix64.FromInt(tdef.Footprint * MapData.TileSize / 2);
        Fix64 reach = def.BuildRange + half - Fix64.One;

        if (order.Target >= 0)
        {
            if (!units.IsValid(order.Target) || units.Complete[order.Target]) { units.PopOrder(id); return; }
            if (!EconomySystem.InBuildRange(world, id, def, order.Target))
                MoveTo(world, id, def, units.Position[order.Target], reach);
            else
                units.ClearPath(id);
            return;
        }

        if (FixVec2.DistanceSquared(units.Position[id], order.Position) > reach * reach)
        {
            MoveTo(world, id, def, order.Position, reach);
            return;
        }

        units.ClearPath(id);
        int frame = world.StartOrJoinFrame(id, order.TypeIndex, order.Position);
        if (frame < 0)
        {
            world.Events.Add(new GameEvent(GameEventType.OrderFailed, id, units.Owner[id], 2, Position: order.Position));
            units.PopOrder(id);
            return;
        }
        order.Target = frame;
        units.Orders[id][0] = order;
    }

    /// <summary>The closest reclaimable thing to <paramref name="from"/> within <paramref name="radius"/> of <paramref name="centre"/>, or -1.</summary>
    private static int NearestFeatureIn(World world, FixVec2 centre, Fix64 radius, FixVec2 from)
    {
        int best = -1;
        Fix64 bestD = Fix64.MaxValue;
        Fix64 r2 = radius * radius;
        foreach (Feature f in world.Features)
        {
            if (f.Gone) continue;
            if (FixVec2.DistanceSquared(f.Position, centre) > r2) continue;
            Fix64 d = FixVec2.DistanceSquared(f.Position, from);
            if (d < bestD) { bestD = d; best = f.Id; }
        }
        return best;
    }

    /// <summary>
    /// Area attack: fight the nearest visible enemy inside the circle until none are left. On the
    /// way in it fights whatever it meets, like attack-move; once inside with nothing left, it's done.
    /// </summary>
    private static void ExecuteAttackArea(World world, int id, UnitDef def, Order order)
    {
        UnitStore units = world.Units;
        Fix64 radius = Fix64.FromInt(order.TypeIndex);
        int team = world.TeamOf(id);
        int target = order.Target;
        bool keep = units.IsValid(target) && world.CanSee(team, target, radarCounts: true)
                    && FixVec2.DistanceSquared(units.Position[target], order.Position) <= (radius + Fix64.FromInt(8)) * (radius + Fix64.FromInt(8));
        if (!keep)
        {
            // Look again straight away when a target is lost; while walking in, every few ticks.
            target = -1;
            if (order.Target >= 0 || (world.Tick + id) % 8 == 0)
                target = NearestEnemyIn(world, id, def, order.Position, radius);
            if (order.Target != target)
            {
                order.Target = target;
                units.Orders[id][0] = order;
            }
        }

        if (target >= 0)
        {
            Fix64 range = CombatSystem.BestRangeAgainst(world, def, target);
            Fix64 stop = HasBomb(def) ? Fix64.One : range * Fix64.Parse("0.9");
            if (FixVec2.DistanceSquared(units.Position[id], units.Position[target]) > stop * stop || HasBomb(def))
                MoveTo(world, id, def, units.Position[target], stop);
            else
                units.ClearPath(id);
            return;
        }

        if (units.Fire[id] != FireState.HoldFire && Engage(world, id, def, Fix64.Zero)) return;
        Fix64 inner = radius / 2;
        if (FixVec2.DistanceSquared(units.Position[id], order.Position) <= inner * inner
            || MoveTo(world, id, def, order.Position, inner))
        {
            if (NearestEnemyIn(world, id, def, order.Position, radius) < 0) units.PopOrder(id);
        }
    }

    /// <summary>The closest visible enemy to the unit inside the circle that it can hurt, or -1. Ties go to the lower id.</summary>
    private static int NearestEnemyIn(World world, int id, UnitDef def, FixVec2 centre, Fix64 radius)
    {
        UnitStore units = world.Units;
        int team = world.TeamOf(id);
        Fix64 r2 = radius * radius;
        int best = -1;
        Fix64 bestD = Fix64.MaxValue;
        for (int e = 0; e < units.Count; e++)
        {
            if (!units.Alive[e] || world.Players[units.Owner[e]].Team == team) continue;
            if (FixVec2.DistanceSquared(units.Position[e], centre) > r2) continue;
            if (!world.CanSee(team, e, radarCounts: true) || !CombatSystem.CanEverHit(world, def, e)) continue;
            Fix64 d = FixVec2.DistanceSquared(units.Position[e], units.Position[id]);
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }

    private static bool NeedsRepair(World world, int target)
    {
        UnitStore units = world.Units;
        UnitDef tdef = world.Catalog[units.TypeIndex[target]];
        return !units.Complete[target] || units.Health[target] < tdef.MaxHealth;
    }

    /// <summary>The closest friendly frame or damaged unit to the worker inside the circle, or -1.</summary>
    private static int NearestRepairJobIn(World world, int worker, FixVec2 centre, Fix64 radius)
    {
        UnitStore units = world.Units;
        int owner = units.Owner[worker];
        int best = -1;
        Fix64 bestD = Fix64.MaxValue;
        Fix64 r2 = radius * radius;
        for (int id = 0; id < units.Count; id++)
        {
            if (!units.Alive[id] || id == worker || world.AreEnemies(owner, units.Owner[id])) continue;
            if (!NeedsRepair(world, id)) continue;
            if (FixVec2.DistanceSquared(units.Position[id], centre) > r2) continue;
            Fix64 d = FixVec2.DistanceSquared(units.Position[id], units.Position[worker]);
            if (d < bestD) { bestD = d; best = id; }
        }
        return best;
    }

    private static void ExecuteAssist(World world, int id, UnitDef def, Order order)
    {
        UnitStore units = world.Units;
        int target = order.Target;
        if (!units.IsValid(target)) { units.PopOrder(id); return; }
        UnitDef tdef = world.Catalog[units.TypeIndex[target]];
        FixVec2 tpos = units.Position[target];

        bool canWork = def.CanAssist;
        bool needsWork = !units.Complete[target]
                         || (tdef.IsFactory && units.FactoryCurrent[target] >= 0)
                         || units.Health[target] < tdef.MaxHealth;

        if (canWork && tdef.IsStructure && units.Complete[target] && !tdef.IsFactory && !needsWork)
        {
            units.PopOrder(id); // finished and healthy: nothing left to help with
            return;
        }

        if (canWork && needsWork)
        {
            if (!EconomySystem.InBuildRange(world, id, def, target))
            {
                Fix64 half = tdef.IsStructure ? Fix64.FromInt(tdef.Footprint * MapData.TileSize / 2) : tdef.Radius;
                MoveTo(world, id, def, tpos, def.BuildRange + half - Fix64.One);
            }
            else
            {
                units.ClearPath(id);
            }
            return;
        }

        // Guard: stay close to the target.
        if (!def.CanMove) return;
        Fix64 follow = Fix64.FromInt(14) + tdef.Radius;
        if (FixVec2.DistanceSquared(units.Position[id], tpos) > follow * follow)
            MoveTo(world, id, def, tpos, follow - Fix64.FromInt(4));
        else
            units.ClearPath(id);
    }

    private static void Idle(World world, int id, UnitDef def)
    {
        UnitStore units = world.Units;
        if (!def.CanMove) return;

        if (def.IsArmed && units.Fire[id] == FireState.FireAtWill && !def.IsCommander && Engage(world, id, def, Leash))
            return;

        // Idle units let themselves be nudged about; only a big shove sends them back to their post.
        Fix64 drift = Fix64.FromInt(20);
        if (FixVec2.DistanceSquared(units.Position[id], units.Anchor[id]) > drift * drift)
        {
            if (MoveTo(world, id, def, units.Anchor[id], Fix64.FromInt(2)))
                units.Anchor[id] = units.Position[id];
        }
        else
        {
            units.ClearPath(id);
        }
    }

    /// <summary>
    /// Closes on the nearest visible enemy this unit can hurt, if there is one within sight.
    /// With a leash, only chases enemies within that distance of the unit's anchor.
    /// Returns true if it found something to fight.
    /// </summary>
    private static bool Engage(World world, int id, UnitDef def, Fix64 leash)
    {
        UnitStore units = world.Units;
        int team = world.TeamOf(id);
        FixVec2 pos = units.Position[id];
        int enemy = world.Grid.FindNearest(units, pos, def.SightRange, e =>
            units.Alive[e] && world.Players[units.Owner[e]].Team != team && CombatSystem.CanEverHit(world, def, e)
            && world.CanSee(team, e));
        if (enemy < 0) return false;
        if (leash > Fix64.Zero && FixVec2.DistanceSquared(units.Anchor[id], units.Position[enemy]) > leash * leash) return false;

        Fix64 stop = CombatSystem.BestRangeAgainst(world, def, enemy) * Fix64.Parse("0.85");
        if (HasBomb(def)) stop = Fix64.One;
        if (FixVec2.DistanceSquared(pos, units.Position[enemy]) > stop * stop)
            MoveTo(world, id, def, units.Position[enemy], stop);
        else
            units.ClearPath(id);
        return true;
    }

    private static bool HasBomb(UnitDef def)
    {
        foreach (WeaponDef w in def.Weapons) if (w.Kind == WeaponKind.Bomb) return true;
        return false;
    }

    // ---- Movement ------------------------------------------------------------------------------

    /// <summary>
    /// Moves one tick toward <paramref name="goal"/>, pathing round terrain and structures.
    /// Returns true once within <paramref name="stop"/> of the goal, or as close as it can get.
    /// </summary>
    internal static bool MoveTo(World world, int id, UnitDef def, FixVec2 goal, Fix64 stop)
    {
        UnitStore units = world.Units;
        if (!def.CanMove) return true;
        FixVec2 pos = units.Position[id];
        if (stop < Fix64.One) stop = Fix64.One;
        if (FixVec2.DistanceSquared(pos, goal) <= stop * stop)
        {
            units.ClearPath(id);
            return true;
        }

        if (def.IsAir)
        {
            units.Position[id] = world.ClampToMap(FixVec2.MoveTowards(pos, goal, def.SpeedPerTick));
            return false;
        }

        // Standing on a structure (just built it, or rolling off a factory pad): walk straight out.
        int tx = MapData.TileOf(pos.X), ty = MapData.TileOf(pos.Y);
        if (!world.Terrain.Passable(def.MoveClass, tx, ty) && world.Terrain.TerrainPassable(def.MoveClass, tx, ty))
        {
            units.Position[id] = world.ClampToMap(FixVec2.MoveTowards(pos, goal, def.SpeedPerTick));
            units.ClearPath(id);
            return false;
        }

        Fix64 regoal = Fix64.FromInt(6);
        var path = units.Path[id];
        bool stale = path is null || FixVec2.DistanceSquared(units.PathGoal[id], goal) > regoal * regoal;
        if (stale)
        {
            if (world.Pathfinder.StraightLineClear(def.MoveClass, pos, goal))
            {
                // Open ground: walk straight there, no search needed.
                path = new List<FixVec2> { goal };
                units.Path[id] = path;
                units.PathIndex[id] = 0;
                units.PathGoal[id] = goal;
                units.PathPending[id] = false;
                units.StuckTicks[id] = 0;
            }
            else
            {
                units.PathRequestGoal[id] = goal;
                if (!units.PathPending[id])
                {
                    units.PathPending[id] = true;
                    world.PathQueue.Enqueue(id);
                }
                if (path is null) return false; // wait for the search
                // Otherwise keep following the old path until the new one arrives.
            }
        }

        if (path is null) return false;
        if (units.PathIndex[id] >= path.Count) return true; // unreachable or done: as close as it gets

        FixVec2 waypoint = path[units.PathIndex[id]];
        FixVec2 next = FixVec2.MoveTowards(pos, waypoint, def.SpeedPerTick);
        units.Position[id] = next;
        if (next == waypoint) units.PathIndex[id]++;

        // Stuck detection (counted after crowd pushing in Tick): going nowhere for a few seconds
        // means repath, or give up if we're already close.
        if (units.StuckTicks[id] > 45)
        {
            Fix64 near = stop + def.Radius * 6 + Fix64.FromInt(6);
            if (FixVec2.DistanceSquared(pos, goal) <= near * near) { units.ClearPath(id); return true; }
            units.Path[id] = null;
            units.StuckTicks[id] = 0;
        }

        return units.PathIndex[id] >= path.Count && FixVec2.DistanceSquared(units.Position[id], goal) <= (stop + def.Radius * 2) * (stop + def.Radius * 2);
    }

    /// <summary>Runs queued path searches, oldest first, up to this tick's budget.</summary>
    private static void ServePathRequests(World world)
    {
        UnitStore units = world.Units;
        while (world.PathBudget > 0 && world.PathQueue.Count > 0)
        {
            int id = world.PathQueue.Dequeue();
            if (!units.Alive[id] || !units.PathPending[id]) continue;
            UnitDef def = world.Catalog[units.TypeIndex[id]];
            FixVec2 goal = units.PathRequestGoal[id];
            units.Path[id] = world.Pathfinder.Find(def.MoveClass, units.Position[id], goal);
            // The budget counts tiles searched, not requests: many short paths fit in a tick,
            // one long search uses most of it.
            world.PathBudget -= Math2.Max(1, world.Pathfinder.LastExpansions);
            units.PathIndex[id] = 0;
            units.PathGoal[id] = goal;
            units.PathPending[id] = false;
            units.StuckTicks[id] = 0;
        }
    }

    /// <summary>Pushes overlapping ground units apart, in id order, never onto ground they can't stand on.</summary>
    private static void Separate(World world)
    {
        UnitStore units = world.Units;
        for (int id = 0; id < units.Count; id++)
        {
            if (!units.Alive[id] || !units.Complete[id]) continue;
            UnitDef a = world.Catalog[units.TypeIndex[id]];
            if (!a.CanMove || a.IsAir) continue;

            var neighbours = world.ScratchNeighbours;
            neighbours.Clear();
            world.Grid.Query(units, units.Position[id], a.Radius, neighbours, Fix64.FromInt(10));
            foreach (int other in neighbours)
            {
                if (other <= id || !units.Alive[other] || !units.Complete[other]) continue;
                UnitDef b = world.Catalog[units.TypeIndex[other]];
                if (!b.CanMove || b.IsAir) continue;

                Fix64 minDist = a.Radius + b.Radius;
                FixVec2 delta = units.Position[other] - units.Position[id];
                Fix64 d2 = delta.LengthSquared();
                if (d2 >= minDist * minDist) continue;

                Fix64 d = Fix64.Sqrt(d2);
                FixVec2 dir;
                if (d == Fix64.Zero)
                {
                    // Exactly on top of each other: split along a direction picked from the ids.
                    int k = (id * 7 + other * 13) & 3;
                    dir = k switch
                    {
                        0 => new FixVec2(Fix64.One, Fix64.Zero),
                        1 => new FixVec2(Fix64.Zero, Fix64.One),
                        2 => new FixVec2(-Fix64.One, Fix64.Zero),
                        _ => new FixVec2(Fix64.Zero, -Fix64.One),
                    };
                }
                else
                {
                    dir = delta * (Fix64.One / d);
                }

                Fix64 push = Fix64.Min((minDist - d) / 2, MaxPushPerTick);
                // A unit that is still moving gives way less than one that is parked.
                bool aMoving = units.Path[id] is not null;
                bool bMoving = units.Path[other] is not null;
                Fix64 pushA = aMoving && !bMoving ? push / 2 : push;
                Fix64 pushB = bMoving && !aMoving ? push / 2 : push;
                TryPlace(world, id, a, units.Position[id] - dir * pushA);
                TryPlace(world, other, b, units.Position[other] + dir * pushB);
            }
        }
    }

    private static void TryPlace(World world, int id, UnitDef def, FixVec2 p)
    {
        p = world.ClampToMap(p);
        if (world.Terrain.Passable(def.MoveClass, p) || !world.Terrain.Passable(def.MoveClass, world.Units.Position[id]))
            world.Units.Position[id] = p;
    }
}
