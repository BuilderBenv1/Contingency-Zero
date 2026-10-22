using ContingencyZero.Game.App;
using Godot;

namespace ContingencyZero.Game.UI;

/// <summary>Title screen over a live AI battle.</summary>
public partial class MainMenu : CanvasLayer
{
    public override void _Ready()
    {
        Layer = 10;
        var root = new Control { Theme = UiTheme.Theme };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        // A dark gradient on the left so the menu reads over the battle.
        var shade = new TextureRect
        {
            Texture = new GradientTexture2D
            {
                Gradient = new Gradient
                {
                    Colors = new[] { new Color(0.02f, 0.03f, 0.05f, 0.92f), new Color(0.02f, 0.03f, 0.05f, 0.0f) },
                    Offsets = new[] { 0.35f, 1f },
                },
                FillFrom = new Vector2(0, 0),
                FillTo = new Vector2(1, 0),
                Width = 64,
                Height = 4,
            },
            StretchMode = TextureRect.StretchModeEnum.Scale,
        };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(shade);

        var column = new VBoxContainer();
        column.AnchorTop = 0; column.AnchorBottom = 1;
        column.OffsetLeft = 80; column.OffsetRight = 520; column.OffsetTop = 90; column.OffsetBottom = -60;
        column.AddThemeConstantOverride("separation", 10);
        root.AddChild(column);

        var title = UiTheme.Label("CONTINGENCY ZERO", 52, UiTheme.Ink);
        title.AddThemeConstantOverride("outline_size", 8);
        title.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.7f));
        column.AddChild(title);
        column.AddChild(UiTheme.Label("The Lattice and the Crucible. One planet's metal. No retreat.", 17, UiTheme.Dim));
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 40) });

        void Add(string text, System.Action act, string tip = "")
        {
            var b = UiTheme.Button(text, act, 300, 20);
            b.CustomMinimumSize = new Vector2(320, 48);
            b.Alignment = HorizontalAlignment.Left;
            b.TooltipText = tip;
            column.AddChild(b);
        }

        GameRoot g = GameRoot.Instance;
        Add("Skirmish", g.ShowSkirmishSetup, "Play against the computer on any map");
        Add("Campaign", g.ShowCampaign, "Story missions for both factions");
        Add("Multiplayer", g.ShowMultiplayer, "Play over a local network or the internet");
        Add("Map Editor", g.ShowEditor, "Make your own battlefields");
        Add("Replays", g.ShowReplays, "Watch saved matches");
        Add("Settings", () => AddChild(SettingsPanel.Create(() => { })));
        Add("Controls", () => AddChild(HelpPanel.Create()));
        Add("Quit", () => { Settings.Current.Save(); GetTree().Quit(); });

        var version = UiTheme.Label("v1.0 · Built in Godot", 13, UiTheme.Dim);
        version.AnchorTop = 1; version.AnchorBottom = 1; version.OffsetTop = -34; version.OffsetLeft = 80; version.OffsetRight = 600;
        root.AddChild(version);
    }
}
