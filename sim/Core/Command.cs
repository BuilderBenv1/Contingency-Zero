namespace ContingencyZero.Sim.Core;

public enum CommandType : byte
{
    /// <summary>Move a unit to <see cref="Command.Position"/>. Clears any build or reclaim order.</summary>
    Move = 1,
    /// <summary>Clear every order on a unit.</summary>
    Stop = 2,
    /// <summary>Builder walks to <see cref="Command.Position"/> and builds <see cref="Command.TypeIndex"/> there.</summary>
    Build = 3,
    /// <summary>Builder walks to wreck <see cref="Command.TargetId"/> and reclaims its metal.</summary>
    Reclaim = 4,
    /// <summary>Unit focuses fire on enemy <see cref="Command.TargetId"/>, moving into range if needed.</summary>
    Attack = 5,
}

/// <summary>
/// The only way anything outside the sim changes the game. Humans, AI and remote players all
/// produce these; replays are just a seed, a scenario and a list of them.
/// </summary>
public readonly record struct Command(
    int Tick,
    int Player,
    CommandType Type,
    int UnitId,
    FixVec2 Position = default,
    int TargetId = -1,
    int TypeIndex = -1);
