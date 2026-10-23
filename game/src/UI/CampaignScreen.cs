using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Missions;
using Godot;

namespace ContingencyZero.Game.UI;

/// <summary>
/// Mission select: the two campaigns side by side, or (with the Co-op toggle) the co-op missions;
/// a briefing, and Start. Co-op missions can be played alone with a computer ally, or hosted for a
/// friend from the multiplayer lobby.
/// </summary>
public partial class CampaignScreen : CanvasLayer
{
    /// <summary>Open on the co-op list (remembered while the game runs).</summary>
    public static bool ShowCoop;

    private List<MissionInfo> _missions = new();
    private MissionInfo? _selected;
    private RichTextLabel _briefing = null!;
    private TextureRect _preview = null!;
    private Button _start = null!;
    private Button _host = null!;
    private HBoxContainer _cols = null!;
    private PanelContainer _briefingPanel = null!;
    private Button _campaignTab = null!;
    private Button _coopTab = null!;

    public override void _Ready()
    {
        Layer = 10;
        var root = new Control { Theme = UiTheme.Theme };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        var shade = new ColorRect { Color = new Color(0.035f, 0.035f, 0.03f, 0.82f) };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(shade);

        var outer = new VBoxContainer();
        outer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        outer.OffsetLeft = 60; outer.OffsetTop = 40; outer.OffsetRight = -60; outer.OffsetBottom = -40;
        outer.AddThemeConstantOverride("separation", 14);
        root.AddChild(outer);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 14);
        header.AddChild(UiTheme.Label("CAMPAIGN", 36));
        header.AddChild(new Control { CustomMinimumSize = new Vector2(30, 0) });
        _campaignTab = UiTheme.Button("Campaigns", () => SetCoop(false), 170, 17);
        _coopTab = UiTheme.Button("Co-op", () => SetCoop(true), 170, 17);
        _campaignTab.ToggleMode = _coopTab.ToggleMode = true;
        _campaignTab.SizeFlagsVertical = _coopTab.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        header.AddChild(_campaignTab);
        header.AddChild(_coopTab);
        outer.AddChild(header);

        _cols = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _cols.AddThemeConstantOverride("separation", 20);
        outer.AddChild(_cols);

        _missions = MissionInfo.LoadAll();

        var right = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 18);
        _preview = new TextureRect
        {
            CustomMinimumSize = new Vector2(260, 260),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        right.AddChild(_preview);
        _briefing = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Text = "Pick a mission.",
        };
        _briefing.AddThemeFontSizeOverride("normal_font_size", 17);
        _briefing.AddThemeFontSizeOverride("bold_font_size", 24);
        _briefing.AddThemeColorOverride("default_color", UiTheme.Ink);
        right.AddChild(_briefing);
        _briefingPanel = UiTheme.Panel(right);
        _briefingPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddThemeConstantOverride("separation", 12);
        buttons.AddChild(UiTheme.Button("Back", GameRoot.Instance.ShowMainMenu, 140));
        _host = UiTheme.Button("Host co-op game", HostCoop, 220, 20);
        _host.TooltipText = "Open the multiplayer lobby with this mission picked, for a friend to join";
        buttons.AddChild(_host);
        _start = UiTheme.Button("Start mission", () => { if (_selected is not null) GameRoot.Instance.StartMission(_selected); }, 220, 20);
        _start.Disabled = true;
        buttons.AddChild(_start);
        outer.AddChild(buttons);

        SetCoop(ShowCoop);
    }

    private void SetCoop(bool coop)
    {
        ShowCoop = coop;
        _campaignTab.SetPressedNoSignal(!coop);
        _coopTab.SetPressedNoSignal(coop);
        if (_briefingPanel.GetParent() == _cols) _cols.RemoveChild(_briefingPanel);
        foreach (Node c in _cols.GetChildren()) c.QueueFree();
        _selected = null;
        _start.Disabled = true;
        _host.Visible = coop;
        _briefing.Text = "Pick a mission.";
        _preview.Texture = null;

        if (coop) BuildCoopColumn();
        else
            foreach (var campaign in _missions.Where(m => !m.IsCoop).GroupBy(m => m.Campaign))
            {
                var col = new VBoxContainer { CustomMinimumSize = new Vector2(320, 0) };
                col.AddThemeConstantOverride("separation", 8);
                bool lattice = campaign.Key == "lattice";
                col.AddChild(UiTheme.Label(lattice ? "THE LATTICE" : "THE CRUCIBLE", 22, lattice ? UiTheme.LatticeColour : UiTheme.CrucibleColour));
                col.AddChild(UiTheme.Label(lattice ? "Precision, range, information." : "Armour, attrition, salvage.", 14, UiTheme.Dim));
                foreach (MissionInfo m in campaign) col.AddChild(MissionButton(m, CampaignProgress.Won.Contains(m.Key) ? "   [won]" : ""));
                _cols.AddChild(UiTheme.Panel(col));
            }
        _cols.AddChild(_briefingPanel);

        MissionInfo? first = coop
            ? _missions.LastOrDefault(m => m.IsCoop && CampaignProgress.Unlocked(m, _missions))
            : _missions.LastOrDefault(m => !m.IsCoop && CampaignProgress.Unlocked(m, _missions) && m.Campaign == "lattice");
        if (first is not null) Select(first);
    }

    private void BuildCoopColumn()
    {
        var col = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
        col.AddThemeConstantOverride("separation", 8);
        col.AddChild(UiTheme.Label("CO-OP", 22, UiTheme.Amber));
        col.AddChild(UiTheme.Label("Missions for two commanders on one side.", 14, UiTheme.Dim));
        foreach (MissionInfo m in _missions.Where(m => m.IsCoop))
        {
            string mark = CampaignProgress.WonTogether(m.Key) ? "   [won together]" : CampaignProgress.WonSolo(m.Key) ? "   [won solo]" : "";
            col.AddChild(MissionButton(m, mark));
        }
        col.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        var note = UiTheme.Label("Start mission plays it alone, with a computer ally in the second seat. Host co-op game opens the multiplayer lobby so a friend can take that seat.", 14, UiTheme.Dim);
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        note.CustomMinimumSize = new Vector2(340, 0);
        col.AddChild(note);
        _cols.AddChild(UiTheme.Panel(col));
    }

    private Button MissionButton(MissionInfo m, string mark)
    {
        bool unlocked = CampaignProgress.Unlocked(m, _missions);
        var b = UiTheme.Button($"{m.Order}. {m.Title}{mark}", () => Select(m), 300, 16);
        b.Alignment = HorizontalAlignment.Left;
        b.Disabled = !unlocked;
        if (!unlocked) b.TooltipText = "Win the previous mission to unlock this one";
        return b;
    }

    private void HostCoop()
    {
        if (_selected is not { IsCoop: true } m) return;
        MultiplayerLobby.PendingCoopMission = m.Key;
        GameRoot.Instance.ShowMultiplayer();
    }

    private void Select(MissionInfo m)
    {
        _selected = m;
        _start.Disabled = false;
        _host.Disabled = !m.IsCoop;
        string mapName = m.MapKey.Replace('_', ' ');
        var entry = GameData.Maps().FirstOrDefault(e => e.Key == m.MapKey);
        if (entry is not null)
        {
            var map = entry.Load();
            mapName = map.Name;
            _preview.Texture = ImageTexture.CreateFromImage(Minimap.RenderBase(map, 256, 256 * map.TilesY / map.TilesX));
        }
        if (m.IsCoop)
        {
            var seats = string.Join(" and ", m.HumanPlayers.Where(i => i >= 0 && i < m.Players.Count)
                .Select(i => $"{m.Players[i].Name} ({(m.Players[i].Faction == "crucible" ? "Crucible" : "Lattice")})"));
            string won = CampaignProgress.WonTogether(m.Key) ? "  ·  [color=#59e673]won together[/color]"
                : CampaignProgress.WonSolo(m.Key) ? "  ·  [color=#59e673]won solo[/color]" : "";
            _briefing.Text = $"[b]{m.Title}[/b]\n[color=#94948a]Co-op · mission {m.Order} · {mapName}{won}[/color]\n" +
                             $"[color=#94948a]Seats: {seats}[/color]\n\n{m.Briefing}";
            return;
        }
        string side = m.Campaign == "lattice" ? "Lattice" : "Crucible";
        string done = CampaignProgress.Won.Contains(m.Key) ? "  ·  [color=#59e673]completed[/color]" : "";
        _briefing.Text = $"[b]{m.Title}[/b]\n[color=#94948a]{side} campaign · mission {m.Order} · {mapName}{done}[/color]\n\n{m.Briefing}";
    }
}
