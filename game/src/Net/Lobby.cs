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

/// <summary>What kind of game the lobby will start.</summary>
public enum LobbyMode : byte
{
    /// <summary>Free-for-all or teams on a skirmish map; computer players allowed.</summary>
    Skirmish = 0,
    /// <summary>A campaign mission written for two humans. Extra people watch.</summary>
    Coop = 1,
    /// <summary>Every human on one team against scripted waves.</summary>
    Survival = 2,
}

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

/// <summary>Someone in the lobby who will watch the match rather than play it.</summary>
public sealed class LobbySpectator
{
    public int Peer;
    public string Name = "";
    /// <summary>What they'd play as if they take a seat.</summary>
    public string Faction = "lattice";
    public int PingMs;
}

/// <summary>The lobby as the host sees it; clients get a copy whenever it changes.</summary>
public sealed class LobbyState
{
    public int Version;
    public LobbyMode Mode = LobbyMode.Skirmish;
    public string MapKey = "";
    public string MapName = "";
    public ulong MapHash;
    /// <summary>Start positions on the map (at most 8), or in co-op the mission's human seats: how many slots are usable.</summary>
    public int MapPlayers = 2;
    public int StartResources = 1000;
    public WinCondition Win = WinCondition.Commander;
    public bool RandomStarts;
    /// <summary>Co-op: which mission.</summary>
    public string MissionKey = "";
    public string MissionTitle = "";
    /// <summary>Survival: how hard the waves are.</summary>
    public AiDifficulty Difficulty = AiDifficulty.Normal;
    public LobbySlot[] Slots = Enumerable.Range(0, NetProtocol.MaxSlots).Select(i => new LobbySlot { Team = i }).ToArray();
    public List<LobbySpectator> Spectators = new();

    public int SlotOfPeer(int peer)
    {
        for (int i = 0; i < Slots.Length; i++)
            if (Slots[i].Kind == SlotKind.Human && Slots[i].Peer == peer) return i;
        return -1;
    }

    public int SpectatorOfPeer(int peer)
    {
        for (int i = 0; i < Spectators.Count; i++)
            if (Spectators[i].Peer == peer) return i;
        return -1;
    }

    /// <summary>Whether players pick their own faction in this mode (co-op missions fix it).</summary>
    public bool FactionsFree => Mode != LobbyMode.Coop;
    /// <summary>Whether players pick their own team in this mode (co-op and survival fix it).</summary>
    public bool TeamsFree => Mode == LobbyMode.Skirmish;
    /// <summary>Whether the host can put computer players in slots.</summary>
    public bool AiAllowed => Mode == LobbyMode.Skirmish;

    public void Write(BinaryWriter w)
    {
        w.Write(Version);
        w.Write((byte)Mode);
        w.Write(MapKey);
        w.Write(MapName);
        w.Write(MapHash);
        w.Write((byte)MapPlayers);
        w.Write(StartResources);
        w.Write((byte)Win);
        w.Write(RandomStarts);
        w.Write(MissionKey);
        w.Write(MissionTitle);
        w.Write((byte)Difficulty);
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
        w.Write((byte)Spectators.Count);
        foreach (LobbySpectator s in Spectators)
        {
            w.Write(s.Peer);
            w.Write(s.Name);
            w.Write(s.Faction);
            w.Write((short)Math.Clamp(s.PingMs, 0, short.MaxValue));
        }
    }

    public static LobbyState Read(BinaryReader r)
    {
        var s = new LobbyState
        {
            Version = r.ReadInt32(),
            Mode = (LobbyMode)Math.Clamp((int)r.ReadByte(), 0, 2),
            MapKey = r.ReadString(),
            MapName = r.ReadString(),
            MapHash = r.ReadUInt64(),
            MapPlayers = Math.Clamp((int)r.ReadByte(), 1, NetProtocol.MaxSlots),
            StartResources = r.ReadInt32(),
            Win = (WinCondition)r.ReadByte(),
            RandomStarts = r.ReadBoolean(),
            MissionKey = r.ReadString(),
            MissionTitle = r.ReadString(),
            Difficulty = (AiDifficulty)r.ReadByte(),
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
        int watchers = Math.Min((int)r.ReadByte(), NetProtocol.MaxSpectators);
        for (int i = 0; i < watchers; i++)
        {
            s.Spectators.Add(new LobbySpectator
            {
                Peer = r.ReadInt32(),
                Name = r.ReadString(),
                Faction = r.ReadString(),
                PingMs = r.ReadInt16(),
            });
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

    public static string ModeName(LobbyMode mode) => mode switch
    {
        LobbyMode.Coop => "Co-op mission",
        LobbyMode.Survival => "Survival",
        _ => "Skirmish",
    };
}

/// <summary>Everything a machine needs to begin a networked match. <see cref="LocalPlayer"/> is -1 for a spectator.</summary>
public sealed record NetMatchStart(Scenario Scenario, int LocalPlayer, LockstepChannel Channel, string Title = "Multiplayer")
{
    public bool IsSpectator => LocalPlayer < 0;

    /// <summary>The match title for a lobby mode (the end screen and replay names use it).</summary>
    public static string TitleFor(LobbyMode mode, string missionTitle) => mode switch
    {
        LobbyMode.Coop => missionTitle.Length > 0 ? $"Co-op: {missionTitle}" : "Co-op",
        LobbyMode.Survival => "Survival",
        _ => "Multiplayer",
    };
}

/// <summary>
/// A co-op mission as the host's lobby needs it: the map it plays on, which of the mission's
/// players are human seats (in seat order), and how to build the scenario for a given number of
/// humans (fewer humans than seats: the mission fills the rest with allied computer players).
/// Loading mission files is the game's job (it needs Godot); the lobby only holds the result.
/// </summary>
public sealed record CoopMission(string Key, string Title, MapData Map, IReadOnlyList<int> HumanSeats, Func<int, Scenario> Build);

/// <summary>
/// How a survival game is put together. The lobby only knows "humans on one team plus a Horde
/// slot that owns the waves"; the details live with the mission code and are plugged in here so
/// the lobby (and its tests) stay free of Godot.
/// </summary>
public sealed class SurvivalRules
{
    /// <summary>How many humans fit on a map (the Horde may need a start position of its own).</summary>
    public Func<MapData, int> HumanCapacity { get; init; } = map => Math.Min(NetProtocol.MaxSlots, Math.Max(1, map.StartPositions.Count - 1));

    /// <summary>The match's players: the humans as given (team 0, in slot order) plus the Horde slot.</summary>
    public Func<IReadOnlyList<PlayerSetup>, MapData, List<PlayerSetup>> Players { get; init; } = (humans, map) =>
    {
        var list = humans.ToList();
        // Placeholder Horde: an AI-less player at the last start position. The real rules replace this.
        list.Add(new PlayerSetup("Horde", 1, "crucible", Math.Max(0, map.StartPositions.Count - 1)));
        return list;
    };

    /// <summary>Mission JSON for the waves, or null for none (the game then never ends by itself).</summary>
    public Func<MapData, IReadOnlyList<PlayerSetup>, AiDifficulty, int, string>? BuildJson { get; init; }

    /// <summary>Match options given the lobby's starting resources.</summary>
    public Func<int, MatchOptions> Options { get; init; } = res => new MatchOptions(res, res, WinCondition.Annihilation);

    /// <summary>True once the real wave builder is plugged in.</summary>
    public bool Available => BuildJson is not null;
}

/// <summary>
/// The host's lobby: accepts players into open slots (and watchers into the spectator list),
/// owns the slots, mode and options, relays chat, sends the map to everyone, and at Start builds
/// the scenario and hands the connection over to a <see cref="LockstepHost"/>.
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
    private CoopMission? _coop;
    private IReadOnlyList<PlayerSetup> _coopFull = Array.Empty<PlayerSetup>();   // the mission's players with every seat human
    private (string Key, MapData Map)? _skirmishMap;   // the map to go back to when leaving co-op

    public LobbyState State { get; } = new();
    public MapData? Map { get; private set; }
    /// <summary>How survival games are built. Replaced by the game once the mission code is loaded.</summary>
    public SurvivalRules Survival { get; set; } = new();
    /// <summary>The co-op mission chosen, if the mode is co-op.</summary>
    public CoopMission? Coop => _coop;

    /// <summary>Slots, options or the map changed.</summary>
    public event Action? Changed;
    /// <summary>Chat line: slot (-1 for system notes, -2 from a spectator), name, text.</summary>
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
    public int SpectatorCount => State.Spectators.Count;

    // ---- Host's controls ---------------------------------------------------------------------

    /// <summary>Picks the map (skirmish and survival). Fails (returning why) if the current humans won't fit on it.</summary>
    public string? SetMap(string key, MapData map)
    {
        if (State.Mode == LobbyMode.Coop) return "The mission decides the map in co-op.";
        int usable = Capacity(map);
        if (usable < 1) return "That map has no start positions.";
        if (HumanCount > usable) return $"{map.Name} only has room for {usable} players.";
        _skirmishMap = (key, map);
        ApplyMap(key, map, usable);
        return null;
    }

    private void ApplyMap(string key, MapData map, int usable)
    {
        // Pull humans sitting beyond the new map's start positions into earlier slots.
        for (int i = usable; i < NetProtocol.MaxSlots; i++)
        {
            if (State.Slots[i].Kind != SlotKind.Human) continue;
            int free = Enumerable.Range(0, usable).First(j => State.Slots[j].Kind != SlotKind.Human);
            Swap(i, free);
        }
        for (int i = usable; i < NetProtocol.MaxSlots; i++)
        {
            State.Slots[i] = FreshSlot(i);
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
        ApplyModeToSlots();
        ResetReady();
        Publish();
    }

    /// <summary>Usable slots on a map in the current mode.</summary>
    private int Capacity(MapData map) => State.Mode == LobbyMode.Survival
        ? Math.Min(NetProtocol.MaxSlots, Survival.HumanCapacity(map))
        : Math.Min(NetProtocol.MaxSlots, map.StartPositions.Count);

    private void Swap(int a, int b)
    {
        (State.Slots[a], State.Slots[b]) = (State.Slots[b], State.Slots[a]);
    }

    /// <summary>Back to a skirmish on the last skirmish map. Teams go back to one per slot.</summary>
    public void SetSkirmish()
    {
        if (State.Mode == LobbyMode.Skirmish) return;
        State.Mode = LobbyMode.Skirmish;
        _coop = null;
        State.MissionKey = "";
        State.MissionTitle = "";
        for (int i = 0; i < NetProtocol.MaxSlots; i++) State.Slots[i].Team = i;
        if (_skirmishMap is { } m) ApplyMap(m.Key, m.Map, Capacity(m.Map));
        else { ApplyModeToSlots(); ResetReady(); Publish(); }
    }

    /// <summary>
    /// Co-op: the mission brings its own map and seats. Humans beyond the seats go to the
    /// spectator list; computer players are removed (the mission adds its own allies).
    /// </summary>
    public string? SetCoop(CoopMission mission)
    {
        if (mission.HumanSeats.Count < 1) return "That mission has no human seats.";
        int seats = Math.Min(NetProtocol.MaxSlots, mission.HumanSeats.Count);
        State.Mode = LobbyMode.Coop;
        _coop = mission;
        _coopFull = mission.Build(mission.HumanSeats.Count).Players;
        State.MissionKey = mission.Key;
        State.MissionTitle = mission.Title;
        // Extra humans watch rather than play.
        var humans = Enumerable.Range(0, NetProtocol.MaxSlots).Where(i => State.Slots[i].Kind == SlotKind.Human).ToList();
        foreach (int i in humans.Skip(seats))
        {
            LobbySlot s = State.Slots[i];
            if (State.Spectators.Count < NetProtocol.MaxSpectators)
            {
                State.Spectators.Add(new LobbySpectator { Peer = s.Peer, Name = s.Name, Faction = s.Faction });
                State.Slots[i] = FreshSlot(i);
                SystemChat($"{s.Name} is watching: the mission has {seats} seats.");
            }
            else Kick(i);
        }
        ApplyMap(mission.Key, mission.Map, seats);
        return null;
    }

    /// <summary>Survival: everyone on one team against the waves, on the last skirmish map.</summary>
    public string? SetSurvival(AiDifficulty difficulty)
    {
        if (_skirmishMap is not { } m) return "Pick a map first.";
        LobbyMode before = State.Mode;
        State.Mode = LobbyMode.Survival;
        int usable = Capacity(m.Map);
        if (HumanCount > usable)
        {
            State.Mode = before;
            return $"{m.Map.Name} only has room for {usable} survivors.";
        }
        _coop = null;
        State.MissionKey = "";
        State.MissionTitle = "";
        State.Difficulty = difficulty == AiDifficulty.None ? AiDifficulty.Normal : difficulty;
        ApplyMap(m.Key, m.Map, usable);
        return null;
    }

    /// <summary>Brings slots in line with the mode: no computer players outside skirmish, fixed teams and factions where the mode says so.</summary>
    private void ApplyModeToSlots()
    {
        for (int i = 0; i < NetProtocol.MaxSlots; i++)
        {
            LobbySlot s = State.Slots[i];
            if (!State.AiAllowed && s.Kind == SlotKind.Ai) State.Slots[i] = s = FreshSlot(i);
            if (State.Mode == LobbyMode.Survival && s.Kind != SlotKind.Closed) s.Team = 0;
            if (State.Mode == LobbyMode.Coop && i < _coop?.HumanSeats.Count)
            {
                PlayerSetup seat = SeatSetup(i);
                s.Faction = seat.Faction;
                s.Team = seat.Team;
            }
            else if (State.Mode == LobbyMode.Skirmish && s.Kind == SlotKind.Open) s.Team = i;
        }
    }

    /// <summary>The mission's own setup for a human seat (faction and team are the mission's, not the player's).</summary>
    private PlayerSetup SeatSetup(int seat)
    {
        int index = _coop is not null && seat < _coop.HumanSeats.Count ? _coop.HumanSeats[seat] : -1;
        return index >= 0 && index < _coopFull.Count ? _coopFull[index] : new PlayerSetup("", 0, "lattice", 0);
    }

    /// <summary>An empty slot with the current mode's team and faction.</summary>
    private LobbySlot FreshSlot(int slot)
    {
        var s = new LobbySlot { Team = slot };
        if (State.Mode == LobbyMode.Survival) s.Team = 0;
        else if (State.Mode == LobbyMode.Coop)
        {
            PlayerSetup seat = SeatSetup(slot);
            s.Faction = seat.Faction;
            s.Team = seat.Team;
        }
        return s;
    }

    public void SetSlotKind(int slot, SlotKind kind, AiDifficulty ai = AiDifficulty.Normal)
    {
        if (!Usable(slot) || State.Slots[slot].Kind == SlotKind.Human || kind == SlotKind.Human) return;
        if (kind == SlotKind.Ai && !State.AiAllowed) return;
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
        if (!Usable(slot) || !HostControls(slot) || !State.FactionsFree) return;
        State.Slots[slot].Faction = faction == "crucible" ? "crucible" : "lattice";
        Publish();
    }

    public void SetTeam(int slot, int team)
    {
        if (!Usable(slot) || !HostControls(slot) || !State.TeamsFree) return;
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

    public void SetDifficulty(AiDifficulty difficulty)
    {
        State.Difficulty = difficulty == AiDifficulty.None ? AiDifficulty.Normal : difficulty;
        ResetReady();
        Publish();
    }

    public void Kick(int slot)
    {
        if (!Usable(slot)) return;
        LobbySlot s = State.Slots[slot];
        if (s.Kind != SlotKind.Human || s.Peer == INetTransport.HostPeer) return;
        Drop(s.Peer, "The host removed you from the game.");
        string name = s.Name;
        State.Slots[slot] = FreshSlot(slot);
        SystemChat($"{name} was removed by the host.");
        Publish();
    }

    public void KickSpectator(int index)
    {
        if (index < 0 || index >= State.Spectators.Count) return;
        LobbySpectator s = State.Spectators[index];
        Drop(s.Peer, "The host removed you from the game.");
        State.Spectators.RemoveAt(index);
        SystemChat($"{s.Name} was removed by the host.");
        Publish();
    }

    private void Drop(int peer, string reason)
    {
        _transport.Send(peer, NetProtocol.Build(Msg.Reject, w => w.Write(reason)));
        _transport.Flush();
        _transport.Kick(peer);
    }

    /// <summary>Host: moves a seated player to the spectator list.</summary>
    public void MoveToSpectators(int slot)
    {
        if (!Usable(slot) || State.Slots[slot].Kind != SlotKind.Human || State.Slots[slot].Peer == INetTransport.HostPeer) return;
        SetRole(State.Slots[slot].Peer, true);
    }

    /// <summary>Host: seats a spectator in the first open slot.</summary>
    public void Seat(int spectatorIndex)
    {
        if (spectatorIndex < 0 || spectatorIndex >= State.Spectators.Count) return;
        SetRole(State.Spectators[spectatorIndex].Peer, false);
    }

    private void SetRole(int peer, bool spectate)
    {
        int slot = State.SlotOfPeer(peer);
        int watcher = State.SpectatorOfPeer(peer);
        if (spectate && slot >= 0)
        {
            LobbySlot s = State.Slots[slot];
            if (State.Spectators.Count >= NetProtocol.MaxSpectators) { Tell(peer, "No room for more spectators."); return; }
            State.Spectators.Add(new LobbySpectator { Peer = peer, Name = s.Name, Faction = s.Faction, PingMs = s.PingMs });
            State.Slots[slot] = FreshSlot(slot);
            SystemChat($"{s.Name} is now watching.");
            Publish();
        }
        else if (!spectate && watcher >= 0)
        {
            int open = UsableSlots().FirstOrDefault(i => State.Slots[i].Kind == SlotKind.Open, -1);
            if (open < 0) { Tell(peer, "There's no open slot to play in. Ask the host to open one."); return; }
            LobbySpectator w = State.Spectators[watcher];
            State.Spectators.RemoveAt(watcher);
            Occupy(open, peer, w.Name, w.Faction, w.PingMs);
            SystemChat($"{w.Name} is now playing.");
            Publish();
        }
    }

    public void SendChat(string text)
    {
        text = NetProtocol.Clean(text, 200);
        if (text.Length > 0) RelayChat(0, State.Slots[0].Name, text);
    }

    /// <summary>Whether the match can start; otherwise why not.</summary>
    public string? StartProblem()
    {
        if (Map is null) return State.Mode == LobbyMode.Coop ? "Pick a mission." : "Pick a map.";
        var players = UsableSlots().Where(i => State.Slots[i].IsPlayer).Select(i => State.Slots[i]).ToList();
        switch (State.Mode)
        {
            case LobbyMode.Coop:
                if (_coop is null) return "Pick a mission.";
                break;
            case LobbyMode.Survival:
                if (players.Count < 1) return "You need at least one player.";
                break;
            default:
                if (players.Count < 2) return "You need at least two players.";
                if (players.Select(p => p.Team).Distinct().Count() < 2) return "Everyone is on the same team.";
                break;
        }
        var waiting = players.Where(p => p.Kind == SlotKind.Human && !p.Ready).Select(p => p.Name).ToList();
        if (waiting.Count > 0) return $"Waiting for {string.Join(", ", waiting)} to get ready.";
        return null;
    }

    /// <summary>Builds the scenario, tells every client (spectators too), and hands the connection to the lockstep host.</summary>
    public NetMatchStart StartMatch(ulong seed)
    {
        if (Map is null) throw new InvalidOperationException("No map chosen");
        if (_channel is not null) throw new InvalidOperationException("Already started");

        var peerToPlayer = new Dictionary<int, int>();
        int hostPlayer = 0;
        Scenario scenario = State.Mode switch
        {
            LobbyMode.Coop => BuildCoop(seed, peerToPlayer, out hostPlayer),
            LobbyMode.Survival => BuildSurvival(seed, peerToPlayer, out hostPlayer),
            _ => BuildSkirmish(seed, peerToPlayer, out hostPlayer),
        };
        byte[] encoded = NetProtocol.EncodeScenario(scenario, _fingerprint);
        // Everyone, the host included, builds the match from the same bytes.
        Scenario decoded = NetProtocol.DecodeScenario(encoded, out _);

        var names = decoded.Players.Select(p => p.Name).ToList();
        var spectators = State.Spectators.Select(s => (s.Peer, s.Name)).ToList();
        var channel = new LockstepHost(_transport, _clock, hostPlayer, names, peerToPlayer, spectators: spectators);
        void SendStart(int peer, int player) => _transport.Send(peer, NetProtocol.Build(Msg.Start, w =>
        {
            w.Write(player);
            w.Write(hostPlayer);
            w.Write((byte)channel.Delay);
            w.Write(encoded.Length);
            w.Write(encoded);
            w.Write((byte)spectators.Count);
            foreach (var (_, name) in spectators) w.Write(name);
        }));
        foreach (var (peer, player) in peerToPlayer) SendStart(peer, player);
        foreach (var (peer, _) in spectators) SendStart(peer, NetProtocol.SpectatorPlayer);
        foreach (int peer in _greeting)
        {
            _transport.Send(peer, NetProtocol.Build(Msg.Reject, w => w.Write("That game has already started.")));
            _transport.Kick(peer);
        }
        _greeting.Clear();
        _transport.Flush();
        _channel = channel;
        return new NetMatchStart(decoded, hostPlayer, channel, NetMatchStart.TitleFor(State.Mode, State.MissionTitle));
    }

    private Scenario BuildSkirmish(ulong seed, Dictionary<int, int> peerToPlayer, out int hostPlayer)
    {
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
        hostPlayer = 0;
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
        return new Scenario(Map!, seed, players, options, new List<Spawn>());
    }

    /// <summary>
    /// Co-op: the mission builds the scenario for however many humans are seated; the humans take
    /// the mission's human seats in slot order and lend them their names.
    /// </summary>
    private Scenario BuildCoop(ulong seed, Dictionary<int, int> peerToPlayer, out int hostPlayer)
    {
        if (_coop is null) throw new InvalidOperationException("No mission chosen");
        var humans = UsableSlots().Where(i => State.Slots[i].Kind == SlotKind.Human).Select(i => State.Slots[i]).ToList();
        Scenario built = _coop.Build(humans.Count);
        var players = built.Players.ToList();
        hostPlayer = 0;
        for (int i = 0; i < humans.Count && i < _coop.HumanSeats.Count; i++)
        {
            int index = _coop.HumanSeats[i];
            if (index < 0 || index >= players.Count) continue;
            players[index] = players[index] with { Name = humans[i].Name, Ai = AiDifficulty.None };
            if (humans[i].Peer == INetTransport.HostPeer) hostPlayer = index;
            else peerToPlayer[humans[i].Peer] = index;
        }
        return built with { Players = players };
    }

    /// <summary>Survival: humans on team 0 at the first start positions, then the Horde slot, waves from the mission code.</summary>
    private Scenario BuildSurvival(ulong seed, Dictionary<int, int> peerToPlayer, out int hostPlayer)
    {
        var humans = new List<PlayerSetup>();
        hostPlayer = 0;
        foreach (int slot in UsableSlots().Where(i => State.Slots[i].Kind == SlotKind.Human))
        {
            LobbySlot s = State.Slots[slot];
            int index = humans.Count;
            humans.Add(new PlayerSetup(s.Name, 0, s.Faction, slot));
            if (s.Peer == INetTransport.HostPeer) hostPlayer = index;
            else peerToPlayer[s.Peer] = index;
        }
        List<PlayerSetup> players = Survival.Players(humans, Map!);
        string json = Survival.BuildJson?.Invoke(Map!, players, State.Difficulty, unchecked((int)seed)) ?? "";
        return new Scenario(Map!, seed, players, Survival.Options(State.StartResources), new List<Spawn>(), json);
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
        int watcher = State.SpectatorOfPeer(e.Peer);
        switch (type)
        {
            case Msg.Hello:
                Greet(e.Peer, r);
                break;
            case Msg.SlotRequest when slot >= 0:
            {
                string faction = r.ReadString();
                int team = r.ReadByte();
                if (State.FactionsFree) State.Slots[slot].Faction = faction == "crucible" ? "crucible" : "lattice";
                if (State.TeamsFree) State.Slots[slot].Team = Math.Clamp(team, 0, 7);
                State.Slots[slot].Ready = false;
                Publish();
                break;
            }
            case Msg.SlotRequest when watcher >= 0:
                State.Spectators[watcher].Faction = r.ReadString() == "crucible" ? "crucible" : "lattice";
                break;
            case Msg.ReadyFlag when slot >= 0:
                State.Slots[slot].Ready = r.ReadBoolean();
                Publish();
                break;
            case Msg.RoleRequest when slot >= 0 || watcher >= 0:
                SetRole(e.Peer, r.ReadBoolean());
                break;
            case Msg.Chat when slot >= 0:
                RelayChat(slot, State.Slots[slot].Name, NetProtocol.Clean(r.ReadString(), 200));
                break;
            case Msg.Chat when watcher >= 0:
                RelayChat(NetProtocol.SpectatorChat, State.Spectators[watcher].Name, NetProtocol.Clean(r.ReadString(), 200));
                break;
            case Msg.Pong when slot >= 0:
                State.Slots[slot].PingMs = (int)Math.Round(Math.Max(0, _clock() - r.ReadDouble()) * 1000);
                _pingsChanged = true;
                break;
            case Msg.Pong when watcher >= 0:
                State.Spectators[watcher].PingMs = (int)Math.Round(Math.Max(0, _clock() - r.ReadDouble()) * 1000);
                _pingsChanged = true;
                break;
            case Msg.Leave:
                _greeting.Remove(e.Peer);
                RemovePeer(e.Peer, "left");
                _transport.Kick(e.Peer);
                break;
        }
    }

    private void Greet(int peer, BinaryReader r)
    {
        if (!_greeting.Remove(peer) || State.SlotOfPeer(peer) >= 0 || State.SpectatorOfPeer(peer) >= 0) return;
        // Read the version before anything else: an older client's Hello may be shorter than ours.
        int version = r.ReadInt32();
        if (version != NetProtocol.Version)
        {
            Refuse(peer, "Your game version doesn't match the host's.");
            return;
        }
        ulong fingerprint = r.ReadUInt64();
        string name = CleanName(r.ReadString());
        string faction = r.ReadString() == "crucible" ? "crucible" : "lattice";
        bool wantsToWatch = r.ReadBoolean();
        if (fingerprint != _fingerprint)
        {
            Refuse(peer, "Your unit data doesn't match the host's (different game version or mods).");
            return;
        }

        int slot = wantsToWatch ? -1 : UsableSlots().FirstOrDefault(i => State.Slots[i].Kind == SlotKind.Open, -1);
        bool watch = wantsToWatch || slot < 0;
        if (watch && State.Spectators.Count >= NetProtocol.MaxSpectators)
        {
            Refuse(peer, wantsToWatch ? "No room for more spectators." : "The game is full.");
            return;
        }

        string unique = name;
        for (int n = 2; NameTaken(unique); n++) unique = $"{name} ({n})";
        if (watch)
        {
            State.Spectators.Add(new LobbySpectator { Peer = peer, Name = unique, Faction = faction });
        }
        else
        {
            Occupy(slot, peer, unique, faction, 0);
        }
        _transport.Send(peer, NetProtocol.Build(Msg.Welcome, w => w.Write(peer)));
        if (_mapMessage is not null) _transport.Send(peer, _mapMessage);
        SystemChat(watch
            ? (wantsToWatch ? $"{unique} is watching." : $"{unique} joined as a spectator: every slot is taken.")
            : $"{unique} joined.");
        Publish();
    }

    private bool NameTaken(string name) =>
        State.Slots.Any(s => s.Kind == SlotKind.Human && s.Name == name) || State.Spectators.Any(s => s.Name == name);

    /// <summary>Puts a human in a slot with the mode's team and faction rules applied.</summary>
    private void Occupy(int slot, int peer, string name, string faction, int ping)
    {
        LobbySlot s = FreshSlot(slot);
        s.Kind = SlotKind.Human;
        s.Peer = peer;
        s.Name = name;
        s.PingMs = ping;
        if (State.FactionsFree) s.Faction = faction;
        State.Slots[slot] = s;
    }

    private void Refuse(int peer, string problem)
    {
        _transport.Send(peer, NetProtocol.Build(Msg.Reject, w => w.Write(problem)));
        _transport.Flush();
        _transport.Kick(peer);
    }

    /// <summary>A private note to one person (shown as a system line on their side only).</summary>
    private void Tell(int peer, string text)
    {
        _transport.Send(peer, NetProtocol.Build(Msg.Chat, w =>
        {
            w.Write(NetProtocol.SystemChat);
            w.Write("");
            w.Write(text);
        }));
    }

    private void RemovePeer(int peer, string verb)
    {
        int slot = State.SlotOfPeer(peer);
        if (slot >= 0)
        {
            string name = State.Slots[slot].Name;
            State.Slots[slot] = FreshSlot(slot);
            SystemChat($"{name} {verb}.");
            Publish();
            return;
        }
        int watcher = State.SpectatorOfPeer(peer);
        if (watcher < 0) return;
        string who = State.Spectators[watcher].Name;
        State.Spectators.RemoveAt(watcher);
        SystemChat($"{who} {verb}.");
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

    private void SystemChat(string text) => RelayChat(NetProtocol.SystemChat, "", text);

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

    /// <summary>Every remote person in the lobby: seated players and spectators.</summary>
    private IEnumerable<int> JoinedPeers() =>
        State.Slots.Where(s => s.Kind == SlotKind.Human && s.Peer != INetTransport.HostPeer).Select(s => s.Peer)
            .Concat(State.Spectators.Select(s => s.Peer)).ToList();

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
/// A client's lobby: says hello (as a player or a spectator), mirrors the host's lobby state and
/// map, sends this person's choices, and when the host starts, builds a <see cref="LockstepClient"/>
/// from the scenario.
/// </summary>
public sealed class LobbyClient : IDisposable
{
    private readonly INetTransport _transport;
    private readonly Func<double> _clock;
    private readonly ulong _fingerprint;
    private readonly string _name;
    private readonly string _faction;
    private readonly bool _wantsToWatch;
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
    /// <summary>My place in the spectator list, or -1 when seated (or not in yet).</summary>
    public int MySpectatorIndex => State is null ? -1 : State.SpectatorOfPeer(_myPeer);
    public bool IsSpectator => MySpectatorIndex >= 0;
    public bool HandedOff => _channel is not null;

    public event Action? Changed;
    public event Action<int, string, string>? Chat;
    /// <summary>Couldn't join, was refused, or lost the host. Raised once.</summary>
    public event Action<string>? Failed;
    public event Action<NetMatchStart>? Started;

    public LobbyClient(INetTransport transport, Func<double> clock, string name, string faction, ulong catalogFingerprint, bool spectate = false)
    {
        _transport = transport;
        _clock = clock;
        _name = name;
        _faction = faction;
        _fingerprint = catalogFingerprint;
        _wantsToWatch = spectate;
    }

    public void SetChoice(string faction, int team) =>
        Send(NetProtocol.Build(Msg.SlotRequest, w =>
        {
            w.Write(faction);
            w.Write((byte)Math.Clamp(team, 0, 7));
        }));

    public void SetReady(bool ready) => Send(NetProtocol.Build(Msg.ReadyFlag, w => w.Write(ready)));

    /// <summary>Ask to watch instead of play, or to take an open slot.</summary>
    public void SetSpectating(bool spectate) => Send(NetProtocol.Build(Msg.RoleRequest, w => w.Write(spectate)));

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
                    w.Write(_wantsToWatch);
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
                int watchers = r.ReadByte();
                var spectators = new List<string>(watchers);
                for (int i = 0; i < watchers; i++) spectators.Add(r.ReadString());
                if (fingerprint != _fingerprint)
                {
                    Fail("Your unit data doesn't match the host's.");
                    return;
                }
                if (localPlayer >= scenario.Players.Count) localPlayer = NetProtocol.SpectatorPlayer;
                var names = scenario.Players.Select(p => p.Name).ToList();
                _channel = new LockstepClient(_transport, _clock, localPlayer, hostPlayer, names, delay, spectators);
                string title = State is null ? "Multiplayer" : NetMatchStart.TitleFor(State.Mode, State.MissionTitle);
                Started?.Invoke(new NetMatchStart(scenario, localPlayer, _channel, title));
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
