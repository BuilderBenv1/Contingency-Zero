using System.Collections.Generic;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Pathing;
using ContingencyZero.Sim.State;

namespace ContingencyZero.Sim.Economy;

/// <summary>
/// The flow economy. Each tick every player gets income from their units, reclaim adds metal and
/// energy from features, and every builder and factory that is working asks for metal and energy
/// in proportion to the build work it does. If a player can't cover the total, every build is
/// slowed by the same ratio (a stall). Surplus above storage is wasted. Repair is free.
/// </summary>
internal static class EconomySystem
{
    /// <summary>Energy from reclaiming trees comes this many times faster than metal.</summary>
    private const int EnergyReclaimFactor = 5;

    private readonly struct Work
    {
        public readonly int Player;
        public readonly int Builder;
        public readonly int Frame;
        public readonly Fix64 Amount;
        public readonly Fix64 Metal;
        public readonly Fix64 Energy;

        public Work(int player, int builder, int frame, Fix64 amount, Fix64 metal, Fix64 energy)
        {
            Player = player;
            Builder = builder;
            Frame = frame;
            Amount = amount;
            Metal = metal;
            Energy = energy;
        }
    }

    public static void Tick(World world)
    {
        PlayerState[] players = world.Players;
        UnitStore units = world.Units;
        UnitCatalog catalog = world.Catalog;
        int playerCount = players.Length;

        var metalIncome = new Fix64[playerCount];
        var energyIncome = new Fix64[playerCount];
        var metalDemand = new Fix64[playerCount];
        var energyDemand = new Fix64[playerCount];
        var metalStorage = new Fix64[playerCount];
        var energyStorage = new Fix64[playerCount];
        var convertEnergy = new Fix64[playerCount];
        var convertMetal = new Fix64[playerCount];
        for (int p = 0; p < playerCount; p++)
        {
            metalStorage[p] = World.BaseStorage;
            energyStorage[p] = World.BaseStorage;
        }

        // Planned work per frame this tick, so several builders on one frame never overshoot it.
        var planned = new Dictionary<int, Fix64>();
        var pending = new List<Work>();
        Fix64 windPerTick = world.Wind / World.TicksPerSecond;

        for (int id = 0; id < units.Count; id++)
        {
            if (!units.Alive[id] || !units.Complete[id]) continue;
            int owner = units.Owner[id];
            UnitDef def = catalog[units.TypeIndex[id]];
            Fix64 mult = players[owner].IncomeMultiplier;

            metalIncome[owner] += (def.MetalIncomePerTick + units.ExtractorYield[id]) * mult;
            energyIncome[owner] += def.EnergyIncomePerTick * mult;
            if (def.UsesWind) energyIncome[owner] += windPerTick * mult;
            metalStorage[owner] += def.MetalStorage;
            energyStorage[owner] += def.EnergyStorage;
            if (def.ConvertEnergyPerTick > Fix64.Zero && players[owner].ConvertersOn)
            {
                convertEnergy[owner] += def.ConvertEnergyPerTick;
                convertMetal[owner] += def.ConvertMetalPerTick * mult;
            }

            if (def.SelfReclaimRange > Fix64.Zero)
                SelfReclaim(world, id, def);

            if (def.BuildPowerPerTick <= Fix64.Zero) continue;

            // Factories put their power into the unit on the pad.
            if (def.IsFactory)
            {
                int product = units.FactoryCurrent[id];
                if (product >= 0 && units.IsValid(product) && !units.Complete[product])
                    PlanWork(world, planned, pending, metalDemand, energyDemand, owner, id, product, def.BuildPowerPerTick);
                continue;
            }

            if (units.Orders[id].Count == 0) continue;
            Order order = units.Orders[id][0];
            switch (order.Type)
            {
                case OrderType.Build:
                    if (order.Target >= 0 && units.IsValid(order.Target) && !units.Complete[order.Target]
                        && InBuildRange(world, id, def, order.Target))
                        PlanWork(world, planned, pending, metalDemand, energyDemand, owner, id, order.Target, def.BuildPowerPerTick);
                    break;

                case OrderType.Assist:
                case OrderType.RepairArea:
                {
                    int target = order.Target;
                    if (!units.IsValid(target)) break;
                    UnitDef tdef = catalog[units.TypeIndex[target]];
                    if (!InBuildRange(world, id, def, target)) break;
                    if (!units.Complete[target])
                    {
                        PlanWork(world, planned, pending, metalDemand, energyDemand, owner, id, target, def.BuildPowerPerTick);
                    }
                    else if (tdef.IsFactory && units.FactoryCurrent[target] >= 0 && units.IsValid(units.FactoryCurrent[target])
                             && !units.Complete[units.FactoryCurrent[target]])
                    {
                        PlanWork(world, planned, pending, metalDemand, energyDemand, owner, id, units.FactoryCurrent[target], def.BuildPowerPerTick);
                    }
                    else if (units.Health[target] < tdef.MaxHealth)
                    {
                        // Repair: free, at build-power speed.
                        Fix64 heal = tdef.MaxHealth * def.BuildPowerPerTick / tdef.BuildTime;
                        if (heal < Fix64.One) heal = Fix64.One;
                        units.Health[target] = Fix64.Min(tdef.MaxHealth, units.Health[target] + heal);
                        world.Events.Add(new GameEvent(GameEventType.NanoBeam, id, target, Position2: units.Position[target]));
                    }
                    break;
                }

                case OrderType.Reclaim:
                case OrderType.ReclaimArea:
                {
                    if (order.Target < 0 || order.Target >= world.Features.Count) break;
                    Feature f = world.Features[order.Target];
                    if (f.Gone) break;
                    Fix64 reach = def.BuildRange + f.Radius;
                    if (FixVec2.DistanceSquared(units.Position[id], f.Position) > reach * reach) break;
                    Fix64 rate = def.BuildPowerPerTick * def.ReclaimMultiplier / 10;
                    if (rate < Fix64.FromRatio(1, 15)) rate = Fix64.FromRatio(1, 15);
                    Fix64 m = Fix64.Min(f.Metal, rate);
                    Fix64 e = Fix64.Min(f.Energy, rate * EnergyReclaimFactor);
                    f.Metal -= m;
                    f.Energy -= e;
                    metalIncome[owner] += m;
                    energyIncome[owner] += e;
                    world.Events.Add(new GameEvent(GameEventType.ReclaimBeam, id, order.Target, Position2: f.Position));
                    if (f.Gone) world.OnFeatureReclaimed(owner, f);
                    break;
                }
            }
        }

        // One funding ratio per player: the tighter of metal and energy.
        var ratio = new Fix64[playerCount];
        for (int p = 0; p < playerCount; p++)
        {
            PlayerState player = players[p];
            Fix64 r = Fix64.One;
            if (metalDemand[p] > Fix64.Zero)
                r = Fix64.Min(r, (player.Metal + metalIncome[p]) / metalDemand[p]);
            if (energyDemand[p] > Fix64.Zero)
                r = Fix64.Min(r, (player.Energy + energyIncome[p]) / energyDemand[p]);
            ratio[p] = Fix64.Max(Fix64.Zero, r);
            player.MetalSpent = Fix64.Zero;
            player.EnergySpent = Fix64.Zero;
        }

        foreach (Work w in pending)
        {
            Fix64 r = ratio[w.Player];
            Fix64 done = w.Amount * r;
            UnitDef target = catalog[units.TypeIndex[w.Frame]];
            units.BuildProgress[w.Frame] += done;
            units.Health[w.Frame] = Fix64.Min(target.MaxHealth, units.Health[w.Frame] + target.MaxHealth * done / target.BuildTime);
            players[w.Player].MetalSpent += w.Metal * r;
            players[w.Player].EnergySpent += w.Energy * r;
            if (w.Builder != w.Frame)
                world.Events.Add(new GameEvent(GameEventType.NanoBeam, w.Builder, w.Frame, Position2: units.Position[w.Frame]));
        }

        for (int p = 0; p < playerCount; p++)
        {
            PlayerState player = players[p];
            player.MetalStorage = metalStorage[p];
            player.EnergyStorage = energyStorage[p];
            player.BuildRatio = ratio[p];

            Fix64 metal = Fix64.Max(Fix64.Zero, player.Metal + metalIncome[p] - player.MetalSpent);
            Fix64 energy = Fix64.Max(Fix64.Zero, player.Energy + energyIncome[p] - player.EnergySpent);

            // Metal makers only use energy above half of storage, so they soak up surplus without
            // starving construction; near the line they run part-time.
            Fix64 reserve = energyStorage[p] / 2;
            player.ConverterEnergy = Fix64.Zero;
            if (convertEnergy[p] > Fix64.Zero && energy > reserve)
            {
                Fix64 used = Fix64.Min(energy - reserve, convertEnergy[p]);
                Fix64 made = convertMetal[p] * (used / convertEnergy[p]);
                energy -= used;
                metal += made;
                player.EnergySpent += used;
                player.ConverterEnergy = used;
                metalIncome[p] += made;
            }

            player.MetalIncome = metalIncome[p];
            player.EnergyIncome = energyIncome[p];
            player.MetalProduced += metalIncome[p];
            player.EnergyProduced += energyIncome[p];
            player.MetalWasted = Fix64.Max(Fix64.Zero, metal - metalStorage[p]);
            player.EnergyWasted = Fix64.Max(Fix64.Zero, energy - energyStorage[p]);
            player.Metal = Fix64.Min(metal, metalStorage[p]);
            player.Energy = Fix64.Min(energy, energyStorage[p]);
        }

        // Finish frames in id order.
        foreach (Work w in pending)
        {
            int frame = w.Frame;
            if (!units.Alive[frame] || units.Complete[frame]) continue;
            if (units.BuildProgress[frame] < catalog[units.TypeIndex[frame]].BuildTime) continue;
            world.OnCompleted(frame);
            world.Events.Add(new GameEvent(GameEventType.BuildComplete, frame, units.Owner[frame], Position: units.Position[frame]));
            // Mobile units are counted when their factory releases them.
            if (!catalog[units.TypeIndex[frame]].CanMove) world.Players[units.Owner[frame]].UnitsBuilt++;
        }
    }

    internal static bool InBuildRange(World world, int builder, UnitDef def, int target)
    {
        UnitDef tdef = world.Catalog[world.Units.TypeIndex[target]];
        Fix64 targetRadius = tdef.IsStructure ? Fix64.FromInt(tdef.Footprint * MapData.TileSize / 2) : tdef.Radius;
        Fix64 reach = def.BuildRange + targetRadius;
        return FixVec2.DistanceSquared(world.Units.Position[builder], world.Units.Position[target]) <= reach * reach;
    }

    private static void PlanWork(World world, Dictionary<int, Fix64> planned, List<Work> pending,
        Fix64[] metalDemand, Fix64[] energyDemand, int owner, int builder, int frame, Fix64 power)
    {
        UnitDef target = world.Catalog[world.Units.TypeIndex[frame]];
        planned.TryGetValue(frame, out Fix64 already);
        Fix64 remaining = target.BuildTime - world.Units.BuildProgress[frame] - already;
        Fix64 amount = Fix64.Min(power, remaining);
        if (amount <= Fix64.Zero) return;
        planned[frame] = already + amount;

        Fix64 share = amount / target.BuildTime;
        Fix64 metal = target.MetalCost * share;
        Fix64 energy = target.EnergyCost * share;
        metalDemand[owner] += metal;
        energyDemand[owner] += energy;
        pending.Add(new Work(owner, builder, frame, amount, metal, energy));
    }

    /// <summary>Mantle Crawler: eats wrecks in range and turns the metal into armour.</summary>
    private static void SelfReclaim(World world, int id, UnitDef def)
    {
        UnitStore units = world.Units;
        if (units.Health[id] >= def.MaxHealth) return;
        Fix64 rangeSq = def.SelfReclaimRange * def.SelfReclaimRange;
        Fix64 budget = Fix64.FromInt(2); // metal per tick
        foreach (Feature f in world.Features)
        {
            if (f.Gone || f.Metal <= Fix64.Zero) continue;
            if (FixVec2.DistanceSquared(units.Position[id], f.Position) > rangeSq) continue;
            Fix64 take = Fix64.Min(f.Metal, budget);
            f.Metal -= take;
            units.Health[id] = Fix64.Min(def.MaxHealth, units.Health[id] + take * 12);
            world.Events.Add(new GameEvent(GameEventType.ReclaimBeam, id, f.Id, Position2: f.Position));
            return;
        }
    }
}
