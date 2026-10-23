using System;
using System.Linq;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Match;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.State;
using Godot;

namespace ContingencyZero.Game.UI;

/// <summary>Dims the screen and centres a panel on it.</summary>
public partial class ModalLayer : CanvasLayer
{
    protected Control Root { get; private set; } = null!;
    protected VBoxContainer Body { get; private set; } = null!;

    protected void Build(float width, float dim = 0.55f)
    {
        Layer = 20;
        Root = new Control { Theme = UiTheme.Theme };
        Root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(Root);
        var shade = new ColorRect { Color = new Color(0, 0, 0, dim), MouseFilter = Control.MouseFilterEnum.Stop };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        Root.AddChild(shade);
        var centre = new CenterContainer();
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        Root.AddChild(centre);
        Body = new VBoxContainer { CustomMinimumSize = new Vector2(width, 0) };
        Body.AddThemeConstantOverride("separation", 10);
        centre.AddChild(UiTheme.Panel(Body, 22));
    }
}

/// <summary>Esc menu: resume, settings, restart, surrender, quit.</summary>
public partial class PauseMenu : ModalLayer
{
    public event Action? Closed;
    private MatchScene _match = null!;

    public void Init(MatchScene match)
    {
        _match = match;
        Build(340);
        Body.AddChild(UiTheme.Title("PAUSED", 30));
        if (match.Session.IsMultiplayer) Body.AddChild(UiTheme.Label("The game keeps running in multiplayer.", 14, UiTheme.Dim, HorizontalAlignment.Center));
        Body.AddChild(UiTheme.Button("Resume", Close));
        Body.AddChild(UiTheme.Button("Settings", () => AddChild(SettingsPanel.Create(() => { }))));
        Body.AddChild(UiTheme.Button("Controls", () => AddChild(HelpPanel.Create())));
        if (match.Kind is MatchKind.Skirmish or MatchKind.Mission)
            Body.AddChild(UiTheme.Button("Restart", () => { Close(); match.Restart(); }));
        if (match.Kind != MatchKind.Replay && match.Session.LocalPlayer >= 0 && !match.Session.World.IsOver)
            Body.AddChild(UiTheme.Button("Surrender", () =>
            {
                match.Session.Issue(new Command(0, match.Session.LocalPlayer, CommandType.Resign, -1));
                Close();
            }));
        Body.AddChild(UiTheme.Button(GameRoot.Instance.ReturnsToEditor ? "Back to the editor" : "Quit to main menu", () => { Close(); match.Quit(); }));
    }

    public void Close()
    {
        Closed?.Invoke();
        QueueFree();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }
}

/// <summary>Victory / defeat screen with per-player statistics.</summary>
public partial class GameOverPanel : ModalLayer
{
    public void Init(MatchScene match, bool won, bool spectator, string? replayFile)
    {
        Build(720, 0.35f);
        World w = match.Session.World;
        string title = spectator
            ? (w.WinningTeam is { } t ? $"TEAM {t + 1} WINS" : "DRAW")
            : won ? "VICTORY" : "DEFEAT";
        var titleLabel = UiTheme.Title(title, 46);
        titleLabel.AddThemeColorOverride("font_color", spectator ? UiTheme.Ink : won ? UiTheme.Good : UiTheme.Bad);
        Body.AddChild(titleLabel);
        int secs = w.Tick / World.TicksPerSecond;
        Body.AddChild(UiTheme.Label($"{match.Title} · {w.Map.Name} · {secs / 60}:{secs % 60:D2}", 15, UiTheme.Dim, HorizontalAlignment.Center));
        // Survival: score and the map's best (saved here for the players, not for replays or spectators).
        if (Missions.SurvivalScores.GameOverText(w, save: !spectator && match.Kind != MatchKind.Replay) is { } survival)
        {
            if (!spectator) titleLabel.Text = "OVERRUN";
            Body.AddChild(UiTheme.Label(survival, 18, UiTheme.Amber, HorizontalAlignment.Center));
        }

        var grid = new GridContainer { Columns = 7 };
        grid.AddThemeConstantOverride("h_separation", 18);
        grid.AddThemeConstantOverride("v_separation", 6);
        foreach (string h in new[] { "Player", "Result", "Built", "Lost", "Kills", "Metal", "Energy" })
            grid.AddChild(UiTheme.Label(h, 14, UiTheme.Dim));
        foreach (PlayerState p in w.Players)
        {
            bool winner = w.WinningTeam == p.Team;
            var name = UiTheme.Label(p.Name, 15, UiTheme.TeamColour(p.Id));
            grid.AddChild(name);
            grid.AddChild(UiTheme.Label(winner ? "Victorious" : p.Defeated ? "Destroyed" : "-", 15, winner ? UiTheme.Good : UiTheme.Ink));
            grid.AddChild(UiTheme.Label(p.UnitsBuilt.ToString(), 15));
            grid.AddChild(UiTheme.Label(p.UnitsLost.ToString(), 15));
            grid.AddChild(UiTheme.Label(p.Kills.ToString(), 15));
            grid.AddChild(UiTheme.Label($"{p.MetalProduced.Raw / 65536f:0}", 15));
            grid.AddChild(UiTheme.Label($"{p.EnergyProduced.Raw / 65536f:0}", 15));
        }
        Body.AddChild(grid);
        if (replayFile is not null)
            Body.AddChild(UiTheme.Label($"Replay saved: {replayFile}", 13, UiTheme.Dim, HorizontalAlignment.Center));

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 12);
        buttons.AddChild(UiTheme.Button("Keep watching", QueueFree, 160));
        if (replayFile is not null)
        {
            var share = UiTheme.Button("Copy share code", () =>
            {
                try { AddChild(ShareCodeDialog.ShowCode("SHARE REPLAY", "replay", ReplayFiles.CodeFor(replayFile))); }
                catch (Exception e) { GD.PushWarning($"Couldn't make a share code: {e.Message}"); }
            }, 180);
            share.TooltipText = "Puts this match's replay on the clipboard as a text code to paste into chat";
            share.Name = "CopyShareCode";
            buttons.AddChild(share);
        }
        if (match.Kind is MatchKind.Skirmish or MatchKind.Mission)
            buttons.AddChild(UiTheme.Button(match.Kind == MatchKind.Mission && !won ? "Retry mission" : "Play again", () => { QueueFree(); match.Restart(); }, 160));
        buttons.AddChild(UiTheme.Button(GameRoot.Instance.ReturnsToEditor ? "Editor" : "Main menu", () => { QueueFree(); match.Quit(); }, 160));
        Body.AddChild(buttons);
    }
}
