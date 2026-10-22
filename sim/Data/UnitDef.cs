using ContingencyZero.Sim.Core;

namespace ContingencyZero.Sim.Data;

/// <summary>
/// One unit or structure type. Loaded from data/units/*.json. Per-second values from the data
/// files are converted to per-tick values once, at load time.
/// </summary>
public sealed class UnitDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Faction { get; init; }

    // Cost and construction
    public Fix64 MetalCost { get; init; }
    public Fix64 EnergyCost { get; init; }
    /// <summary>Build work needed. A builder with 1 build power per second finishes it in this many seconds.</summary>
    public Fix64 BuildTime { get; init; }

    // Body
    public Fix64 MaxHealth { get; init; }
    public Fix64 SpeedPerTick { get; init; }
    public Fix64 SightRange { get; init; }
    public bool IsCommander { get; init; }

    // Weapon (Damage zero means unarmed)
    public Fix64 WeaponRange { get; init; }
    public Fix64 WeaponDamage { get; init; }
    public int ReloadTicks { get; init; }
    /// <summary>Chance to hit, 0 to 1.</summary>
    public Fix64 Accuracy { get; init; }

    // Builder
    public Fix64 BuildPowerPerTick { get; init; }
    public Fix64 BuildRange { get; init; }

    // Economy
    public Fix64 MetalIncomePerTick { get; init; }
    public Fix64 EnergyIncomePerTick { get; init; }
    public Fix64 MetalStorage { get; init; }
    public Fix64 EnergyStorage { get; init; }

    /// <summary>Share of metal cost left in the wreck when this unit dies.</summary>
    public Fix64 WreckFraction { get; init; }

    public bool CanMove => SpeedPerTick > Fix64.Zero;
    public bool IsArmed => WeaponDamage > Fix64.Zero;
    public bool CanBuild => BuildPowerPerTick > Fix64.Zero;
}
