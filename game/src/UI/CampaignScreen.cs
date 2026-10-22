using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Missions;
using Godot;

namespace ContingencyZero.Game.UI;

/// <summary>Mission select: the two campaigns side by side, a briefing, and Start.</summary>
public partial class CampaignScreen : CanvasLayer
{
    private List<MissionInfo> _missions = new();
    private MissionInfo? _selected;
    private RichTextLabel _briefing = null!;
    private TextureRect _preview = null!;
    private Button _start = null!;

    public override void _Ready()
    {
        Layer = 10;
        var root = new Control { Theme = UiTheme.Theme };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        var shade = new ColorRect { Color = new Color(0.02f, 0.03f, 0.05f, 0.82f) };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(shade);

        var outer = new VBoxContainer();
        outer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        outer.OffsetLeft = 60; outer.OffsetTop = 40; outer.OffsetRight = -60; outer.OffsetBottom = -40;
        outer.AddThemeConstantOverride("separation", 14);
        root.AddChild(outer);
        outer.AddChild(UiTheme.Label("CAMPAIGN", 36));

        var cols = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        cols.AddThemeConstantOverride("separation", 20);
        outer.AddChild(cols);

        _missions = MissionInfo.LoadAll();
        foreach (var campaign in _missions.GroupBy(m => m.Campaign))
        {
            var col = new VBoxContainer { CustomMinimumSize = new Vector2(320, 0) };
            col.AddThemeConstantOverride("separation", 8);
            bool lattice = campaign.Key == "lattice";
            col.AddChild(UiTheme.Label(lattice ? "THE LATTICE" : "THE CRUCIBLE", 22, lattice ? UiTheme.LatticeColour : UiTheme.CrucibleColour));
            col.AddChild(UiTheme.Label(lattice ? "Precision, range, information." : "Armour, attrition, salvage.", 14, UiTheme.Dim));
            foreach (MissionInfo m in campaign)
            {
                bool unlocked = CampaignProgress.Unlocked(m, _missions);
                bool won = CampaignProgress.Won.Contains(m.Key);
                var b = UiTheme.Button($"{m.Order}. {m.Title}{(won ? "   [won]" : "")}", () => Select(m), 300, 16);
                b.Alignment = HorizontalAlignment.Left;
                b.Disabled = !unlocked;
                if (!unlocked) b.TooltipText = "Win the previous mission to unlock this one";
                col.AddChild(b);
            }
            cols.AddChild(UiTheme.Panel(col));
        }

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
        var briefingPanel = UiTheme.Panel(right);
        briefingPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        cols.AddChild(briefingPanel);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddThemeConstantOverride("separation", 12);
        buttons.AddChild(UiTheme.Button("Back", GameRoot.Instance.ShowMainMenu, 140));
        _start = UiTheme.Button("Start mission", () => { if (_selected is not null) GameRoot.Instance.StartMission(_selected); }, 220, 20);
        _start.Disabled = true;
        buttons.AddChild(_start);
        outer.AddChild(buttons);

        var first = _missions.LastOrDefault(m => CampaignProgress.Unlocked(m, _missions) && m.Campaign == "lattice");
        if (first is not null) Select(first);
    }

    private void Select(MissionInfo m)
    {
        _selected = m;
        _start.Disabled = false;
        string mapName = m.MapKey.Replace('_', ' ');
        var entry = GameData.Maps().FirstOrDefault(e => e.Key == m.MapKey);
        if (entry is not null)
        {
            var map = entry.Load();
            mapName = map.Name;
            _preview.Texture = ImageTexture.CreateFromImage(Minimap.RenderBase(map, 256, 256 * map.TilesY / map.TilesX));
        }
        string side = m.Campaign == "lattice" ? "Lattice" : "Crucible";
        string won = CampaignProgress.Won.Contains(m.Key) ? "  ·  [color=#59e673]completed[/color]" : "";
        _briefing.Text = $"[b]{m.Title}[/b]\n[color=#8a96a8]{side} campaign · mission {m.Order} · {mapName}{won}[/color]\n\n{m.Briefing}";
    }
}
