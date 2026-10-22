using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using ContingencyZero.Sim.Missions;

namespace ContingencyZero.Sim.Replay;

/// <summary>Everything needed to start a match: the map, the players, options and any extra starting units.</summary>
public sealed record Scenario(
    MapData Map,
    ulong Seed,
    IReadOnlyList<PlayerSetup> Players,
    MatchOptions Options,
    IReadOnlyList<Spawn> Spawns,
    string MissionJson = "")
{
    public World CreateWorld(UnitCatalog catalog)
    {
        MissionScript? mission = MissionJson.Length > 0 ? MissionScript.Parse(MissionJson) : null;
        var world = new World(catalog, Map, Seed, Players, Options, mission);
        foreach (Spawn s in Spawns)
            world.SpawnUnit(s.TypeId, s.Owner, s.Position);
        return world;
    }
}

/// <summary>
/// A recorded match: a scenario, every human command, and how many ticks were played. Computer
/// players run inside the sim, so they replay by themselves. Because the sim is deterministic this
/// is all it takes to reproduce the match exactly.
/// </summary>
public sealed class MatchReplay
{
    private const uint Magic = 0x5A4E_4F43; // file marker, "CONZ" in little-endian bytes
    private const int FormatVersion = 2;

    public Scenario Scenario { get; }
    public IReadOnlyList<Command> Commands { get; }
    public int TickCount { get; set; }
    public ulong CatalogFingerprint { get; set; }
    /// <summary>State hash at the end, so a replay can check it played back correctly.</summary>
    public ulong FinalHash { get; set; }
    public string Title { get; set; } = "";

    public MatchReplay(Scenario scenario, IEnumerable<Command> commands, int tickCount)
    {
        Scenario = scenario;
        Commands = commands.ToList();
        TickCount = tickCount;
    }

    /// <summary>Plays the whole replay and returns the final world.</summary>
    public World Play(UnitCatalog catalog, Action<World>? afterEachTick = null)
    {
        World world = Scenario.CreateWorld(catalog);
        ILookup<int, Command> byTick = Commands.ToLookup(c => c.Tick);
        for (int t = 0; t < TickCount; t++)
        {
            world.Step(byTick[t].ToList());
            afterEachTick?.Invoke(world);
        }
        return world;
    }

    public void Write(Stream stream)
    {
        using var w = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        w.Write(Magic);
        w.Write(FormatVersion);
        using var body = new MemoryStream();
        using (var z = new DeflateStream(body, CompressionLevel.Optimal, leaveOpen: true))
        using (var bw = new BinaryWriter(z, Encoding.UTF8))
        {
            bw.Write(Title);
            bw.Write(CatalogFingerprint);
            bw.Write(FinalHash);
            bw.Write(Scenario.Seed);
            bw.Write(Scenario.Map.ToJson());
            bw.Write(Scenario.MissionJson);

            MatchOptions o = Scenario.Options;
            bw.Write(o.StartMetal);
            bw.Write(o.StartEnergy);
            bw.Write((byte)o.WinCondition);
            bw.Write(o.SpawnCommanders);
            bw.Write(o.RevealMap);

            bw.Write(Scenario.Players.Count);
            foreach (PlayerSetup p in Scenario.Players)
            {
                bw.Write(p.Name);
                bw.Write(p.Team);
                bw.Write(p.Faction);
                bw.Write(p.StartIndex);
                bw.Write((byte)p.Ai);
            }

            bw.Write(Scenario.Spawns.Count);
            foreach (Spawn s in Scenario.Spawns)
            {
                bw.Write(s.TypeId);
                bw.Write(s.Owner);
                bw.Write(s.Position.X.Raw);
                bw.Write(s.Position.Y.Raw);
            }

            bw.Write(TickCount);
            bw.Write(Commands.Count);
            foreach (Command c in Commands)
            {
                bw.Write(c.Tick);
                bw.Write(c.Player);
                bw.Write((byte)c.Type);
                bw.Write(c.UnitId);
                bw.Write(c.Position.X.Raw);
                bw.Write(c.Position.Y.Raw);
                bw.Write(c.TargetId);
                bw.Write(c.TypeIndex);
                bw.Write(c.Queue);
                bw.Write(c.Arg);
            }
        }
        w.Write((int)body.Length);
        w.Write(body.ToArray());
    }

    public static MatchReplay Read(Stream stream)
    {
        using var r0 = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (r0.ReadUInt32() != Magic) throw new InvalidDataException("Not a Contingency Zero replay");
        int version = r0.ReadInt32();
        if (version != FormatVersion) throw new InvalidDataException($"Unsupported replay version {version}");
        int length = r0.ReadInt32();
        byte[] compressed = r0.ReadBytes(length);

        using var z = new DeflateStream(new MemoryStream(compressed), CompressionMode.Decompress);
        using var r = new BinaryReader(z, Encoding.UTF8);

        string title = r.ReadString();
        ulong fingerprint = r.ReadUInt64();
        ulong finalHash = r.ReadUInt64();
        ulong seed = r.ReadUInt64();
        MapData map = MapData.FromJson(r.ReadString());
        string mission = r.ReadString();

        var options = new MatchOptions(
            StartMetal: r.ReadInt32(),
            StartEnergy: r.ReadInt32(),
            WinCondition: (WinCondition)r.ReadByte(),
            SpawnCommanders: r.ReadBoolean(),
            RevealMap: r.ReadBoolean());

        var players = new PlayerSetup[r.ReadInt32()];
        for (int i = 0; i < players.Length; i++)
            players[i] = new PlayerSetup(r.ReadString(), r.ReadInt32(), r.ReadString(), r.ReadInt32(), (AiDifficulty)r.ReadByte());

        var spawns = new Spawn[r.ReadInt32()];
        for (int i = 0; i < spawns.Length; i++)
            spawns[i] = new Spawn(r.ReadString(), r.ReadInt32(), new FixVec2(Fix64.FromRaw(r.ReadInt64()), Fix64.FromRaw(r.ReadInt64())));

        int tickCount = r.ReadInt32();
        var commands = new Command[r.ReadInt32()];
        for (int i = 0; i < commands.Length; i++)
        {
            int tick = r.ReadInt32();
            int player = r.ReadInt32();
            var type = (CommandType)r.ReadByte();
            int unit = r.ReadInt32();
            var pos = new FixVec2(Fix64.FromRaw(r.ReadInt64()), Fix64.FromRaw(r.ReadInt64()));
            int target = r.ReadInt32();
            int typeIndex = r.ReadInt32();
            bool queue = r.ReadBoolean();
            int arg = r.ReadInt32();
            commands[i] = new Command(tick, player, type, unit, pos, target, typeIndex, queue, arg);
        }

        var scenario = new Scenario(map, seed, players, options, spawns, mission);
        return new MatchReplay(scenario, commands, tickCount)
        {
            Title = title,
            CatalogFingerprint = fingerprint,
            FinalHash = finalHash,
        };
    }
}
