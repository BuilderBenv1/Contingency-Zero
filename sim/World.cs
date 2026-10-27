using System;
using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Sim.AI;
using ContingencyZero.Sim.Combat;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Economy;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using ContingencyZero.Sim.Missions;
using ContingencyZero.Sim.Pathing;
using ContingencyZero.Sim.State;
using ContingencyZero.Sim.Vision;

namespace ContingencyZero.Sim;

/// <summary>
/// The whole game state and the tick loop. Given the same catalog, map, setup, seed and commands,
/// every machine computes the same world, tick for tick. Nothing outside the sim writes to it
/// except through <see cref="Step"/>.
/// </summary>
public sealed partial class World
{
    public const int TicksPerSecond = 15;
    /// <summary>Base storage every player has before storage buildings.</summary>
    public static readonly Fix64 BaseStorage = Fix64.FromInt(1000);

    public UnitCatalog Catalog { get; }
    public MapData Map { get; }
    public Terrain Terrain { get; }
    public MatchOptions Options { get; }
    public DeterministicRng Rng { get; }
    public int Tick { get; private set; }
    public int MapWidth => Map.Width;
    public int MapHeight => Map.Height;

    public UnitStore Units { get; } = new();
    public PlayerState[] Players { get; }
    public List<Feature> Features { get; } = new();
    public List<Projectile> Projectiles { get; } = new();
    /// <summary>What happened during the last tick. Output only.</summary>
    public List<GameEvent> Events { get; } = new();

    /// <summary>Current wind for wind turbines, energy per second.</summary>
    public Fix64 Wind { get; internal set; }
    public int? WinningTeam { get; private set; }
    public bool IsOver => WinningTeam is not null || _drawn;
    private bool _drawn;

    internal SpatialGrid Grid { get; }
    public Pathfinder Pathfinder { get; }
    public VisionSystem Vision { get; }
    public MissionScript? Mission { get; }

    // Per-world scratch lists so systems never share mutable state between worlds (or threads).
    internal readonly List<int> ScratchNeighbours = new();
    internal readonly List<int> ScratchHits = new();

    private readonly List<AiController> _ais = new();
    private readonly List<Command> _aiCommands = new();
    private int _nextProjectileId;
    /// <summary>Path searches allowed per tick; the rest wait their turn in <see cref="PathQueue"/>.</summary>
    internal int PathBudget;
    /// <summary>Path search budget per tick, in tiles expanded (always serves at least one request).</summary>
    public const int PathTilesPerTick = 20000;
    /// <summary>Units waiting for a path search, first come first served.</summary>
    internal readonly Queue<int> PathQueue = new();
    /// <summary>Units waiting for a path search (diagnostics).</summary>
    public int PathQueueCount() => PathQueue.Count;

    public World(UnitCatalog catalog, MapData map, ulong seed, IReadOnlyList<PlayerSetup> players,
        MatchOptions? options = null, MissionScript? mission = null)
    {
        if (players.Count == 0) throw new ArgumentException("A match needs at least one player", nameof(players));
        Catalog = catalog;
        // The match gets its own copy of the ground: craters reshape it, and the scenario's map must
        // stay pristine so a replay starts from the same place.
        Map = map.Clone();
        Options = options ?? new MatchOptions();
        Rng = new DeterministicRng(seed);
        Terrain = new Terrain(Map);
        Pathfinder = new Pathfinder(Terrain);
        Units.InitializeGoals(map.TilesX * map.TilesY);
        Grid = new SpatialGrid(map.Width, map.Height);
        Vision = new VisionSystem(map.TilesX, map.TilesY) { RevealAll = Options.RevealMap };
        Mission = mission;

        Players = new PlayerState[players.Count];
        for (int i = 0; i < players.Count; i++)
        {
            PlayerSetup p = players[i];
            if (p.Team < 0 || p.Team >= VisionSystem.MaxTeams)
                throw new ArgumentOutOfRangeException(nameof(players), "Teams are numbered 0 to 7");
            Players[i] = new PlayerState
            {
                Id = i,
                Team = p.Team,
                Faction = p.Faction,
                Name = p.Name,
                Metal = Fix64.FromInt(Options.StartMetal),
                Energy = Fix64.FromInt(Options.StartEnergy),
                MetalStorage = BaseStorage,
                EnergyStorage = BaseStorage,
                IncomeMultiplier = p.Ai == AiDifficulty.Brutal ? Fix64.Parse("1.5") : Fix64.One,
                SalvageProgress = new int[catalog.Count],
            };
        }

        Wind = (map.WindMin + map.WindMax) / 2;

        for (int i = 0; i < map.Features.Count; i++)
        {
            FeaturePlacement f = map.Features[i];
            Features.Add(new Feature
            {
                Id = Features.Count,
                Kind = f.Kind,
                Position = f.Position,
                Metal = f.Metal,
                Energy = f.Energy,
                Radius = f.Kind == FeatureKind.Tree ? Fix64.FromInt(2) : Fix64.FromInt(3),
            });
        }

        if (Options.SpawnCommanders)
        {
            for (int i = 0; i < players.Count; i++)
            {
                PlayerSetup p = players[i];
                if (p.StartIndex < 0 || p.StartIndex >= map.StartPositions.Count)
                    throw new ArgumentOutOfRangeException(nameof(players), $"Start position {p.StartIndex} doesn't exist on {map.Name}");
                SpawnUnit(catalog.CommanderOf(p.Faction), i, map.StartPositions[p.StartIndex]);
            }
        }

        // Mission units first, so a computer player without a Commander finds its home among them.
        Mission?.Attach(this);

        for (int i = 0; i < players.Count; i++)
            if (players[i].Ai != AiDifficulty.None)
                _ais.Add(new AiController(this, i, players[i].Ai, seed));

        Vision.Update(this);
    }

    /// <summary>Places a unit. Structures snap to the tile grid and claim their footprint.</summary>
    public int SpawnUnit(int typeIndex, int owner, FixVec2 position, bool complete = true)
    {
        if (owner < 0 || owner >= Players.Length) throw new ArgumentOutOfRangeException(nameof(owner));
        UnitDef def = Catalog[typeIndex];
        FixVec2 pos = ClampToMap(position);
        int tx = -1, ty = -1;
        if (def.IsStructure)
        {
            (tx, ty) = Terrain.FootprintOrigin(pos, def.Footprint);
            tx = Math2.Clamp(tx, 0, Map.TilesX - def.Footprint);
            ty = Math2.Clamp(ty, 0, Map.TilesY - def.Footprint);
            pos = Terrain.FootprintCentre(tx, ty, def.Footprint);
        }

        Fix64 health = complete ? def.MaxHealth : Fix64.One;
        int id = Units.Add(typeIndex, owner, pos, health, complete, Tick);
        if (complete) Units.BuildProgress[id] = def.BuildTime;
        if (def.IsStructure)
        {
            Units.TileX[id] = tx;
            Units.TileY[id] = ty;
            Terrain.SetOccupant(tx, ty, def.Footprint, id);
        }
        if (complete) OnCompleted(id);
        return id;
    }

    public int SpawnUnit(string typeId, int owner, FixVec2 position) =>
        SpawnUnit(Catalog.IndexOf(typeId), owner, position);

    /// <summary>
    /// Advances one tick. <paramref name="commands"/> are the commands scheduled for this tick, from
    /// every player; they are applied in (player, arrival) order so the order they reached this
    /// machine in doesn't matter between players. Computer players' commands from the previous tick
    /// are applied first.
    /// </summary>
    public void Step(IReadOnlyList<Command> commands)
    {
        Events.Clear();
        if (IsOver)
        {
            Tick++;
            return;
        }

        foreach (Command c in _aiCommands) ApplyCommand(c);
        _aiCommands.Clear();

        var ordered = commands
            .Select((c, i) => (c, i))
            .OrderBy(x => x.c.Player)
            .ThenBy(x => x.i);
        foreach (var (command, _) in ordered)
            ApplyCommand(command);

        PathBudget = PathTilesPerTick;
        FactorySystem.Tick(this);
        OrderSystem.Tick(this);
        EconomySystem.Tick(this);
        Grid.Rebuild(Units);
        CombatSystem.Tick(this);
        ProjectileSystem.Tick(this);
        ResolveDeaths();

        if (Tick % (30 * TicksPerSecond) == 0) ChangeWind();
        if (Tick % VisionSystem.UpdateInterval == 0) Vision.Update(this);

        Mission?.Update(this);
        CheckVictory();

        foreach (AiController ai in _ais)
            ai.Update(_aiCommands);

        Tick++;
    }

    public void Step() => Step(Array.Empty<Command>());

    public IReadOnlyList<AiController> Ais => _ais;

    private void ChangeWind()
    {
        Fix64 span = Map.WindMax - Map.WindMin;
        Wind = Map.WindMin + span * Rng.NextFix();
    }

    internal int NextProjectileId() => _nextProjectileId++;

    public FixVec2 ClampToMap(FixVec2 p) => new(
        Fix64.Clamp(p.X, Fix64.Zero, Fix64.FromInt(Map.Width) - Fix64.One),
        Fix64.Clamp(p.Y, Fix64.Zero, Fix64.FromInt(Map.Height) - Fix64.One));

    public int TeamOf(int unitId) => Players[Units.Owner[unitId]].Team;

    public bool AreEnemies(int playerA, int playerB) => Players[playerA].Team != Players[playerB].Team;

    /// <summary>Whether a team can currently see a unit (or the unit is on its radar, when <paramref name="radarCounts"/>).</summary>
    public bool CanSee(int team, int unitId, bool radarCounts = false)
    {
        if (Players[Units.Owner[unitId]].Team == team) return true;
        FixVec2 p = Units.Position[unitId];
        int tx = MapData.TileOf(p.X), ty = MapData.TileOf(p.Y);
        if (Vision.IsVisible(team, tx, ty)) return true;
        return radarCounts && Vision.IsOnRadar(team, tx, ty);
    }

    // ---- Completion, death and victory -------------------------------------------------------

    /// <summary>Called once when a unit finishes construction (or spawns complete).</summary>
    internal void OnCompleted(int id)
    {
        UnitDef def = Catalog[Units.TypeIndex[id]];
        Units.Complete[id] = true;
        Units.BuildProgress[id] = def.BuildTime;
        if (Units.Health[id] > def.MaxHealth) Units.Health[id] = def.MaxHealth;
        Units.Anchor[id] = Units.Position[id];

        if (def.ExtractorMultiplier > Fix64.Zero)
            Units.ExtractorYield[id] = MetalUnder(id) * def.ExtractorMultiplier / TicksPerSecond;
    }

    /// <summary>Total yield of metal spots inside a structure's footprint (per second).</summary>
    internal Fix64 MetalUnder(int id)
    {
        UnitDef def = Catalog[Units.TypeIndex[id]];
        return MetalUnderFootprint(Units.TileX[id], Units.TileY[id], def.Footprint);
    }

    public Fix64 MetalUnderFootprint(int tx, int ty, int footprint)
    {
        Fix64 total = Fix64.Zero;
        foreach (MetalSpot s in Map.MetalSpots)
        {
            int sx = MapData.TileOf(s.Position.X), sy = MapData.TileOf(s.Position.Y);
            if (sx >= tx && sx < tx + footprint && sy >= ty && sy < ty + footprint) total += s.Yield;
        }
        return total;
    }

    private readonly List<(FixVec2 Pos, Fix64 Damage, Fix64 Radius, int Source)> _explosions = new();

    /// <summary>Removes every unit at zero health, leaves wrecks, sets off death explosions (which can chain).</summary>
    internal void ResolveDeaths()
    {
        for (int round = 0; round < 4; round++)
        {
            bool any = false;
            for (int id = 0; id < Units.Count; id++)
            {
                if (Units.Alive[id] && Units.Health[id] <= Fix64.Zero)
                {
                    Kill(id, leaveWreck: true, explode: true);
                    any = true;
                }
            }
            if (_explosions.Count > 0)
            {
                var blasts = _explosions.ToList();
                _explosions.Clear();
                foreach (var (pos, damage, radius, source) in blasts)
                {
                    CombatSystem.Splash(this, pos, damage, radius, source, hitsAir: false);
                    Craters.FromExplosion(this, pos, radius);
                }
                any = true;
            }
            if (!any) break;
        }
    }

    private const int TerrainLogSize = 256;
    private readonly (int Tx0, int Ty0, int Tx1, int Ty1)[] _terrainLog = new (int, int, int, int)[TerrainLogSize];

    /// <summary>How many times the ground has been reshaped this match. Output only (the heights themselves are hashed).</summary>
    public int TerrainRevision { get; private set; }

    /// <summary>
    /// The tile rectangles (inclusive) reshaped since <paramref name="revision"/>, oldest first, for
    /// the renderer to catch up on (even across ticks it never drew, such as a replay seek). Returns
    /// false if that's too far back to list, in which case redraw everything.
    /// </summary>
    public bool TerrainChangesSince(int revision, List<(int Tx0, int Ty0, int Tx1, int Ty1)> into)
    {
        into.Clear();
        if (revision >= TerrainRevision) return true;
        if (TerrainRevision - revision > TerrainLogSize || revision < 0) return false;
        for (int r = revision; r < TerrainRevision; r++) into.Add(_terrainLog[r % TerrainLogSize]);
        return true;
    }

    /// <summary>
    /// Blasts a crater into the ground and tells everyone: the renderer (an event) and any unit
    /// whose path ran through the area (it will search again).
    /// </summary>
    public void DeformTerrain(FixVec2 centre, Fix64 radius, int depthSteps)
    {
        var rect = Terrain.Deform(centre, radius, depthSteps);
        if (rect is null) return;
        var (tx0, ty0, tx1, ty1) = rect.Value;
        _terrainLog[TerrainRevision % TerrainLogSize] = rect.Value;
        TerrainRevision++;
        Events.Add(new GameEvent(GameEventType.TerrainChanged,
            Position: FixVec2.FromInts(tx0 * MapData.TileSize, ty0 * MapData.TileSize),
            Position2: FixVec2.FromInts((tx1 + 1) * MapData.TileSize, (ty1 + 1) * MapData.TileSize)));

        // Ground units near a crater that changed where anything can drive drop their paths; the
        // next step plans round the new shape. Shallow pockmarks usually change nothing.
        if (!Terrain.LastDeformChangedPassability) return;
        Fix64 reach = radius * 3 / 2 + Fix64.FromInt(8);
        Fix64 reach2 = reach * reach;
        for (int id = 0; id < Units.Count; id++)
        {
            if (!Units.Alive[id] || Units.Path[id] is null) continue;
            if (Catalog[Units.TypeIndex[id]].IsAir) continue;
            if (FixVec2.DistanceSquared(Units.Position[id], centre) <= reach2 || PathCrosses(id, centre, reach2))
                Units.ClearPath(id);
        }
    }

    /// <summary>Whether the rest of a unit's path passes within sqrt(<paramref name="reach2"/>) of a point.</summary>
    private bool PathCrosses(int id, FixVec2 centre, Fix64 reach2)
    {
        var path = Units.Path[id]!;
        FixVec2 a = Units.Position[id];
        for (int i = Units.PathIndex[id]; i < path.Count; i++)
        {
            FixVec2 b = path[i];
            // Closest point on segment a-b to the centre.
            FixVec2 ab = b - a;
            Fix64 len2 = ab.LengthSquared();
            FixVec2 closest = a;
            if (len2 > Fix64.Zero)
            {
                FixVec2 ac = centre - a;
                Fix64 t = Fix64.Clamp((ac.X * ab.X + ac.Y * ab.Y) / len2, Fix64.Zero, Fix64.One);
                closest = a + ab * t;
            }
            if (FixVec2.DistanceSquared(closest, centre) <= reach2) return true;
            a = b;
        }
        return false;
    }

    /// <summary>Removes a unit right away.</summary>
    public void Kill(int id, bool leaveWreck = true, bool explode = true)
    {
        if (!Units.IsValid(id)) return;
        UnitDef def = Catalog[Units.TypeIndex[id]];
        int owner = Units.Owner[id];
        FixVec2 pos = Units.Position[id];
        bool wasComplete = Units.Complete[id];

        Units.Alive[id] = false;
        Units.Health[id] = Fix64.Zero;
        Units.ClearOrders(id);
        Players[owner].UnitsLost++;

        if (def.IsStructure)
            Terrain.ClearOccupant(id, Units.TileX[id], Units.TileY[id], def.Footprint);

        // A factory takes its half-built unit with it.
        int product = Units.FactoryCurrent[id];
        if (product >= 0 && Units.IsValid(product) && !Units.Complete[product])
            Kill(product, leaveWreck: false, explode: false);
        Units.FactoryCurrent[id] = -1;

        Events.Add(new GameEvent(GameEventType.UnitDestroyed, id, owner, Units.TypeIndex[id], 0, pos));

        if (leaveWreck && wasComplete && def.WreckFraction > Fix64.Zero && !def.IsAir)
        {
            Fix64 metal = def.MetalCost * def.WreckFraction;
            if (metal > Fix64.Zero)
            {
                Features.Add(new Feature
                {
                    Id = Features.Count,
                    Kind = FeatureKind.Wreck,
                    Position = pos,
                    SourceTypeIndex = Units.TypeIndex[id],
                    Metal = metal,
                    Radius = def.IsStructure ? Fix64.FromInt(def.Footprint * 3) : def.Radius + Fix64.One,
                });
            }
            // A fallen structure also scatters rubble round its footprint: small, lasting scars
            // that mark where a base stood, each worth a little metal.
            if (def.IsStructure && def.Footprint >= 2)
            {
                int chunks = 2 + def.Footprint / 2;
                Fix64 half = Fix64.FromInt(def.Footprint * MapData.TileSize / 2);
                for (int i = 0; i < chunks; i++)
                {
                    // Spread round the edge; the shared RNG keeps replays in step.
                    Fix64 ox = Fix64.FromInt(Rng.Range(-8, 9)) + (Rng.Range(0, 2) == 0 ? -half : half);
                    Fix64 oy = Fix64.FromInt(Rng.Range(-8, 9)) + (Rng.Range(0, 2) == 0 ? -half : half);
                    if (Rng.Range(0, 2) == 0) ox = Fix64.FromInt(Rng.Range(-half.ToIntFloor(), half.ToIntFloor() + 1));
                    else oy = Fix64.FromInt(Rng.Range(-half.ToIntFloor(), half.ToIntFloor() + 1));
                    Features.Add(new Feature
                    {
                        Id = Features.Count,
                        Kind = FeatureKind.Rubble,
                        Position = ClampToMap(pos + new FixVec2(ox, oy)),
                        SourceTypeIndex = Units.TypeIndex[id],
                        Metal = Fix64.Max(Fix64.One, def.MetalCost / 40),
                        Radius = Fix64.FromInt(2),
                    });
                }
            }
        }

        if (explode && wasComplete && def.DeathExplosionDamage > Fix64.Zero)
        {
            _explosions.Add((pos, def.DeathExplosionDamage, def.DeathExplosionRadius, id));
            Events.Add(new GameEvent(GameEventType.Explosion, id, owner, def.DeathExplosionRadius.ToIntFloor(), 0, pos));
        }

        if (def.IsCommander && Options.WinCondition == WinCondition.Commander && wasComplete)
            DefeatPlayer(owner);
    }

    /// <summary>Default number of enemy wrecks of one type to reclaim before a player's factories can build it (unit data can change it per type).</summary>
    public const int SalvageWrecksNeeded = 3;

    /// <summary>Enemy wrecks of this type a player must reclaim to salvage it (from the unit's data).</summary>
    public int SalvageNeeded(int typeIndex) => Catalog[typeIndex].SalvageWrecks;

    /// <summary>A wreck was fully reclaimed by <paramref name="player"/>: count it toward salvaging that design.</summary>
    internal void OnFeatureReclaimed(int player, Feature f)
    {
        if (f.Kind != FeatureKind.Wreck || f.SourceTypeIndex < 0) return;
        UnitDef source = Catalog[f.SourceTypeIndex];
        if (!IsSalvageable(player, source)) return;
        int[] progress = Players[player].SalvageProgress;
        if (progress[f.SourceTypeIndex] >= SalvageNeeded(f.SourceTypeIndex)) return;
        progress[f.SourceTypeIndex]++;
        Events.Add(new GameEvent(GameEventType.Salvage, Other: player, Value: f.SourceTypeIndex, Kind: (byte)progress[f.SourceTypeIndex]));
    }

    /// <summary>Designs worth salvaging: another faction's mobile units (not Commanders).</summary>
    public bool IsSalvageable(int player, UnitDef def) =>
        !def.IsStructure && !def.IsCommander && def.Faction != Players[player].Faction;

    /// <summary>True once the player has reclaimed enough wrecks of this type to build it.</summary>
    public bool HasSalvaged(int player, int typeIndex) =>
        typeIndex >= 0 && typeIndex < Catalog.Count && Players[player].SalvageProgress[typeIndex] >= SalvageNeeded(typeIndex);

    /// <summary>
    /// Salvaged designs this factory can build: the player has salvaged them and the factory already
    /// builds something with the same role (a bot lab builds salvaged bots, a shipyard salvaged ships).
    /// </summary>
    public List<int> SalvagedOptions(UnitDef factory, int player)
    {
        var list = new List<int>();
        if (!factory.IsFactory) return list;
        int[] progress = Players[player].SalvageProgress;
        for (int t = 0; t < Catalog.Count; t++)
        {
            if (progress[t] < SalvageNeeded(t)) continue;
            if (!FactoryTakesRole(factory, Catalog[t].Role)) continue;
            list.Add(t);
        }
        return list;
    }

    private bool FactoryTakesRole(UnitDef factory, string role)
    {
        foreach (int o in factory.BuildOptions)
            if (Catalog[o].Role == role) return true;
        return false;
    }

    internal void DefeatPlayer(int player)
    {
        PlayerState p = Players[player];
        if (p.Defeated) return;
        p.Defeated = true;
        p.DefeatedTick = Tick;
        Events.Add(new GameEvent(GameEventType.PlayerDefeated, Other: player));
        for (int id = 0; id < Units.Count; id++)
            if (Units.Alive[id] && Units.Owner[id] == player)
                Kill(id, leaveWreck: true, explode: false);
    }

    private void CheckVictory()
    {
        if (Options.WinCondition == WinCondition.Annihilation && Tick % TicksPerSecond == 0)
        {
            var hasUnits = new bool[Players.Length];
            for (int id = 0; id < Units.Count; id++)
                if (Units.Alive[id]) hasUnits[Units.Owner[id]] = true;
            for (int p = 0; p < Players.Length; p++)
                if (!hasUnits[p] && !Players[p].Defeated) DefeatPlayer(p);
        }

        if (Mission is not null) return; // missions decide their own ending
        int? team = null;
        bool several = false;
        foreach (PlayerState p in Players)
        {
            if (p.Defeated) continue;
            if (team is null) team = p.Team;
            else if (team != p.Team) several = true;
        }
        if (several) return;
        if (Players.Length == 1 && team is not null) return; // sandbox: one player never "wins"
        EndMatch(team);
    }

    /// <summary>Ends the match. Null means nobody won (everyone was knocked out on the same tick).</summary>
    public void EndMatch(int? winningTeam)
    {
        if (IsOver) return;
        if (winningTeam is null) _drawn = true;
        WinningTeam = winningTeam;
        Events.Add(new GameEvent(GameEventType.GameOver, Value: winningTeam ?? -1));
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
        h.Add(Wind);

        foreach (PlayerState p in Players)
        {
            h.Add(p.Team);
            h.Add(p.Metal);
            h.Add(p.Energy);
            h.Add(p.MetalStorage);
            h.Add(p.EnergyStorage);
            h.Add(p.BuildRatio);
            h.Add(p.Defeated);
            h.Add(p.ConvertersOn);
            foreach (int n in p.SalvageProgress) h.Add(n);
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
            h.Add(Units.BuildProgress[id]);
            h.Add(Units.Reload0[id]);
            h.Add(Units.Reload1[id]);
            h.Add(Units.Target0[id]);
            h.Add(Units.Target1[id]);
            h.Add((int)Units.Fire[id]);
            h.Add(Units.ReservedGoal[id]);
            h.Add(Units.Orders[id].Count);
            foreach (Order o in Units.Orders[id])
            {
                h.Add((int)o.Type);
                h.Add(o.Position);
                h.Add(o.Target);
                h.Add(o.TypeIndex);
            }
            h.Add(Units.FactoryQueue[id].Count);
            foreach (int q in Units.FactoryQueue[id]) h.Add(q);
            h.Add(Units.FactoryCurrent[id]);
            h.Add(Units.RallyEvery[id]);
            h.Add(Units.RallyCount[id]);
            h.Add(Units.HasRally[id]);
            if (Units.HasRally[id]) h.Add(Units.Rally[id]);
            h.Add(Units.RallyExtra[id].Count);
            foreach (FixVec2 r in Units.RallyExtra[id]) h.Add(r);
        }

        foreach (int height in Map.Heights) h.Add(height);

        h.Add(Features.Count);
        foreach (Feature f in Features)
        {
            h.Add(f.Position);
            h.Add(f.Metal);
            h.Add(f.Energy);
        }

        h.Add(Projectiles.Count);
        foreach (Projectile p in Projectiles)
        {
            h.Add(p.Position);
            h.Add(p.Aim);
            h.Add(p.TargetUnit);
        }

        h.Add(PathQueue.Count);
        foreach (int q in PathQueue) h.Add(q);
        foreach (AiController ai in _ais) h.Add(ai.StateHash());
        if (Mission is not null) h.Add(Mission.StateHash());
        return h.Value;
    }
}
