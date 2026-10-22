using System;
using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Sim.Combat;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Economy;
using ContingencyZero.Sim.Pathing;
using ContingencyZero.Sim.State;

namespace ContingencyZero.Sim;

/// <summary>
/// The whole game state and the tick loop. Given the same catalog, seed, setup and commands, every
/// machine computes the same world, tick for tick. Nothing outside the sim writes to it except
/// through <see cref="Step"/>.
/// </summary>
public sealed class World
{
    public const int TicksPerSecond = 15;

    public UnitCatalog Catalog { get; }
    public DeterministicRng Rng { get; }
    public int MapWidth { get; }
    public int MapHeight { get; }
    public int Tick { get; private set; }

    public UnitStore Units { get; } = new();
    public PlayerState[] Players { get; }
    public List<Wreck> Wrecks { get; } = new();
    public List<BuildJob> Jobs { get; } = new();

    internal SpatialGrid Grid { get; }

    /// <param name="playerTeams">Team number for each player, indexed by player id.</param>
    public World(UnitCatalog catalog, ulong seed, int mapWidth, int mapHeight, IReadOnlyList<int> playerTeams,
        int startMetal = 1000, int startEnergy = 1000)
    {
        if (mapWidth <= 0 || mapHeight <= 0) throw new ArgumentOutOfRangeException(nameof(mapWidth));
        Catalog = catalog;
        Rng = new DeterministicRng(seed);
        MapWidth = mapWidth;
        MapHeight = mapHeight;
        Grid = new SpatialGrid(mapWidth, mapHeight);
        Players = playerTeams.Select((team, id) => new PlayerState
        {
            Id = id,
            Team = team,
            Metal = Fix64.FromInt(startMetal),
            Energy = Fix64.FromInt(startEnergy),
            MetalStorage = EconomySystem.BaseStorage,
            EnergyStorage = EconomySystem.BaseStorage,
        }).ToArray();
    }

    /// <summary>Places a finished unit. Used for scenario setup and by completed build jobs.</summary>
    public int SpawnUnit(int typeIndex, int owner, FixVec2 position)
    {
        if (owner < 0 || owner >= Players.Length) throw new ArgumentOutOfRangeException(nameof(owner));
        return Units.Add(typeIndex, owner, ClampToMap(position), Catalog[typeIndex].MaxHealth);
    }

    public int SpawnUnit(string typeId, int owner, FixVec2 position) =>
        SpawnUnit(Catalog.IndexOf(typeId), owner, position);

    /// <summary>
    /// Advances one tick. <paramref name="commands"/> are the commands scheduled for this tick, from
    /// every player; they are applied in (player, arrival) order so the order they reached this
    /// machine in doesn't matter between players.
    /// </summary>
    public void Step(IReadOnlyList<Command> commands)
    {
        var ordered = commands
            .Select((c, i) => (c, i))
            .OrderBy(x => x.c.Player)
            .ThenBy(x => x.i);
        foreach (var (command, _) in ordered)
            Apply(command);

        OrderSystem.Tick(this);
        EconomySystem.Tick(this);
        CombatSystem.Tick(this);
        Tick++;
    }

    public void Step() => Step(Array.Empty<Command>());

    /// <summary>Removes a unit, leaves a wreck, and checks the Commander win condition.</summary>
    public void Kill(int id)
    {
        if (!Units.IsValid(id)) return;
        UnitDef def = Catalog[Units.TypeIndex[id]];
        Units.Alive[id] = false;
        Units.Health[id] = Fix64.Zero;
        Units.ClearOrder(id);

        Fix64 metal = def.MetalCost * def.WreckFraction;
        if (metal > Fix64.Zero)
        {
            Wrecks.Add(new Wreck
            {
                Id = Wrecks.Count,
                Position = Units.Position[id],
                SourceTypeIndex = Units.TypeIndex[id],
                Metal = metal,
            });
        }

        if (def.IsCommander)
            Players[Units.Owner[id]].Defeated = true;
    }

    /// <summary>The single team with players still standing, or null while the match is undecided.</summary>
    public int? WinningTeam()
    {
        int? team = null;
        foreach (PlayerState p in Players)
        {
            if (p.Defeated) continue;
            if (team is null) team = p.Team;
            else if (team != p.Team) return null;
        }
        return team;
    }

    public FixVec2 ClampToMap(FixVec2 p) => new(
        Fix64.Clamp(p.X, Fix64.Zero, Fix64.FromInt(MapWidth)),
        Fix64.Clamp(p.Y, Fix64.Zero, Fix64.FromInt(MapHeight)));

    private void Apply(Command c)
    {
        if (c.Player < 0 || c.Player >= Players.Length || Players[c.Player].Defeated) return;
        if (!Units.IsValid(c.UnitId) || Units.Owner[c.UnitId] != c.Player) return;

        int id = c.UnitId;
        UnitDef def = Catalog[Units.TypeIndex[id]];

        switch (c.Type)
        {
            case CommandType.Stop:
                Units.ClearOrder(id);
                break;

            case CommandType.Move:
                if (!def.CanMove) return;
                Units.Order[id] = OrderType.Move;
                Units.OrderPosition[id] = ClampToMap(c.Position);
                Units.OrderTarget[id] = -1;
                break;

            case CommandType.Attack:
                if (!def.IsArmed || !Units.IsValid(c.TargetId)) return;
                if (Players[Units.Owner[c.TargetId]].Team == Players[c.Player].Team) return;
                Units.Order[id] = OrderType.Attack;
                Units.OrderTarget[id] = c.TargetId;
                break;

            case CommandType.Build:
            {
                if (!def.CanBuild || c.TypeIndex < 0 || c.TypeIndex >= Catalog.Count) return;
                if (Catalog[c.TypeIndex].Faction != def.Faction) return;
                var job = new BuildJob
                {
                    Id = Jobs.Count,
                    Owner = c.Player,
                    TypeIndex = c.TypeIndex,
                    Position = ClampToMap(c.Position),
                };
                Jobs.Add(job);
                Units.Order[id] = OrderType.Build;
                Units.OrderTarget[id] = job.Id;
                Units.OrderPosition[id] = job.Position;
                break;
            }

            case CommandType.Reclaim:
                if (!def.CanBuild || c.TargetId < 0 || c.TargetId >= Wrecks.Count || Wrecks[c.TargetId].Gone) return;
                Units.Order[id] = OrderType.Reclaim;
                Units.OrderTarget[id] = c.TargetId;
                Units.OrderPosition[id] = Wrecks[c.TargetId].Position;
                break;
        }
    }

    /// <summary>
    /// Hash of every piece of state that affects the future. Two machines with the same hash on the
    /// same tick are in sync.
    /// </summary>
    public ulong ComputeHash()
    {
        var h = new StateHasher();
        h.Add(Tick);
        h.Add(Rng.State);

        foreach (PlayerState p in Players)
        {
            h.Add(p.Team);
            h.Add(p.Metal);
            h.Add(p.Energy);
            h.Add(p.MetalStorage);
            h.Add(p.EnergyStorage);
            h.Add(p.BuildRatio);
            h.Add(p.Defeated);
        }

        h.Add(Units.Count);
        for (int id = 0; id < Units.Count; id++)
        {
            h.Add(Units.Alive[id]);
            if (!Units.Alive[id]) continue;
            h.Add(Units.TypeIndex[id]);
            h.Add(Units.Owner[id]);
            h.Add(Units.Position[id]);
            h.Add(Units.Health[id]);
            h.Add(Units.ReloadLeft[id]);
            h.Add(Units.WeaponTarget[id]);
            h.Add((int)Units.Order[id]);
            h.Add(Units.OrderPosition[id]);
            h.Add(Units.OrderTarget[id]);
        }

        h.Add(Wrecks.Count);
        foreach (Wreck w in Wrecks)
        {
            h.Add(w.Position);
            h.Add(w.Metal);
        }

        h.Add(Jobs.Count);
        foreach (BuildJob j in Jobs)
        {
            h.Add(j.TypeIndex);
            h.Add(j.Owner);
            h.Add(j.Position);
            h.Add(j.Progress);
            h.Add(j.Done);
        }

        return h.Value;
    }
}
