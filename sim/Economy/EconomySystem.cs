using System.Collections.Generic;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Pathing;
using ContingencyZero.Sim.State;

namespace ContingencyZero.Sim.Economy;

/// <summary>
/// The flow economy. Each tick every player gets income from their units, reclaim adds metal from
/// wrecks, and every builder that is working asks for metal and energy in proportion to the build
/// work it does. If a player can't cover the total, every build is slowed by the same ratio (a stall).
/// Surplus above storage is wasted.
/// </summary>
internal static class EconomySystem
{
    /// <summary>Base storage every player has before storage buildings.</summary>
    public static readonly Fix64 BaseStorage = Fix64.FromInt(1000);

    private readonly struct Work
    {
        public readonly int Player;
        public readonly BuildJob Job;
        public readonly Fix64 Amount;
        public readonly Fix64 Metal;
        public readonly Fix64 Energy;

        public Work(int player, BuildJob job, Fix64 amount, Fix64 metal, Fix64 energy)
        {
            Player = player;
            Job = job;
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
        for (int p = 0; p < playerCount; p++)
        {
            metalStorage[p] = BaseStorage;
            energyStorage[p] = BaseStorage;
        }

        // Planned work per job this tick, so several builders on one job never overshoot it.
        var plannedWork = new Dictionary<int, Fix64>();
        var pending = new List<Work>();

        for (int id = 0; id < units.Count; id++)
        {
            if (!units.Alive[id]) continue;
            int owner = units.Owner[id];
            UnitDef def = catalog[units.TypeIndex[id]];

            metalIncome[owner] += def.MetalIncomePerTick;
            energyIncome[owner] += def.EnergyIncomePerTick;
            metalStorage[owner] += def.MetalStorage;
            energyStorage[owner] += def.EnergyStorage;

            if (units.Order[id] == OrderType.Reclaim)
            {
                Wreck wreck = world.Wrecks[units.OrderTarget[id]];
                if (!wreck.Gone && OrderSystem.InRange(units.Position[id], wreck.Position, def.BuildRange))
                {
                    Fix64 take = Fix64.Min(wreck.Metal, def.BuildPowerPerTick);
                    wreck.Metal -= take;
                    metalIncome[owner] += take;
                    if (wreck.Gone) units.ClearOrder(id);
                }
            }
            else if (units.Order[id] == OrderType.Build)
            {
                BuildJob job = world.Jobs[units.OrderTarget[id]];
                if (job.Done || !OrderSystem.InRange(units.Position[id], job.Position, def.BuildRange)) continue;

                UnitDef target = catalog[job.TypeIndex];
                plannedWork.TryGetValue(job.Id, out Fix64 already);
                Fix64 remaining = target.BuildTime - job.Progress - already;
                Fix64 amount = Fix64.Min(def.BuildPowerPerTick, remaining);
                if (amount <= Fix64.Zero) continue;
                plannedWork[job.Id] = already + amount;

                Fix64 share = amount / target.BuildTime;
                Fix64 metal = target.MetalCost * share;
                Fix64 energy = target.EnergyCost * share;
                metalDemand[owner] += metal;
                energyDemand[owner] += energy;
                pending.Add(new Work(owner, job, amount, metal, energy));
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
            w.Job.Progress += w.Amount * r;
            players[w.Player].MetalSpent += w.Metal * r;
            players[w.Player].EnergySpent += w.Energy * r;
        }

        for (int p = 0; p < playerCount; p++)
        {
            PlayerState player = players[p];
            player.MetalIncome = metalIncome[p];
            player.EnergyIncome = energyIncome[p];
            player.MetalStorage = metalStorage[p];
            player.EnergyStorage = energyStorage[p];
            player.BuildRatio = ratio[p];

            Fix64 metal = Fix64.Max(Fix64.Zero, player.Metal + metalIncome[p] - player.MetalSpent);
            Fix64 energy = Fix64.Max(Fix64.Zero, player.Energy + energyIncome[p] - player.EnergySpent);
            player.MetalWasted = Fix64.Max(Fix64.Zero, metal - metalStorage[p]);
            player.EnergyWasted = Fix64.Max(Fix64.Zero, energy - energyStorage[p]);
            player.Metal = Fix64.Min(metal, metalStorage[p]);
            player.Energy = Fix64.Min(energy, energyStorage[p]);
        }

        // Finish jobs in id order.
        foreach (BuildJob job in world.Jobs)
        {
            if (job.Done || job.Progress < catalog[job.TypeIndex].BuildTime) continue;
            job.Done = true;
            job.ResultUnit = world.SpawnUnit(job.TypeIndex, job.Owner, job.Position);
            for (int id = 0; id < units.Count; id++)
            {
                if (units.Alive[id] && units.Order[id] == OrderType.Build && units.OrderTarget[id] == job.Id)
                    units.ClearOrder(id);
            }
        }
    }
}
