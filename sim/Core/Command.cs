namespace ContingencyZero.Sim.Core;

public enum CommandType : byte
{
    /// <summary>Move to Position.</summary>
    Move = 1,
    /// <summary>Clear every order (and a factory's queue stays).</summary>
    Stop = 2,
    /// <summary>Build TypeIndex at Position.</summary>
    Build = 3,
    /// <summary>Reclaim feature TargetId.</summary>
    Reclaim = 4,
    /// <summary>Attack unit TargetId, or the ground at Position if TargetId is -1.</summary>
    Attack = 5,
    /// <summary>Guard / help build / repair unit TargetId.</summary>
    Assist = 6,
    /// <summary>Patrol between the unit's position and Position.</summary>
    Patrol = 7,
    /// <summary>Commander fires the Breach Cannon at Position.</summary>
    DGun = 8,
    /// <summary>Factory: add Arg copies of TypeIndex to the queue (negative Arg removes).</summary>
    FactoryQueue = 9,
    /// <summary>Factory: Arg 1 turns repeat on, 0 off.</summary>
    FactoryRepeat = 10,
    /// <summary>Factory: new units head to Position. Arg 1 sets the secondary (ferry) rally point instead.</summary>
    SetRally = 11,
    /// <summary>Arg is a FireState.</summary>
    SetFireState = 12,
    /// <summary>The player gives up. UnitId is ignored.</summary>
    Resign = 13,
    /// <summary>Destroy your own unit (no wreck).</summary>
    SelfDestruct = 14,
    /// <summary>Arg 1 turns this player's metal makers on, 0 off. UnitId is ignored.</summary>
    SetConverters = 15,
    /// <summary>Attack-move to Position: fight anything met on the way.</summary>
    Fight = 16,
    /// <summary>Reclaim everything within Arg world units of Position, one thing after another.</summary>
    ReclaimArea = 17,
    /// <summary>Repair and help build everything friendly within Arg world units of Position.</summary>
    RepairArea = 18,
    /// <summary>Factory: every Arg-th unit goes to the secondary rally point (0 turns it off).</summary>
    FactoryRallyEvery = 19,
}

/// <summary>
/// The only way anything outside the sim changes the game. Humans, AI and remote players all
/// produce these; replays are just a scenario and a list of them.
/// </summary>
/// <param name="Queue">Append to the unit's order queue instead of replacing it (shift-click).</param>
public readonly record struct Command(
    int Tick,
    int Player,
    CommandType Type,
    int UnitId,
    FixVec2 Position = default,
    int TargetId = -1,
    int TypeIndex = -1,
    bool Queue = false,
    int Arg = 0);
