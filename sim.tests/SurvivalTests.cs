using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using ContingencyZero.Sim.Missions;
using ContingencyZero.Sim.Replay;
using ContingencyZero.Sim.State;
using Xunit;

namespace ContingencyZero.Sim.Tests;

/// <summary>
/// Survival mode: the sim builds the wave script from the map, the defenders and a seed, then runs
/// it like any mission. Same inputs, same waves; replays reproduce; waves grow and change over time.
/// </summary>
public class SurvivalTests
{
    private static MapData Map(string key = "crater_basin") => TestData.LoadMap(key);

    private static List<PlayerSetup> Defenders(int count, AiDifficulty ai = AiDifficulty.None) =>
        Enumerable.Range(0, count).Select(i => new PlayerSetup($"P{i}", 0, i % 2 == 0 ? "lattice" : "crucible", i, ai)).ToList();

    private static Scenario Scenario(int defenders = 1, AiDifficulty difficulty = AiDifficulty.Normal, int seed = 7, string map = "crater_basin") =>
        SurvivalMode.CreateScenario(Map(map), Defenders(defenders), difficulty, seed);

    /// <summary>Every wave spawn: (wave trigger id, unit, count) at the tick it fires, from a world's events.</summary>
    private static List<(int Wave, string Unit, int Owner)> Spawns(World world, int fromId)
    {
        var list = new List<(int, string, int)>();
        for (int id = fromId; id < world.Units.Count; id++)
            list.Add((world.Mission!.Wave, world.Catalog[world.Units.TypeIndex[id]].Id, world.Units.Owner[id]));
        return list;
    }

    [Fact]
    public void Same_inputs_build_the_same_script()
    {
        Assert.Equal(Scenario(seed: 11).MissionJson, Scenario(seed: 11).MissionJson);
        Assert.Equal(Scenario(2, AiDifficulty.Hard, 3).MissionJson, Scenario(2, AiDifficulty.Hard, 3).MissionJson);
    }

    [Fact]
    public void The_scenario_is_a_survival_mission_for_every_defender()
    {
        Scenario s = Scenario(2);
        MissionScript script = MissionScript.Parse(s.MissionJson);
        Assert.Equal(SurvivalMode.ModeKey, script.Mode);
        Assert.Equal("Normal", script.Difficulty);
        Assert.Equal(new[] { 0, 1 }, script.HumanPlayers);
        Assert.Equal(3, s.Players.Count);
        Assert.Equal(SurvivalMode.HordeName, s.Players[2].Name);
        Assert.NotEqual(s.Players[0].Team, s.Players[2].Team);
        Assert.False(s.Options.SpawnCommanders);

        World w = s.CreateWorld(TestData.Catalog);
        int Commanders(int p) => Enumerable.Range(0, w.Units.Count).Count(id => w.Units.Owner[id] == p && w.Catalog[w.Units.TypeIndex[id]].IsCommander);
        Assert.Equal(1, Commanders(0));
        Assert.Equal(1, Commanders(1));
        Assert.Equal(0, Commanders(2));
    }

    [Fact]
    public void Same_seed_gives_the_same_waves_and_state()
    {
        // Through the first two waves on Normal (75 s apart).
        const int ticks = 160 * World.TicksPerSecond;
        World a = Scenario(seed: 5).CreateWorld(TestData.Catalog);
        World b = Scenario(seed: 5).CreateWorld(TestData.Catalog);
        int start = a.Units.Count;
        for (int i = 0; i < ticks; i++)
        {
            a.Step();
            b.Step();
            if (i % World.TicksPerSecond == 0) Assert.Equal(a.ComputeHash(), b.ComputeHash());
        }
        Assert.Equal(2, a.Mission!.Wave);
        Assert.Equal(Spawns(a, start), Spawns(b, start));
        Assert.True(a.Units.Count - start > 5, "the waves spawned units");
    }

    [Fact]
    public void A_survival_replay_reproduces_the_match()
    {
        Scenario s = Scenario(seed: 9);
        var replay = new MatchReplay(s, new List<Command>(), 100 * World.TicksPerSecond);
        World first = replay.Play(TestData.Catalog);

        using var stream = new MemoryStream();
        replay.Write(stream);
        stream.Position = 0;
        MatchReplay loaded = MatchReplay.Read(stream);
        World again = loaded.Play(TestData.Catalog);
        Assert.Equal(first.ComputeHash(), again.ComputeHash());
        Assert.Equal(1, again.Mission!.Wave);
    }

    [Fact]
    public void Waves_get_bigger_and_bring_new_units_over_time()
    {
        // Read the wave sizes straight from the script at different times.
        Scenario s = Scenario(difficulty: AiDifficulty.Normal);
        using var doc = JsonDocument.Parse(s.MissionJson);
        JsonElement lane = doc.RootElement.GetProperty("triggers").EnumerateArray().First(t => t.GetProperty("id").GetString() == "lane0");
        JsonElement units = lane.GetProperty("actions").EnumerateArray().First(a => a.GetProperty("type").GetString() == "spawn").GetProperty("units");

        Dictionary<string, int> At(int minutes) => units.EnumerateArray()
            .GroupBy(u => u.GetProperty("unit").GetString()!)
            .ToDictionary(g => g.Key, g => g.Sum(u => MissionScript.SpawnCount(u, minutes * 60 * World.TicksPerSecond)));
        int Total(Dictionary<string, int> d) => d.Values.Sum();

        var early = At(1);
        var mid = At(10);
        var late = At(30);
        Assert.True(Total(early) < Total(mid) && Total(mid) < Total(late), $"wave sizes {Total(early)}, {Total(mid)}, {Total(late)}");
        Assert.Equal(0, early.GetValueOrDefault("crucible_gunship"));
        Assert.True(mid.GetValueOrDefault("crucible_artillery_vehicle") > 0, "artillery by ten minutes");
        Assert.True(late.GetValueOrDefault("crucible_gunship") > 0 && late.GetValueOrDefault("crucible_bomber") > 0, "air by thirty minutes");
        Assert.True(late.GetValueOrDefault("crucible_experimental") > 0, "experimentals by thirty minutes");
    }

    [Fact]
    public void Harder_and_more_crowded_games_get_bigger_waves()
    {
        static int FirstWave(Scenario s)
        {
            using var doc = JsonDocument.Parse(s.MissionJson);
            JsonElement lane = doc.RootElement.GetProperty("triggers").EnumerateArray().First(t => t.GetProperty("id").GetString() == "lane0");
            JsonElement units = lane.GetProperty("actions").EnumerateArray().First(a => a.GetProperty("type").GetString() == "spawn").GetProperty("units");
            int at = lane.GetProperty("after").GetInt32() * World.TicksPerSecond;
            return units.EnumerateArray().Sum(u => MissionScript.SpawnCount(u, at + 5 * 60 * World.TicksPerSecond));
        }
        Assert.True(FirstWave(Scenario(1, AiDifficulty.Easy)) < FirstWave(Scenario(1, AiDifficulty.Hard)));
        Assert.True(FirstWave(Scenario(1, AiDifficulty.Normal)) < FirstWave(Scenario(2, AiDifficulty.Normal)));
        Assert.True(FirstWave(Scenario(2, AiDifficulty.Normal)) < FirstWave(Scenario(4, AiDifficulty.Normal)));
        Assert.True(SurvivalMode.WaveSeconds(AiDifficulty.Hard) < SurvivalMode.WaveSeconds(AiDifficulty.Easy));
    }

    [Fact]
    public void Waves_attack_the_defenders()
    {
        World w = Scenario(seed: 3).CreateWorld(TestData.Catalog);
        int horde = w.Players.Length - 1;
        int start = w.Units.Count;
        for (int i = 0; i < 80 * World.TicksPerSecond; i++) w.Step();
        var wave = Enumerable.Range(start, w.Units.Count - start).Where(id => w.Units.Alive[id] && w.Units.Owner[id] == horde).ToList();
        Assert.NotEmpty(wave);
        foreach (int id in wave) Assert.Equal(OrderType.Fight, w.Units.CurrentOrder(id).Type);
        Assert.Equal("Wave 1 incoming", w.Mission!.Messages[w.Mission.MessageLog[^1].Message][..15]);
    }

    [Fact]
    public void Every_four_start_map_takes_four_defenders_and_every_map_one()
    {
        foreach (string file in Directory.GetFiles(Path.Combine(TestData.DataDirectory(), "maps"), "*.json"))
        {
            string key = Path.GetFileNameWithoutExtension(file);
            MapData map = Map(key);
            for (int n = 1; n <= SurvivalMode.Capacity(map); n++)
            {
                Scenario s = SurvivalMode.CreateScenario(map, Defenders(n), AiDifficulty.Normal, 1);
                MissionScript script = MissionScript.Parse(s.MissionJson);
                Assert.Equal(n, script.HumanPlayers.Count);
                // A world builds and the first wave arrives without trouble.
                World w = s.CreateWorld(TestData.Catalog);
                for (int i = 0; i < 80 * World.TicksPerSecond; i++) w.Step();
                Assert.True(w.Mission!.Wave >= 1, $"{key} with {n}: no wave");
                Assert.False(w.IsOver, $"{key} with {n}: over already");
            }
        }
    }

    [Fact]
    public void The_score_counts_waves_beaten_and_time_held()
    {
        World w = Scenario(1, AiDifficulty.Hard, 4).CreateWorld(TestData.Catalog);
        for (int i = 0; i < 70 * World.TicksPerSecond; i++) w.Step();
        var live = SurvivalMode.Score(w)!.Value;
        Assert.Equal(1, live.Waves);
        Assert.Equal(70, live.Seconds);
        Assert.Equal((100 + 70) * 2, live.Points);

        // Lose: the wave that overran you doesn't count, and the clock stops when the last Commander fell.
        foreach (int id in Enumerable.Range(0, w.Units.Count).Where(id => w.Units.Alive[id] && w.Catalog[w.Units.TypeIndex[id]].IsCommander).ToList())
            w.Kill(id);
        for (int i = 0; i < 5 * World.TicksPerSecond && !w.IsOver; i++) w.Step();
        Assert.True(w.IsOver);
        var final = SurvivalMode.Score(w)!.Value;
        Assert.Equal(0, final.Waves);
        Assert.Equal(70, final.Seconds);
        Assert.Null(SurvivalMode.Score(TestData.Flat()));
    }

    [Fact]
    public void The_next_wave_countdown_ignores_the_hunt_trigger()
    {
        World w = Scenario(1, AiDifficulty.Normal).CreateWorld(TestData.Catalog);
        for (int i = 0; i < 20 * World.TicksPerSecond; i++) w.Step();
        Assert.Equal(SurvivalMode.WaveSeconds(AiDifficulty.Normal) * World.TicksPerSecond, w.Mission!.NextPeriodicTick);
    }
}
