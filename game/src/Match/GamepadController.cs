using System;
using ContingencyZero.Game.App;
using ContingencyZero.Game.UI;
using Godot;

namespace ContingencyZero.Game.Match;

/// <summary>
/// Plays the game with a controller (Steam Deck, Xbox or PlayStation pads) by driving a virtual
/// mouse cursor, so every bit of mouse code keeps working untouched:
///   right stick     move the cursor          left stick    pan the camera
///   triggers        zoom out (LT) / in (RT)  A             left click (select, confirm, place)
///   B               right click (order)      X             attack-move mode
///   Y               stop                     LB            next control group
///   RB              next idle builder        D-pad         walk the command card, A presses
///   Start           pause menu               Back          select the Commander
/// Joypad events are swallowed here so Godot's default ui_* focus actions don't fight the cursor.
/// On-screen hints appear while a pad is connected or being used (or always, per Settings).
/// </summary>
public partial class GamepadController : Node
{
    private const float Deadzone = 0.2f;
    private const float CursorSpeed = 1100f;   // logical pixels per second at full tilt

    private InputController _input = null!;
    private SimSession _session = null!;
    private CameraRig _rig = null!;
    private Selection _selection = null!;
    private GamepadOverlay _overlay = null!;
    private double _lastPadUse = -100;
    private bool _navigatingCard;
    private int _device;
    private Vector2 _lastSynthetic = new(-1000, -1000);
    private bool _aHeld;

    /// <summary>The command card, for d-pad navigation (set by the HUD once it exists).</summary>
    public CommandCard? Card { get; set; }
    /// <summary>Where the virtual cursor is, in viewport coordinates.</summary>
    public Vector2 Cursor { get; private set; }
    /// <summary>True while the player is using the pad (the drawn cursor shows; the mouse hides it again).</summary>
    public bool Active { get; private set; }
    /// <summary>Whether a pad is plugged in, or one has been used in this match.</summary>
    public bool Connected => Input.GetConnectedJoypads().Count > 0 || Now - _lastPadUse < 600;
    public bool HintsVisible => Connected || Settings.Current.GamepadHints;

    private static double Now => Time.GetTicksMsec() / 1000.0;

    public void Init(InputController input, SimSession session, CameraRig rig, Selection selection)
    {
        _input = input;
        _session = session;
        _rig = rig;
        _selection = selection;
    }

    public override void _Ready()
    {
        Cursor = GetViewport().GetVisibleRect().Size / 2;
        var layer = new CanvasLayer { Layer = 15, Name = "GamepadLayer" };
        AddChild(layer);
        _overlay = new GamepadOverlay { Pad = this, MouseFilter = Control.MouseFilterEnum.Ignore };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(_overlay);
    }

    // ---- Events ----------------------------------------------------------------------------------

    public override void _Input(InputEvent e)
    {
        if (_session is null) return;
        switch (e)
        {
            case InputEventJoypadButton jb:
                Touch(jb.Device);
                OnButton(jb.ButtonIndex, jb.Pressed);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventJoypadMotion jm:
                if (Math.Abs(jm.AxisValue) > Deadzone) Touch(jm.Device);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseMotion mm when Active:
                // The real mouse hides the virtual cursor; our own synthesized motion (and the OS cursor
                // warp that follows it) lands exactly where we put it, so it doesn't count.
                if (mm.Position.DistanceTo(_lastSynthetic) > 1.5f && mm.Position.DistanceTo(Cursor) > 1.5f) Active = false;
                break;
        }
    }

    private void Touch(int device)
    {
        _device = device;
        _lastPadUse = Now;
        if (!Active)
        {
            Active = true;
            MoveCursor(Cursor, force: true);
        }
    }

    private void OnButton(JoyButton b, bool pressed)
    {
        switch (b)
        {
            case JoyButton.A:
                if (_navigatingCard && Card is not null && Card.NavIndex >= 0)
                {
                    if (pressed) Card.NavPress();
                    return;
                }
                _aHeld = pressed;
                Click(MouseButton.Left, pressed);
                return;
            case JoyButton.B:
                Click(MouseButton.Right, pressed);
                return;
            case JoyButton.X when pressed:
                if (_input.Mode == ClickMode.Fight) _input.SetMode(ClickMode.Select);
                else if (_selection.Count > 0) _input.SetMode(ClickMode.Fight);
                return;
            case JoyButton.Y when pressed:
                if (_input.Mode != ClickMode.Select) _input.SetMode(ClickMode.Select);
                else _input.StopSelection();
                return;
            case JoyButton.LeftShoulder when pressed:
                _input.CycleGroup();
                return;
            case JoyButton.RightShoulder when pressed:
                _input.SelectNextIdleBuilder();
                return;
            case JoyButton.DpadUp when pressed: Nav(0, -1); return;
            case JoyButton.DpadDown when pressed: Nav(0, 1); return;
            case JoyButton.DpadLeft when pressed: Nav(-1, 0); return;
            case JoyButton.DpadRight when pressed: Nav(1, 0); return;
            case JoyButton.Start when pressed:
                // F10 is the keyboard's pause-menu key; the match scene handles it.
                SyntheticInput.Key(Key.F10, true);
                SyntheticInput.Key(Key.F10, false);
                return;
            case JoyButton.Back when pressed:
                _input.SelectCommander();
                return;
        }
    }

    private void Nav(int dx, int dy)
    {
        if (Card is null) return;
        if (Card.NavMove(dx, dy)) _navigatingCard = true;
    }

    private void LeaveCardNavigation()
    {
        if (!_navigatingCard) return;
        _navigatingCard = false;
        Card?.NavClear();
    }

    private void Click(MouseButton button, bool pressed)
    {
        LeaveCardNavigation();
        SyntheticInput.MouseButton(GetViewport(), Cursor, button, pressed);
    }

    // ---- Per frame -------------------------------------------------------------------------------

    public override void _Process(double delta)
    {
        if (_session is null) return;
        float dt = (float)delta;
        if (!Active) { _overlay.QueueRedraw(); return; }

        // Right stick: cursor, with a gentle curve so small tilts give fine control.
        Vector2 rs = Stick(JoyAxis.RightX, JoyAxis.RightY);
        if (rs != Vector2.Zero)
        {
            float mag = Mathf.Min(1, rs.Length());
            Vector2 step = rs.Normalized() * (mag * mag) * CursorSpeed * dt;
            LeaveCardNavigation();
            MoveCursor(Cursor + step);
        }

        // Left stick: pan the camera.
        Vector2 ls = Stick(JoyAxis.LeftX, JoyAxis.LeftY);
        if (ls != Vector2.Zero && _rig.InputEnabled)
        {
            Vector3 move = _rig.GroundRight * ls.X - _rig.GroundForward * ls.Y;
            _rig.JumpTo(_rig.Focus + move * _rig.Size * 1.1f * Settings.Current.ScrollSpeed * dt);
        }

        // Triggers: zoom.
        float zoomOut = Axis(JoyAxis.TriggerLeft), zoomIn = Axis(JoyAxis.TriggerRight);
        if (zoomOut > Deadzone || zoomIn > Deadzone)
            _rig.SetZoom(_rig.Size * (1 + (zoomOut - zoomIn) * 1.6f * dt));

        _overlay.QueueRedraw();
    }

    private float Axis(JoyAxis axis)
    {
        float v = Input.GetJoyAxis(_device, axis);
        return Math.Abs(v) < Deadzone ? 0 : v;
    }

    private Vector2 Stick(JoyAxis x, JoyAxis y)
    {
        var v = new Vector2(Input.GetJoyAxis(_device, x), Input.GetJoyAxis(_device, y));
        if (v.Length() < Deadzone) return Vector2.Zero;
        if (Math.Abs(v.X) > Deadzone || Math.Abs(v.Y) > Deadzone) _lastPadUse = Now;
        return v;
    }

    /// <summary>Moves the virtual cursor and tells the game about it as a mouse move.</summary>
    public void MoveCursor(Vector2 to, bool force = false)
    {
        Vector2 size = GetViewport().GetVisibleRect().Size;
        to = new Vector2(Mathf.Clamp(to.X, 0, size.X - 1), Mathf.Clamp(to.Y, 0, size.Y - 1));
        if (!force && to == Cursor) return;
        Vector2 rel = to - Cursor;
        Cursor = to;
        _lastSynthetic = to;
        SyntheticInput.MouseMove(GetViewport(), to, rel);
        // Keep the OS pointer with us so hover and edge-scrolling agree (harmless where unsupported).
        GetViewport().WarpMouse(to);
    }

    /// <summary>The hint strip's text, in button order.</summary>
    public string HintsText()
    {
        if (_navigatingCard) return "D-pad: choose   A: pick   B / stick: back to the cursor";
        return _input.Mode switch
        {
            ClickMode.Select => "A select   B order   X attack-move   Y stop   LB group   RB idle builder   D-pad build menu   Start menu   Back commander",
            ClickMode.Place or ClickMode.PlaceTemplate => "A place   B cancel   Y cancel   Right stick: aim   Triggers: zoom",
            _ => "A confirm   B cancel   Y cancel   Right stick: aim",
        };
    }
}

/// <summary>Draws the virtual cursor and the button hints.</summary>
public partial class GamepadOverlay : Control
{
    public GamepadController? Pad;
    private Font? _font;

    public override void _Ready()
    {
        _font = UiTheme.Theme.DefaultFont ?? ThemeDB.FallbackFont;
    }

    public override void _Draw()
    {
        if (Pad is null) return;
        if (Pad.HintsVisible)
        {
            Vector2 size = Size;
            string text = Pad.HintsText();
            Font font = _font ?? ThemeDB.FallbackFont;
            const int fontSize = 13;
            Vector2 extent = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize);
            // Above the command card, right-aligned, out of the way of the message feed on the left.
            var pos = new Vector2(size.X - 12 - extent.X, size.Y - 338);
            DrawRect(new Rect2(pos - new Vector2(8, 15), extent + new Vector2(16, 22)), new Color(0, 0, 0, 0.55f));
            DrawString(font, pos, text, HorizontalAlignment.Left, -1, fontSize, UiTheme.Dim);
        }
        if (Pad.Active)
        {
            Vector2 c = Pad.Cursor;
            var ring = new Color(1f, 0.95f, 0.6f, 0.95f);
            DrawArc(c, 9, 0, Mathf.Tau, 24, new Color(0, 0, 0, 0.8f), 4);
            DrawArc(c, 9, 0, Mathf.Tau, 24, ring, 2);
            DrawCircle(c, 2.2f, ring);
            foreach (Vector2 d in new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right })
                DrawLine(c + d * 11, c + d * 17, ring, 2);
        }
    }
}
