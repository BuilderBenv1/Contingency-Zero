using System;
using ContingencyZero.Sim.Core;

namespace ContingencyZero.Sim.State;

public enum OrderType : byte
{
    None = 0,
    Move = 1,
    Build = 2,
    Reclaim = 3,
    Attack = 4,
}

/// <summary>
/// Every unit and structure, stored as parallel arrays indexed by unit id (structure of arrays).
/// Ids are handed out in order and never reused, so iterating 0..Count is the canonical order.
/// </summary>
public sealed class UnitStore
{
    private const int InitialCapacity = 256;

    public int Count { get; private set; }

    public int[] TypeIndex = new int[InitialCapacity];
    public int[] Owner = new int[InitialCapacity];
    public bool[] Alive = new bool[InitialCapacity];
    public FixVec2[] Position = new FixVec2[InitialCapacity];
    public Fix64[] Health = new Fix64[InitialCapacity];
    public int[] ReloadLeft = new int[InitialCapacity];
    /// <summary>Current weapon target unit id, or -1.</summary>
    public int[] WeaponTarget = new int[InitialCapacity];

    // Current order
    public OrderType[] Order = new OrderType[InitialCapacity];
    public FixVec2[] OrderPosition = new FixVec2[InitialCapacity];
    /// <summary>Wreck id for Reclaim, enemy unit id for Attack, build job id for Build.</summary>
    public int[] OrderTarget = new int[InitialCapacity];

    public int Add(int typeIndex, int owner, FixVec2 position, Fix64 health)
    {
        if (Count == TypeIndex.Length) Grow();
        int id = Count++;
        TypeIndex[id] = typeIndex;
        Owner[id] = owner;
        Alive[id] = true;
        Position[id] = position;
        Health[id] = health;
        ReloadLeft[id] = 0;
        WeaponTarget[id] = -1;
        Order[id] = OrderType.None;
        OrderPosition[id] = position;
        OrderTarget[id] = -1;
        return id;
    }

    public bool IsValid(int id) => id >= 0 && id < Count && Alive[id];

    public void ClearOrder(int id)
    {
        Order[id] = OrderType.None;
        OrderTarget[id] = -1;
        OrderPosition[id] = Position[id];
    }

    private void Grow()
    {
        int size = TypeIndex.Length * 2;
        Array.Resize(ref TypeIndex, size);
        Array.Resize(ref Owner, size);
        Array.Resize(ref Alive, size);
        Array.Resize(ref Position, size);
        Array.Resize(ref Health, size);
        Array.Resize(ref ReloadLeft, size);
        Array.Resize(ref WeaponTarget, size);
        Array.Resize(ref Order, size);
        Array.Resize(ref OrderPosition, size);
        Array.Resize(ref OrderTarget, size);
    }
}
