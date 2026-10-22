using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using ContingencyZero.Sim.Missions;
using ContingencyZero.Sim.Replay;
using Xunit;
using Xunit.Abstractions;

namespace ContingencyZero.Sim.Tests;

/// <summary>
/// Checks every campaign mission in data/missions: the file says what the game and the mission
/// script expect, everything it names exists, its starting units sit on ground they can use, and
/// the match it builds runs without trouble.
/// </summary>
public class MissionDataTests
{
    private readonly ITestOutputHelper _out;
    public MissionDataTests(ITestOutputHelper output) => _out = output;

    // Mission script vocabulary, as sim/Missions/MissionScript.cs reads it.
    private static readonly HashSet<string> Conditions = new()
        { "time", "destroyed", "count", "noStructures", "area", "fired", "metal", "objectivesDone", "all", "any" };
    private static readonly HashSet<string> Actions = new()
        { "message", "objective", "spawn", "attack", "reveal", "give", "victory", "defeat" };
    private static readonly HashSet<string> ObjectiveStates = new() { "active", "complete", "failed", "hidden" };

    /// <summary>
    /// One mission file, read the way game/src/Missions/Campaign.cs reads it (the sim tests can't
    /// reference the Godot project, so this mirrors MissionInfo).
    /// </summary>
    private sealed record MissionFile(
        string Key, string Json, string Campaign, int Order, string Title, string Briefing, string MapKey,
        List<PlayerSetup> Players, int HumanPlayer, int StartMetal, int StartEnergy, bool SpawnCommanders)
    {
        public MatchOptions Options => new(StartMetal, StartEnergy, WinCondition.Commander, SpawnCommanders);

        /// <summary>The same scenario MissionInfo.BuildScenario makes.</summary>
        public Scenario BuildScenario() =>
            new(TestData.LoadMap(MapKey), (ulong)(Order * 7919 + 17), Players, Options, new List<Spawn>(), Json);
    }

    private static string MissionsDirectory() => Path.Combine(TestData.DataDirectory(), "missions");

    private static List<MissionFile> LoadAll()
    {
        var list = new List<MissionFile>();
        foreach (string path in Directory.GetFiles(MissionsDirectory(), "*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            string json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            JsonElement r = doc.RootElement;
            var players = new List<PlayerSetup>();
            foreach (JsonElement p in r.GetProperty("players").EnumerateArray())
                players.Add(new PlayerSetup(
                    p.GetProperty("name").GetString()!,
                    p.GetProperty("team").GetInt32(),
                    p.GetProperty("faction").GetString()!,
                    p.GetProperty("start").GetInt32(),
                    Enum.Parse<AiDifficulty>(p.TryGetProperty("ai", out var ai) ? ai.GetString()! : "None")));
            list.Add(new MissionFile(
                Path.GetFileNameWithoutExtension(path),
                json,
                r.GetProperty("campaign").GetString()!,
                r.GetProperty("order").GetInt32(),
                r.GetProperty("title").GetString()!,
                r.GetProperty("briefing").GetString()!,
                r.GetProperty("map").GetString()!,
                players,
                r.TryGetProperty("humanPlayer", out var hp) ? hp.GetInt32() : 0,
                r.TryGetProperty("startMetal", out var sm) ? sm.GetInt32() : 1000,
                r.TryGetProperty("startEnergy", out var se) ? se.GetInt32() : 1000,
                !r.TryGetProperty("spawnCommanders", out var sc) || sc.GetBoolean()));
        }
        return list;
    }

    private static FixVec2 Pos(JsonElement o) =>
        new(Fix64.Parse(o.GetProperty("x").GetRawText()), Fix64.Parse(o.GetProperty("y").GetRawText()));

    [Fact]
    public void Both_campaigns_have_their_missions_in_order()
    {
        var missions = LoadAll();
        foreach (string campaign in new[] { "lattice", "crucible" })
        {
            var orders = missions.Where(m => m.Campaign == campaign).Select(m => m.Order).OrderBy(o => o).ToList();
            Assert.True(orders.Count >= 5, $"{campaign} has {orders.Count} missions");
            Assert.Equal(Enumerable.Range(1, orders.Count).ToList(), orders);
        }
        foreach (MissionFile m in missions)
        {
            Assert.True(m.Title.Length > 0, $"{m.Key} has no title");
            Assert.True(m.Briefing.Length > 0, $"{m.Key} has no briefing");
            Assert.False(m.Briefing.Contains('[') || m.Briefing.Contains(']'), $"{m.Key}: the briefing is shown as BBCode, so no square brackets");
        }
    }

    [Fact]
    public void Every_mission_names_only_things_that_exist()
    {
        UnitCatalog catalog = TestData.Catalog;
        foreach (MissionFile m in LoadAll())
        {
            string where = m.Key;
            MapData map = TestData.LoadMap(m.MapKey);
            MissionScript script = MissionScript.Parse(m.Json);

            // Players: a human slot, someone to fight, real factions and start positions.
            Assert.InRange(m.HumanPlayer, 0, m.Players.Count - 1);
            Assert.Equal(m.HumanPlayer, script.HumanPlayer);
            Assert.True(m.Players[m.HumanPlayer].Ai == AiDifficulty.None, $"{where}: the human's slot must have ai None");
            Assert.True(m.Players.Any(p => p.Team != m.Players[m.HumanPlayer].Team), $"{where}: nobody to fight");
            foreach (PlayerSetup p in m.Players)
            {
                Assert.True(p.Faction is "lattice" or "crucible", $"{where}: unknown faction {p.Faction}");
                Assert.True(p.StartIndex >= 0 && p.StartIndex < map.StartPositions.Count, $"{where}: {p.Name} has no start {p.StartIndex}");
            }
            Assert.Equal(m.Players.Count, m.Players.Select(p => p.StartIndex).Distinct().Count());

            using var doc = JsonDocument.Parse(m.Json);
            JsonElement root = doc.RootElement;
            var objectives = script.Objectives.Select(o => o.Id).ToHashSet();
            Assert.Equal(script.Objectives.Count, objectives.Count);
            Assert.True(script.Objectives.Any(o => !o.Optional), $"{where}: no required objective");

            var triggerIds = new HashSet<string>();
            var spawnedTags = new HashSet<string>();
            if (root.TryGetProperty("spawns", out var spawns))
                foreach (JsonElement s in spawns.EnumerateArray())
                {
                    CheckUnit(where, s.GetProperty("type").GetString()!, s.GetProperty("owner").GetInt32());
                    if (s.TryGetProperty("tag", out var tag) && tag.GetString() is { Length: > 0 } t) spawnedTags.Add(t);
                }
            foreach (JsonElement t in root.GetProperty("triggers").EnumerateArray())
            {
                Assert.True(triggerIds.Add(t.GetProperty("id").GetString()!), $"{where}: two triggers called {t.GetProperty("id")}");
                foreach (JsonElement a in t.GetProperty("actions").EnumerateArray())
                    if (a.GetProperty("type").GetString() == "spawn")
                        spawnedTags.Add(a.GetProperty("tag").GetString()!);
            }

            foreach (JsonElement t in root.GetProperty("triggers").EnumerateArray())
            {
                string id = $"{where}/{t.GetProperty("id").GetString()}";
                CheckCondition(id, t.GetProperty("when"));
                foreach (JsonElement a in t.GetProperty("actions").EnumerateArray())
                {
                    string kind = a.GetProperty("type").GetString()!;
                    Assert.True(Actions.Contains(kind), $"{id}: unknown action '{kind}'");
                    switch (kind)
                    {
                        case "message":
                            Assert.InRange(a.GetProperty("message").GetInt32(), 0, script.Messages.Count - 1);
                            break;
                        case "objective":
                            Assert.True(objectives.Contains(a.GetProperty("id").GetString()!), $"{id}: no objective {a.GetProperty("id")}");
                            if (a.TryGetProperty("state", out var st))
                                Assert.True(ObjectiveStates.Contains(st.GetString()!), $"{id}: unknown objective state {st}");
                            break;
                        case "spawn":
                            foreach (JsonElement u in a.GetProperty("units").EnumerateArray())
                                CheckUnit(id, u.GetProperty("unit").GetString()!, u.GetProperty("owner").GetInt32());
                            break;
                        case "attack":
                            if (a.TryGetProperty("tag", out var at))
                                Assert.True(spawnedTags.Contains(at.GetString()!), $"{id}: attack with tag '{at}' that nothing spawns");
                            break;
                        case "give":
                            Assert.InRange(a.GetProperty("player").GetInt32(), 0, m.Players.Count - 1);
                            break;
                    }
                }
            }

            // A mission has to be winnable by its script: something must declare victory.
            Assert.True(root.GetProperty("triggers").EnumerateArray()
                .SelectMany(t => t.GetProperty("actions").EnumerateArray())
                .Any(a => a.GetProperty("type").GetString() == "victory"), $"{where}: no trigger declares victory");

            void CheckUnit(string context, string type, int owner)
            {
                Assert.True(catalog.TryIndexOf(type, out int index), $"{context}: unknown unit '{type}'");
                Assert.InRange(owner, 0, m.Players.Count - 1);
                Assert.Equal(m.Players[owner].Faction, catalog[index].Faction);
            }

            void CheckCondition(string context, JsonElement c)
            {
                string kind = c.GetProperty("type").GetString()!;
                Assert.True(Conditions.Contains(kind), $"{context}: unknown condition '{kind}'");
                switch (kind)
                {
                    case "destroyed":
                        Assert.True(spawnedTags.Contains(c.GetProperty("tag").GetString()!), $"{context}: nothing is tagged '{c.GetProperty("tag")}'");
                        break;
                    case "fired":
                        Assert.True(triggerIds.Contains(c.GetProperty("trigger").GetString()!), $"{context}: no trigger '{c.GetProperty("trigger")}'");
                        break;
                    case "count":
                        if (c.TryGetProperty("unit", out var unit))
                            Assert.True(catalog.TryIndexOf(unit.GetString()!, out _), $"{context}: unknown unit '{unit}'");
                        if (c.TryGetProperty("role", out var role))
                            Assert.True(catalog.All.Any(d => d.Role == role.GetString()), $"{context}: no unit has role '{role}'");
                        Assert.InRange(c.GetProperty("player").GetInt32(), 0, m.Players.Count - 1);
                        break;
                    case "noStructures":
                    case "area":
                    case "metal":
                        Assert.InRange(c.GetProperty("player").GetInt32(), 0, m.Players.Count - 1);
                        break;
                    case "all":
                    case "any":
                        foreach (JsonElement sub in c.GetProperty("of").EnumerateArray()) CheckCondition(context, sub);
                        break;
                }
            }
        }
    }

    [Fact]
    public void Mission_units_start_on_ground_they_can_use()
    {
        UnitCatalog catalog = TestData.Catalog;
        foreach (MissionFile m in LoadAll())
        {
            // A world without the script, so each structure can be checked before it claims its tiles.
            var world = new World(catalog, TestData.LoadMap(m.MapKey), 1, m.Players, m.Options);
            using var doc = JsonDocument.Parse(m.Json);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("spawns", out var spawns))
                foreach (JsonElement s in spawns.EnumerateArray())
                {
                    int type = catalog.IndexOf(s.GetProperty("type").GetString()!);
                    int owner = s.GetProperty("owner").GetInt32();
                    FixVec2 pos = Pos(s);
                    UnitDef def = catalog[type];
                    if (def.IsStructure)
                    {
                        PlacementResult placed = world.CanPlace(type, owner, pos);
                        Assert.True(placed == PlacementResult.Ok, $"{m.Key}: {def.Id} at {pos} can't go there ({placed})");
                        world.SpawnUnit(type, owner, pos);
                    }
                    else
                    {
                        Assert.True(world.Terrain.Passable(def.MoveClass, pos), $"{m.Key}: {def.Id} starts stuck at {pos}");
                    }
                }

            // Reinforcements and waves: every unit of every group lands somewhere it can move.
            foreach (JsonElement t in root.GetProperty("triggers").EnumerateArray())
                foreach (JsonElement a in t.GetProperty("actions").EnumerateArray())
                {
                    if (a.GetProperty("type").GetString() != "spawn") continue;
                    foreach (JsonElement u in a.GetProperty("units").EnumerateArray())
                    {
                        UnitDef def = catalog[catalog.IndexOf(u.GetProperty("unit").GetString()!)];
                        Assert.False(def.IsStructure, $"{m.Key}/{t.GetProperty("id")}: spawn actions are for mobile units");
                        int count = u.TryGetProperty("count", out var n) ? n.GetInt32() : 1;
                        FixVec2 at = Pos(u);
                        for (int i = 0; i < count; i++)
                        {
                            // MissionScript lays groups out in rows of five, five units apart.
                            FixVec2 p = at + FixVec2.FromInts((i % 5) * 5 - 10, (i / 5) * 5);
                            Assert.True(world.Terrain.Passable(def.MoveClass, p), $"{m.Key}/{t.GetProperty("id")}: {def.Id} #{i} lands stuck at {p}");
                        }
                    }
                }
        }
    }

    [Fact]
    public void Every_mission_plays_three_minutes()
    {
        const int ticks = 3 * 60 * World.TicksPerSecond;
        foreach (MissionFile m in LoadAll())
        {
            World world = m.BuildScenario().CreateWorld(TestData.Catalog);
            Assert.True(world.Mission is not null, $"{m.Key}: no mission script attached");
            for (int i = 0; i < ticks && !world.IsOver; i++) world.Step();

            int units = 0;
            for (int id = 0; id < world.Units.Count; id++)
                if (world.Units.Alive[id] && world.Units.Owner[id] == m.HumanPlayer) units++;
            MissionScript mission = world.Mission!;
            _out.WriteLine($"{m.Key}: {units} units after 3 min, {mission.MessageLog.Count} messages, " +
                           string.Join(" ", mission.Objectives.Select(o => $"{o.Id}={o.State}")));
            Assert.False(world.IsOver, $"{m.Key} ended on its own within three minutes");
            Assert.False(world.Players[m.HumanPlayer].Defeated, $"{m.Key}: the player was defeated without doing anything");
            Assert.True(units > 0, $"{m.Key}: the player has no units");
            Assert.NotEmpty(mission.MessageLog);
        }
    }

    [Fact]
    public void A_mission_replays_identically()
    {
        // Missions run inside the sim, so the same mission and seed must give the same world.
        MissionFile m = LoadAll().First(x => x.Campaign == "lattice" && x.Order == 1);
        ulong Run()
        {
            World world = m.BuildScenario().CreateWorld(TestData.Catalog);
            for (int i = 0; i < 4 * 60 * World.TicksPerSecond; i++) world.Step();
            return world.ComputeHash();
        }
        Assert.Equal(Run(), Run());
    }
}
