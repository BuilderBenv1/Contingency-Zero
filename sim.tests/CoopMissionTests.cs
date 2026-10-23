using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using ContingencyZero.Sim.Missions;
using ContingencyZero.Sim.Replay;
using ContingencyZero.Sim.State;
using Xunit;

namespace ContingencyZero.Sim.Tests;

/// <summary>Missions with several human players: parsing, shared objectives, and when co-op is won or lost.</summary>
public class CoopMissionTests
{
    private static MapData FourCorners()
    {
        var map = MapData.Flat("Test", 64, 64);
        map.StartPositions.Clear();
        map.StartPositions.Add(FixVec2.FromInts(60, 60));
        map.StartPositions.Add(FixVec2.FromInts(450, 60));
        map.StartPositions.Add(FixVec2.FromInts(60, 450));
        map.StartPositions.Add(FixVec2.FromInts(450, 450));
        return map;
    }

    /// <summary>Two humans on team 0 (players 0 and 1), an enemy with no Commander on team 1.</summary>
    private static World Coop(string json)
    {
        var players = new[]
        {
            new PlayerSetup("West", 0, "lattice", 0),
            new PlayerSetup("East", 0, "crucible", 1),
            new PlayerSetup("Enemy", 1, "crucible", 3),
        };
        var scenario = new Scenario(FourCorners(), 9, players, new MatchOptions(SpawnCommanders: false), new List<Spawn>(), json);
        return scenario.CreateWorld(TestData.Catalog);
    }

    private const string Commanders = """
        "spawns": [
          { "tag": "cmdrWest", "type": "lattice_commander", "owner": 0, "x": 60, "y": 60 },
          { "tag": "cmdrEast", "type": "crucible_commander", "owner": 1, "x": 450, "y": 60 },
          { "tag": "camp", "type": "crucible_light_turret", "owner": 2, "x": 452, "y": 452 }
        ]
        """;

    private static void Run(World world, int seconds)
    {
        for (int i = 0; i < seconds * World.TicksPerSecond && !world.IsOver; i++) world.Step();
    }

    private static int Tagged(World world, string tag) => world.Mission!.UnitsTagged(tag).Single();

    [Fact]
    public void Human_players_list_is_parsed_and_the_first_is_the_human_player()
    {
        MissionScript coop = MissionScript.Parse("""{ "title": "t", "humanPlayer": 1, "humanPlayers": [1, 0] }""");
        Assert.Equal(new[] { 1, 0 }, coop.HumanPlayers);
        Assert.Equal(1, coop.HumanPlayer);

        MissionScript single = MissionScript.Parse("""{ "title": "t", "humanPlayer": 2 }""");
        Assert.Equal(new[] { 2 }, single.HumanPlayers);
        Assert.Equal(2, single.HumanPlayer);

        MissionScript none = MissionScript.Parse("""{ "title": "t" }""");
        Assert.Equal(new[] { 0 }, none.HumanPlayers);
    }

    [Fact]
    public void Co_op_plays_on_while_one_commander_stands_and_is_lost_when_both_fall()
    {
        World world = Coop("{ \"title\": \"t\", \"humanPlayers\": [0, 1], " + Commanders + ", \"objectives\": [ { \"id\": \"o\", \"text\": \"x\" } ] }");
        Run(world, 2);
        world.Kill(Tagged(world, "cmdrWest"));
        Run(world, 3);
        Assert.True(world.Players[0].Defeated);
        Assert.False(world.IsOver, "one co-op player falling must not end the mission");

        world.Kill(Tagged(world, "cmdrEast"));
        Run(world, 3);
        Assert.True(world.IsOver);
        Assert.Equal(1, world.WinningTeam);
    }

    [Fact]
    public void A_mission_can_make_either_commander_falling_a_defeat()
    {
        // A per-mission rule: "any Commander lost" is a defeat.
        World world = Coop("{ \"title\": \"t\", \"humanPlayers\": [0, 1], " + Commanders + """
            , "triggers": [ { "id": "lost", "when": { "type": "any", "of": [ { "type": "defeated", "player": 0 }, { "type": "defeated", "player": 1 } ] },
                              "actions": [ { "type": "defeat" } ] } ] }
            """);
        Run(world, 2);
        world.Kill(Tagged(world, "cmdrEast"));
        Run(world, 3);
        Assert.True(world.IsOver);
        Assert.Equal(1, world.WinningTeam);
    }

    [Fact]
    public void Shared_objectives_count_both_players_and_victory_goes_to_their_team()
    {
        // "team" counts units of either human; the objective needs one turret from each of them.
        World world = Coop("{ \"title\": \"t\", \"humanPlayers\": [0, 1], " + Commanders + """
            , "objectives": [ { "id": "towers", "text": "Two turrets between you" } ],
              "triggers": [
                { "id": "west", "when": { "type": "time", "seconds": 1 }, "actions": [ { "type": "give", "player": 0, "metal": 0 } ] },
                { "id": "towers", "when": { "type": "count", "team": 0, "role": "light_turret", "atLeast": 2 }, "actions": [ { "type": "objective", "id": "towers", "state": "complete" } ] },
                { "id": "win", "when": { "type": "objectivesDone" }, "actions": [ { "type": "victory" } ] }
              ] }
            """);
        Run(world, 2);
        world.SpawnUnit("lattice_light_turret", 0, FixVec2.FromInts(100, 100));
        Run(world, 2);
        Assert.False(world.IsOver, "one turret is not two");
        world.SpawnUnit("crucible_light_turret", 1, FixVec2.FromInts(400, 100));
        Run(world, 3);
        Assert.True(world.IsOver);
        Assert.Equal(0, world.WinningTeam);
        Assert.Equal(ObjectiveState.Complete, world.Mission!.Objectives.Single().State);
    }

    [Fact]
    public void Bounty_pays_every_human()
    {
        World world = Coop("{ \"title\": \"t\", \"humanPlayers\": [0, 1], " + Commanders + """
            , "triggers": [ { "id": "pay", "when": { "type": "time", "seconds": 1 }, "actions": [ { "type": "bounty", "metal": 200 } ] } ] }
            """);
        // Storage starts full, so empty it first to see the payment.
        foreach (var p in world.Players) p.Metal = Fix64.Zero;
        Run(world, 2);
        Assert.True(world.Players[0].Metal >= Fix64.FromInt(200));
        Assert.True(world.Players[1].Metal >= Fix64.FromInt(200));
        Assert.True(world.Players[2].Metal < Fix64.FromInt(200), "the enemy gets no bounty");
    }

    [Fact]
    public void Hunt_sends_idle_units_to_the_nearest_enemy()
    {
        World world = Coop("{ \"title\": \"t\", \"humanPlayers\": [0, 1], " + Commanders + """
            , "triggers": [ { "id": "hunt", "every": 1, "actions": [ { "type": "hunt", "player": 2 } ] } ] }
            """);
        int bot = world.SpawnUnit("crucible_assault_bot", 2, FixVec2.FromInts(400, 200));
        Run(world, 2);
        Order o = world.Units.CurrentOrder(bot);
        Assert.Equal(OrderType.Fight, o.Type);
        // East's Commander (450, 60) is nearer than West's (60, 60).
        Assert.Equal(FixVec2.FromInts(450, 60), o.Position);
    }
}
