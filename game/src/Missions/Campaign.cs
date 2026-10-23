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
    /// <summary>Every human seat ("humanPlayers"), or just <see cref="HumanPlayer"/>. Co-op missions have two.</summary>
    public IReadOnlyList<int> HumanPlayers { get; init; } = Array.Empty<int>();
    /// <summary>A mission written for several humans (the "coop" campaign).</summary>
    public bool IsCoop => Campaign == CoopCampaign || HumanPlayers.Count > 1;
    public const string CoopCampaign = "coop";
    /// <summary>Who fills a human seat nobody takes (co-op played alone, or by fewer people).</summary>
    public const AiDifficulty StandInAi = AiDifficulty.Hard;
    public int StartMetal { get; init; } = 1000;
    public int StartEnergy { get; init; } = 1000;
    public bool SpawnCommanders { get; init; } = true;

    /// <summary>The mission for one human (co-op: the other seats get allied computer players).</summary>
    public Scenario BuildScenario() => BuildScenario(1);

    /// <summary>
    /// The mission with its first <paramref name="humans"/> human seats left to people and any other
    /// human seats played by an allied <see cref="StandInAi"/>, so co-op works with fewer players.
    /// </summary>
    public Scenario BuildScenario(int humans)
    {
        MapData map = GameData.Maps().First(m => m.Key == MapKey).Load();
        var players = Players.ToList();
        IReadOnlyList<int> seats = HumanPlayers.Count > 0 ? HumanPlayers : new[] { HumanPlayer };
        for (int i = Math.Max(1, humans); i < seats.Count; i++)
        {
            int seat = seats[i];
            if (seat < 0 || seat >= players.Count) continue;
            players[seat] = players[seat] with { Name = $"{players[seat].Name} (AI)", Ai = StandInAi };
        }
        return new Scenario(map, (ulong)(Order * 7919 + 17), players,
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
                HumanPlayers = r.TryGetProperty("humanPlayers", out var hps) && hps.ValueKind == JsonValueKind.Array
                    ? hps.EnumerateArray().Select(e => e.GetInt32()).ToList()
                    : new List<int> { r.TryGetProperty("humanPlayer", out var hp1) ? hp1.GetInt32() : 0 },
                StartMetal = r.TryGetProperty("startMetal", out var sm) ? sm.GetInt32() : 1000,
                StartEnergy = r.TryGetProperty("startEnergy", out var se) ? se.GetInt32() : 1000,
                SpawnCommanders = !r.TryGetProperty("spawnCommanders", out var sc) || sc.GetBoolean(),
            });
        }
        return list.OrderBy(m => m.Campaign).ThenBy(m => m.Order).ToList();
    }
}

/// <summary>
/// Remembers which missions have been won and unlocks the next. Single-player wins live in
/// user://campaign.json; co-op wins in user://campaign_coop.json, split by how they were won: with
/// another person ("together") or alone with an allied computer player ("solo").
/// </summary>
public static class CampaignProgress
{
    private const string FilePath = "user://campaign.json";
    private const string CoopPath = "user://campaign_coop.json";
    private static HashSet<string>? _won;
    private static CoopRecord? _coop;

    private sealed class CoopRecord
    {
        public List<string> Together { get; set; } = new();
        public List<string> Solo { get; set; } = new();
    }

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

    private static CoopRecord Coop
    {
        get
        {
            if (_coop is not null) return _coop;
            _coop = new CoopRecord();
            if (FileAccess.FileExists(CoopPath))
            {
                try
                {
                    _coop = JsonSerializer.Deserialize<CoopRecord>(FileAccess.GetFileAsString(CoopPath)) ?? new CoopRecord();
                }
                catch (JsonException) { }
            }
            return _coop;
        }
    }

    /// <summary>Co-op mission won with another person.</summary>
    public static bool WonTogether(string key) => Coop.Together.Contains(key);
    /// <summary>Co-op mission won alone, with a computer ally.</summary>
    public static bool WonSolo(string key) => Coop.Solo.Contains(key);
    /// <summary>Won at all, in whichever list the mission belongs to.</summary>
    public static bool IsWon(MissionInfo m) => m.IsCoop ? WonTogether(m.Key) || WonSolo(m.Key) : Won.Contains(m.Key);

    public static void MarkWon(string key)
    {
        Won.Add(key);
        using var f = FileAccess.Open(FilePath, FileAccess.ModeFlags.Write);
        f?.StoreString(JsonSerializer.Serialize(Won.OrderBy(k => k).ToList()));
    }

    /// <summary>Records a co-op win. <paramref name="together"/>: more than one person played.</summary>
    public static void MarkCoopWon(string key, bool together)
    {
        List<string> list = together ? Coop.Together : Coop.Solo;
        if (list.Contains(key)) return;
        list.Add(key);
        list.Sort(StringComparer.Ordinal);
        using var f = FileAccess.Open(CoopPath, FileAccess.ModeFlags.Write);
        f?.StoreString(JsonSerializer.Serialize(Coop));
    }

    public static bool Unlocked(MissionInfo m, List<MissionInfo> all)
    {
        if (m.Order <= 1) return true;
        var previous = all.FirstOrDefault(x => x.Campaign == m.Campaign && x.Order == m.Order - 1);
        return previous is null || IsWon(previous);
    }

    /// <summary>Watches a mission match and records a win (co-op missions in the co-op record, as a solo win).</summary>
    public static void Track(MatchScene match, MissionInfo mission) => Track(match, mission, together: false);

    /// <summary>Watches a mission match (single player, or a networked co-op match) and records a win once.</summary>
    public static void Track(MatchScene match, MissionInfo mission, bool together)
    {
        bool done = false;
        match.Session.Ticked += () =>
        {
            if (done) return;
            var w = match.Session.World;
            if (!w.IsOver) return;
            done = true;
            if (w.WinningTeam != w.Players[mission.HumanPlayer].Team) return;
            if (mission.IsCoop) MarkCoopWon(mission.Key, together);
            else if (!Won.Contains(mission.Key)) MarkWon(mission.Key);
        };
    }
}
