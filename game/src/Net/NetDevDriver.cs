using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Match;
using ContingencyZero.Game.UI;
using ContingencyZero.Sim.Match;
using Godot;

namespace ContingencyZero.Game.Net;

/// <summary>
/// Command-line hooks for testing multiplayer with two or more game instances on one machine.
/// Ignored in normal play. Flags (after the double dash):
///   --dev-host[=port]            open the multiplayer screen and host
///   --dev-join=addr[:port]       open the multiplayer screen and join (retries until the host is up)
///   --dev-net-name=Name          player name for this instance
///   --dev-net-faction=crucible   faction for this instance
///   --dev-net-team=N             (client) team number, 1-based
///   --dev-net-map=key            (host) map key, e.g. crater_basin or user:mymap.json
///   --dev-net-ai=hard[,easy...]  (host) fill the last slots with computer players
///   --dev-net-players=N          (host) humans to wait for before autostart (default 2)
///   --dev-net-autostart=S        (host) start the match once everyone is ready and S seconds have passed
///   --dev-net-ready              (client) press Ready after joining
///   --dev-net-orders             issue pseudo-random orders for this player during the match
///   --dev-net-chat=text          say this in the lobby and again a few seconds into the match
///   --dev-net-log=file           append "tick hash" lines (one per second of game time) and net events
///   --dev-net-quit-at=S          leave the match S seconds after it starts (tests disconnects)
///   --dev-net-backdrop           show the main menu's AI battle behind the lobby (slow on weak machines)
///   --dev-net-lowgfx             cheaper 3D (no MSAA, half-res 3D, small shadow map) for several instances on one box
///   --dev-net-event-shots=dir    screenshot shortly after a player leaves, the host is lost, or a desync
///   --dev-net-desync-at=S        S seconds into the match, secretly turn this player's converters off on this
///                                machine only, to check desync detection end to end
/// Screenshots and the overall time limit come from DevAutomation (--dev-shots, --dev-shot-times, --dev-seconds).
/// </summary>
public partial class NetDevDriver : Node
{
    private readonly Dictionary<string, string> _args;
    private MultiplayerLobby? _lobby;
    private double _elapsed;
    private double _retryAt = -1;
    private double _matchStarted = -1;
    private double _nextOrder;
    private bool _readySent;
    private bool _choiceSent;
    private bool _lobbyChatSent;
    private bool _matchChatSent;
    private bool _quit;
    private MatchScene? _match;
    private NetOrderBot? _bot;
    private string _log = "";
    private double _waitStart = -1;
    private bool _waitShot;
    private int _ordersIssued;
    private bool _desyncSent;
    private bool _chatShot;
    private readonly List<(double At, string Name)> _eventShots = new();

    public NetDevDriver(Dictionary<string, string> args) { _args = args; }

    public static bool Wants(IReadOnlyDictionary<string, string> args) =>
        args.ContainsKey("--dev-host") || args.ContainsKey("--dev-join");

    private string Arg(string name, string fallback = "") => _args.TryGetValue(name, out var v) && v.Length > 0 ? v : fallback;
    private double Num(string name, double fallback) =>
        double.TryParse(Arg(name), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : fallback;

    public override void _Ready()
    {
        _log = Arg("--dev-net-log");
        if (Arg("--dev-net-name") is { Length: > 0 } name) Settings.Current.PlayerName = name;
        if (Arg("--dev-net-faction") is { Length: > 0 } faction) Settings.Current.LastFaction = faction;
        if (_args.ContainsKey("--dev-net-lowgfx"))
        {
            // Two or more instances on one software-rendered test box: cheapen the 3D view.
            Viewport vp = GetViewport();
            vp.Msaa3D = Viewport.Msaa.Disabled;
            vp.Scaling3DScale = 0.5f;
            RenderingServer.DirectionalShadowAtlasSetSize(1024, true);
        }
        GameRoot root = GameRoot.Instance;
        // The menu's AI battle backdrop costs a lot of CPU on slow test machines; opt in.
        if (_args.ContainsKey("--dev-net-backdrop")) root.ShowMainMenu();
        root.ShowMultiplayer();
        _lobby = root.GetChildren().OfType<MultiplayerLobby>().LastOrDefault();
        if (_lobby is null) { Note("no lobby screen"); return; }

        if (_args.ContainsKey("--dev-host"))
        {
            int port = (int)Num("--dev-host", NetProtocol.DefaultPort);
            if (_lobby.HostGame(port) is { } err) { Note("host failed: " + err); return; }
            LobbyHost host = _lobby.HostLobby!;
            if (Arg("--dev-net-map") is { Length: > 0 } key && GameData.Maps().FirstOrDefault(m => m.Key == key) is { } entry)
                host.SetMap(entry.Key, entry.Load());
            string[] ais = Arg("--dev-net-ai").Split(',', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < ais.Length; i++)
            {
                int slot = host.State.MapPlayers - ais.Length + i;
                if (slot <= 0) continue;
                AiDifficulty ai = Enum.TryParse(ais[i], true, out AiDifficulty d) && d != AiDifficulty.None ? d : AiDifficulty.Normal;
                host.SetSlotKind(slot, SlotKind.Ai, ai);
                host.SetTeam(slot, 1);
            }
            Note($"hosting on {port}");
        }
        else
        {
            _retryAt = 0;
        }
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        for (int i = _eventShots.Count - 1; i >= 0; i--)
        {
            if (_elapsed < _eventShots[i].At) continue;
            if (Arg("--dev-net-event-shots") is { Length: > 0 } dir)
            {
                DirAccess.MakeDirRecursiveAbsolute(dir);
                string path = $"{dir}/{_eventShots[i].Name}.png";
                GetViewport().GetTexture().GetImage().SavePng(path);
                Note($"saved {path}");
            }
            _eventShots.RemoveAt(i);
        }
        if (_lobby is not null && !IsInstanceValid(_lobby)) _lobby = null;

        if (_lobby is not null && _args.TryGetValue("--dev-join", out string? target))
        {
            LobbyClient? client = _lobby.ClientLobby;
            if (client is null && _retryAt >= 0 && _elapsed >= _retryAt)
            {
                _retryAt = _elapsed + 2;
                Note($"joining {target}");
                _lobby.JoinGame(target.Length > 0 ? target : "127.0.0.1");
            }
            else if (client is { Joined: true, MySlot: >= 0, State: { } s })
            {
                _retryAt = -1;
                if (!_choiceSent && Arg("--dev-net-team") is { Length: > 0 } team)
                {
                    _choiceSent = true;
                    client.SetChoice(Settings.Current.LastFaction, int.Parse(team, CultureInfo.InvariantCulture) - 1);
                }
                if (_args.ContainsKey("--dev-net-ready") && !s.Slots[client.MySlot].Ready && _elapsed > 1.5 && (!_readySent || _elapsed % 2 < delta))
                {
                    _readySent = true;
                    client.SetReady(true);
                }
                if (!_lobbyChatSent && Arg("--dev-net-chat") is { Length: > 0 } say && _elapsed > 2)
                {
                    _lobbyChatSent = true;
                    client.SendChat(say);
                }
            }
            else if (client is null && _retryAt < 0)
            {
                _retryAt = _elapsed + 2; // failed (host not up yet?): try again shortly
            }
        }

        if (_lobby?.HostLobby is { HandedOff: false } host)
        {
            if (!_lobbyChatSent && Arg("--dev-net-chat") is { Length: > 0 } say && host.HumanCount > 1)
            {
                _lobbyChatSent = true;
                host.SendChat(say);
            }
            int want = (int)Num("--dev-net-players", 2);
            if (_args.ContainsKey("--dev-net-autostart") && _elapsed >= Num("--dev-net-autostart", 8)
                && host.HumanCount >= want && host.StartProblem() is null)
            {
                Note("starting the match");
                _lobby.StartMatch();
            }
        }

        _match ??= FindMatch();
        if (_match is null) return;
        if (!IsInstanceValid(_match)) { _match = null; return; }
        SimSession session = _match.Session;
        if (session.Channel is not LockstepChannel channel) return;
        if (_matchStarted < 0)
        {
            _matchStarted = _elapsed;
            _nextOrder = _elapsed + 3;
            _bot = new NetOrderBot((int)(Time.GetTicksUsec() & 0x7fffffff));
            channel.HashComputed += (tick, hash) => Note($"hash tick={tick} {hash:X16}");
            channel.Desynced += d => { Note($"DESYNC tick={d.Tick} host={d.HostHash:X16} peer={d.PeerHash:X16} player={d.Player}"); _eventShots.Add((_elapsed + 1.5, "desync")); };
            channel.PlayerLeft += p => { Note($"player left: {channel.NameOf(p)}"); _eventShots.Add((_elapsed + 1.5, "player_left")); };
            channel.Lost += why => { Note($"lost: {why}"); _eventShots.Add((_elapsed + 1.5, "lost")); };
            channel.ChatReceived += (p, n, t) =>
            {
                Note($"chat from {n}: {t}");
                if (!_chatShot) { _chatShot = true; _eventShots.Add((_elapsed + 1.0, "chat")); }
            };
            Note($"match started: I am player {session.LocalPlayer} of {session.Scenario.Players.Count}, seed {session.Scenario.Seed:X}, map {session.World.Map.Name}");
        }

        double inMatch = _elapsed - _matchStarted;
        if (session.Waiting && channel.Started)
        {
            if (_waitStart < 0) _waitStart = _elapsed;
            if (!_waitShot && _elapsed - _waitStart > 1.5)
            {
                _waitShot = true;
                _eventShots.Add((_elapsed, "waiting"));
            }
        }
        else if (_waitStart >= 0)
        {
            double waited = _elapsed - _waitStart;
            if (waited > 0.25) Note($"stalled {waited:0.00}s at tick {session.World.Tick}");
            _waitStart = -1;
        }

        if (_args.ContainsKey("--dev-net-orders") && channel.Started && _elapsed >= _nextOrder && _bot is not null)
        {
            _nextOrder = _elapsed + 1.2;
            int before = _ordersIssued;
            _bot.Act(session.World, session.LocalPlayer, c => { session.Issue(c); _ordersIssued++; }, 3);
            if (_ordersIssued / 25 != before / 25) Note($"orders issued so far: {_ordersIssued}");
        }
        if (!_matchChatSent && Arg("--dev-net-chat") is { Length: > 0 } line && inMatch > 6 && channel.Started)
        {
            _matchChatSent = true;
            channel.SendChat(line);
        }
        if (!_desyncSent && Num("--dev-net-desync-at", -1) is var ds && ds > 0 && inMatch >= ds && channel.Started)
        {
            _desyncSent = true;
            Note($"deliberately desyncing this machine at tick {session.World.Tick}");
            channel.DevDesyncNextTick(new Sim.Core.Command(0, session.LocalPlayer, Sim.Core.CommandType.SetConverters, -1, Arg: 0));
        }
        if (!_quit && Num("--dev-net-quit-at", -1) is var q && q > 0 && inMatch >= q)
        {
            _quit = true;
            Note($"quitting the match at tick {session.World.Tick}");
            _match.Quit();
            _match = null;
        }
    }

    private MatchScene? FindMatch() =>
        GameRoot.Instance.GetChildren().OfType<MatchScene>()
            .FirstOrDefault(m => !m.IsQueuedForDeletion() && m.Kind == MatchKind.Multiplayer);

    private void Note(string text)
    {
        string line = $"{_elapsed,7:0.00} {text}";
        GD.Print($"NETDEV {line}");
        if (_log.Length == 0) return;
        try
        {
            File.AppendAllText(_log, line + "\n");
        }
        catch (IOException)
        {
            // Logging is best effort.
        }
    }
}
