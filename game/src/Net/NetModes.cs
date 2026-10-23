using System;
using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Missions;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using ContingencyZero.Sim.Missions;
using ContingencyZero.Sim.Replay;

namespace ContingencyZero.Game.Net;

/// <summary>
/// Joins the lobby's modes to the mission code. The lobby itself (Lobby.cs) knows nothing about
/// mission files or Godot; this is where co-op missions are listed and survival waves are built
/// (by the sim: <see cref="SurvivalMode"/>, so every machine builds the same match).
/// </summary>
public static class NetModes
{
    /// <summary>Missions playable co-operatively, in campaign order.</summary>
    public static List<MissionInfo> CoopMissions() => MissionInfo.LoadAll().Where(m => m.IsCoop).ToList();

    /// <summary>A mission as the lobby needs it. Works for any mission; a single-player one just has one seat.</summary>
    public static CoopMission ToCoop(MissionInfo mission)
    {
        MapData map = GameData.Maps().First(m => m.Key == mission.MapKey).Load();
        return new CoopMission(mission.Key, mission.Title, map, HumanSeats(mission), humans => mission.BuildScenario(humans));
    }

    /// <summary>The mission's human seats: its <c>humanPlayers</c> list when it has one, else its single human.</summary>
    public static IReadOnlyList<int> HumanSeats(MissionInfo mission) =>
        mission.HumanPlayers.Count > 0 ? mission.HumanPlayers : new[] { mission.HumanPlayer };

    /// <summary>Whether survival games have real waves in this build.</summary>
    public static bool SurvivalAvailable => true;

    /// <summary>
    /// The survival rules for a lobby: up to four humans (every start position) on team 0, plus the
    /// Horde slot that the sim's survival script spawns and drives. Commanders come from the script.
    /// </summary>
    public static SurvivalRules SurvivalRules() => new()
    {
        HumanCapacity = SurvivalMode.Capacity,
        Players = (humans, map) => SurvivalMode.Players(humans, map).ToList(),
        BuildJson = SurvivalMode.Build,
        Options = res => SurvivalMode.Options(AiDifficulty.Normal, res),
    };
}
