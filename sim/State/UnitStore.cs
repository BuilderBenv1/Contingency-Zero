using System;
using System.Collections.Generic;
using ContingencyZero.Sim.Core;

namespace ContingencyZero.Sim.State;

public enum OrderType : byte
{
    None = 0,
    Move = 1,
    /// <summary>Attack a unit (Target).</summary>
    Attack = 2,
    /// <summary>Fire at a ground position (Position).</summary>
    AttackGround = 3,
    /// <summary>Build TypeIndex at Position. Target becomes the nanoframe's id once it exists.</summary>
    Build = 4,
    /// <summary>Reclaim feature Target.</summary>
    Reclaim = 5,
    /// <summary>Guard/assist unit Target: help build it, help its factory, repair it, or follow it.</summary>
    Assist = 6,
    /// <summary>Patrol between where the order was given and Position.</summary>
    Patrol = 7,
    /// <summary>Fire the Breach Cannon at Position.</summary>
    DGun = 8,
    /// <summary>Attack-move: head for Position, stopping to fight anything met on the way.</summary>
    Fight = 9,
    /// <summary>Reclaim everything within TypeIndex units of Position; Target is the feature being worked on.</summary>
    ReclaimArea = 10,
    /// <summary>Repair/help build everything friendly within TypeIndex units of Position; Target is the unit being worked on.</summary>
    RepairArea = 11,
    /// <summary>Fight every enemy within TypeIndex units of Position, nearest first; Target is the current one.</summary>
    AttackArea = 12,
}

public enum FireState : byte
{
    HoldFire = 0,
    ReturnFire = 1,
    FireAtWill = 2,
}

public struct Order
{
    public OrderType Type;
    public FixVec2 Position;
    /// <summary>Patrol's other end, captured when the order starts.</summary>
    public FixVec2 Origin;
    public int Target;
    public int TypeIndex;

    public Order(OrderType type, FixVec2 position, int target = -1, int typeIndex = -1)
    {
        Type = type;
        Position = position;
        Origin = position;
        Target = target;
        TypeIndex = typeIndex;
    }
}

/// <summary>
/// Every unit and structure, stored as parallel arrays indexed by unit id (structure of arrays).
/// Ids are handed out in order and never reused, so iterating 0..Count is the canonical order.
/// </summary>
public sealed class UnitStore
{
    public const int MaxWeapons = 2;
    /// <summary>Most rally points a factory can hold (primary plus extras).</summary>
    public const int MaxRallyPoints = 4;
    private const int InitialCapacity = 256;

    public int Count { get; private set; }

    public int[] TypeIndex = new int[InitialCapacity];
    public int[] Owner = new int[InitialCapacity];
    public bool[] Alive = new bool[InitialCapacity];
    public FixVec2[] Position = new FixVec2[InitialCapacity];
    public Fix64[] Health = new Fix64[InitialCapacity];

    /// <summary>Build work done so far. Equal to the type's build time once complete.</summary>
    public Fix64[] BuildProgress = new Fix64[InitialCapacity];
    public bool[] Complete = new bool[InitialCapacity];
    /// <summary>Tick the unit was created, for display and AI bookkeeping.</summary>
    public int[] BornTick = new int[InitialCapacity];

    // Weapons: slot 0 and 1
    public int[] Reload0 = new int[InitialCapacity];
    public int[] Reload1 = new int[InitialCapacity];
    public int[] Target0 = new int[InitialCapacity];
    public int[] Target1 = new int[InitialCapacity];
    public FireState[] Fire = new FireState[InitialCapacity];
    public int[] Kills = new int[InitialCapacity];
    /// <summary>Last unit that damaged this one, for return fire.</summary>
    public int[] LastAttacker = new int[InitialCapacity];

    // Orders and movement
    public List<Order>[] Orders = new List<Order>[InitialCapacity];
    public List<FixVec2>?[] Path = new List<FixVec2>?[InitialCapacity];
    public int[] PathIndex = new int[InitialCapacity];
    public FixVec2[] PathGoal = new FixVec2[InitialCapacity];
    public bool[] PathPending = new bool[InitialCapacity];
    /// <summary>Goal of the queued path search (valid while PathPending).</summary>
    public FixVec2[] PathRequestGoal = new FixVec2[InitialCapacity];
    public int[] StuckTicks = new int[InitialCapacity];
    /// <summary>Where an idle unit was parked, so it returns after chasing something.</summary>
    public FixVec2[] Anchor = new FixVec2[InitialCapacity];
    /// <summary>Movement direction last tick (for aircraft and the renderer).</summary>
    public FixVec2[] Velocity = new FixVec2[InitialCapacity];
    /// <summary>Map tile claimed by the active destination order, or -1. Ids are never reused.</summary>
    public int[] ReservedGoal = new int[InitialCapacity];
    private int[] _goalOwners = Array.Empty<int>();

    // Factories
    public List<int>[] FactoryQueue = new List<int>[InitialCapacity];
    public bool[] FactoryRepeat = new bool[InitialCapacity];
    public int[] FactoryCurrent = new int[InitialCapacity];
    public FixVec2[] Rally = new FixVec2[InitialCapacity];
    public bool[] HasRally = new bool[InitialCapacity];
    /// <summary>Extra (ferry) rally points after the primary one, at most <see cref="MaxRallyPoints"/> - 1.</summary>
    public List<FixVec2>[] RallyExtra = new List<FixVec2>[InitialCapacity];
    /// <summary>0: finished units take the rally points in turn. N &gt; 0: every Nth unit goes to the next extra point, the rest to the primary.</summary>
    public int[] RallyEvery = new int[InitialCapacity];
    public int[] RallyCount = new int[InitialCapacity];

    // Structures
    /// <summary>Top-left tile of a structure's footprint.</summary>
    public int[] TileX = new int[InitialCapacity];
    public int[] TileY = new int[InitialCapacity];
    /// <summary>Metal per tick an extractor pulls from its spot.</summary>
    public Fix64[] ExtractorYield = new Fix64[InitialCapacity];

    public int Add(int typeIndex, int owner, FixVec2 position, Fix64 health, bool complete, int tick)
    {
        if (Count == TypeIndex.Length) Grow();
        int id = Count++;
        TypeIndex[id] = typeIndex;
        Owner[id] = owner;
        Alive[id] = true;
        Position[id] = position;
        Health[id] = health;
        BuildProgress[id] = Fix64.Zero;
        Complete[id] = complete;
        BornTick[id] = tick;
        Reload0[id] = 0;
        Reload1[id] = 0;
        Target0[id] = -1;
        Target1[id] = -1;
        Fire[id] = FireState.FireAtWill;
        Kills[id] = 0;
        LastAttacker[id] = -1;
        Orders[id] = new List<Order>();
        Path[id] = null;
        PathIndex[id] = 0;
        PathGoal[id] = position;
        PathPending[id] = false;
        PathRequestGoal[id] = position;
        StuckTicks[id] = 0;
        Anchor[id] = position;
        Velocity[id] = FixVec2.Zero;
        ReservedGoal[id] = -1;
        FactoryQueue[id] = new List<int>();
        FactoryRepeat[id] = false;
        FactoryCurrent[id] = -1;
        Rally[id] = position;
        HasRally[id] = false;
        RallyExtra[id] = new List<FixVec2>();
        RallyEvery[id] = 0;
        RallyCount[id] = 0;
        TileX[id] = -1;
        TileY[id] = -1;
        ExtractorYield[id] = Fix64.Zero;
        return id;
    }

    /// <summary>Every rally point of a factory in order: the primary first, then the extras.</summary>
    public IEnumerable<FixVec2> RallyPoints(int id)
    {
        if (!HasRally[id]) yield break;
        yield return Rally[id];
        foreach (FixVec2 p in RallyExtra[id]) yield return p;
    }

    public bool IsValid(int id) => id >= 0 && id < Count && Alive[id];

    public Order CurrentOrder(int id) => Orders[id].Count > 0 ? Orders[id][0] : default;

    public void ClearPath(int id)
    {
        Path[id] = null;
        PathIndex[id] = 0;
        PathPending[id] = false;
        StuckTicks[id] = 0;
    }

    public void ClearOrders(int id)
    {
        Orders[id].Clear();
        ReleaseGoal(id);
        ClearPath(id);
        Anchor[id] = Position[id];
    }

    /// <summary>Finishes the current order and moves on to the next queued one.</summary>
    public void PopOrder(int id)
    {
        if (Orders[id].Count > 0) Orders[id].RemoveAt(0);
        ReleaseGoal(id);
        ClearPath(id);
        if (Orders[id].Count == 0) Anchor[id] = Position[id];
    }

    internal void InitializeGoals(int tileCount)
    {
        _goalOwners = new int[tileCount];
        Array.Fill(_goalOwners, -1);
    }

    /// <summary>The unit holding a map tile as its destination, or -1.</summary>
    public int GoalOwner(int tile) => tile >= 0 && tile < _goalOwners.Length ? _goalOwners[tile] : -1;

    internal bool ReserveGoal(int id, int tile)
    {
        if (tile < 0 || tile >= _goalOwners.Length) return false;
        int owner = _goalOwners[tile];
        if (owner >= 0 && owner != id) return false;
        ReleaseGoal(id);
        _goalOwners[tile] = id;
        ReservedGoal[id] = tile;
        return true;
    }

    public void ReleaseGoal(int id)
    {
        int old = ReservedGoal[id];
        if (old >= 0 && old < _goalOwners.Length && _goalOwners[old] == id) _goalOwners[old] = -1;
        ReservedGoal[id] = -1;
    }

    private void Grow()
    {
        int size = TypeIndex.Length * 2;
        Array.Resize(ref TypeIndex, size);
        Array.Resize(ref Owner, size);
        Array.Resize(ref Alive, size);
        Array.Resize(ref Position, size);
        Array.Resize(ref Health, size);
        Array.Resize(ref BuildProgress, size);
        Array.Resize(ref Complete, size);
        Array.Resize(ref BornTick, size);
        Array.Resize(ref Reload0, size);
        Array.Resize(ref Reload1, size);
        Array.Resize(ref Target0, size);
        Array.Resize(ref Target1, size);
        Array.Resize(ref Fire, size);
        Array.Resize(ref Kills, size);
        Array.Resize(ref LastAttacker, size);
        Array.Resize(ref Orders, size);
        Array.Resize(ref Path, size);
        Array.Resize(ref PathIndex, size);
        Array.Resize(ref PathGoal, size);
        Array.Resize(ref PathPending, size);
        Array.Resize(ref PathRequestGoal, size);
        Array.Resize(ref StuckTicks, size);
        Array.Resize(ref Anchor, size);
        Array.Resize(ref Velocity, size);
        Array.Resize(ref ReservedGoal, size);
        Array.Resize(ref FactoryQueue, size);
        Array.Resize(ref FactoryRepeat, size);
        Array.Resize(ref FactoryCurrent, size);
        Array.Resize(ref Rally, size);
        Array.Resize(ref HasRally, size);
        Array.Resize(ref RallyExtra, size);
        Array.Resize(ref RallyEvery, size);
        Array.Resize(ref RallyCount, size);
        Array.Resize(ref TileX, size);
        Array.Resize(ref TileY, size);
        Array.Resize(ref ExtractorYield, size);
    }
}
