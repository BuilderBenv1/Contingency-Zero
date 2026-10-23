namespace ContingencyZero.Sim.Core;

public enum GameEventType : byte
{
    /// <summary>A weapon fired. Unit, Value = weapon slot, Position = muzzle, Position2 = aim, Kind = WeaponKind.</summary>
    WeaponFired,
    /// <summary>A shell or bomb landed. Position, Value = splash radius, Kind = WeaponKind.</summary>
    Impact,
    /// <summary>A unit died. Unit, Position, Value = type index, Other = owner.</summary>
    UnitDestroyed,
    /// <summary>A big death or reactor explosion. Position, Value = radius.</summary>
    Explosion,
    /// <summary>A nanoframe went up. Unit.</summary>
    BuildStarted,
    /// <summary>Construction finished. Unit.</summary>
    BuildComplete,
    /// <summary>A builder is putting work into something. Unit = builder, Other = target, Position2 = target pos.</summary>
    NanoBeam,
    /// <summary>A builder is reclaiming. Unit = builder, Position2 = feature pos.</summary>
    ReclaimBeam,
    /// <summary>A player was knocked out. Other = player.</summary>
    PlayerDefeated,
    /// <summary>A match result was decided. Value = winning team.</summary>
    GameOver,
    /// <summary>A unit was hit. Unit, Other = attacker.</summary>
    UnitDamaged,
    /// <summary>A mission message. Value = message index in the mission script.</summary>
    MissionMessage,
    /// <summary>An order couldn't be carried out. Unit, Other = owner, Value = reason code.</summary>
    OrderFailed,
    /// <summary>Salvage progress: a player finished reclaiming an enemy wreck. Other = player, Value = unit type, Kind = wrecks so far (unlocked at <see cref="World.SalvageWrecksNeeded"/>).</summary>
    Salvage,
    /// <summary>The ground was reshaped (a crater). Position = min corner, Position2 = max corner of the changed area, in world units.</summary>
    TerrainChanged,
}

/// <summary>
/// Something that happened this tick, for the renderer and sound. Events are output only: they are
/// rebuilt every tick and never feed back into the simulation.
/// </summary>
public readonly record struct GameEvent(
    GameEventType Type,
    int Unit = -1,
    int Other = -1,
    int Value = 0,
    byte Kind = 0,
    FixVec2 Position = default,
    FixVec2 Position2 = default);
