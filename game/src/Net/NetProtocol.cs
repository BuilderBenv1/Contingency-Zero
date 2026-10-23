using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Replay;

namespace ContingencyZero.Game.Net;

/// <summary>Every message on the wire starts with one of these bytes.</summary>
public enum Msg : byte
{
    // ---- Lobby ----
    /// <summary>Client to host: protocol version, data fingerprint, name, preferred faction.</summary>
    Hello = 1,
    /// <summary>Host to client: you're in, this is your slot.</summary>
    Welcome = 2,
    /// <summary>Host to client: you can't join, here's why. The host then disconnects you.</summary>
    Reject = 3,
    /// <summary>Host to clients: the whole lobby (slots and options).</summary>
    LobbyState = 4,
    /// <summary>Host to clients: the chosen map as compressed JSON, so user-made maps work everywhere.</summary>
    MapData = 5,
    /// <summary>Client to host: change my faction and team.</summary>
    SlotRequest = 6,
    /// <summary>Client to host: ready or not.</summary>
    ReadyFlag = 7,
    /// <summary>Client to host: text. Host to clients: who said it and the text.</summary>
    Chat = 8,
    /// <summary>Host to client: the match is on: your player index, the input delay and the full scenario.</summary>
    Start = 9,

    // ---- Match ----
    /// <summary>Client to host: the match scene is built and ready to run.</summary>
    Loaded = 10,
    /// <summary>Host to clients: everyone is loaded, start the clock.</summary>
    Go = 11,
    /// <summary>Client to host: my commands for a range of ticks (always sent, even if empty).</summary>
    Input = 12,
    /// <summary>Host to clients: every player's commands for one tick.</summary>
    Bundle = 13,
    /// <summary>Client to host: my state hash after a tick, once a second.</summary>
    Hash = 14,
    /// <summary>Host to clients: hashes disagreed.</summary>
    Desync = 15,
    /// <summary>Host to clients: a player disconnected (their Resign arrives in a bundle).</summary>
    PlayerLeft = 16,
    /// <summary>Host to clients: who the host is waiting on.</summary>
    Waiting = 17,
    /// <summary>Host to client: round-trip probe.</summary>
    Ping = 18,
    /// <summary>Client to host: the probe, echoed.</summary>
    Pong = 19,
    /// <summary>Either way: I'm leaving on purpose.</summary>
    Leave = 20,

    // ---- Spectators (protocol 2) ----
    /// <summary>Client to host: I want to watch instead of play (or the reverse).</summary>
    RoleRequest = 21,
    /// <summary>Host to clients: a spectator disconnected (nothing happens to the game).</summary>
    SpectatorLeft = 22,
}

/// <summary>
/// Encoding helpers. Messages are little binary blobs: a <see cref="Msg"/> byte, then fields
/// written with <see cref="BinaryWriter"/>. Commands use the same field layout as replays, and
/// the scenario is sent as a replay with no commands, so there's one binary format for both.
/// </summary>
public static class NetProtocol
{
    /// <summary>Bumped whenever the wire format changes; mismatched versions are refused at the door.</summary>
    public const int Version = 2;
    public const int DefaultPort = 7777;
    public const int MaxSlots = 8;
    /// <summary>People watching a game without playing. They hold no slot and the lockstep never waits on them.</summary>
    public const int MaxSpectators = 8;
    /// <summary>
    /// The player index of a spectator on its own machine (<see cref="Match.SimSession.LocalPlayer"/>): it owns
    /// nothing and issues no commands.
    /// </summary>
    public const int SpectatorPlayer = -1;
    /// <summary>In chat messages the sender is a player index, or this for a system note...</summary>
    public const int SystemChat = -1;
    /// <summary>...or this for a line from a spectator (the name says who).</summary>
    public const int SpectatorChat = -2;
    /// <summary>A single player can't flood a tick with more than this many commands.</summary>
    public const int MaxCommandsPerInput = 1000;

    public static byte[] Build(Msg type, Action<BinaryWriter>? body = null)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write((byte)type);
            body?.Invoke(w);
        }
        return ms.ToArray();
    }

    /// <summary>Opens a received message; the reader is positioned after the type byte.</summary>
    public static BinaryReader Open(byte[] data, out Msg type)
    {
        if (data.Length == 0) throw new InvalidDataException("Empty message");
        type = (Msg)data[0];
        var r = new BinaryReader(new MemoryStream(data, 1, data.Length - 1, writable: false), Encoding.UTF8);
        return r;
    }

    // ---- Commands (same field order as MatchReplay) ---------------------------------------------

    public static void WriteCommand(BinaryWriter w, in Command c)
    {
        w.Write(c.Tick);
        w.Write(c.Player);
        w.Write((byte)c.Type);
        w.Write(c.UnitId);
        w.Write(c.Position.X.Raw);
        w.Write(c.Position.Y.Raw);
        w.Write(c.TargetId);
        w.Write(c.TypeIndex);
        w.Write(c.Queue);
        w.Write(c.Arg);
    }

    public static Command ReadCommand(BinaryReader r)
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
        return new Command(tick, player, type, unit, pos, target, typeIndex, queue, arg);
    }

    public static void WriteCommands(BinaryWriter w, IReadOnlyList<Command> commands)
    {
        w.Write(commands.Count);
        foreach (Command c in commands) WriteCommand(w, c);
    }

    public static List<Command> ReadCommands(BinaryReader r)
    {
        int n = r.ReadInt32();
        if (n < 0 || n > MaxCommandsPerInput * MaxSlots) throw new InvalidDataException($"Bad command count {n}");
        var list = new List<Command>(n);
        for (int i = 0; i < n; i++) list.Add(ReadCommand(r));
        return list;
    }

    public static bool IsKnownCommand(CommandType t) => t >= CommandType.Move && t <= CommandType.Fight;

    // ---- Scenario and map --------------------------------------------------------------------

    /// <summary>The scenario as a command-less replay (deflated, includes the full map JSON).</summary>
    public static byte[] EncodeScenario(Scenario scenario, ulong catalogFingerprint)
    {
        var replay = new MatchReplay(scenario, Array.Empty<Command>(), 0) { CatalogFingerprint = catalogFingerprint, Title = "net" };
        using var ms = new MemoryStream();
        replay.Write(ms);
        return ms.ToArray();
    }

    public static Scenario DecodeScenario(byte[] data, out ulong catalogFingerprint)
    {
        using var ms = new MemoryStream(data);
        MatchReplay replay = MatchReplay.Read(ms);
        catalogFingerprint = replay.CatalogFingerprint;
        return replay.Scenario;
    }

    public static byte[] Deflate(string text)
    {
        using var ms = new MemoryStream();
        using (var z = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            byte[] raw = Encoding.UTF8.GetBytes(text);
            z.Write(raw, 0, raw.Length);
        }
        return ms.ToArray();
    }

    public static string Inflate(byte[] data)
    {
        using var z = new DeflateStream(new MemoryStream(data), CompressionMode.Decompress);
        using var reader = new StreamReader(z, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>FNV-1a over the text, to tell maps apart without sending them again.</summary>
    public static ulong HashText(string text)
    {
        var h = new StateHasher();
        foreach (char c in text) h.Add((int)c);
        return h.Value;
    }

    /// <summary>Formats a tick as m:ss of game time.</summary>
    public static string Clock(int tick)
    {
        int secs = tick / Sim.World.TicksPerSecond;
        return $"{secs / 60}:{secs % 60:D2}";
    }

    public static string Clean(string text, int max)
    {
        var sb = new StringBuilder(Math.Min(text.Length, max));
        foreach (char c in text)
        {
            if (sb.Length >= max) break;
            if (!char.IsControl(c)) sb.Append(c);
        }
        return sb.ToString().Trim();
    }
}
