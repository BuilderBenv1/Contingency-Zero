using System;
using ContingencyZero.Game.App;
using Godot;

namespace ContingencyZero.Game.UI;

/// <summary>Settings: volumes, window, scrolling, health bars.</summary>
public static class SettingsPanel
{
    public static CanvasLayer Create(Action onClosed)
    {
        var layer = new CanvasLayer { Layer = 30 };
        var root = new Control { Theme = UiTheme.Theme };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(root);
        var shade = new ColorRect { Color = new Color(0, 0, 0, 0.6f) };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(shade);
        var centre = new CenterContainer();
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(centre);
        var body = new VBoxContainer { CustomMinimumSize = new Vector2(480, 0) };
        body.AddThemeConstantOverride("separation", 10);
        centre.AddChild(UiTheme.Panel(body, 22));

        Settings s = Settings.Current;
        body.AddChild(UiTheme.Title("SETTINGS", 28));

        HSlider Slider(string name, float value, Action<float> set)
        {
            var row = new HBoxContainer();
            var l = UiTheme.Label(name, 16);
            l.CustomMinimumSize = new Vector2(170, 0);
            row.AddChild(l);
            var slider = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05, Value = value, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(220, 24) };
            slider.ValueChanged += v => { set((float)v); s.Apply(); };
            row.AddChild(slider);
            body.AddChild(row);
            return slider;
        }

        CheckBox Check(string name, bool value, Action<bool> set)
        {
            var c = new CheckBox { Text = name, ButtonPressed = value };
            c.Toggled += v => { set(v); s.Apply(); };
            body.AddChild(c);
            return c;
        }

        Slider("Master volume", s.MasterVolume, v => s.MasterVolume = v);
        Slider("Effects volume", s.SfxVolume, v => s.SfxVolume = v);
        Slider("Music volume", s.MusicVolume, v => s.MusicVolume = v);
        var scroll = Slider("Scroll speed", (s.ScrollSpeed - 0.4f) / 1.6f, v => s.ScrollSpeed = 0.4f + v * 1.6f);
        Check("Fullscreen", s.Fullscreen, v => s.Fullscreen = v);
        Check("Scroll at screen edges", s.EdgeScroll, v => s.EdgeScroll = v);
        Check("Always show health bars", s.AlwaysShowHealthBars, v => s.AlwaysShowHealthBars = v);

        var nameRow = new HBoxContainer();
        var nl = UiTheme.Label("Player name", 16);
        nl.CustomMinimumSize = new Vector2(170, 0);
        nameRow.AddChild(nl);
        var name = new LineEdit { Text = s.PlayerName, MaxLength = 20, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        name.TextChanged += t => s.PlayerName = t.Length == 0 ? "Commander" : t;
        nameRow.AddChild(name);
        body.AddChild(nameRow);

        body.AddChild(UiTheme.Button("Done", () =>
        {
            s.Save();
            layer.QueueFree();
            onClosed();
        }));
        return layer;
    }
}

/// <summary>A reference card of controls and the game's rules.</summary>
public static class HelpPanel
{
    public const string Text =
@"[b]Camera[/b]   Arrow keys or screen edges to scroll · Mouse wheel to zoom · Middle-drag to pan · Q / E to turn the view · Click or drag the minimap

[b]Selecting[/b]   Left click a unit · Drag a box · Shift adds · Double-click (or Ctrl-click) selects every unit of that type on screen
Ctrl+A all combat units · Ctrl+B all builders · Ctrl+Z all of the selected types · Home your Commander
Ctrl+1..9 saves a group · 1..9 recalls it (twice to jump to it)

[b]Orders[/b]   Right click: move, attack an enemy, help build or repair a friendly unit, reclaim a wreck, or set a factory's rally point
Shift+right click queues orders · Ctrl+right click attack-moves
A attack / attack-move · S stop · M move · P patrol · G guard · R reclaim · D Breach Cannon · F fire state · Tab health bars

[b]Building[/b]   Select a builder, pick a structure, click to place (drag for a row, shift to keep placing). Right click cancels.
Factories: left click a unit to queue it (shift +5), right click to remove one.

[b]Economy[/b]   Metal and energy flow continuously. Everything being built draws from the flow; if you can't cover it, everything slows down (a stall).
Metal extractors go on the glowing metal spots. Solar collectors and wind turbines make energy. Storage stops surplus going to waste.
Wrecks, rocks and trees can be reclaimed by builders for metal and energy: winning a fight pays twice.

[b]Winning[/b]   Destroy the enemy Commander. Lose yours and you lose the match.
The Commander's Breach Cannon (D) destroys almost anything it touches, for 500 energy a shot.

[b]Game speed[/b]   + / - change speed in single player · Pause key pauses · Esc opens the menu";

    public static CanvasLayer Create()
    {
        var layer = new CanvasLayer { Layer = 31 };
        var root = new Control { Theme = UiTheme.Theme };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(root);
        var shade = new ColorRect { Color = new Color(0, 0, 0, 0.6f) };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(shade);
        var centre = new CenterContainer();
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(centre);
        var body = new VBoxContainer { CustomMinimumSize = new Vector2(900, 0) };
        body.AddThemeConstantOverride("separation", 10);
        centre.AddChild(UiTheme.Panel(body, 22));
        body.AddChild(UiTheme.Title("CONTROLS", 28));
        var text = new RichTextLabel { BbcodeEnabled = true, Text = Text, FitContent = true, CustomMinimumSize = new Vector2(860, 0), ScrollActive = false };
        text.AddThemeFontSizeOverride("normal_font_size", 15);
        text.AddThemeFontSizeOverride("bold_font_size", 15);
        text.AddThemeColorOverride("default_color", UiTheme.Ink);
        body.AddChild(text);
        body.AddChild(UiTheme.Button("Close", layer.QueueFree));
        return layer;
    }
}
