using System;
using System.Collections.Generic;
using System.IO;
using ContingencyZero.Game.Match;
using ContingencyZero.Sim.Core;

namespace ContingencyZero.Game.Net;

/// <summary>Two machines disagreed about the state after <see cref="Tick"/>.</summary>
public sealed record DesyncReport(int Tick, ulong HostHash, ulong PeerHash, int Player);

/// <summary>
/// Lockstep over a host relay. Each human sends its commands for tick T+delay when it steps T
/// (an empty message still counts: it says "nothing from me for that tick"). The host merges
/// every human's commands for a tick into a bundle, sorted by player, and broadcasts it. Every
/// machine, host included, steps tick T only once it holds T's bundle, so everyone applies the
/// same commands on the same tick. The first <see cref="Delay"/> ticks are empty by definition.
/// Messages may arrive in any order; only reliability matters.
/// </summary>
public abstract class LockstepChannel : ICommandChannel, IDisposable
{
    public const int InitialDelay = 4;
    public const int MinDelay = 3;
    public const int MaxDelay = 15;

    protected readonly INetTransport Transport;
    protected readonly Func<double> Clock;
    protected readonly Dictionary<int, List<Command>> Bundles = new();
    protected readonly List<Command> Pending = new();
    /// <summary>The last tick our own input covers.</summary>
    protected int SentThrough;
    /// <summary>The next tick the session will step.</summary>
    protected int NextTick;

    private readonly List<NetEvent> _events = new();
    private readonly List<NetEvent> _inbox = new();
    private double _stallSince = -1;
    private bool _disposed;

    /// <summary>This machine's player index.</summary>
    public int LocalPlayer { get; }
    /// <summary>Names of every player in the match, by index.</summary>
    public IReadOnlyList<string> PlayerNames { get; }
    /// <summary>Ticks between issuing a command and it taking effect.</summary>
    public int Delay { get; protected set; }
    /// <summary>False until every human has loaded the match and the host says go.</summary>
    public bool Started { get; protected set; }
    /// <summary>Set when the connection to the host is gone (clients only).</summary>
    public string? LostReason { get; protected set; }
    /// <summary>The first desync seen, if any.</summary>
    public DesyncReport? Desync { get; protected set; }

    /// <summary>How long the session has been held up waiting for commands, in seconds.</summary>
    public double StallSeconds => _stallSince < 0 ? 0 : Clock() - _stallSince;

    // Diagnostics for tests and the dev log.
    public double TotalStallSeconds { get; private set; }
    public double LongestStallSeconds { get; private set; }
    public int MinDelaySeen { get; private set; } = int.MaxValue;
    public int MaxDelaySeen { get; private set; }

    /// <summary>A player dropped out (their Resign is already in a bundle).</summary>
    public event Action<int>? PlayerLeft;
    /// <summary>Hashes disagreed. Raised once.</summary>
    public event Action<DesyncReport>? Desynced;
    /// <summary>The host is gone. Raised once, with a reason to show.</summary>
    public event Action<string>? Lost;
    /// <summary>Chat: player index (-1 for system notes), name, text.</summary>
    public event Action<int, string, string>? ChatReceived;
    /// <summary>This machine's per-second state hash (tick, hash), for logs.</summary>
    public event Action<int, ulong>? HashComputed;

    protected LockstepChannel(INetTransport transport, Func<double> clock, int localPlayer, IReadOnlyList<string> names, int delay)
    {
        Transport = transport;
        Clock = clock;
        LocalPlayer = localPlayer;
        PlayerNames = names;
        Delay = Math.Clamp(delay, 1, MaxDelay);
        NoteDelay();
        // Ticks before the first delay are empty for everyone: nobody could have sent anything yet.
        for (int t = 0; t < Delay; t++) Bundles[t] = new List<Command>();
        SentThrough = Delay - 1;
    }

    public string NameOf(int player) =>
        player >= 0 && player < PlayerNames.Count ? PlayerNames[player] : $"Player {player + 1}";

    /// <summary>Players this machine is waiting on right now (empty if not waiting or unknown).</summary>
    public abstract IReadOnlyList<int> WaitingOn { get; }

    /// <summary>Clock multiplier for the session: a little above 1 while catching up with the host.</summary>
    public virtual double TimeScale => 1.0;

    // ---- ICommandChannel ----------------------------------------------------------------------

    public void Submit(Command command)
    {
        if (Pending.Count < NetProtocol.MaxCommandsPerInput) Pending.Add(command);
    }

    public bool Ready(int tick)
    {
        bool ready = Started && LostReason is null && Bundles.ContainsKey(tick);
        OnWant(tick);
        double now = Clock();
        if (!ready)
        {
            if (_stallSince < 0) _stallSince = now;
        }
        else if (_stallSince >= 0)
        {
            double stall = now - _stallSince;
            TotalStallSeconds += stall;
            LongestStallSeconds = Math.Max(LongestStallSeconds, stall);
            _stallSince = -1;
        }
        return ready;
    }

    public List<Command> Take(int tick)
    {
        List<Command> bundle = Bundles[tick];
        Bundles.Remove(tick);
        if (_devLocalOnly is { } extra)
        {
            bundle.Add(extra with { Tick = tick });
            _devLocalOnly = null;
        }
        NextTick = tick + 1;
        int target = tick + Delay;
        if (target > SentThrough)
        {
            var commands = new List<Command>(Pending.Count);
            foreach (Command c in Pending) commands.Add(c with { Tick = target, Player = LocalPlayer });
            Pending.Clear();
            SendInput(SentThrough + 1, target, commands);
            SentThrough = target;
        }
        AfterTake();
        Transport.Flush();
        return bundle;
    }

    public void AfterTick(int tick, ulong hash)
    {
        if (hash == 0) return;
        HashComputed?.Invoke(tick, hash);
        OnLocalHash(tick, hash);
    }

    public void Poll()
    {
        if (_disposed) return;
        _events.Clear();
        _events.AddRange(_inbox);
        _inbox.Clear();
        Transport.Poll(_events);
        foreach (NetEvent e in _events)
        {
            try
            {
                Handle(e);
            }
            catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException or IOException or ArgumentException)
            {
                Log($"dropped a bad message from peer {e.Peer}: {ex.Message}");
            }
        }
        AfterPoll();
        Transport.Flush();
    }

    /// <summary>Events that arrived before this channel existed (handed over by the lobby).</summary>
    public void Enqueue(NetEvent e) => _inbox.Add(e);

    private Command? _devLocalOnly;

    /// <summary>
    /// Test hook (--dev-net-desync-at): applies <paramref name="command"/> on this machine only at
    /// the next tick, deliberately desyncing it so the detection and banner can be checked.
    /// </summary>
    public void DevDesyncNextTick(Command command) => _devLocalOnly = command;

    // ---- For subclasses -------------------------------------------------------------------------

    protected abstract void Handle(NetEvent e);
    protected abstract void SendInput(int from, int through, List<Command> commands);
    protected virtual void OnWant(int tick) { }
    protected virtual void AfterTake() { }
    protected virtual void AfterPoll() { }
    protected abstract void OnLocalHash(int tick, ulong hash);
    public abstract void SendChat(string text);

    protected void SetDelay(int delay)
    {
        Delay = Math.Clamp(delay, 1, MaxDelay);
        NoteDelay();
    }

    private void NoteDelay()
    {
        MinDelaySeen = Math.Min(MinDelaySeen, Delay);
        MaxDelaySeen = Math.Max(MaxDelaySeen, Delay);
    }

    protected void RaisePlayerLeft(int player) => PlayerLeft?.Invoke(player);
    protected void RaiseChat(int player, string name, string text) => ChatReceived?.Invoke(player, name, text);

    protected void RaiseDesync(DesyncReport report)
    {
        if (Desync is not null) return;
        Desync = report;
        Log($"DESYNC at tick {report.Tick} ({NetProtocol.Clock(report.Tick)}): host {report.HostHash:X16}, {NameOf(report.Player)} {report.PeerHash:X16}");
        Desynced?.Invoke(report);
    }

    protected void RaiseLost(string reason)
    {
        if (LostReason is not null) return;
        LostReason = reason;
        Log($"connection lost: {reason}");
        Lost?.Invoke(reason);
    }

    /// <summary>Where diagnostics go; the game points this at Godot's log.</summary>
    public static Action<string> Log { get; set; } = _ => { };

    public virtual void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Transport.Dispose();
        GC.SuppressFinalize(this);
    }
}
