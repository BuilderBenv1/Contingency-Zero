using System;
using System.Linq;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Audio;
using ContingencyZero.Game.Match;
using ContingencyZero.Game.UI;
using Godot;

namespace ContingencyZero.Game.Net;

/// <summary>Starts a networked match on this machine once the lobby has handed over.</summary>
public static class NetGame
{
    private const double MaxGap = 8 / 60.0;
    private static ulong _lastUsec;
    private static double _now;

    /// <summary>
    /// The lockstep's clock, in seconds. It follows real time but, like Godot's frame delta, never
    /// jumps more than 8/60 s between readings, so on a machine too slow to keep up the round-trip
    /// times (and so the input delay) are measured in game time, which is what the sim runs on.
    /// </summary>
    public static double Now()
    {
        ulong t = Time.GetTicksUsec();
        if (_lastUsec != 0 && t > _lastUsec) _now += Math.Min((t - _lastUsec) / 1_000_000.0, MaxGap);
        _lastUsec = t;
        return _now;
    }

    public static void RouteLogs() => LockstepChannel.Log = s => GD.Print($"[net] {s}");

    public static MatchScene Launch(NetMatchStart start)
    {
        RouteLogs();
        // A spectator's session has no local player (-1): it issues nothing and sees everything.
        var session = new SimSession(start.Scenario, GameData.Catalog, start.LocalPlayer, start.Channel) { IsMultiplayer = true };
        MatchScene match = GameRoot.Instance.StartMatch(session, MatchKind.Multiplayer, start.Title, restart: null);
        match.AddChild(new NetMatchOverlay(match, start.Channel) { Name = "NetOverlay" });
        if (start.IsSpectator)
        {
            match.SetViewer(-1);
            match.AddChild(new SpectatorBar(match) { Name = "SpectatorBar" });
        }
        return match;
    }
}

/// <summary>
/// What a spectator gets on top of the HUD: whose view to watch (everyone, or one player's fog
/// and radar), and a switch for the casting overlay. Nothing here touches the sim.
/// </summary>
public partial class SpectatorBar : CanvasLayer
{
    private readonly MatchScene _match;
    private OptionButton _view = null!;
    private Button _cast = null!;

    public SpectatorBar(MatchScene match) { _match = match; }

    public override void _Ready()
    {
        Layer = 15;
        var root = new Control { Theme = UiTheme.Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 10);
        var tag = UiTheme.Label("SPECTATING", 14, UiTheme.Amber);
        tag.VerticalAlignment = VerticalAlignment.Center;
        bar.AddChild(tag);
        var viewLabel = UiTheme.Label("View", 14, UiTheme.Dim);
        viewLabel.VerticalAlignment = VerticalAlignment.Center;
        bar.AddChild(viewLabel);
        _view = new OptionButton { CustomMinimumSize = new Vector2(170, 0) };
        _view.AddItem("Everyone");
        var players = _match.Session.World.Players;
        foreach (var p in players) _view.AddItem(p.Name);
        _view.ItemSelected += i => _match.SetViewer(i == 0 ? -1 : players[i - 1].Team);
        bar.AddChild(_view);
        _cast = UiTheme.Button("Casting overlay (C)", ToggleCasting, 0, 14);
        _cast.TooltipText = "Show the caster's view: scores, income and army sizes for every player";
        bar.AddChild(_cast);
        var panel = UiTheme.Panel(bar, 8);
        panel.AnchorLeft = 0.5f; panel.AnchorRight = 0.5f;
        panel.OffsetLeft = -300; panel.OffsetRight = 300; panel.OffsetTop = 58; panel.OffsetBottom = 104;
        panel.GrowHorizontal = Control.GrowDirection.Both;
        root.AddChild(panel);
    }

    /// <summary>Cycles the watched view: Everyone, then each player in turn.</summary>
    public void NextView()
    {
        int next = (_view.Selected + 1) % _view.ItemCount;
        _view.Select(next);
        _match.SetViewer(next == 0 ? -1 : _match.Session.World.Players[next - 1].Team);
    }

    /// <summary>
    /// The casting overlay belongs to the HUD (UI/CastingOverlay.cs, key C). It's looked up by
    /// node name so this bar works whether or not it's present in this build.
    /// </summary>
    public void ToggleCasting()
    {
        if (_match.GetNodeOrNull<CanvasItem>("CastingOverlay") is { } item) { item.Visible = !item.Visible; return; }
        if (_match.GetNodeOrNull<CanvasLayer>("CastingOverlay") is { } layer) { layer.Visible = !layer.Visible; return; }
        // Not in this build: the C key reaches the HUD's own handler if it has one.
        _match.Hud?.Post("Casting overlay: press C.", UiTheme.Dim, 4);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (k.Keycode == Key.V) { NextView(); GetViewport().SetInputAsHandled(); }
    }
}

/// <summary>
/// Multiplayer extras on top of the HUD: "Waiting for ..." when the game is held up, a desync
/// banner, "lost connection" with a way back to the menu, players leaving, and chat (Enter to type).
/// </summary>
public partial class NetMatchOverlay : CanvasLayer
{
    private readonly MatchScene _match;
    private readonly LockstepChannel _channel;
    private Control _root = null!;
    private Label _waiting = null!;
    private PanelContainer _desyncPanel = null!;
    private Label _desyncTitle = null!;
    private Label _desync = null!;
    private Label _status = null!;
    private PanelContainer _chatPanel = null!;
    private LineEdit _chat = null!;
    private bool _lostShown;
    private readonly System.Collections.Generic.HashSet<int> _left = new();
    private double _statusTimer;

    public NetMatchOverlay(MatchScene match, LockstepChannel channel)
    {
        _match = match;
        _channel = channel;
    }

    public override void _Ready()
    {
        Layer = 15;
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiTheme.Theme };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        _waiting = UiTheme.Label("", 22, UiTheme.Amber, HorizontalAlignment.Center);
        _waiting.AnchorLeft = 0.5f; _waiting.AnchorRight = 0.5f; _waiting.AnchorTop = 0.32f; _waiting.AnchorBottom = 0.32f;
        _waiting.OffsetLeft = -420; _waiting.OffsetRight = 420; _waiting.OffsetTop = -16; _waiting.OffsetBottom = 16;
        _waiting.AddThemeConstantOverride("outline_size", 7);
        _waiting.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        _waiting.MouseFilter = Control.MouseFilterEnum.Ignore;
        _waiting.Visible = false;
        _root.AddChild(_waiting);

        _desyncTitle = UiTheme.Label("", 21, new Color(1f, 0.82f, 0.78f), HorizontalAlignment.Center);
        _desync = UiTheme.Label("", 15, UiTheme.Ink, HorizontalAlignment.Center);
        _desync.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _desync.CustomMinimumSize = new Vector2(640, 0);
        var desyncBody = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        desyncBody.AddThemeConstantOverride("separation", 4);
        desyncBody.AddChild(_desyncTitle);
        desyncBody.AddChild(_desync);
        _desyncPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _desyncPanel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.28f, 0.05f, 0.05f, 0.92f), UiTheme.Bad, 2, 4, 10));
        _desyncPanel.AddChild(desyncBody);
        _desyncPanel.AnchorLeft = 0.5f; _desyncPanel.AnchorRight = 0.5f;
        _desyncPanel.OffsetLeft = -340; _desyncPanel.OffsetRight = 340; _desyncPanel.OffsetTop = 60;
        _desyncPanel.GrowHorizontal = Control.GrowDirection.Both;
        _desyncPanel.Visible = false;
        _root.AddChild(_desyncPanel);

        _status = UiTheme.Label("", 13, UiTheme.Dim, HorizontalAlignment.Right);
        _status.AnchorLeft = 1; _status.AnchorRight = 1;
        _status.OffsetLeft = -420; _status.OffsetRight = -12; _status.OffsetTop = 54; _status.OffsetBottom = 72;
        _status.AddThemeConstantOverride("outline_size", 4);
        _status.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        _status.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(_status);

        var chatRow = new HBoxContainer();
        chatRow.AddThemeConstantOverride("separation", 8);
        var say = UiTheme.Label("Say:", 15, UiTheme.Accent);
        say.VerticalAlignment = VerticalAlignment.Center;
        chatRow.AddChild(say);
        _chat = new LineEdit { MaxLength = 200, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, PlaceholderText = "Enter to send, Esc to cancel" };
        _chat.TextSubmitted += Send;
        chatRow.AddChild(_chat);
        _chatPanel = UiTheme.Panel(chatRow, 6);
        _chatPanel.AnchorTop = 1; _chatPanel.AnchorBottom = 1;
        _chatPanel.OffsetLeft = 256; _chatPanel.OffsetRight = 776; _chatPanel.OffsetTop = -222; _chatPanel.OffsetBottom = -178;
        _chatPanel.Visible = false;
        _root.AddChild(_chatPanel);

        _channel.PlayerLeft += p =>
        {
            _left.Add(p);
            _match.Hud?.Post($"{_channel.NameOf(p)} left the game", UiTheme.Amber, 10);
        };
        _channel.SpectatorLeft += s =>
        {
            _gone++;
            _match.Hud?.Post($"{_channel.NameOf(s)} stopped watching", UiTheme.Dim, 6);
        };
        _channel.ChatReceived += OnChat;
        _channel.Desynced += OnDesync;
        _channel.Lost += _ => ShowLost();
        if (_channel.Desync is { } d) OnDesync(d);
        _match.Hud?.Post("Press Enter to chat.", UiTheme.Dim, 8);
        if (_channel.IsSpectator) _match.Hud?.Post("You are spectating: V cycles whose view you watch.", UiTheme.Amber, 10);
    }

    /// <param name="player">A player index, <see cref="NetProtocol.SpectatorChat"/> for a watcher, or <see cref="NetProtocol.SystemChat"/>.</param>
    private void OnChat(int player, string name, string text)
    {
        if (player == NetProtocol.SpectatorChat)
        {
            _match.Hud?.Post($"{name} (spectator): {text}", UiTheme.Ink, 14);
            Sfx.Play(SfxKind.Select, -6);
            return;
        }
        Color c = player >= 0 ? UiTheme.TeamColour(player) : UiTheme.Dim;
        _match.Hud?.Post(player >= 0 ? $"{name}: {text}" : text, c, 14);
        if (player != _channel.LocalPlayer) Sfx.Play(SfxKind.Select, -6);
    }

    private int _gone;   // spectators who have left

    private void OnDesync(DesyncReport d)
    {
        string? saved = _match.SaveReplay();
        GD.PrintErr($"DESYNC at tick {d.Tick} ({NetProtocol.Clock(d.Tick)}): host hash {d.HostHash:X16}, {_channel.NameOf(d.Player)} hash {d.PeerHash:X16}. Replay: {saved ?? "not saved"}");
        _desyncTitle.Text = $"Desync detected at {NetProtocol.Clock(d.Tick)}";
        _desync.Text = "This game is out of sync: players may now be seeing different battles. " +
            $"You can play on, but the result may not count.{(saved is null ? "" : $"\nReplay saved for a bug report: {saved}")}";
        _desyncPanel.Visible = true;
        Sfx.Play(SfxKind.Error);
    }

    private void ShowLost()
    {
        if (_lostShown) return;
        _lostShown = true;
        _chatPanel.Visible = false;
        string reason = _channel.LostReason ?? "Lost connection to host.";
        if (_match.Session.World.IsOver)
        {
            // The result is in; the game-over panel already offers the way out.
            _match.Hud?.Post($"{reason} The match is over.", UiTheme.Amber, 12);
            return;
        }
        AddChild(new LostConnectionPanel(_match, reason));
    }

    public override void _Process(double delta)
    {
        if (_channel.LostReason is not null)
        {
            _waiting.Visible = false;
            ShowLost();
            return;
        }

        double stalled = _match.Session.Waiting ? _channel.StallSeconds : 0;
        if (!_channel.Started && stalled > 0.2)
        {
            var who = _channel.WaitingOn.Select(_channel.NameOf).ToList();
            _waiting.Text = who.Count > 0 ? $"Waiting for {string.Join(", ", who)} to load..." : "Waiting for players to load...";
            _waiting.Visible = true;
        }
        else if (stalled > 0.6)
        {
            var who = _channel.WaitingOn.Where(p => p != _channel.LocalPlayer).Select(_channel.NameOf).ToList();
            string names = who.Count > 0 ? string.Join(", ", who) : "other players";
            _waiting.Text = $"Waiting for {names}...{(stalled >= 3 ? $"  {stalled:0}s" : "")}";
            _waiting.Visible = true;
        }
        else _waiting.Visible = false;

        _statusTimer -= delta;
        if (_statusTimer <= 0)
        {
            _statusTimer = 0.5;
            // Missions (co-op, survival) have script-driven players with no AI that aren't people: count the human seats.
            var setups = _match.Session.Scenario.Players;
            int humans = _match.Session.World.Mission is { } mission
                ? mission.HumanPlayers.Count(h => h < setups.Count && setups[h].Ai == Sim.Match.AiDifficulty.None)
                : setups.Count(p => p.Ai == Sim.Match.AiDifficulty.None);
            int ais = _match.Session.Scenario.Players.Count(p => p.Ai != Sim.Match.AiDifficulty.None);
            string ping = _channel is LockstepHost host
                ? string.Join("  ", Enumerable.Range(0, _channel.ParticipantCount)
                    .Where(p => p != host.LocalPlayer && host.IsConnected(p) && host.RoundTrip(p) > 0)
                    .Select(p => $"{_channel.NameOf(p)} {host.RoundTrip(p) * 1000:0} ms"))
                : "";
            string people = _left.Count > 0 ? $"{humans - _left.Count} of {humans} humans" : $"{humans} humans";
            int watching = _channel.SpectatorNames.Count - _gone;
            string watchers = watching > 0 ? $" · {watching} watching" : "";
            _status.Text = $"Online · {people}{(ais > 0 ? $", {ais} AI" : "")}{watchers} · input delay {_channel.Delay * 1000 / Sim.World.TicksPerSecond} ms" + (ping.Length > 0 ? $"\n{ping}" : "");
        }
    }

    public override void _Input(InputEvent e)
    {
        if (!_chatPanel.Visible || e is not InputEventKey { Pressed: true, Keycode: Key.Escape }) return;
        CloseChat();
        GetViewport().SetInputAsHandled();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_lostShown || e is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (k.Keycode is Key.Enter or Key.KpEnter && !_chatPanel.Visible)
        {
            _chatPanel.Visible = true;
            _chat.Text = "";
            _chat.GrabFocus();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Send(string text)
    {
        _channel.SendChat(text);
        CloseChat();
    }

    private void CloseChat()
    {
        _chat.ReleaseFocus();
        _chatPanel.Visible = false;
    }
}

/// <summary>"Lost connection to host" with the way back to the menu.</summary>
public partial class LostConnectionPanel : ModalLayer
{
    private readonly MatchScene _match;
    private readonly string _reason;

    public LostConnectionPanel(MatchScene match, string reason)
    {
        _match = match;
        _reason = reason;
    }

    public override void _Ready()
    {
        Build(440, 0.5f);
        Layer = 25;
        var title = UiTheme.Title("CONNECTION LOST", 30);
        title.AddThemeColorOverride("font_color", UiTheme.Bad);
        Body.AddChild(title);
        var why = UiTheme.Label(_reason, 16, UiTheme.Ink, HorizontalAlignment.Center);
        why.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Body.AddChild(why);
        int secs = _match.Session.World.Tick / Sim.World.TicksPerSecond;
        Body.AddChild(UiTheme.Label($"The match stopped at {secs / 60}:{secs % 60:D2}.", 14, UiTheme.Dim, HorizontalAlignment.Center));
        Body.AddChild(UiTheme.Button("Back to menu", () => { QueueFree(); _match.Quit(); }));
    }
}
