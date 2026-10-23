using Godot;

namespace ContingencyZero.Game.Match;

/// <summary>
/// Feeds made-up mouse, key and joypad events into Godot as if they came from the hardware, so
/// the gamepad's virtual cursor and the dev input scripts drive exactly the same code paths as
/// a real mouse. Positions are given in the viewport's (logical, UI-scaled) coordinates; they are
/// converted to window pixels because <see cref="Input.ParseInputEvent"/> expects what the
/// display server would have produced.
/// </summary>
public static class SyntheticInput
{
    /// <summary>Device id stamped on made-up mouse events, so real mouse use can be told apart from the gamepad cursor.</summary>
    public const int DeviceId = 0x5A17;

    public static bool IsSynthetic(InputEvent e) => e.Device == DeviceId;

    private static Vector2 ToWindow(Viewport vp, Vector2 logical) => vp.GetFinalTransform() * logical;

    public static void MouseMove(Viewport vp, Vector2 logical, Vector2 relative = default)
    {
        Vector2 p = ToWindow(vp, logical);
        Input.ParseInputEvent(new InputEventMouseMotion
        {
            Position = p,
            GlobalPosition = p,
            Relative = relative,
            ButtonMask = Input.GetMouseButtonMask(),
            Device = DeviceId,
        });
    }

    public static void MouseButton(Viewport vp, Vector2 logical, MouseButton button, bool pressed, bool shift = false, bool ctrl = false)
    {
        Vector2 p = ToWindow(vp, logical);
        MouseButtonMask mask = Input.GetMouseButtonMask();
        MouseButtonMask bit = button switch { Godot.MouseButton.Left => MouseButtonMask.Left, Godot.MouseButton.Right => MouseButtonMask.Right, Godot.MouseButton.Middle => MouseButtonMask.Middle, _ => 0 };
        mask = pressed ? mask | bit : mask & ~bit;
        Input.ParseInputEvent(new InputEventMouseButton
        {
            ButtonIndex = button,
            Pressed = pressed,
            Position = p,
            GlobalPosition = p,
            ButtonMask = mask,
            ShiftPressed = shift,
            CtrlPressed = ctrl,
            Device = DeviceId,
        });
    }

    public static void Key(Key key, bool pressed, bool shift = false, bool ctrl = false, string unicode = "")
    {
        var ev = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed, ShiftPressed = shift, CtrlPressed = ctrl, Device = DeviceId };
        if (unicode.Length > 0) ev.Unicode = unicode[0];
        Input.ParseInputEvent(ev);
    }

    public static void JoyButton(JoyButton button, bool pressed, int device = 0) =>
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = pressed, Device = device });

    public static void JoyAxis(JoyAxis axis, float value, int device = 0) =>
        Input.ParseInputEvent(new InputEventJoypadMotion { Axis = axis, AxisValue = value, Device = device });
}
