using System;
using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Net;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using Godot;

namespace ContingencyZero.Game.UI;

/// <summary>
/// Host or join a networked game, then the lobby: map, slots (open, closed, people, computer
/// players), factions and teams, options, chat and ready flags. The host starts the match.
/// </summary>
public partial class MultiplayerLobby : CanvasLayer
{
    private const double ConnectTimeout = 10;
    private static readonly string[] SlotChoices = { "Open", "Closed", "Easy AI", "Normal AI", "Hard AI", "Brutal AI" };
    private static readonly int[] ResourceChoices = { 1000, 2500, 5000 };

    private Control _root = null!;
    private Control? _page;
    private LobbyHost? _host;
    private LobbyClient? _client;
    private string _status = "";
    private bool _statusIsError;
    private string _joinTarget = "";
    private int _port = NetProtocol.DefaultPort;
    private double _connectStarted;

    // Lobby page.
    private List<MapEntry> _maps = new();
    private ItemList? _mapList;
    private int _mapIndex = -1;
    private TextureRect _preview = null!;
    private Label _mapInfo = null!;
    private ulong _previewKey;
    private readonly List<SlotRowUi> _rows = new();
    private OptionButton _resources = null!;
    private OptionButton _win = null!;
    private OptionButton _starts = null!;
    private RichTextLabel _chatLog = null!;
    private LineEdit _chatInput = null!;
    private Button? _readyButton;
    private Button? _startButton;
    private Label _hint = null!;
    private bool _refreshing;
    private readonly List<string> _chatLines = new();

    /// <summary>The host side of the lobby while hosting (for tests and dev automation).</summary>
    public LobbyHost? HostLobby => _host;
    /// <summary>The client side of the lobby while joined (for tests and dev automation).</summary>
    public LobbyClient? ClientLobby => _client;

    private sealed class SlotRowUi
    {
        public required ColorRect Swatch;
        public required HBoxContainer Who;
        public required OptionButton Faction;
        public required OptionButton Team;
        public required Label Ping;
        public required Label State;
        public required HBoxContainer Row;
        public string WhoKey = "";
    }

    public override void _Ready()
    {
        Layer = 10;
        NetGame.RouteLogs();
        _root = new Control { Theme = UiTheme.Theme };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);
        var shade = new ColorRect { Color = new Color(0.02f, 0.03f, 0.05f, 0.85f) };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(shade);
        ShowStartPage();
    }

    public override void _Process(double delta)
    {
        _host?.Poll();
        _client?.Poll();
        if (_client is { Connected: false, HandedOff: false } c && _page is not null && NetGame.Now() - _connectStarted > ConnectTimeout)
        {
            _client = null;
            c.Dispose();
            Fail($"Couldn't reach {_joinTarget}. Check the address, and that the host's port is open.");
        }
    }

    public override void _ExitTree()
    {
        if (_host is { HandedOff: false }) _host.Dispose();
        if (_client is { HandedOff: false }) _client.Dispose();
        _host = null;
        _client = null;
    }

    private void SetPage(Control page)
    {
        _page?.QueueFree();
        _page = page;
        _root.AddChild(page);
    }

    // ---- Start page: host or join ------------------------------------------------------------

    private void ShowStartPage()
    {
        _rows.Clear();
        _mapList = null;
        _readyButton = null;
        _startButton = null;

        var outer = new VBoxContainer();
        outer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        outer.OffsetLeft = 80; outer.OffsetTop = 50; outer.OffsetRight = -80; outer.OffsetBottom = -50;
        outer.AddThemeConstantOverride("separation", 16);
        outer.AddChild(UiTheme.Label("MULTIPLAYER", 36, UiTheme.Ink));
        outer.AddChild(UiTheme.Label("Play against friends over a local network, or over the internet if the host opens a port.", 16, UiTheme.Dim));

        // Who you are.
        var you = new HBoxContainer();
        you.AddThemeConstantOverride("separation", 12);
        you.AddChild(Centered(UiTheme.Label("Your name", 16)));
        var name = new LineEdit { Text = Settings.Current.PlayerName, MaxLength = 20, CustomMinimumSize = new Vector2(260, 0) };
        name.TextChanged += t => Settings.Current.PlayerName = t.Trim().Length == 0 ? "Commander" : t.Trim();
        you.AddChild(name);
        you.AddChild(new Control { CustomMinimumSize = new Vector2(24, 0) });
        you.AddChild(Centered(UiTheme.Label("Faction", 16)));
        var faction = FactionPicker();
        faction.Select(Settings.Current.LastFaction == "crucible" ? 1 : 0);
        faction.ItemSelected += f => Settings.Current.LastFaction = f == 1 ? "crucible" : "lattice";
        you.AddChild(faction);
        outer.AddChild(UiTheme.Panel(you));

        var cols = new HBoxContainer();
        cols.AddThemeConstantOverride("separation", 20);
        outer.AddChild(cols);

        // Host.
        var hostCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        hostCol.AddThemeConstantOverride("separation", 10);
        hostCol.AddChild(UiTheme.Label("HOST A GAME", 15, UiTheme.Dim));
        hostCol.AddChild(Wrapped(UiTheme.Label("You pick the map and the rules; other players join using your IP address.", 15)));
        var portRow = new HBoxContainer();
        portRow.AddThemeConstantOverride("separation", 10);
        portRow.AddChild(Centered(UiTheme.Label("Port", 16)));
        var portEdit = new LineEdit { Text = _port.ToString(), MaxLength = 5, CustomMinimumSize = new Vector2(100, 0) };
        portRow.AddChild(portEdit);
        hostCol.AddChild(portRow);
        var hostButton = UiTheme.Button("Host game", () =>
        {
            int port = int.TryParse(portEdit.Text.Trim(), out int p) && p is > 0 and < 65536 ? p : NetProtocol.DefaultPort;
            if (HostGame(port) is { } err) Fail(err);
        }, 220, 19);
        hostButton.AddThemeColorOverride("font_color", UiTheme.Amber);
        hostButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        hostCol.AddChild(hostButton);
        string lan = string.Join(", ", LanAddresses());
        hostCol.AddChild(Wrapped(UiTheme.Label(lan.Length > 0 ? $"Your LAN address: {lan}" : "No network address found (only this computer can join).", 14, UiTheme.Ink)));
        hostCol.AddChild(Wrapped(UiTheme.Label("Internet play needs the port (UDP) forwarded on the host's router.", 14, UiTheme.Dim)));
        cols.AddChild(Grow(UiTheme.Panel(hostCol, 18), 1));

        // Join.
        var joinCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        joinCol.AddThemeConstantOverride("separation", 10);
        joinCol.AddChild(UiTheme.Label("JOIN A GAME", 15, UiTheme.Dim));
        joinCol.AddChild(Wrapped(UiTheme.Label("Ask the host for their IP address (add :port if they changed it).", 15)));
        var addrRow = new HBoxContainer();
        addrRow.AddThemeConstantOverride("separation", 10);
        addrRow.AddChild(Centered(UiTheme.Label("Address", 16)));
        var addr = new LineEdit { Text = Settings.Current.LastHost, PlaceholderText = "192.168.1.20", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        addrRow.AddChild(addr);
        joinCol.AddChild(addrRow);
        void Join()
        {
            string a = addr.Text.Trim();
            if (a.Length == 0) { Fail("Type the host's address first."); return; }
            Settings.Current.LastHost = a;
            Settings.Current.Save();
            JoinGame(a);
        }
        addr.TextSubmitted += _ => Join();
        var joinButton = UiTheme.Button("Join game", Join, 220, 19);
        joinButton.AddThemeColorOverride("font_color", UiTheme.Amber);
        joinButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        joinCol.AddChild(joinButton);
        cols.AddChild(Grow(UiTheme.Panel(joinCol, 18), 1));

        var status = Wrapped(UiTheme.Label(_status, 16, _statusIsError ? UiTheme.Bad : UiTheme.Dim));
        outer.AddChild(status);
        outer.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin };
        buttons.AddChild(UiTheme.Button("Back", () => { Settings.Current.Save(); GameRoot.Instance.ShowMainMenu(); }, 140));
        outer.AddChild(buttons);
        SetPage(outer);
    }

    private void Fail(string message)
    {
        _status = message;
        _statusIsError = true;
        _host?.Dispose();
        _client?.Dispose();
        _host = null;
        _client = null;
        _chatLines.Clear();
        ShowStartPage();
    }

    /// <summary>Opens the lobby as host. Returns an error to show, or null.</summary>
    public string? HostGame(int port)
    {
        Settings.Current.Save();
        EnetTransport? t = EnetTransport.Host(port, NetProtocol.MaxSlots - 1, out string error);
        if (t is null) return error;
        _port = port;
        _host = new LobbyHost(t, NetGame.Now, Settings.Current.PlayerName, Settings.Current.LastFaction, GameData.Catalog.Fingerprint);
        _host.Changed += Refresh;
        _host.Chat += AddChat;
        _maps = GameData.Maps();
        int initial = Math.Max(0, _maps.FindIndex(m => m.Key == Settings.Current.LastMap));
        _host.SetMap(_maps[initial].Key, _maps[initial].Load());
        _mapIndex = initial;
        _status = "";
        ShowLobbyPage();
        AddChat(-1, "", $"Hosting on port {port}. Waiting for players to join.");
        return null;
    }

    /// <summary>Starts connecting to a host ("address" or "address:port").</summary>
    public void JoinGame(string target)
    {
        string address = target;
        int port = NetProtocol.DefaultPort;
        int colon = target.LastIndexOf(':');
        if (colon > 0 && target.IndexOf(':') == colon && int.TryParse(target[(colon + 1)..], out int p) && p is > 0 and < 65536)
        {
            address = target[..colon];
            port = p;
        }
        _joinTarget = $"{address}:{port}";
        EnetTransport? t = EnetTransport.Join(address, port, out string error);
        if (t is null) { Fail(error); return; }
        _client = new LobbyClient(t, NetGame.Now, Settings.Current.PlayerName, Settings.Current.LastFaction, GameData.Catalog.Fingerprint);
        _client.Changed += () =>
        {
            if (_client is { Joined: true } && _rows.Count == 0) ShowLobbyPage();
            Refresh();
        };
        _client.Chat += AddChat;
        _client.Failed += reason => Fail(reason);
        _client.Started += start => NetGame.Launch(start);
        _connectStarted = NetGame.Now();
        ShowConnectingPage();
    }

    private void ShowConnectingPage()
    {
        var centre = new CenterContainer();
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var col = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
        col.AddThemeConstantOverride("separation", 14);
        col.AddChild(UiTheme.Title("CONNECTING", 28));
        col.AddChild(UiTheme.Label($"Joining {_joinTarget}...", 17, UiTheme.Ink, HorizontalAlignment.Center));
        col.AddChild(UiTheme.Button("Cancel", () =>
        {
            _client?.Dispose();
            _client = null;
            _status = "";
            _statusIsError = false;
            ShowStartPage();
        }));
        centre.AddChild(UiTheme.Panel(col, 22));
        SetPage(centre);
    }

    // ---- Lobby page --------------------------------------------------------------------------

    private bool IsHost => _host is not null;
    private LobbyState? State => _host?.State ?? _client?.State;
    private MapData? Map => _host?.Map ?? _client?.Map;
    private int MySlot => _host is not null ? 0 : _client?.MySlot ?? -1;

    private void ShowLobbyPage()
    {
        _rows.Clear();
        _previewKey = 0;

        var outer = new VBoxContainer();
        outer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        outer.OffsetLeft = 40; outer.OffsetTop = 28; outer.OffsetRight = -40; outer.OffsetBottom = -28;
        outer.AddThemeConstantOverride("separation", 12);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 20);
        header.AddChild(UiTheme.Label("MULTIPLAYER LOBBY", 32, UiTheme.Ink));
        header.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var info = new VBoxContainer();
        info.AddThemeConstantOverride("separation", 0);
        if (IsHost)
        {
            string lan = string.Join(", ", LanAddresses());
            info.AddChild(UiTheme.Label(lan.Length > 0 ? $"Hosting on {lan}  ·  port {_port}" : $"Hosting on port {_port}", 15, UiTheme.Ink, HorizontalAlignment.Right));
            info.AddChild(UiTheme.Label($"Internet play: forward UDP port {_port} on your router to this computer.", 13, UiTheme.Dim, HorizontalAlignment.Right));
        }
        else
        {
            info.AddChild(UiTheme.Label($"Connected to {_joinTarget}", 15, UiTheme.Ink, HorizontalAlignment.Right));
            info.AddChild(UiTheme.Label("The host picks the map and rules. Set your faction and team, then get ready.", 13, UiTheme.Dim, HorizontalAlignment.Right));
        }
        header.AddChild(info);
        outer.AddChild(header);

        var cols = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        cols.AddThemeConstantOverride("separation", 14);
        outer.AddChild(cols);

        // Map.
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(290, 0) };
        left.AddThemeConstantOverride("separation", 8);
        left.AddChild(UiTheme.Label("MAP", 15, UiTheme.Dim));
        if (IsHost)
        {
            _mapList = new ItemList { CustomMinimumSize = new Vector2(0, 150), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            _mapList.AddThemeFontSizeOverride("font_size", 16);
            foreach (MapEntry m in _maps) _mapList.AddItem(m.Name);
            if (_mapIndex >= 0) _mapList.Select(_mapIndex);
            _mapList.ItemSelected += i => PickMap((int)i);
            left.AddChild(_mapList);
        }
        _preview = new TextureRect
        {
            CustomMinimumSize = new Vector2(270, 270),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        left.AddChild(_preview);
        _mapInfo = Wrapped(UiTheme.Label("", 14, UiTheme.Ink));
        _mapInfo.CustomMinimumSize = new Vector2(270, 0);
        left.AddChild(_mapInfo);
        if (!IsHost) left.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        cols.AddChild(UiTheme.Panel(left, 12));

        // Players and options.
        var middle = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        middle.AddThemeConstantOverride("separation", 6);
        var heads = new HBoxContainer();
        heads.AddThemeConstantOverride("separation", 8);
        heads.AddChild(Fixed(UiTheme.Label("PLAYERS", 15, UiTheme.Dim), 250));
        heads.AddChild(Fixed(UiTheme.Label("FACTION", 13, UiTheme.Dim), 128));
        heads.AddChild(Fixed(UiTheme.Label("TEAM", 13, UiTheme.Dim), 104));
        heads.AddChild(Fixed(UiTheme.Label("PING", 13, UiTheme.Dim), 64));
        heads.AddChild(UiTheme.Label("STATUS", 13, UiTheme.Dim));
        middle.AddChild(heads);
        for (int i = 0; i < NetProtocol.MaxSlots; i++) middle.AddChild(BuildRow(i));

        middle.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        middle.AddChild(UiTheme.Label("OPTIONS", 15, UiTheme.Dim));
        var opts = new GridContainer { Columns = 2 };
        opts.AddThemeConstantOverride("h_separation", 14);
        opts.AddThemeConstantOverride("v_separation", 6);
        opts.AddChild(UiTheme.Label("Starting resources", 16));
        _resources = new OptionButton { CustomMinimumSize = new Vector2(300, 0) };
        foreach (string r in new[] { "Standard (1,000)", "Boosted (2,500)", "Rich (5,000)" }) _resources.AddItem(r);
        _resources.ItemSelected += _ => OptionsChanged();
        opts.AddChild(_resources);
        opts.AddChild(UiTheme.Label("Victory", 16));
        _win = new OptionButton();
        _win.AddItem("Destroy the Commander");
        _win.AddItem("Annihilation (destroy everything)");
        _win.ItemSelected += _ => OptionsChanged();
        opts.AddChild(_win);
        opts.AddChild(UiTheme.Label("Start positions", 16));
        _starts = new OptionButton();
        _starts.AddItem("By slot (slot 1 at start 1...)");
        _starts.AddItem("Random");
        _starts.ItemSelected += _ => OptionsChanged();
        opts.AddChild(_starts);
        middle.AddChild(opts);
        middle.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        middle.AddChild(UiTheme.Label("Brutal AI sees the whole map and earns 50% more. Computer players run on every machine.", 13, UiTheme.Dim));
        cols.AddChild(Grow(UiTheme.Panel(middle, 12), 1.8f));

        // Chat.
        var right = new VBoxContainer { CustomMinimumSize = new Vector2(330, 0) };
        right.AddThemeConstantOverride("separation", 8);
        right.AddChild(UiTheme.Label("CHAT", 15, UiTheme.Dim));
        _chatLog = new RichTextLabel
        {
            BbcodeEnabled = true,
            ScrollFollowing = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SelectionEnabled = true,
        };
        _chatLog.AddThemeFontSizeOverride("normal_font_size", 15);
        _chatLog.AddThemeFontSizeOverride("italics_font_size", 14);
        _chatLog.AddThemeStyleboxOverride("normal", UiTheme.Box(new Color(0.03f, 0.04f, 0.05f, 0.9f), new Color(0.25f, 0.35f, 0.45f), 1, 3, 8));
        foreach (string line in _chatLines) _chatLog.AppendText(line);
        right.AddChild(_chatLog);
        var chatRow = new HBoxContainer();
        chatRow.AddThemeConstantOverride("separation", 6);
        _chatInput = new LineEdit { MaxLength = 200, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, PlaceholderText = "Say something" };
        _chatInput.TextSubmitted += _ => SendChat();
        chatRow.AddChild(_chatInput);
        chatRow.AddChild(UiTheme.Button("Send", SendChat, 70, 15));
        right.AddChild(chatRow);
        cols.AddChild(Grow(UiTheme.Panel(right, 12), 1));

        // Bottom bar.
        var bottom = new HBoxContainer();
        bottom.AddThemeConstantOverride("separation", 12);
        bottom.AddChild(UiTheme.Button("Leave", Leave, 140));
        _hint = UiTheme.Label("", 15, UiTheme.Dim, HorizontalAlignment.Right);
        _hint.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _hint.VerticalAlignment = VerticalAlignment.Center;
        bottom.AddChild(_hint);
        if (IsHost)
        {
            _startButton = UiTheme.Button("Start game", StartMatch, 220, 20);
            _startButton.AddThemeColorOverride("font_color", UiTheme.Amber);
            bottom.AddChild(_startButton);
        }
        else
        {
            _readyButton = UiTheme.Button("Ready", ToggleReady, 220, 20);
            bottom.AddChild(_readyButton);
        }
        outer.AddChild(bottom);

        SetPage(outer);
        Refresh();
    }

    private HBoxContainer BuildRow(int slot)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var swatch = new ColorRect { CustomMinimumSize = new Vector2(16, 32) };
        row.AddChild(swatch);
        row.AddChild(Fixed(Centered(UiTheme.Label($"{slot + 1}", 16, UiTheme.Dim)), 18));
        var who = new HBoxContainer { CustomMinimumSize = new Vector2(200, 0) };
        who.AddThemeConstantOverride("separation", 6);
        row.AddChild(who);
        var faction = FactionPicker();
        faction.CustomMinimumSize = new Vector2(128, 0);
        faction.ItemSelected += _ => SlotChanged(slot);
        row.AddChild(faction);
        var team = new OptionButton { CustomMinimumSize = new Vector2(104, 0) };
        for (int t = 0; t < NetProtocol.MaxSlots; t++) team.AddItem($"Team {t + 1}");
        team.ItemSelected += _ => SlotChanged(slot);
        row.AddChild(team);
        var ping = Fixed(Centered(UiTheme.Label("", 14, UiTheme.Dim)), 64);
        row.AddChild(ping);
        var state = Centered(UiTheme.Label("", 15));
        row.AddChild(state);
        _rows.Add(new SlotRowUi { Swatch = swatch, Who = who, Faction = faction, Team = team, Ping = ping, State = state, Row = row });
        return row;
    }

    /// <summary>Brings every control in line with the lobby state. Never sends anything.</summary>
    private void Refresh()
    {
        LobbyState? s = State;
        if (s is null || _rows.Count == 0 || _page is null) return;
        _refreshing = true;
        try
        {
            int mine = MySlot;
            int playerIndex = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                SlotRowUi row = _rows[i];
                LobbySlot slot = s.Slots[i];
                bool usable = i < s.MapPlayers;
                row.Row.Visible = usable;
                if (!usable) continue;

                row.Swatch.Color = slot.IsPlayer ? UiTheme.TeamColour(playerIndex++) : new Color(0.2f, 0.22f, 0.25f);
                RefreshWho(i, slot, mine);

                bool editable = i == mine || (IsHost && slot.Kind == SlotKind.Ai);
                row.Faction.Visible = slot.IsPlayer;
                row.Team.Visible = slot.IsPlayer;
                row.Faction.Select(slot.Faction == "crucible" ? 1 : 0);
                row.Team.Select(Math.Clamp(slot.Team, 0, NetProtocol.MaxSlots - 1));
                row.Faction.Disabled = !editable;
                row.Team.Disabled = !editable;

                row.Ping.Text = slot.Kind == SlotKind.Human && slot.Peer != INetTransport.HostPeer && slot.PingMs > 0 ? $"{slot.PingMs} ms" : "";
                (string text, Color colour) = slot.Kind switch
                {
                    SlotKind.Human when slot.Peer == INetTransport.HostPeer => ("Host", UiTheme.Accent),
                    SlotKind.Human => slot.Ready ? ("Ready", UiTheme.Good) : ("Not ready", UiTheme.Amber),
                    SlotKind.Ai => ("Computer", UiTheme.Dim),
                    _ => ("", UiTheme.Dim),
                };
                row.State.Text = text;
                row.State.AddThemeColorOverride("font_color", colour);
            }

            _resources.Select(Math.Max(0, Array.IndexOf(ResourceChoices, s.StartResources)));
            _win.Select(s.Win == WinCondition.Annihilation ? 1 : 0);
            _starts.Select(s.RandomStarts ? 1 : 0);
            _resources.Disabled = _win.Disabled = _starts.Disabled = !IsHost;
            if (_mapList is not null)
            {
                int shown = _maps.FindIndex(m => m.Key == s.MapKey);
                if (shown >= 0 && !_mapList.IsSelected(shown)) _mapList.Select(shown);
                if (shown >= 0) _mapIndex = shown;
            }

            RefreshMap(s);

            if (_host is not null && _startButton is not null)
            {
                string? problem = _host.StartProblem();
                _startButton.Disabled = problem is not null;
                _hint.Text = problem ?? "Everyone is ready.";
                _hint.AddThemeColorOverride("font_color", problem is null ? UiTheme.Good : UiTheme.Dim);
            }
            else if (_readyButton is not null)
            {
                bool ready = mine >= 0 && s.Slots[mine].Ready;
                _readyButton.Text = ready ? "Cancel ready" : "Ready";
                _readyButton.AddThemeColorOverride("font_color", ready ? UiTheme.Ink : UiTheme.Amber);
                _hint.Text = ready ? "Waiting for the host to start." : "Press Ready when you're set.";
                _hint.AddThemeColorOverride("font_color", UiTheme.Dim);
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void RefreshWho(int index, LobbySlot slot, int mine)
    {
        SlotRowUi row = _rows[index];
        string key = $"{slot.Kind}|{slot.Ai}|{slot.Peer}|{slot.Name}|{index == mine}|{IsHost}";
        if (key == row.WhoKey) return;
        row.WhoKey = key;
        foreach (Node c in row.Who.GetChildren()) c.QueueFree();

        if (slot.Kind == SlotKind.Human)
        {
            string suffix = index == mine ? "  (you)" : "";
            var name = Centered(UiTheme.Label(slot.Name + suffix, 17, UiTheme.Ink));
            name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            name.ClipText = true;
            row.Who.AddChild(name);
            if (IsHost && slot.Peer != INetTransport.HostPeer)
            {
                var kick = UiTheme.Button("Kick", () => _host?.Kick(index), 56, 13);
                kick.CustomMinimumSize = new Vector2(56, 30);
                kick.TooltipText = $"Remove {slot.Name} from the game";
                row.Who.AddChild(kick);
            }
            return;
        }

        string label = slot.Kind switch
        {
            SlotKind.Ai => LobbyState.AiName(slot.Ai),
            SlotKind.Closed => "Closed",
            _ => "Open",
        };
        if (!IsHost)
        {
            row.Who.AddChild(Centered(UiTheme.Label(label, 16, slot.Kind == SlotKind.Ai ? UiTheme.Ink : UiTheme.Dim)));
            return;
        }
        var pick = new OptionButton { CustomMinimumSize = new Vector2(200, 0) };
        foreach (string c in SlotChoices) pick.AddItem(c);
        pick.Select(slot.Kind switch
        {
            SlotKind.Closed => 1,
            SlotKind.Ai => (int)slot.Ai + 1,
            _ => 0,
        });
        pick.ItemSelected += choice =>
        {
            if (_refreshing || _host is null) return;
            if (choice == 0) _host.SetSlotKind(index, SlotKind.Open);
            else if (choice == 1) _host.SetSlotKind(index, SlotKind.Closed);
            else _host.SetSlotKind(index, SlotKind.Ai, (AiDifficulty)(choice - 1));
        };
        row.Who.AddChild(pick);
    }

    private void RefreshMap(LobbyState s)
    {
        MapData? map = Map;
        ulong key = map is null ? 0 : s.MapHash ^ (ulong)s.MapPlayers ^ (s.RandomStarts ? 0x5151UL : 0);
        if (map is null)
        {
            _mapInfo.Text = $"{s.MapName}\nReceiving map...";
            return;
        }
        if (key == _previewKey) return;
        _previewKey = key;
        Image img = Minimap.RenderBase(map, 256, Math.Max(1, 256 * map.TilesY / map.TilesX));
        for (int i = 0; i < Math.Min(map.StartPositions.Count, NetProtocol.MaxSlots); i++)
        {
            var p = map.StartPositions[i];
            int x = (int)(p.X.Raw / 65536f / map.Width * img.GetWidth()), y = (int)(p.Y.Raw / 65536f / map.Height * img.GetHeight());
            Color c = s.RandomStarts ? UiTheme.Ink : UiTheme.TeamColour(i);
            for (int dy = -5; dy <= 5; dy++)
            for (int dx = -5; dx <= 5; dx++)
            {
                int d = dx * dx + dy * dy;
                if (d > 25 || x + dx < 0 || y + dy < 0 || x + dx >= img.GetWidth() || y + dy >= img.GetHeight()) continue;
                img.SetPixel(x + dx, y + dy, d > 16 ? Colors.Black : c);
            }
        }
        _preview.Texture = ImageTexture.CreateFromImage(img);
        string starts = s.RandomStarts ? "Start positions are shuffled when the game begins." : "Slot numbers match the start positions shown.";
        _mapInfo.Text = $"{map.Name}{(s.MapKey.StartsWith("user:") ? "  (custom map)" : "")}\n{map.Description}\n\n" +
            $"{map.Width} x {map.Height} · {s.MapPlayers} players · {map.MetalSpots.Count} metal spots\n{starts}";
    }

    // ---- Lobby actions -----------------------------------------------------------------------

    private void PickMap(int index)
    {
        if (_host is null || index < 0 || index >= _maps.Count) return;
        MapEntry entry = _maps[index];
        MapData map;
        try
        {
            map = entry.Load();
        }
        catch (Exception e)
        {
            AddChat(-1, "", $"Couldn't load {entry.Name}: {e.Message}");
            _mapList?.Select(_mapIndex);
            return;
        }
        if (_host.SetMap(entry.Key, map) is { } problem)
        {
            AddChat(-1, "", problem);
            _mapList?.Select(_mapIndex);
            return;
        }
        _mapIndex = index;
        Settings.Current.LastMap = entry.Key;
    }

    private void SlotChanged(int slot)
    {
        if (_refreshing) return;
        SlotRowUi row = _rows[slot];
        string faction = row.Faction.Selected == 1 ? "crucible" : "lattice";
        int team = row.Team.Selected;
        if (_host is not null)
        {
            _host.SetFaction(slot, faction);
            _host.SetTeam(slot, team);
            if (slot == 0) Settings.Current.LastFaction = faction;
        }
        else if (_client is not null && slot == _client.MySlot)
        {
            _client.SetChoice(faction, team);
            Settings.Current.LastFaction = faction;
        }
    }

    private void OptionsChanged()
    {
        if (_refreshing || _host is null) return;
        _host.SetOptions(ResourceChoices[Math.Clamp(_resources.Selected, 0, ResourceChoices.Length - 1)],
            _win.Selected == 1 ? WinCondition.Annihilation : WinCondition.Commander, _starts.Selected == 1);
    }

    private void ToggleReady()
    {
        if (_client?.State is not { } s || _client.MySlot < 0) return;
        _client.SetReady(!s.Slots[_client.MySlot].Ready);
    }

    private void SendChat()
    {
        string text = _chatInput.Text.Trim();
        _chatInput.Text = "";
        if (text.Length == 0) return;
        if (_host is not null) _host.SendChat(text);
        else _client?.SendChat(text);
    }

    private void AddChat(int slot, string name, string text)
    {
        string safe = text.Replace("[", "[lb]");
        string line = slot < 0
            ? $"[i][color=#{UiTheme.Dim.ToHtml(false)}]{safe}[/color][/i]\n"
            : $"[color=#{UiTheme.TeamColour(PlayerIndexOfSlot(slot)).ToHtml(false)}]{name.Replace("[", "[lb]")}:[/color] {safe}\n";
        _chatLines.Add(line);
        if (_chatLines.Count > 200) _chatLines.RemoveAt(0);
        if (_chatLog is not null && IsInstanceValid(_chatLog) && _chatLog.IsInsideTree()) _chatLog.AppendText(line);
    }

    private int PlayerIndexOfSlot(int slot)
    {
        LobbyState? s = State;
        if (s is null) return slot;
        int index = 0;
        for (int i = 0; i < slot && i < s.Slots.Length; i++) if (s.Slots[i].IsPlayer) index++;
        return index;
    }

    /// <summary>Host: starts the match if everyone is ready.</summary>
    public void StartMatch()
    {
        if (_host is null || _host.StartProblem() is not null) return;
        Settings.Current.Save();
        ulong seed = unchecked((ulong)Random.Shared.NextInt64() ^ Time.GetTicksUsec());
        NetMatchStart start = _host.StartMatch(seed);
        NetGame.Launch(start);
    }

    private void Leave()
    {
        _host?.Dispose();
        _client?.Dispose();
        _host = null;
        _client = null;
        _chatLines.Clear();
        _status = "";
        _statusIsError = false;
        ShowStartPage();
    }

    // ---- Helpers -----------------------------------------------------------------------------

    private static OptionButton FactionPicker()
    {
        var f = new OptionButton { CustomMinimumSize = new Vector2(140, 0) };
        f.AddItem("Lattice");
        f.AddItem("Crucible");
        return f;
    }

    private static Label Centered(Label l)
    {
        l.VerticalAlignment = VerticalAlignment.Center;
        l.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        return l;
    }

    private static Label Wrapped(Label l)
    {
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return l;
    }

    /// <summary>Lets a panel take its share of the row's spare width.</summary>
    private static PanelContainer Grow(PanelContainer p, float ratio)
    {
        p.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        p.SizeFlagsStretchRatio = ratio;
        return p;
    }

    private static T Fixed<T>(T c, float width) where T : Control
    {
        c.CustomMinimumSize = new Vector2(width, c.CustomMinimumSize.Y);
        return c;
    }

    /// <summary>This machine's IPv4 addresses that others on the network could reach.</summary>
    public static List<string> LanAddresses()
    {
        var list = new List<string>();
        foreach (string a in IP.GetLocalAddresses())
        {
            if (a.Contains(':') || a.StartsWith("127.") || a.StartsWith("169.254.")) continue;
            list.Add(a);
        }
        // Private ranges first: those are what a friend on the same network will use.
        return list.OrderBy(a => a.StartsWith("192.168.") || a.StartsWith("10.") || a.StartsWith("172.") ? 0 : 1)
                   .ThenBy(a => a, StringComparer.Ordinal).Take(3).ToList();
    }
}
