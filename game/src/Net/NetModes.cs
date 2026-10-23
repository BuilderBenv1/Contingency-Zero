using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Missions;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using ContingencyZero.Sim.Replay;

namespace ContingencyZero.Game.Net;

/// <summary>
/// Joins the lobby's modes to the mission code. The lobby itself (Lobby.cs) knows nothing about
/// mission files or Godot; this is where co-op missions are listed and survival waves are built.
/// <para>
/// The co-op and survival mission APIs (<c>MissionInfo.HumanPlayers</c>, <c>MissionInfo.BuildScenario(int)</c>,
/// <c>Sim.Missions.SurvivalMode.Build(...)</c>) are owned by the missions code and are bound here by
/// name, so this file builds and the lobby works (with a plain skirmish fallback for survival)
/// whether or not they are present in a given build.
/// </para>
/// </summary>
public static class NetModes
{
    /// <summary>Missions playable co-operatively, in campaign order.</summary>
    public static List<MissionInfo> CoopMissions() =>
        MissionInfo.LoadAll().Where(m => string.Equals(m.Campaign, "coop", StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>A mission as the lobby needs it. Works for any mission; a single-player one just has one seat.</summary>
    public static CoopMission ToCoop(MissionInfo mission)
    {
        MapData map = GameData.Maps().First(m => m.Key == mission.MapKey).Load();
        IReadOnlyList<int> seats = HumanSeats(mission);
        return new CoopMission(mission.Key, mission.Title, map, seats, humans => Build(mission, humans));
    }

    /// <summary>The mission's human seats: its <c>HumanPlayers</c> list when it has one, else its single human.</summary>
    public static IReadOnlyList<int> HumanSeats(MissionInfo mission)
    {
        if (mission.GetType().GetProperty("HumanPlayers", BindingFlags.Public | BindingFlags.Instance)?.GetValue(mission) is IEnumerable<int> list)
        {
            var seats = list.ToList();
            if (seats.Count > 0) return seats;
        }
        return new[] { mission.HumanPlayer };
    }

    /// <summary>The mission's scenario for a number of humans (<c>BuildScenario(int)</c> when the mission supports it).</summary>
    public static Scenario Build(MissionInfo mission, int humans)
    {
        MethodInfo? withCount = mission.GetType().GetMethod("BuildScenario", BindingFlags.Public | BindingFlags.Instance, new[] { typeof(int) });
        if (withCount is not null && withCount.Invoke(mission, new object[] { Math.Max(1, humans) }) is Scenario s) return s;
        return mission.BuildScenario();
    }

    private static MethodInfo? _survivalBuild;
    private static bool _survivalLooked;

    /// <summary><c>ContingencyZero.Sim.Missions.SurvivalMode.Build(MapData, IReadOnlyList&lt;PlayerSetup&gt;, AiDifficulty, int)</c>, if this build has it.</summary>
    private static MethodInfo? SurvivalBuild()
    {
        if (_survivalLooked) return _survivalBuild;
        _survivalLooked = true;
        Type? type = typeof(Scenario).Assembly.GetType("ContingencyZero.Sim.Missions.SurvivalMode");
        _survivalBuild = type?.GetMethod("Build", BindingFlags.Public | BindingFlags.Static,
            new[] { typeof(MapData), typeof(IReadOnlyList<PlayerSetup>), typeof(AiDifficulty), typeof(int) });
        return _survivalBuild;
    }

    /// <summary>Whether survival games have real waves in this build.</summary>
    public static bool SurvivalAvailable => SurvivalBuild() is not null;

    /// <summary>
    /// The survival rules for a lobby. The "Horde" is an extra player with no AI and no Commander
    /// whose units the mission script spawns and drives; it sits on its own team at the last start
    /// position so it never collides with a human's base.
    /// </summary>
    public static SurvivalRules SurvivalRules()
    {
        MethodInfo? build = SurvivalBuild();
        return new SurvivalRules
        {
            HumanCapacity = map => Math.Min(NetProtocol.MaxSlots, Math.Max(1, map.StartPositions.Count - 1)),
            Players = (humans, map) =>
            {
                var list = humans.ToList();
                list.Add(new PlayerSetup("Horde", 1, "crucible", Math.Max(0, map.StartPositions.Count - 1)));
                return list;
            },
            BuildJson = build is null ? null : (map, players, difficulty, seed) =>
                build.Invoke(null, new object[] { map, players, difficulty, seed }) as string ?? "",
            // The Horde owns no Commander: Annihilation means the humans lose when their last unit falls,
            // and the script decides when they have won.
            Options = res => new MatchOptions(res, res, WinCondition.Annihilation),
        };
    }
}
