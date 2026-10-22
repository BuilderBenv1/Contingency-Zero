using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;

namespace ContingencyZero.Sim.Replay;

/// <summary>A unit placed on the map before tick 0.</summary>
public readonly record struct Spawn(string TypeId, int Owner, FixVec2 Position);

/// <summary>Everything needed to start a match: the map, the players and the starting units.</summary>
public sealed record Scenario(
    ulong Seed,
    int MapWidth,
    int MapHeight,
    IReadOnlyList<int> PlayerTeams,
    IReadOnlyList<Spawn> Spawns,
    int StartMetal = 1000,
    int StartEnergy = 1000)
{
    public World CreateWorld(UnitCatalog catalog)
    {
        var world = new World(catalog, Seed, MapWidth, MapHeight, PlayerTeams, StartMetal, StartEnergy);
        foreach (Spawn s in Spawns)
            world.SpawnUnit(s.TypeId, s.Owner, s.Position);
        return world;
    }
}

/// <summary>
/// A recorded match: a scenario, every command, and how many ticks were played. Because the sim
/// is deterministic this is all it takes to reproduce the match exactly, and it stays tiny.
/// </summary>
public sealed class MatchReplay
{
    private const uint Magic = 0x5A4E_4F43; // file marker, "CONZ" in little-endian bytes
    private const int FormatVersion = 1;

    public Scenario Scenario { get; }
    public IReadOnlyList<Command> Commands { get; }
    public int TickCount { get; }

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

        w.Write(Scenario.Seed);
        w.Write(Scenario.MapWidth);
        w.Write(Scenario.MapHeight);
        w.Write(Scenario.StartMetal);
        w.Write(Scenario.StartEnergy);
        w.Write(Scenario.PlayerTeams.Count);
        foreach (int team in Scenario.PlayerTeams) w.Write(team);
        w.Write(Scenario.Spawns.Count);
        foreach (Spawn s in Scenario.Spawns)
        {
            w.Write(s.TypeId);
            w.Write(s.Owner);
            w.Write(s.Position.X.Raw);
            w.Write(s.Position.Y.Raw);
        }

        w.Write(TickCount);
        w.Write(Commands.Count);
        foreach (Command c in Commands)
        {
            w.Write(c.Tick);
            w.Write(c.Player);
            w.Write((byte)c.Type);
            w.Write(c.UnitId);
            w.Write(c.Position.X.Raw);
            w.Write(c.Position.Y.Raw);
            w.Write(c.TargetId);
            w.Write(c.TypeIndex);
        }
    }

    public static MatchReplay Read(Stream stream)
    {
        using var r = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (r.ReadUInt32() != Magic) throw new InvalidDataException("Not a Contingency Zero replay");
        int version = r.ReadInt32();
        if (version != FormatVersion) throw new InvalidDataException($"Unsupported replay version {version}");

        ulong seed = r.ReadUInt64();
        int width = r.ReadInt32();
        int height = r.ReadInt32();
        int startMetal = r.ReadInt32();
        int startEnergy = r.ReadInt32();
        var teams = new int[r.ReadInt32()];
        for (int i = 0; i < teams.Length; i++) teams[i] = r.ReadInt32();
        var spawns = new Spawn[r.ReadInt32()];
        for (int i = 0; i < spawns.Length; i++)
        {
            string type = r.ReadString();
            int owner = r.ReadInt32();
            var pos = new FixVec2(Fix64.FromRaw(r.ReadInt64()), Fix64.FromRaw(r.ReadInt64()));
            spawns[i] = new Spawn(type, owner, pos);
        }

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
            commands[i] = new Command(tick, player, type, unit, pos, target, typeIndex);
        }

        var scenario = new Scenario(seed, width, height, teams, spawns, startMetal, startEnergy);
        return new MatchReplay(scenario, commands, tickCount);
    }
}
