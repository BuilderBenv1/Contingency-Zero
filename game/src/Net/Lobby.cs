using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using ContingencyZero.Sim.Replay;

namespace ContingencyZero.Game.Net;

/// <summary>What sits in a lobby slot.</summary>
public enum SlotKind : byte { Open = 0, Closed = 1, Human = 2, Ai = 3 }

/// <summary>One of the eight lobby slots. Slot i starts at the map's start position i unless starts are random.</summary>
public sealed class LobbySlot
{
    public SlotKind Kind = SlotKind.Open;
    public AiDifficulty Ai = AiDifficulty.Normal;
    /// <summary>Transport peer id of the human here (the host is 1).</summary>
    public int Peer;
    public string Name = "";
    public string Faction = "lattice";
    public int Team;
    public bool Ready;
    public int PingMs;

    public bool IsPlayer => Kind is SlotKind.Human or SlotKind.Ai;
}

/// <summary>The lobby as the host sees it; clients get a copy whenever it changes.</summary>
public sealed class LobbyState
{
    public int Version;
    public string MapKey = "";
    public string MapName = "";
    public ulong MapHash;
    /// <summary>Start positions on the map (at most 8): how many slots are usable.</summary>
    public int MapPlayers = 2;
    public int StartResources = 1000;
    public WinCondition Win = WinCondition.Commander;
    public bool RandomStarts;
    public LobbySlot[] Slots = Enumerable.Range(0, NetProtocol.MaxSlots).Select(i => new LobbySlot { Team = i }).ToArray();

    public int SlotOfPeer(int peer)
    {
        for (int i = 0; i < Slots.Length; i++)
            if (Slots[i].Kind == SlotKind.Human && Slots[i].Peer == peer) return i;
        return -1;
    }

    public void Write(BinaryWriter w)
    {
        w.Write(Version);
        w.Write(MapKey);
        w.Write(MapName);
        w.Write(MapHash);
        w.Write((byte)MapPlayers);
        w.Write(StartResources);
        w.Write((byte)Win);
        w.Write(RandomStarts);
        foreach (LobbySlot s in Slots)
        {
            w.Write((byte)s.Kind);
            w.Write((byte)s.Ai);
            w.Write(s.Peer);
            w.Write(s.Name);
            w.Write(s.Faction);
            w.Write((byte)s.Team);
            w.Write(s.Ready);
            w.Write((short)Math.Clamp(s.PingMs, 0, short.MaxValue));
        }
    }

    public static LobbyState Read(BinaryReader r)
    {
        var s = new LobbyState
        {
            Version = r.ReadInt32(),
            MapKey = r.ReadString(),
            MapName = r.ReadString(),
            MapHash = r.ReadUInt64(),
            MapPlayers = Math.Clamp((int)r.ReadByte(), 1, NetProtocol.MaxSlots),
            StartResources = r.ReadInt32(),
            Win = (WinCondition)r.ReadByte(),
            RandomStarts = r.ReadBoolean(),
        };
        foreach (LobbySlot slot in s.Slots)
        {
            slot.Kind = (SlotKind)r.ReadByte();
            slot.Ai = (AiDifficulty)r.ReadByte();
            slot.Peer = r.ReadInt32();
            slot.Name = r.ReadString();
            slot.Faction = r.ReadString();
            slot.Team = Math.Clamp((int)r.ReadByte(), 0, 7);
            slot.Ready = r.ReadBoolean();
            slot.PingMs = r.ReadInt16();
        }
        return s;
    }

    public static string FactionName(string faction) => faction == "crucible" ? "Crucible" : "Lattice";

    public static string AiName(AiDifficulty ai) => ai switch
    {
        AiDifficulty.Easy => "Easy AI",
        AiDifficulty.Hard => "Hard AI",
        AiDifficulty.Brutal => "Brutal AI",
        _ => "Normal AI",
    };
}

/// <summary>Everything a machine needs to begin a networked match.</summary>
public sealed record NetMatchStart(Scenario Scenario, int LocalPlayer, LockstepChannel Channel);

/// <summary>
/// The host's lobby: accepts players into open slots, owns the slots and options, relays chat,
/// sends the map to everyone, and at Start builds the scenario and hands the connection over to a
/// <see cref="LockstepHost"/>.
/// </summary>
public sealed class LobbyHost : IDisposable
{
    private readonly INetTransport _transport;
    private readonly Func<double> _clock;
    private readonly ulong _fingerprint;
    private readonly List<NetEvent> _events = new();
    private readonly HashSet<int> _greeting = new();   // connected, no Hello yet
    private byte[]? _mapMessage;
    private double _nextPing;
    private bool _pingsChanged;
    private LockstepHost? _channel;

    public LobbyState State { get; } = new();
    public MapData? Map { get; private set; }

    /// <summary>Slots, options or the map changed.</summary>
    public event Action? Changed;
    /// <summary>Chat line: slot (-1 for system notes), name, text.</summary>
    public event Action<int, string, string>? Chat;

    public LobbyHost(INetTransport transport, Func<double> clock, string name, string faction, ulong catalogFingerprint)
    {
        _transport = transport;
        _clock = clock;
        _fingerprint = catalogFingerprint;
        LobbySlot me = State.Slots[0];
        me.Kind = SlotKind.Human;
        me.Peer = INetTransport.HostPeer;
        me.Name = CleanName(name);
        me.Faction = faction == "crucible" ? "crucible" : "lattice";
        me.Ready = true;
    }

    public bool HandedOff => _channel is not null;
    public int HumanCount => State.Slots.Count(s => s.Kind == SlotKind.Human);

    // ---- Host's controls ---------------------------------------------------------------------

    /// <summary>Picks the map. Fails (returning why) if the current humans won't fit on it.</summary>
    public string? SetMap(string key, MapData map)
    {
        int usable = Math.Min(NetProtocol.MaxSlots, map.StartPositions.Count);
        if (usable < 1) return "That map has no start positions.";
        if (HumanCount > usable) return $"{map.Name} only has room for {usable} players.";

        // Pull humans sitting beyond the new map's start positions into earlier slots.
        for (int i = usable; i < NetProtocol.MaxSlots; i++)
        {
            if (State.Slots[i].Kind != SlotKind.Human) continue;
            int free = Enumerable.Range(0, usable).First(j => State.Slots[j].Kind != SlotKind.Human);
            Swap(i, free);
        }
        for (int i = usable; i < NetProtocol.MaxSlots; i++)
        {
            State.Slots[i] = new LobbySlot { Team = i };
        }

        string json = map.ToJson();
        Map = MapData.FromJson(json);
        State.MapKey = key;
        State.MapName = map.Name;
        State.MapHash = NetProtocol.HashText(json);
        State.MapPlayers = usable;
        byte[] packed = NetProtocol.Deflate(json);
        _mapMessage = NetProtocol.Build(Msg.MapData, w =>
        {
            w.Write(key);
            w.Write(State.MapHash);
            w.Write(packed.Length);
            w.Write(packed);
        });
        foreach (int peer in JoinedPeers()) _transport.Send(peer, _mapMessage);
        ResetReady();
        Publish();
        return null;
    }

    private void Swap(int a, int b)
    {
        (State.Slots[a], State.Slots[b]) = (State.Slots[b], State.Slots[a]);
    }

    public void SetSlotKind(int slot, SlotKind kind, AiDifficulty ai = AiDifficulty.Normal)
    {
        if (!Usable(slot) || State.Slots[slot].Kind == SlotKind.Human || kind == SlotKind.Human) return;
        LobbySlot s = State.Slots[slot];
        if (kind == SlotKind.Ai && s.Kind != SlotKind.Ai) s.Faction = slot % 2 == 0 ? "lattice" : "crucible";
        s.Kind = kind;
        s.Ai = kind == SlotKind.Ai ? ai : AiDifficulty.Normal;
        s.Peer = 0;
        s.Name = kind == SlotKind.Ai ? LobbyState.AiName(ai) : "";
        s.Ready = kind == SlotKind.Ai;
        Publish();
    }

    /// <summary>The host can set its own faction and team and those of computer players.</summary>
    public void SetFaction(int slot, string faction)
    {
        if (!Usable(slot) || !HostControls(slot)) return;
        State.Slots[slot].Faction = faction == "crucible" ? "crucible" : "lattice";
        Publish();
    }

    public void SetTeam(int slot, int team)
    {
        if (!Usable(slot) || !HostControls(slot)) return;
        State.Slots[slot].Team = Math.Clamp(team, 0, 7);
        Publish();
    }

    public void SetOptions(int startResources, WinCondition win, bool randomStarts)
    {
        State.StartResources = startResources;
        State.Win = win;
        State.RandomStarts = randomStarts;
        ResetReady();
        Publish();
    }

    public void Kick(int slot)
    {
        if (!Usable(slot)) return;
        LobbySlot s = State.Slots[slot];
        if (s.Kind != SlotKind.Human || s.Peer == INetTransport.HostPeer) return;
        _transport.Send(s.Peer, NetProtocol.Build(Msg.Reject, w => w.Write("The host removed you from the game.")));
        _transport.Flush();
        _transport.Kick(s.Peer);
        string name = s.Name;
        State.Slots[slot] = new LobbySlot { Team = slot };
        SystemChat($"{name} was removed by the host.");
        Publish();
    }

    public void SendChat(string text)
    {
        text = NetProtocol.Clean(text, 200);
        if (text.Length > 0) RelayChat(0, State.Slots[0].Name, text);
    }

    /// <summary>Whether the match can start; otherwise why not.</summary>
    public string? StartProblem()
    {
        if (Map is null) return "Pick a map.";
        var players = UsableSlots().Where(i => State.Slots[i].IsPlayer).Select(i => State.Slots[i]).ToList();
        if (players.Count < 2) return "You need at least two players.";
        if (players.Select(p => p.Team).Distinct().Count() < 2) return "Everyone is on the same team.";
        var waiting = players.Where(p => p.Kind == SlotKind.Human && !p.Ready).Select(p => p.Name).ToList();
        if (waiting.Count > 0) return $"Waiting for {string.Join(", ", waiting)} to get ready.";
        return null;
    }

    /// <summary>Builds the scenario, tells every client, and hands the connection to the lockstep host.</summary>
    public NetMatchStart StartMatch(ulong seed)
    {
        if (Map is null) throw new InvalidOperationException("No map chosen");
        if (_channel is not null) throw new InvalidOperationException("Already started");

        var slots = UsableSlots().Where(i => State.Slots[i].IsPlayer).ToList();
        var starts = UsableSlots().ToList();
        if (State.RandomStarts)
        {
            var rng = new Random(unchecked((int)seed ^ (int)(seed >> 32)));
            for (int i = starts.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (starts[i], starts[j]) = (starts[j], starts[i]);
            }
        }

        var players = new List<PlayerSetup>();
        var peerToPlayer = new Dictionary<int, int>();
        int hostPlayer = 0;
        foreach (int slot in slots)
        {
            LobbySlot s = State.Slots[slot];
            int index = players.Count;
            string name = s.Kind == SlotKind.Ai ? $"{LobbyState.AiName(s.Ai)} ({LobbyState.FactionName(s.Faction)})" : s.Name;
            players.Add(new PlayerSetup(name, s.Team, s.Faction, starts[slot], s.Kind == SlotKind.Ai ? s.Ai : AiDifficulty.None));
            if (s.Kind != SlotKind.Human) continue;
            if (s.Peer == INetTransport.HostPeer) hostPlayer = index;
            else peerToPlayer[s.Peer] = index;
        }

        var options = new MatchOptions(State.StartResources, State.StartResources, State.Win);
        var scenario = new Scenario(Map, seed, players, options, new List<Spawn>());
        byte[] encoded = NetProtocol.EncodeScenario(scenario, _fingerprint);
        // Everyone, the host included, builds the match from the same bytes.
        Scenario decoded = NetProtocol.DecodeScenario(encoded, out _);

        var names = players.Select(p => p.Name).ToList();
        var channel = new LockstepHost(_transport, _clock, hostPlayer, names, peerToPlayer);
        foreach (var (peer, player) in peerToPlayer)
        {
            _transport.Send(peer, NetProtocol.Build(Msg.Start, w =>
            {
                w.Write(player);
                w.Write(hostPlayer);
                w.Write((byte)channel.Delay);
                w.Write(encoded.Length);
                w.Write(encoded);
            }));
        }
        foreach (int peer in _greeting)
        {
            _transport.Send(peer, NetProtocol.Build(Msg.Reject, w => w.Write("That game has already started.")));
            _transport.Kick(peer);
        }
        _greeting.Clear();
        _transport.Flush();
        _channel = channel;
        return new NetMatchStart(decoded, hostPlayer, channel);
    }

    // ---- Network -----------------------------------------------------------------------------

    public void Poll()
    {
        if (_channel is not null) return;
        _events.Clear();
        _transport.Poll(_events);
        foreach (NetEvent e in _events)
        {
            if (_channel is not null)
            {
                _channel.Enqueue(e);
                continue;
            }
            try
            {
                Handle(e);
            }
            catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException or IOException or ArgumentException)
            {
                LockstepChannel.Log($"lobby: bad message from peer {e.Peer}: {ex.Message}");
            }
        }

        double now = _clock();
        if (now >= _nextPing)
        {
            _nextPing = now + 1.0;
            byte[] ping = NetProtocol.Build(Msg.Ping, w => w.Write(now));
            foreach (int peer in JoinedPeers()) _transport.Send(peer, ping);
            if (_pingsChanged)
            {
                _pingsChanged = false;
                Publish();
            }
        }
        _transport.Flush();
    }

    private void Handle(NetEvent e)
    {
        switch (e.Kind)
        {
            case NetEventKind.Connected:
                _greeting.Add(e.Peer);
                return;
            case NetEventKind.Disconnected:
                _greeting.Remove(e.Peer);
                RemovePeer(e.Peer, "left");
                return;
        }
        if (e.Data is null) return;
        using var r = NetProtocol.Open(e.Data, out Msg type);
        int slot = State.SlotOfPeer(e.Peer);
        switch (type)
        {
            case Msg.Hello:
                Greet(e.Peer, r.ReadInt32(), r.ReadUInt64(), r.ReadString(), r.ReadString());
                break;
            case Msg.SlotRequest when slot >= 0:
            {
                string faction = r.ReadString();
                int team = r.ReadByte();
                State.Slots[slot].Faction = faction == "crucible" ? "crucible" : "lattice";
                State.Slots[slot].Team = Math.Clamp(team, 0, 7);
                State.Slots[slot].Ready = false;
                Publish();
                break;
            }
            case Msg.ReadyFlag when slot >= 0:
                State.Slots[slot].Ready = r.ReadBoolean();
                Publish();
                break;
            case Msg.Chat when slot >= 0:
                RelayChat(slot, State.Slots[slot].Name, NetProtocol.Clean(r.ReadString(), 200));
                break;
            case Msg.Pong when slot >= 0:
                State.Slots[slot].PingMs = (int)Math.Round(Math.Max(0, _clock() - r.ReadDouble()) * 1000);
                _pingsChanged = true;
                break;
            case Msg.Leave:
                _greeting.Remove(e.Peer);
                RemovePeer(e.Peer, "left");
                _transport.Kick(e.Peer);
                break;
        }
    }

    private void Greet(int peer, int version, ulong fingerprint, string name, string faction)
    {
        if (!_greeting.Remove(peer) || State.SlotOfPeer(peer) >= 0) return;
        string? problem = null;
        if (version != NetProtocol.Version) problem = "Your game version doesn't match the host's.";
        else if (fingerprint != _fingerprint) problem = "Your unit data doesn't match the host's (different game version or mods).";
        int slot = UsableSlots().FirstOrDefault(i => State.Slots[i].Kind == SlotKind.Open, -1);
        if (problem is null && slot < 0) problem = "The game is full.";
        if (problem is not null)
        {
            _transport.Send(peer, NetProtocol.Build(Msg.Reject, w => w.Write(problem)));
            _transport.Flush();
            _transport.Kick(peer);
            return;
        }

        name = CleanName(name);
        string unique = name;
        for (int n = 2; State.Slots.Any(s => s.Kind == SlotKind.Human && s.Name == unique); n++) unique = $"{name} ({n})";
        State.Slots[slot] = new LobbySlot
        {
            Kind = SlotKind.Human,
            Peer = peer,
            Name = unique,
            Faction = faction == "crucible" ? "crucible" : "lattice",
            Team = slot,
        };
        _transport.Send(peer, NetProtocol.Build(Msg.Welcome, w => w.Write(peer)));
        if (_mapMessage is not null) _transport.Send(peer, _mapMessage);
        SystemChat($"{unique} joined.");
        Publish();
    }

    private void RemovePeer(int peer, string verb)
    {
        int slot = State.SlotOfPeer(peer);
        if (slot < 0) return;
        string name = State.Slots[slot].Name;
        State.Slots[slot] = new LobbySlot { Team = slot };
        SystemChat($"{name} {verb}.");
        Publish();
    }

    private void RelayChat(int slot, string name, string text)
    {
        if (text.Length == 0) return;
        byte[] msg = NetProtocol.Build(Msg.Chat, w =>
        {
            w.Write(slot);
            w.Write(name);
            w.Write(text);
        });
        foreach (int peer in JoinedPeers()) _transport.Send(peer, msg);
        Chat?.Invoke(slot, name, text);
    }

    private void SystemChat(string text) => RelayChat(-1, "", text);

    private void Publish()
    {
        State.Version++;
        byte[] msg = NetProtocol.Build(Msg.LobbyState, State.Write);
        foreach (int peer in JoinedPeers()) _transport.Send(peer, msg);
        Changed?.Invoke();
    }

    private void ResetReady()
    {
        foreach (LobbySlot s in State.Slots)
            if (s.Kind == SlotKind.Human && s.Peer != INetTransport.HostPeer) s.Ready = false;
    }

    private IEnumerable<int> JoinedPeers() =>
        State.Slots.Where(s => s.Kind == SlotKind.Human && s.Peer != INetTransport.HostPeer).Select(s => s.Peer).ToList();

    private IEnumerable<int> UsableSlots() => Enumerable.Range(0, State.MapPlayers);
    private bool Usable(int slot) => slot >= 0 && slot < State.MapPlayers;
    private bool HostControls(int slot) => slot == 0 || State.Slots[slot].Kind == SlotKind.Ai;

    private static string CleanName(string name)
    {
        name = NetProtocol.Clean(name, 20);
        return name.Length == 0 ? "Commander" : name;
    }

    public void Dispose()
    {
        if (_channel is not null) return; // the match owns the connection now
        try
        {
            byte[] bye = NetProtocol.Build(Msg.Leave, w => w.Write("The host closed the lobby."));
            foreach (int peer in JoinedPeers()) _transport.Send(peer, bye);
            _transport.Flush();
        }
        catch (Exception e) when (e is InvalidOperationException or ObjectDisposedException)
        {
            // Already closed.
        }
        _transport.Dispose();
    }
}

/// <summary>
/// A client's lobby: says hello, mirrors the host's lobby state and map, sends this player's
/// choices, and when the host starts, builds a <see cref="LockstepClient"/> from the scenario.
/// </summary>
public sealed class LobbyClient : IDisposable
{
    private readonly INetTransport _transport;
    private readonly Func<double> _clock;
    private readonly ulong _fingerprint;
    private readonly string _name;
    private readonly string _faction;
    private readonly List<NetEvent> _events = new();
    private LockstepClient? _channel;
    private int _myPeer = -1;
    private bool _failed;

    public LobbyState? State { get; private set; }
    public MapData? Map { get; private set; }
    public ulong MapHash { get; private set; }
    public bool Connected { get; private set; }
    public bool Joined => _myPeer >= 0 && State is not null;
    public int MySlot => State is null ? -1 : State.SlotOfPeer(_myPeer);
    public bool HandedOff => _channel is not null;

    public event Action? Changed;
    public event Action<int, string, string>? Chat;
    /// <summary>Couldn't join, was refused, or lost the host. Raised once.</summary>
    public event Action<string>? Failed;
    public event Action<NetMatchStart>? Started;

    public LobbyClient(INetTransport transport, Func<double> clock, string name, string faction, ulong catalogFingerprint)
    {
        _transport = transport;
        _clock = clock;
        _name = name;
        _faction = faction;
        _fingerprint = catalogFingerprint;
    }

    public void SetChoice(string faction, int team) =>
        Send(NetProtocol.Build(Msg.SlotRequest, w =>
        {
            w.Write(faction);
            w.Write((byte)Math.Clamp(team, 0, 7));
        }));

    public void SetReady(bool ready) => Send(NetProtocol.Build(Msg.ReadyFlag, w => w.Write(ready)));

    public void SendChat(string text)
    {
        text = NetProtocol.Clean(text, 200);
        if (text.Length > 0) Send(NetProtocol.Build(Msg.Chat, w => w.Write(text)));
    }

    private void Send(byte[] msg)
    {
        if (!Connected || _channel is not null || _failed) return;
        _transport.Send(INetTransport.HostPeer, msg);
        _transport.Flush();
    }

    public void Poll()
    {
        if (_channel is not null || _failed) return;
        _events.Clear();
        _transport.Poll(_events);
        foreach (NetEvent e in _events)
        {
            if (_channel is not null)
            {
                _channel.Enqueue(e);
                continue;
            }
            if (_failed) break;
            try
            {
                Handle(e);
            }
            catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException or IOException or ArgumentException)
            {
                LockstepChannel.Log($"lobby: bad message from host: {ex.Message}");
            }
        }
        _transport.Flush();
    }

    private void Handle(NetEvent e)
    {
        if (e.Peer != INetTransport.HostPeer) return;
        switch (e.Kind)
        {
            case NetEventKind.Connected:
                Connected = true;
                _transport.Send(INetTransport.HostPeer, NetProtocol.Build(Msg.Hello, w =>
                {
                    w.Write(NetProtocol.Version);
                    w.Write(_fingerprint);
                    w.Write(_name);
                    w.Write(_faction);
                }));
                return;
            case NetEventKind.Disconnected:
                Fail(Connected ? "Lost connection to host." : "Couldn't reach the host.");
                return;
        }
        if (e.Data is null) return;
        using var r = NetProtocol.Open(e.Data, out Msg type);
        switch (type)
        {
            case Msg.Welcome:
                _myPeer = r.ReadInt32();
                Changed?.Invoke();
                break;
            case Msg.Reject:
                Fail(r.ReadString());
                break;
            case Msg.Leave:
                Fail(r.ReadString());
                break;
            case Msg.LobbyState:
            {
                LobbyState s = LobbyState.Read(r);
                if (State is null || s.Version > State.Version)
                {
                    State = s;
                    Changed?.Invoke();
                }
                break;
            }
            case Msg.MapData:
            {
                r.ReadString(); // key
                ulong hash = r.ReadUInt64();
                int length = r.ReadInt32();
                if (length < 0 || length > 64 * 1024 * 1024) throw new InvalidDataException("Map too large");
                string json = NetProtocol.Inflate(r.ReadBytes(length));
                Map = MapData.FromJson(json);
                MapHash = hash;
                Changed?.Invoke();
                break;
            }
            case Msg.Chat:
                Chat?.Invoke(r.ReadInt32(), r.ReadString(), r.ReadString());
                break;
            case Msg.Ping:
            {
                double sent = r.ReadDouble();
                _transport.Send(INetTransport.HostPeer, NetProtocol.Build(Msg.Pong, w => w.Write(sent)));
                break;
            }
            case Msg.Start:
            {
                int localPlayer = r.ReadInt32();
                int hostPlayer = r.ReadInt32();
                int delay = r.ReadByte();
                int length = r.ReadInt32();
                Scenario scenario = NetProtocol.DecodeScenario(r.ReadBytes(length), out ulong fingerprint);
                if (fingerprint != _fingerprint)
                {
                    Fail("Your unit data doesn't match the host's.");
                    return;
                }
                var names = scenario.Players.Select(p => p.Name).ToList();
                _channel = new LockstepClient(_transport, _clock, localPlayer, hostPlayer, names, delay);
                Started?.Invoke(new NetMatchStart(scenario, localPlayer, _channel));
                break;
            }
        }
    }

    private void Fail(string reason)
    {
        if (_failed) return;
        _failed = true;
        Failed?.Invoke(reason);
    }

    public void Dispose()
    {
        if (_channel is not null) return; // the match owns the connection now
        try
        {
            if (Connected && !_failed)
            {
                _transport.Send(INetTransport.HostPeer, NetProtocol.Build(Msg.Leave, w => w.Write("left")));
                _transport.Flush();
            }
        }
        catch (Exception e) when (e is InvalidOperationException or ObjectDisposedException)
        {
            // Already closed.
        }
        _transport.Dispose();
    }
}
