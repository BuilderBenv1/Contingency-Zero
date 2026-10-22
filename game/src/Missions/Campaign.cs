using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Match;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using ContingencyZero.Sim.Replay;
using Godot;
using FileAccess = Godot.FileAccess;

namespace ContingencyZero.Game.Missions;

/// <summary>
/// One campaign mission, loaded from data/missions/*.json: which map, who plays, the briefing, and
/// the script the sim runs (objectives, triggers, reinforcements).
/// </summary>
public sealed class MissionInfo
{
    public required string Key { get; init; }
    public required string Campaign { get; init; }
    public required int Order { get; init; }
    public required string Title { get; init; }
    public required string Briefing { get; init; }
    public required string MapKey { get; init; }
    public required string ScriptJson { get; init; }
    public required List<PlayerSetup> Players { get; init; }
    public int HumanPlayer { get; init; }
    public int StartMetal { get; init; } = 1000;
    public int StartEnergy { get; init; } = 1000;
    public bool SpawnCommanders { get; init; } = true;

    public Scenario BuildScenario()
    {
        MapData map = GameData.Maps().First(m => m.Key == MapKey).Load();
        return new Scenario(map, (ulong)(Order * 7919 + 17), Players,
            new MatchOptions(StartMetal, StartEnergy, WinCondition.Commander, SpawnCommanders), new List<Spawn>(), ScriptJson);
    }

    public static List<MissionInfo> LoadAll()
    {
        var list = new List<MissionInfo>();
        foreach (string path in GameData.EmbeddedUnder("data/missions/"))
        {
            string json = GameData.Embedded(path)!;
            using var doc = JsonDocument.Parse(json);
            JsonElement r = doc.RootElement;
            var players = new List<PlayerSetup>();
            foreach (JsonElement p in r.GetProperty("players").EnumerateArray())
            {
                players.Add(new PlayerSetup(
                    p.GetProperty("name").GetString()!,
                    p.GetProperty("team").GetInt32(),
                    p.GetProperty("faction").GetString()!,
                    p.GetProperty("start").GetInt32(),
                    Enum.Parse<AiDifficulty>(p.TryGetProperty("ai", out var ai) ? ai.GetString()! : "None")));
            }
            list.Add(new MissionInfo
            {
                Key = System.IO.Path.GetFileNameWithoutExtension(path),
                Campaign = r.GetProperty("campaign").GetString()!,
                Order = r.GetProperty("order").GetInt32(),
                Title = r.GetProperty("title").GetString()!,
                Briefing = r.GetProperty("briefing").GetString()!,
                MapKey = r.GetProperty("map").GetString()!,
                ScriptJson = json,
                Players = players,
                HumanPlayer = r.TryGetProperty("humanPlayer", out var hp) ? hp.GetInt32() : 0,
                StartMetal = r.TryGetProperty("startMetal", out var sm) ? sm.GetInt32() : 1000,
                StartEnergy = r.TryGetProperty("startEnergy", out var se) ? se.GetInt32() : 1000,
                SpawnCommanders = !r.TryGetProperty("spawnCommanders", out var sc) || sc.GetBoolean(),
            });
        }
        return list.OrderBy(m => m.Campaign).ThenBy(m => m.Order).ToList();
    }
}

/// <summary>Remembers which missions have been won (user://campaign.json) and unlocks the next.</summary>
public static class CampaignProgress
{
    private const string FilePath = "user://campaign.json";
    private static HashSet<string>? _won;

    public static HashSet<string> Won
    {
        get
        {
            if (_won is not null) return _won;
            _won = new HashSet<string>();
            if (FileAccess.FileExists(FilePath))
            {
                try
                {
                    var keys = JsonSerializer.Deserialize<List<string>>(FileAccess.GetFileAsString(FilePath));
                    if (keys is not null) _won.UnionWith(keys);
                }
                catch (JsonException) { }
            }
            return _won;
        }
    }

    public static void MarkWon(string key)
    {
        Won.Add(key);
        using var f = FileAccess.Open(FilePath, FileAccess.ModeFlags.Write);
        f?.StoreString(JsonSerializer.Serialize(Won.OrderBy(k => k).ToList()));
    }

    public static bool Unlocked(MissionInfo m, List<MissionInfo> all)
    {
        if (m.Order <= 1) return true;
        var previous = all.FirstOrDefault(x => x.Campaign == m.Campaign && x.Order == m.Order - 1);
        return previous is null || Won.Contains(previous.Key);
    }

    /// <summary>Watches a mission match and records a win.</summary>
    public static void Track(MatchScene match, MissionInfo mission)
    {
        match.Session.Ticked += () =>
        {
            var w = match.Session.World;
            if (w.IsOver && w.WinningTeam == w.Players[mission.HumanPlayer].Team && !Won.Contains(mission.Key))
                MarkWon(mission.Key);
        };
    }
}
