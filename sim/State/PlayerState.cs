using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;

namespace ContingencyZero.Sim.State;

public sealed class PlayerState
{
    public required int Id { get; init; }
    /// <summary>0 to 7. Players on the same team are allies.</summary>
    public required int Team { get; init; }
    public required string Faction { get; init; }
    public string Name { get; init; } = "";
    /// <summary>Brutal AI gets more out of every source of income.</summary>
    public Fix64 IncomeMultiplier = Fix64.One;

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
    /// <summary>Energy the metal makers drew last tick (already included in <see cref="EnergySpent"/>). Display only; not hashed.</summary>
    public Fix64 ConverterEnergy;
    /// <summary>Share of requested build spending that was actually funded last tick, 0 to 1. Below 1 means a stall.</summary>
    public Fix64 BuildRatio = Fix64.One;
    /// <summary>Metal makers run only while this is true.</summary>
    public bool ConvertersOn = true;

    public bool Defeated;
    public int DefeatedTick = -1;

    /// <summary>
    /// Salvage: enemy wrecks fully reclaimed, per unit type. Reclaiming enough of a type (see
    /// <see cref="World.SalvageWrecksNeeded"/>) lets this player's factories build it.
    /// </summary>
    public int[] SalvageProgress = System.Array.Empty<int>();

    // Match statistics for the score screen
    public int UnitsBuilt;
    public int UnitsLost;
    public int Kills;
    public Fix64 MetalProduced;
    public Fix64 EnergyProduced;
}

/// <summary>Something on the ground that builders can reclaim: a wreck, a rock (metal) or a tree (energy).</summary>
public sealed class Feature
{
    public required int Id { get; init; }
    public required FeatureKind Kind { get; init; }
    public required FixVec2 Position { get; init; }
    /// <summary>For wrecks: the unit type it came from.</summary>
    public int SourceTypeIndex { get; init; } = -1;
    public Fix64 Metal;
    public Fix64 Energy;
    public bool Gone => Metal <= Fix64.Zero && Energy <= Fix64.Zero;
    public Fix64 Radius { get; init; } = Fix64.FromInt(2);
}

/// <summary>A shell, missile, bomb or Breach Cannon bolt in flight.</summary>
public sealed class Projectile
{
    public required int Id { get; init; }
    public required WeaponKind Kind { get; init; }
    public required int Owner { get; init; }
    public required int SourceUnit { get; init; }
    public required int Team { get; init; }
    public FixVec2 Start;
    public FixVec2 Position;
    /// <summary>Where it will land (ballistic) or the target's last known position (homing).</summary>
    public FixVec2 Aim;
    /// <summary>Homing target, or -1.</summary>
    public int TargetUnit = -1;
    public Fix64 SpeedPerTick;
    public Fix64 Damage;
    public Fix64 Splash;
    public bool HitsAir;
    public int TicksAlive;
    /// <summary>Total flight time for ballistic shots, so the renderer can draw the arc.</summary>
    public int FlightTicks;
    public bool Done;
}
