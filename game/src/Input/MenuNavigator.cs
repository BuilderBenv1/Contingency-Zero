using System;
using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Match;
using Godot;

namespace ContingencyZero.Game.Controls;

/// <summary>
/// Makes every menu work with a controller, without each screen having to know about it:
/// <list type="bullet">
/// <item>remembers whether a pad or the mouse/keyboard was used last (<see cref="GamepadLast"/>);</item>
/// <item>when a menu screen or panel is showing and the pad is in use, puts keyboard focus on its
///   first button (Godot's own focus neighbours then let the d-pad / left stick walk it and A press);</item>
/// <item>B presses the screen's Back / Close / Done / Cancel / Resume button;</item>
/// <item>scrolls focused controls into view and draws a bright ring round the focused one;</item>
/// <item>shows an "A select  B back" strip while the pad is in use;</item>
/// <item>on the first run on a Steam Deck, applies the Steam Deck preset (bigger UI, hints on).</item>
/// </list>
/// A "menu" is a visible CanvasLayer with focusable controls at layer 20+ anywhere (pause,
/// game over, settings, help, dialogs) or at layer 10+ outside a match (title screen and the
/// screens it opens). In a match with no menu up, <see cref="GamepadController"/> owns the pad.
/// </summary>
public partial class MenuNavigator : Node
{
    public static MenuNavigator? Instance { get; private set; }

    /// <summary>True when the last input came from a controller (false after real mouse or key use).</summary>
    public static bool GamepadLast { get; private set; }
    public static event Action? DeviceChanged;

    /// <summary>The top-most menu layer showing, or null when only the battlefield and HUD are up.</summary>
    public static CanvasLayer? TopMenu => Instance?.FindTopMenu();
    public static bool MenuOpen => TopMenu is not null;

    private static readonly string[] BackWords = { "Back", "Close", "Done", "Cancel", "Resume" };

    private MenuHintLayer _hints = null!;
    private CanvasLayer? _cachedTop;
    private ulong _cachedFrame = ulong.MaxValue;

    public static void MarkGamepad()
    {
        if (GamepadLast) return;
        GamepadLast = true;
        DeviceChanged?.Invoke();
    }

    public static void MarkMouse()
    {
        if (!GamepadLast) return;
        GamepadLast = false;
        DeviceChanged?.Invoke();
    }

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        EnsureJoypadUiActions();
        _hints = new MenuHintLayer { Name = "MenuHints" };
        AddChild(_hints);
        GetViewport().GuiFocusChanged += OnFocusChanged;
        SteamDeck.ApplyFirstRunPreset();
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Godot's ui_* actions only partly cover pads by default; make sure A, B, d-pad and left stick all work.</summary>
    private static void EnsureJoypadUiActions()
    {
        void Button(string action, JoyButton b)
        {
            if (!InputMap.HasAction(action)) return;
            var ev = new InputEventJoypadButton { ButtonIndex = b, Device = -1 };
            if (!InputMap.ActionHasEvent(action, ev)) InputMap.ActionAddEvent(action, ev);
        }
        void Axis(string action, JoyAxis axis, float dir)
        {
            if (!InputMap.HasAction(action)) return;
            var ev = new InputEventJoypadMotion { Axis = axis, AxisValue = dir, Device = -1 };
            if (!InputMap.ActionHasEvent(action, ev)) InputMap.ActionAddEvent(action, ev);
        }
        Button("ui_accept", JoyButton.A);
        Button("ui_cancel", JoyButton.B);
        Button("ui_up", JoyButton.DpadUp);
        Button("ui_down", JoyButton.DpadDown);
        Button("ui_left", JoyButton.DpadLeft);
        Button("ui_right", JoyButton.DpadRight);
        Axis("ui_up", JoyAxis.LeftY, -1);
        Axis("ui_down", JoyAxis.LeftY, 1);
        Axis("ui_left", JoyAxis.LeftX, -1);
        Axis("ui_right", JoyAxis.LeftX, 1);
    }

    // ---- Device tracking and B ----------------------------------------------------------------

    public override void _Input(InputEvent e)
    {
        switch (e)
        {
            case InputEventJoypadButton jb:
                MarkGamepad();
                if (jb is { Pressed: true, ButtonIndex: JoyButton.B } && FindTopMenu() is { } top && !PopupOpen())
                {
                    if (PressBack(top)) GetViewport().SetInputAsHandled();
                }
                else if (jb.Pressed && jb.ButtonIndex is JoyButton.A or JoyButton.DpadUp or JoyButton.DpadDown or JoyButton.DpadLeft or JoyButton.DpadRight
                         && FindTopMenu() is { } menu && !FocusInside(menu) && !PopupOpen())
                {
                    // Nothing focused yet: the first press just lands on the first button.
                    if (FocusFirst(menu)) GetViewport().SetInputAsHandled();
                }
                break;
            case InputEventJoypadMotion jm when Math.Abs(jm.AxisValue) > 0.5f:
                MarkGamepad();
                break;
            case InputEventMouseMotion mm when !SyntheticInput.IsSynthetic(mm) && mm.Relative.LengthSquared() > 4:
                if (!(GamepadController.Current is { Active: true } pad && mm.Position.DistanceTo(pad.Cursor) < 3))
                    MarkMouse();
                break;
            case InputEventMouseButton mb when !SyntheticInput.IsSynthetic(mb):
                MarkMouse();
                break;
            case InputEventKey k when !SyntheticInput.IsSynthetic(k) && k.Pressed:
                MarkMouse();
                break;
        }
    }

    private bool PopupOpen() => GetViewport().GetEmbeddedSubwindows().Any(w => w.Visible && w is PopupMenu);

    /// <summary>Presses the screen's back-style button. Returns false if it has none (the title screen).</summary>
    public static bool PressBack(CanvasLayer top)
    {
        var buttons = Focusables(top).OfType<BaseButton>().Where(b => !b.Disabled).ToList();
        foreach (string word in BackWords)
        {
            var b = buttons.FirstOrDefault(x => x is Button btn && (btn.Text == word || btn.Text.StartsWith(word + " ") || btn.Text.StartsWith("< " + word)));
            if (b is null) continue;
            Audio.Sfx.Play(Audio.SfxKind.Click);
            b.EmitSignal(BaseButton.SignalName.Pressed);
            return true;
        }
        return false;
    }

    // ---- Finding menus and focus --------------------------------------------------------------

    private CanvasLayer? FindTopMenu()
    {
        ulong frame = Engine.GetProcessFrames();
        if (frame == _cachedFrame && (_cachedTop is null || IsInstanceValid(_cachedTop))) return _cachedTop;
        _cachedFrame = frame;
        CanvasLayer? best = null;
        foreach (CanvasLayer layer in AllLayers(GetTree().Root))
        {
            if (!layer.Visible || layer == _hints) continue;
            bool inMatch = InMatch(layer);
            if (layer.Layer < 10 || (inMatch && layer.Layer < 20)) continue;
            if (best is not null && layer.Layer < best.Layer) continue;
            if (!Focusables(layer).Any()) continue;
            best = layer; // later siblings at the same layer draw on top, so they win ties
        }
        _cachedTop = best;
        return best;
    }

    private static bool InMatch(Node n)
    {
        for (Node? p = n.GetParent(); p is not null; p = p.GetParent())
            if (p is MatchScene) return true;
        return false;
    }

    private static IEnumerable<CanvasLayer> AllLayers(Node n)
    {
        foreach (Node c in n.GetChildren())
        {
            if (c is CanvasLayer cl) yield return cl;
            if (c is Window) continue; // popups have their own viewport
            foreach (CanvasLayer x in AllLayers(c)) yield return x;
        }
    }

    /// <summary>Visible, enabled, focusable controls under a layer, in tree (reading) order; nested layers excluded.</summary>
    public static IEnumerable<Control> Focusables(Node root)
    {
        foreach (Node c in root.GetChildren())
        {
            if (c is CanvasLayer || c is Window) continue;
            if (c is Control ctl)
            {
                if (!ctl.Visible) continue;
                if (ctl.FocusMode == Control.FocusModeEnum.All && !(ctl is BaseButton { Disabled: true }) && ctl.IsVisibleInTree())
                    yield return ctl;
            }
            foreach (Control x in Focusables(c)) yield return x;
        }
    }

    private bool FocusInside(CanvasLayer layer)
    {
        Control? f = GetViewport().GuiGetFocusOwner();
        if (f is null || !f.IsVisibleInTree()) return false;
        for (Node? p = f.GetParent(); p is not null; p = p.GetParent())
        {
            if (p == layer) return true;
            if (p is CanvasLayer) return false;
        }
        return false;
    }

    /// <summary>
    /// Focuses a menu: where it was last time (coming back from a panel it opened), else its first
    /// control in reading order (a text field only if there is nothing else).
    /// </summary>
    public static bool FocusFirst(CanvasLayer layer)
    {
        if (Instance is { } nav && nav._lastFocus.TryGetValue(layer, out Control? last) && IsInstanceValid(last) && last.IsVisibleInTree()
            && last.FocusMode == Control.FocusModeEnum.All)
        {
            last.GrabFocus();
            return true;
        }
        var all = Focusables(layer).ToList();
        Control? first = all.FirstOrDefault(c => c is not LineEdit and not TextEdit) ?? all.FirstOrDefault();
        if (first is null) return false;
        first.GrabFocus();
        return true;
    }

    private readonly Dictionary<CanvasLayer, Control> _lastFocus = new();

    public override void _Process(double delta)
    {
        CanvasLayer? top = FindTopMenu();
        if (top is not null && FocusInside(top) && GetViewport().GuiGetFocusOwner() is { } owner) _lastFocus[top] = owner;
        if (GamepadLast && top is not null && !FocusInside(top) && !PopupOpen()) FocusFirst(top);
        if (_lastFocus.Count > 8)
            foreach (var dead in _lastFocus.Keys.Where(k => !IsInstanceValid(k)).ToList()) _lastFocus.Remove(dead);
        _hints.Show(GamepadLast && top is not null, GamepadLast && top is not null ? GetViewport().GuiGetFocusOwner() : null, top is not null && HasBack(top));
    }

    private static bool HasBack(CanvasLayer top) =>
        Focusables(top).OfType<Button>().Any(b => BackWords.Any(w => b.Text == w || b.Text.StartsWith(w + " ") || b.Text.StartsWith("< " + w)));

    private void OnFocusChanged(Control c)
    {
        // Keep the focused control on screen inside scrolling lists.
        for (Node? p = c.GetParent(); p is not null; p = p.GetParent())
        {
            if (p is ScrollContainer sc) sc.EnsureControlVisible(c);
            if (p is CanvasLayer) break;
        }
        if (GamepadLast) Audio.Sfx.Play(Audio.SfxKind.Hover);
    }
}

/// <summary>The focus ring and the "A select  B back" strip for menus, drawn above everything.</summary>
public partial class MenuHintLayer : CanvasLayer
{
    private MenuHintDraw _draw = null!;

    public override void _Ready()
    {
        Layer = 60;
        _draw = new MenuHintDraw { MouseFilter = Control.MouseFilterEnum.Ignore };
        _draw.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_draw);
    }

    public void Show(bool visible, Control? focus, bool hasBack)
    {
        _draw.Visible = visible;
        _draw.Focus = focus;
        _draw.HasBack = hasBack;
        if (visible) _draw.QueueRedraw();
    }
}

public partial class MenuHintDraw : Control
{
    public Control? Focus;
    public bool HasBack;

    public override void _Draw()
    {
        if (Focus is not null && IsInstanceValid(Focus) && Focus.IsVisibleInTree())
        {
            Rect2 r = Focus.GetGlobalRect().Grow(3);
            DrawRect(r, new Color(0, 0, 0, 0.6f), false, 5);
            DrawRect(r, UiTheme.Amber, false, 2.5f);
        }
        var items = new List<(string, string)> { ("DPAD", "Move"), ("A", "Select") };
        if (HasBack) items.Add(("B", "Back"));
        PadGlyphs.Strip(this, items, new Vector2(Size.X / 2, Size.Y - 26), centred: true);
    }
}
