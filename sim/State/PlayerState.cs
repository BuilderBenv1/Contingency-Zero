using ContingencyZero.Sim.Core;

namespace ContingencyZero.Sim.State;

public sealed class PlayerState
{
    public required int Id { get; init; }
    public required int Team { get; init; }

    public Fix64 Metal;
    public Fix64 Energy;
    public Fix64 MetalStorage;
    public Fix64 EnergyStorage;

    // Last tick's flow, for the resource bar. Recomputed every tick.
    public Fix64 MetalIncome;
    public Fix64 EnergyIncome;
    public Fix64 MetalSpent;
    public Fix64 EnergySpent;
    public Fix64 MetalWasted;
    public Fix64 EnergyWasted;
    /// <summary>Share of requested build spending that was actually funded last tick, 0 to 1. Below 1 means a stall.</summary>
    public Fix64 BuildRatio = Fix64.One;

    public bool Defeated;
}

/// <summary>Debris left by a destroyed unit. Builders reclaim it for metal.</summary>
public sealed class Wreck
{
    public required int Id { get; init; }
    public required FixVec2 Position { get; init; }
    public required int SourceTypeIndex { get; init; }
    public Fix64 Metal;
    public bool Gone => Metal <= Fix64.Zero;
}

/// <summary>A structure or unit being built. Progress goes from 0 to the type's build time.</summary>
public sealed class BuildJob
{
    public required int Id { get; init; }
    public required int Owner { get; init; }
    public required int TypeIndex { get; init; }
    public required FixVec2 Position { get; init; }
    public Fix64 Progress;
    public bool Done;
    /// <summary>Unit id created when the job finished, or -1.</summary>
    public int ResultUnit = -1;
}
