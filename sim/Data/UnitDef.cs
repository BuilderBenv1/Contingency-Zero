using System.Collections.Generic;
using ContingencyZero.Sim.Core;

namespace ContingencyZero.Sim.Data;

/// <summary>How a unit gets around, which decides where it can go.</summary>
public enum MoveClass : byte
{
    /// <summary>Structures. Never moves.</summary>
    None = 0,
    /// <summary>Walkers: climb steep slopes, wade shallow water.</summary>
    Bot = 1,
    /// <summary>Tracks and wheels: gentler slopes, shallow water.</summary>
    Vehicle = 2,
    /// <summary>Gentle slopes, land and any water.</summary>
    Hover = 3,
    /// <summary>Walks along the sea floor: land and any water, moderate slopes.</summary>
    Amphibious = 4,
    /// <summary>Deep water only.</summary>
    Ship = 5,
    /// <summary>Flies over everything.</summary>
    Air = 6,
}

public enum WeaponKind : byte
{
    /// <summary>Instant hit.</summary>
    Laser = 0,
    /// <summary>Fast straight shell with travel time.</summary>
    Cannon = 1,
    /// <summary>Slow high arc to a ground point, splash damage.</summary>
    Artillery = 2,
    /// <summary>Homing missile.</summary>
    Missile = 3,
    /// <summary>Fast anti-air burst.</summary>
    Flak = 4,
    /// <summary>Dropped straight down by aircraft.</summary>
    Bomb = 5,
    /// <summary>The Commander's Breach Cannon: a slow bolt that destroys almost everything it touches.</summary>
    DGun = 6,
}

public sealed class WeaponDef
{
    public required WeaponKind Kind { get; init; }
    public Fix64 Range { get; init; }
    public Fix64 MinRange { get; init; }
    public Fix64 Damage { get; init; }
    public int ReloadTicks { get; init; }
    public Fix64 Accuracy { get; init; } = Fix64.One;
    /// <summary>Distance per tick. Zero means instant.</summary>
    public Fix64 ProjectileSpeedPerTick { get; init; }
    public Fix64 Splash { get; init; }
    public bool TargetsGround { get; init; } = true;
    public bool TargetsAir { get; init; }
    /// <summary>Energy used per shot (the D-gun).</summary>
    public Fix64 EnergyPerShot { get; init; }
}

/// <summary>
/// One unit or structure type. Loaded from data/units/*.json. Per-second values from the data
/// files are converted to per-tick values once, at load time.
/// </summary>
public sealed class UnitDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Faction { get; init; }
    public string Description { get; init; } = "";
    /// <summary>What the unit is for: drives its procedural model and UI grouping ("tank", "factory_bot", ...).</summary>
    public string Role { get; init; } = "";
    public int Tier { get; init; } = 1;

    // Cost and construction
    public Fix64 MetalCost { get; init; }
    public Fix64 EnergyCost { get; init; }
    /// <summary>Build work needed. A builder with 1 build power per second finishes it in this many seconds.</summary>
    public Fix64 BuildTime { get; init; }

    // Body
    public Fix64 MaxHealth { get; init; }
    public MoveClass MoveClass { get; init; }
    public Fix64 SpeedPerTick { get; init; }
    /// <summary>Collision radius for mobile units.</summary>
    public Fix64 Radius { get; init; } = Fix64.FromInt(2);
    public Fix64 SightRange { get; init; }
    public Fix64 RadarRange { get; init; }
    public bool IsCommander { get; init; }

    // Structures
    public bool IsStructure => MoveClass == MoveClass.None;
    /// <summary>Footprint edge in map tiles (structures only).</summary>
    public int Footprint { get; init; } = 1;
    public bool NeedsMetalSpot { get; init; }
    public bool NeedsWater { get; init; }
    /// <summary>Yield multiplier for extractors over the metal spot's base yield.</summary>
    public Fix64 ExtractorMultiplier { get; init; }

    public IReadOnlyList<WeaponDef> Weapons { get; init; } = new List<WeaponDef>();

    // Builder / factory
    public Fix64 BuildPowerPerTick { get; init; }
    public Fix64 BuildRange { get; init; }
    /// <summary>How much faster than its build power this unit reclaims.</summary>
    public Fix64 ReclaimMultiplier { get; init; } = Fix64.One;
    /// <summary>Unit ids this unit can build, as written in the data file.</summary>
    public IReadOnlyList<string> BuildOptionIds { get; init; } = new List<string>();
    /// <summary>Indices into the catalog, resolved after every unit has loaded.</summary>
    public int[] BuildOptions { get; internal set; } = System.Array.Empty<int>();
    public bool IsFactory => IsStructure && BuildOptionIds.Count > 0;

    // Economy
    public Fix64 MetalIncomePerTick { get; init; }
    public Fix64 EnergyIncomePerTick { get; init; }
    /// <summary>Wind turbines produce the map's current wind instead of a fixed amount.</summary>
    public bool UsesWind { get; init; }
    public Fix64 MetalStorage { get; init; }
    public Fix64 EnergyStorage { get; init; }
    /// <summary>Metal makers: energy turned into metal each tick while energy is above 10%.</summary>
    public Fix64 ConvertEnergyPerTick { get; init; }
    public Fix64 ConvertMetalPerTick { get; init; }

    // Death
    /// <summary>Share of metal cost left in the wreck when this unit dies.</summary>
    public Fix64 WreckFraction { get; init; }
    /// <summary>Wrecks of this design another faction must reclaim before its factories can build it.</summary>
    public int SalvageWrecks { get; init; } = 3;
    public Fix64 DeathExplosionDamage { get; init; }
    public Fix64 DeathExplosionRadius { get; init; }

    // Specials
    /// <summary>Mantle Crawler: reclaims nearby wrecks on its own and turns the metal into health.</summary>
    public Fix64 SelfReclaimRange { get; init; }

    public bool CanMove => MoveClass != MoveClass.None && SpeedPerTick > Fix64.Zero;
    public bool IsAir => MoveClass == MoveClass.Air;
    public bool IsArmed => Weapons.Count > 0;
    public bool CanBuild => BuildPowerPerTick > Fix64.Zero && !IsFactory && BuildOptionIds.Count > 0;
    public bool CanReclaim => BuildPowerPerTick > Fix64.Zero && !IsStructure;
    public bool CanAssist => BuildPowerPerTick > Fix64.Zero && !IsStructure;
    public bool HasDGun
    {
        get
        {
            foreach (WeaponDef w in Weapons) if (w.Kind == WeaponKind.DGun) return true;
            return false;
        }
    }
}
