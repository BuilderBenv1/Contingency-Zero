using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Pathing;
using ContingencyZero.Sim.State;

namespace ContingencyZero.Sim;

public enum PlacementResult : byte
{
    Ok = 0,
    OutOfBounds,
    BadGround,
    Occupied,
    NeedsMetalSpot,
    NeedsWater,
    NotBuildable,
}

public sealed partial class World
{
    /// <summary>
    /// Whether <paramref name="player"/> could place a structure of <paramref name="typeIndex"/>
    /// centred near <paramref name="position"/>. Also returns the snapped top-left tile.
    /// Mobile units (from factories) are always placeable.
    /// </summary>
    public PlacementResult CanPlace(int typeIndex, int player, FixVec2 position, out int tx, out int ty)
    {
        UnitDef def = Catalog[typeIndex];
        (tx, ty) = Terrain.FootprintOrigin(position, def.Footprint);
        if (!def.IsStructure) return PlacementResult.NotBuildable;
        if (tx < 0 || ty < 0 || tx + def.Footprint > Map.TilesX || ty + def.Footprint > Map.TilesY)
            return PlacementResult.OutOfBounds;

        if (!Terrain.GroundSuits(tx, ty, def.Footprint, def.NeedsWater))
            return def.NeedsWater ? PlacementResult.NeedsWater : PlacementResult.BadGround;

        for (int y = ty; y < ty + def.Footprint; y++)
        for (int x = tx; x < tx + def.Footprint; x++)
        {
            int occupant = Terrain.Occupant[Terrain.Index(x, y)];
            if (occupant == -1) continue;
            if (IsUpgradeableExtractor(occupant, player, def)) continue;
            if (Units.TypeIndex[occupant] == typeIndex && Units.Owner[occupant] == player && !Units.Complete[occupant])
                continue; // joining an existing nanoframe of the same thing
            return PlacementResult.Occupied;
        }

        if (def.NeedsMetalSpot && MetalUnderFootprint(tx, ty, def.Footprint) <= Fix64.Zero)
            return PlacementResult.NeedsMetalSpot;

        return PlacementResult.Ok;
    }

    public PlacementResult CanPlace(int typeIndex, int player, FixVec2 position) =>
        CanPlace(typeIndex, player, position, out _, out _);

    /// <summary>A deep extractor can be built straight over its owner's standard extractor.</summary>
    private bool IsUpgradeableExtractor(int occupant, int player, UnitDef newDef)
    {
        if (!Units.IsValid(occupant) || Units.Owner[occupant] != player) return false;
        UnitDef old = Catalog[Units.TypeIndex[occupant]];
        return newDef.ExtractorMultiplier > old.ExtractorMultiplier && old.ExtractorMultiplier > Fix64.Zero && Units.Complete[occupant];
    }

    /// <summary>
    /// Starts a nanoframe for a builder, or returns the existing frame of the same type already on
    /// that spot. Returns -1 if the site isn't valid any more.
    /// </summary>
    internal int StartOrJoinFrame(int builder, int typeIndex, FixVec2 position)
    {
        int player = Units.Owner[builder];
        if (CanPlace(typeIndex, player, position, out int tx, out int ty) != PlacementResult.Ok) return -1;
        UnitDef def = Catalog[typeIndex];

        int existing = Terrain.Occupant[Terrain.Index(tx, ty)];
        if (existing >= 0 && Units.TypeIndex[existing] == typeIndex && Units.Owner[existing] == player && !Units.Complete[existing]
            && Units.TileX[existing] == tx && Units.TileY[existing] == ty)
            return existing;

        // Clear the old extractor out of the way of a deep extractor.
        for (int y = ty; y < ty + def.Footprint; y++)
        for (int x = tx; x < tx + def.Footprint; x++)
        {
            int occupant = Terrain.Occupant[Terrain.Index(x, y)];
            if (occupant >= 0 && IsUpgradeableExtractor(occupant, player, def))
                Kill(occupant, leaveWreck: false, explode: false);
        }

        int frame = SpawnUnit(typeIndex, player, Terrain.FootprintCentre(tx, ty, def.Footprint), complete: false);
        Events.Add(new GameEvent(GameEventType.BuildStarted, frame, builder, Position: Units.Position[frame]));
        return frame;
    }

    internal void ApplyCommand(Command c)
    {
        if (c.Player < 0 || c.Player >= Players.Length || Players[c.Player].Defeated) return;

        switch (c.Type)
        {
            case CommandType.Resign:
                DefeatPlayer(c.Player);
                return;
            case CommandType.SetConverters:
                Players[c.Player].ConvertersOn = c.Arg != 0;
                return;
        }

        if (!Units.IsValid(c.UnitId) || Units.Owner[c.UnitId] != c.Player) return;
        int id = c.UnitId;
        UnitDef def = Catalog[Units.TypeIndex[id]];

        // Factory commands work on finished and unfinished factories alike.
        switch (c.Type)
        {
            case CommandType.FactoryQueue:
                ApplyFactoryQueue(id, def, c);
                return;
            case CommandType.FactoryRepeat:
                Units.FactoryRepeat[id] = c.Arg != 0;
                return;
            case CommandType.SetRally:
                if ((c.Arg == 1 || c.Queue) && Units.HasRally[id])
                {
                    // Add a stop to the rotation; once full, the newest replaces the last.
                    var extra = Units.RallyExtra[id];
                    if (extra.Count >= UnitStore.MaxRallyPoints - 1) extra.RemoveAt(extra.Count - 1);
                    extra.Add(ClampToMap(c.Position));
                }
                else
                {
                    Units.Rally[id] = ClampToMap(c.Position);
                    Units.HasRally[id] = true;
                    Units.RallyExtra[id].Clear();
                    Units.RallyCount[id] = 0;
                }
                return;
            case CommandType.ClearRally:
                Units.HasRally[id] = false;
                Units.Rally[id] = Units.Position[id];
                Units.RallyExtra[id].Clear();
                Units.RallyCount[id] = 0;
                return;
            case CommandType.FactoryRallyEvery:
                if (def.IsFactory) Units.RallyEvery[id] = Math2.Clamp(c.Arg, 0, 10);
                return;
            case CommandType.SetFireState:
                if (c.Arg >= 0 && c.Arg <= 2) Units.Fire[id] = (FireState)c.Arg;
                return;
            case CommandType.SelfDestruct:
                Kill(id, leaveWreck: false, explode: true);
                return;
        }

        if (!Units.Complete[id]) return;

        Order order;
        switch (c.Type)
        {
            case CommandType.Stop:
                Units.ClearOrders(id);
                return;

            case CommandType.Move:
                if (!def.CanMove) return;
                order = new Order(OrderType.Move, ClampToMap(c.Position));
                break;

            case CommandType.Attack:
                if (!def.IsArmed) return;
                if (c.TargetId >= 0)
                {
                    if (!Units.IsValid(c.TargetId) || !AreEnemies(c.Player, Units.Owner[c.TargetId])) return;
                    order = new Order(OrderType.Attack, Units.Position[c.TargetId], c.TargetId);
                }
                else
                {
                    order = new Order(OrderType.AttackGround, ClampToMap(c.Position));
                }
                break;

            case CommandType.Build:
                if (!def.CanBuild || c.TypeIndex < 0 || c.TypeIndex >= Catalog.Count) return;
                if (!CanBuildType(def, c.TypeIndex, c.Player)) return;
                if (CanPlace(c.TypeIndex, c.Player, c.Position, out int tx, out int ty) != PlacementResult.Ok) return;
                order = new Order(OrderType.Build, Terrain.FootprintCentre(tx, ty, Catalog[c.TypeIndex].Footprint),
                    target: -1, typeIndex: c.TypeIndex);
                break;

            case CommandType.Reclaim:
                if (!def.CanReclaim || c.TargetId < 0 || c.TargetId >= Features.Count || Features[c.TargetId].Gone) return;
                order = new Order(OrderType.Reclaim, Features[c.TargetId].Position, c.TargetId);
                break;

            case CommandType.ReclaimArea:
                if (!def.CanReclaim || !def.CanMove) return;
                order = new Order(OrderType.ReclaimArea, ClampToMap(c.Position), target: -1, typeIndex: Math2.Clamp(c.Arg, 4, 200));
                break;

            case CommandType.AttackArea:
                if (!def.IsArmed || !def.CanMove) return;
                order = new Order(OrderType.AttackArea, ClampToMap(c.Position), target: -1, typeIndex: Math2.Clamp(c.Arg, 4, 200));
                break;

            case CommandType.RepairArea:
                if (!def.CanAssist || !def.CanMove) return;
                order = new Order(OrderType.RepairArea, ClampToMap(c.Position), target: -1, typeIndex: Math2.Clamp(c.Arg, 4, 200));
                break;

            case CommandType.Assist:
                if (!Units.IsValid(c.TargetId) || c.TargetId == id) return;
                if (AreEnemies(c.Player, Units.Owner[c.TargetId])) return;
                if (!def.CanAssist && !def.CanMove) return;
                order = new Order(OrderType.Assist, Units.Position[c.TargetId], c.TargetId);
                break;

            case CommandType.Fight:
                if (!def.CanMove) return;
                order = new Order(OrderType.Fight, ClampToMap(c.Position));
                break;

            case CommandType.Patrol:
                if (!def.CanMove) return;
                order = new Order(OrderType.Patrol, ClampToMap(c.Position)) { Origin = Units.Position[id] };
                break;

            case CommandType.DGun:
                if (!def.HasDGun) return;
                order = new Order(OrderType.DGun, ClampToMap(c.Position));
                break;

            default:
                return;
        }

        if (!c.Queue) Units.ClearOrders(id);
        Units.Orders[id].Add(order);
    }

    private bool CanBuildType(UnitDef builder, int typeIndex, int player)
    {
        foreach (int o in builder.BuildOptions) if (o == typeIndex) return true;
        // Salvaged enemy designs can be built at a factory that makes that kind of unit.
        if (builder.IsFactory && HasSalvaged(player, typeIndex) && FactoryTakesRole(builder, Catalog[typeIndex].Role)) return true;
        return false;
    }

    private void ApplyFactoryQueue(int id, UnitDef def, Command c)
    {
        if (!def.IsFactory || !CanBuildType(def, c.TypeIndex, c.Player)) return;
        var queue = Units.FactoryQueue[id];
        if (c.Arg > 0)
        {
            int n = Math2.Min(c.Arg, 100 - queue.Count);
            for (int i = 0; i < n; i++) queue.Add(c.TypeIndex);
        }
        else if (c.Arg < 0)
        {
            int remove = -c.Arg;
            // Remove from the back so the unit being built goes last.
            for (int i = queue.Count - 1; i >= 0 && remove > 0; i--)
            {
                if (queue[i] != c.TypeIndex) continue;
                if (i == 0)
                {
                    int current = Units.FactoryCurrent[id];
                    if (current >= 0 && Units.IsValid(current) && !Units.Complete[current])
                        Kill(current, leaveWreck: false, explode: false);
                    Units.FactoryCurrent[id] = -1;
                }
                queue.RemoveAt(i);
                remove--;
            }
        }
    }
}
