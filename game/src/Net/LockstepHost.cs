using System;
using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Sim.Core;

namespace ContingencyZero.Game.Net;

/// <summary>
/// The host's end of the lockstep: collects every human's input, builds and broadcasts a bundle
/// per tick, compares state hashes, resigns players who drop, and picks the input delay from the
/// measured round-trip times.
/// </summary>
public sealed class LockstepHost : LockstepChannel
{
    private readonly Dictionary<int, int> _peerToPlayer;
    private readonly Dictionary<int, int> _playerToPeer = new();
    private readonly int[] _humans;                       // every human player index, ascending
    private readonly int _playerCount;
    private readonly HashSet<int> _active = new();        // humans still connected
    private readonly HashSet<int> _loaded = new();
    private readonly Dictionary<int, int> _inputThrough = new();
    private readonly Dictionary<int, Dictionary<int, (int Through, List<Command> Commands)>> _segments = new();
    private readonly Dictionary<(int Tick, int Player), List<Command>> _commands = new();
    private readonly List<int> _resignQueue = new();
    private readonly Dictionary<int, ulong> _localHashes = new();
    private readonly Dictionary<(int Tick, int Player), ulong> _peerHashes = new();
    private readonly Dictionary<int, Queue<double>> _rtt = new();
    private int _formedThrough;
    private int _wantTick;
    private double _nextPing;
    private double _nextDelayCheck;
    private int _quietChecks;
    private double _nextWaitingNote;
    private bool _waitingAnnounced;

    /// <param name="peerToPlayer">Each remote human's transport peer id and player index.</param>
    public LockstepHost(INetTransport transport, Func<double> clock, int localPlayer, IReadOnlyList<string> names,
        IReadOnlyDictionary<int, int> peerToPlayer, int delay = InitialDelay)
        : base(transport, clock, localPlayer, names, delay)
    {
        _playerCount = names.Count;
        _peerToPlayer = new Dictionary<int, int>(peerToPlayer);
        foreach (var (peer, player) in _peerToPlayer) _playerToPeer[player] = peer;
        _humans = _peerToPlayer.Values.Append(localPlayer).Distinct().OrderBy(p => p).ToArray();
        foreach (int p in _humans)
        {
            _active.Add(p);
            _inputThrough[p] = Delay - 1;
            _segments[p] = new Dictionary<int, (int, List<Command>)>();
        }
        _formedThrough = Delay - 1;
    }

    /// <summary>
    /// Typical round-trip time to a player in seconds, or 0 for the host / unknown: the 60th
    /// percentile of the last ten seconds of probes, so frame hitches and brief outages (which
    /// stall the game whatever the delay) don't push the delay up for everyone.
    /// </summary>
    public double RoundTrip(int player)
    {
        if (!_rtt.TryGetValue(player, out var q) || q.Count == 0) return 0;
        var sorted = q.OrderBy(x => x).ToList();
        return sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * 0.6))];
    }

    public bool IsConnected(int player) => _active.Contains(player);

    public override IReadOnlyList<int> WaitingOn
    {
        get
        {
            if (!Started) return _humans.Where(p => _active.Contains(p) && !_loaded.Contains(p)).ToList();
            return _humans.Where(p => p != LocalPlayer && _active.Contains(p) && _inputThrough[p] < _wantTick).ToList();
        }
    }

    protected override void OnWant(int tick) => _wantTick = tick;

    // ---- Incoming ----------------------------------------------------------------------------

    protected override void Handle(NetEvent e)
    {
        switch (e.Kind)
        {
            case NetEventKind.Connected:
                // Too late to join: the match is running.
                Transport.Send(e.Peer, NetProtocol.Build(Msg.Reject, w => w.Write("That game has already started.")));
                Transport.Flush();
                Transport.Kick(e.Peer);
                return;
            case NetEventKind.Disconnected:
                if (_peerToPlayer.TryGetValue(e.Peer, out int gone)) MarkLeft(gone);
                return;
        }

        if (e.Data is null || !_peerToPlayer.TryGetValue(e.Peer, out int player) || !_active.Contains(player)) return;
        using var r = NetProtocol.Open(e.Data, out Msg type);
        switch (type)
        {
            case Msg.Loaded:
                _loaded.Add(player);
                break;
            case Msg.Input:
            {
                int from = r.ReadInt32();
                int through = r.ReadInt32();
                List<Command> commands = NetProtocol.ReadCommands(r);
                AddInput(player, from, through, commands);
                break;
            }
            case Msg.Hash:
            {
                int tick = r.ReadInt32();
                ulong hash = r.ReadUInt64();
                if (_localHashes.TryGetValue(tick, out ulong mine)) Compare(tick, player, mine, hash);
                else if (tick >= NextTick - 1) _peerHashes[(tick, player)] = hash;
                break;
            }
            case Msg.Pong:
            {
                double sent = r.ReadDouble();
                if (!_rtt.TryGetValue(player, out var q)) _rtt[player] = q = new Queue<double>();
                q.Enqueue(Math.Max(0, Clock() - sent));
                while (q.Count > 20) q.Dequeue();
                break;
            }
            case Msg.Chat:
                SendChatFrom(player, NetProtocol.Clean(r.ReadString(), 200));
                break;
            case Msg.Leave:
                MarkLeft(player);
                break;
        }
    }

    private void AddInput(int player, int from, int through, List<Command> commands)
    {
        if (!_active.Contains(player)) return;
        int have = _inputThrough[player];
        if (from <= have || through < from || through - from > 1000) return; // stale, duplicate or nonsense
        var clean = new List<Command>(Math.Min(commands.Count, NetProtocol.MaxCommandsPerInput));
        foreach (Command c in commands)
        {
            if (clean.Count >= NetProtocol.MaxCommandsPerInput) break;
            if (!NetProtocol.IsKnownCommand(c.Type)) continue;
            clean.Add(c with { Tick = through, Player = player }); // nobody gets to speak for someone else
        }
        var segments = _segments[player];
        segments[from] = (through, clean);
        while (segments.Remove(_inputThrough[player] + 1, out var seg))
        {
            if (seg.Commands.Count > 0)
            {
                if (_commands.TryGetValue((seg.Through, player), out var existing)) existing.AddRange(seg.Commands);
                else _commands[(seg.Through, player)] = seg.Commands;
            }
            _inputThrough[player] = seg.Through;
        }
    }

    private void MarkLeft(int player)
    {
        if (player == LocalPlayer || !_active.Remove(player)) return;
        _loaded.Remove(player);
        _segments[player].Clear();
        foreach (var key in _commands.Keys.Where(k => k.Player == player).ToList()) _commands.Remove(key);
        _resignQueue.Add(player);
        if (_playerToPeer.TryGetValue(player, out int peer)) Transport.Kick(peer);
        Log($"{NameOf(player)} left; resigning them from tick {_formedThrough + 1}");
        Broadcast(NetProtocol.Build(Msg.PlayerLeft, w => w.Write(player)));
        RaisePlayerLeft(player);
    }

    // ---- Our own input ----------------------------------------------------------------------------

    protected override void SendInput(int from, int through, List<Command> commands) =>
        AddInput(LocalPlayer, from, through, commands);

    protected override void AfterTake() => FormBundles();

    protected override void OnLocalHash(int tick, ulong hash)
    {
        _localHashes[tick] = hash;
        foreach (int p in _humans)
            if (_peerHashes.Remove((tick, p), out ulong theirs))
                Compare(tick, p, hash, theirs);
        // Keep a couple of minutes of history; clients are never further behind than that.
        int oldest = tick - 120 * Sim.World.TicksPerSecond;
        if (tick % (60 * Sim.World.TicksPerSecond) == 0)
        {
            foreach (int t in _localHashes.Keys.Where(t => t < oldest).ToList()) _localHashes.Remove(t);
            foreach (var k in _peerHashes.Keys.Where(k => k.Tick < oldest).ToList()) _peerHashes.Remove(k);
        }
    }

    private void Compare(int tick, int player, ulong mine, ulong theirs)
    {
        if (mine == theirs || Desync is not null) return;
        var report = new DesyncReport(tick, mine, theirs, player);
        Broadcast(NetProtocol.Build(Msg.Desync, w =>
        {
            w.Write(report.Tick);
            w.Write(report.HostHash);
            w.Write(report.PeerHash);
            w.Write(report.Player);
        }));
        RaiseDesync(report);
    }

    public override void SendChat(string text) => SendChatFrom(LocalPlayer, NetProtocol.Clean(text, 200));

    private void SendChatFrom(int player, string text)
    {
        if (text.Length == 0) return;
        string name = NameOf(player);
        Broadcast(NetProtocol.Build(Msg.Chat, w =>
        {
            w.Write(player);
            w.Write(name);
            w.Write(text);
        }));
        RaiseChat(player, name, text);
    }

    // ---- Bundles and housekeeping ----------------------------------------------------------------

    protected override void AfterPoll()
    {
        double now = Clock();
        _loaded.Add(LocalPlayer); // polling at all means our match scene is up
        if (!Started && _humans.All(p => !_active.Contains(p) || _loaded.Contains(p)))
        {
            Started = true;
            Broadcast(NetProtocol.Build(Msg.Go));
            Log("everyone loaded: go");
        }

        if (Started && now >= _nextPing) // probes sent while someone is still loading would read as lag
        {
            _nextPing = now + 0.5;
            byte[] ping = NetProtocol.Build(Msg.Ping, w => w.Write(now));
            Broadcast(ping);
        }
        if (now >= _nextDelayCheck)
        {
            _nextDelayCheck = now + 1.0;
            AdaptDelay();
        }

        FormBundles();

        // Tell clients who we're stuck on, so they can say so too.
        bool stuck = StallSeconds > 0.3;
        if (stuck && now >= _nextWaitingNote)
        {
            _nextWaitingNote = now + 0.5;
            _waitingAnnounced = true;
            var waiting = WaitingOn;
            Broadcast(NetProtocol.Build(Msg.Waiting, w =>
            {
                w.Write((byte)waiting.Count);
                foreach (int p in waiting) w.Write(p);
            }));
        }
        else if (!stuck && _waitingAnnounced)
        {
            _waitingAnnounced = false;
            Broadcast(NetProtocol.Build(Msg.Waiting, w => w.Write((byte)0)));
        }
    }

    /// <summary>
    /// Input must make the round trip (client to host to client) within the delay, plus a margin
    /// for frame timing. Raise the delay at once when the network gets worse; lower it one tick at
    /// a time after it has been comfortably better for a few seconds.
    /// </summary>
    private void AdaptDelay()
    {
        double worst = 0;
        bool any = false;
        foreach (int p in _humans)
        {
            if (p == LocalPlayer || !_active.Contains(p) || !_rtt.ContainsKey(p)) continue;
            worst = Math.Max(worst, RoundTrip(p));
            any = true;
        }
        if (!any) return;
        int need = Math.Clamp((int)Math.Ceiling(worst * Sim.World.TicksPerSecond + 1.5), MinDelay, MaxDelay);
        if (need > Delay)
        {
            SetDelay(need);
            _quietChecks = 0;
        }
        else if (need < Delay)
        {
            if (++_quietChecks >= 5)
            {
                SetDelay(Delay - 1);
                _quietChecks = 0;
            }
        }
        else _quietChecks = 0;
    }

    private void FormBundles()
    {
        while (true)
        {
            int tick = _formedThrough + 1;
            foreach (int p in _humans)
                if (_active.Contains(p) && _inputThrough[p] < tick) return;

            var bundle = new List<Command>();
            for (int p = 0; p < _playerCount; p++)
            {
                if (_commands.Remove((tick, p), out var list)) bundle.AddRange(list);
                if (_resignQueue.Contains(p)) bundle.Add(new Command(tick, p, CommandType.Resign, -1));
            }
            _resignQueue.Clear();
            Bundles[tick] = bundle;
            _formedThrough = tick;

            int hostTick = NextTick;
            int delay = Delay;
            Broadcast(NetProtocol.Build(Msg.Bundle, w =>
            {
                w.Write(tick);
                w.Write(hostTick);
                w.Write((byte)delay);
                NetProtocol.WriteCommands(w, bundle);
            }));
        }
    }

    private void Broadcast(byte[] message)
    {
        foreach (var (peer, player) in _peerToPlayer)
            if (_active.Contains(player)) Transport.Send(peer, message);
    }

    public override void Dispose()
    {
        try
        {
            Broadcast(NetProtocol.Build(Msg.Leave, w => w.Write("The host left the game.")));
            Transport.Flush();
        }
        catch (Exception e) when (e is InvalidOperationException or ObjectDisposedException)
        {
            // Already closed.
        }
        base.Dispose();
    }
}
