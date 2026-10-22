using System.Collections.Generic;
using ContingencyZero.Sim.Core;

namespace ContingencyZero.Sim.Match;

public enum AiDifficulty : byte
{
    /// <summary>A human (local or remote) controls this slot.</summary>
    None = 0,
    Easy = 1,
    Normal = 2,
    Hard = 3,
    /// <summary>Sees the whole map and earns 50% more. The lobby says so.</summary>
    Brutal = 4,
}

public enum WinCondition : byte
{
    /// <summary>Lose your Commander, lose the match.</summary>
    Commander = 0,
    /// <summary>Lose when every unit and structure is gone.</summary>
    Annihilation = 1,
}

public sealed record PlayerSetup(
    string Name,
    int Team,
    string Faction,
    int StartIndex,
    AiDifficulty Ai = AiDifficulty.None);

public sealed record MatchOptions(
    int StartMetal = 1000,
    int StartEnergy = 1000,
    WinCondition WinCondition = WinCondition.Commander,
    bool SpawnCommanders = true,
    /// <summary>Everyone sees everything (spectating, tests).</summary>
    bool RevealMap = false);

/// <summary>A unit placed on the map before tick 0 (missions, tests).</summary>
public readonly record struct Spawn(string TypeId, int Owner, FixVec2 Position);
