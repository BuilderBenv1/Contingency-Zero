using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using ContingencyZero.Sim.Missions;
using ContingencyZero.Sim.Replay;
using Xunit;

namespace ContingencyZero.Sim.Tests;

public class MissionScriptTests
{
    private static World Run(string json, int seconds, AiDifficulty enemyAi = AiDifficulty.None, bool commanders = true)
    {
        var map = MapData.Flat("Test", 64, 64);
        map.StartPositions.Clear();
        map.StartPositions.Add(FixVec2.FromInts(60, 60));
        map.StartPositions.Add(FixVec2.FromInts(450, 450));
        var players = new[]
        {
            new PlayerSetup("Human", 0, "lattice", 0),
            new PlayerSetup("Enemy", 1, "crucible", 1, enemyAi),
        };
        var scenario = new Scenario(map, 5, players, new MatchOptions(SpawnCommanders: commanders), new List<Spawn>(), json);
        World world = scenario.CreateWorld(TestData.Catalog);
        for (int i = 0; i < seconds * World.TicksPerSecond; i++) world.Step();
        return world;
    }

    [Fact]
    public void A_completed_objective_is_not_reopened()
    {
        const string json = """
        {
          "title": "t", "briefing": "b", "humanPlayer": 0,
          "objectives": [ { "id": "goal", "text": "Do it" } ],
          "triggers": [
            { "id": "done", "when": { "type": "time", "seconds": 1 }, "actions": [ { "type": "objective", "id": "goal", "state": "complete" } ] },
            { "id": "reveal", "when": { "type": "time", "seconds": 2 }, "actions": [ { "type": "objective", "id": "goal", "state": "active" } ] }
          ]
        }
        """;
        World world = Run(json, 3);
        Assert.Equal(ObjectiveState.Complete, world.Mission!.Objectives.Single(o => o.Id == "goal").State);
    }

    [Fact]
    public void A_computer_player_without_a_commander_makes_its_home_among_its_mission_units()
    {
        // No Commanders: the enemy AI only has the base the mission gives it, far from the map centre.
        const string json = """
        {
          "title": "t", "briefing": "b", "humanPlayer": 0,
          "spawns": [
            { "type": "lattice_strider", "owner": 0, "x": 60, "y": 60 },
            { "type": "crucible_bot_lab", "owner": 1, "x": 440, "y": 440 },
            { "type": "crucible_solar_collector", "owner": 1, "x": 470, "y": 440 }
          ]
        }
        """;
        World world = Run(json, 2, AiDifficulty.Normal, commanders: false);
        var ai = world.Ais.Single(a => a.Player == 1);
        Assert.True(FixVec2.Distance(ai.Home, FixVec2.FromInts(440, 440)) < Fix64.FromInt(60),
            $"AI home is {ai.Home}, expected near its mission base");
    }
}
