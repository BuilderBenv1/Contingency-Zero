using System;
using ContingencyZero.Game.App;
using Godot;

namespace ContingencyZero.Game.Match;

/// <summary>
/// The RTS camera: orthographic, looking down at about 50 degrees from one of four compass
/// angles. Pans with the arrow keys / screen edges / middle-drag, zooms with the wheel, turns in
/// 90-degree steps with Q and E. (Letter keys are unit hotkeys, as in Total Annihilation.)
/// </summary>
public partial class CameraRig : Node3D
{
    public Camera3D Camera { get; private set; } = null!;
    /// <summary>The point on the ground the camera looks at.</summary>
    public Vector3 Focus { get; set; }
    public float MapWidth { get; set; } = 512;
    public float MapHeight { get; set; } = 512;
    public float MinSize { get; set; } = 36;
    public float MaxSize { get; set; } = 700;
    public bool InputEnabled { get; set; } = true;

    private float _size = 220;
    private float _targetSize = 220;
    // North-up and fairly steep, like Total Annihilation (not a 45-degree isometric diamond).
    private float _yaw = 0;
    private float _targetYaw = 0;
    private const float Pitch = 50f;
    private bool _dragging;
    private Vector2 _dragLast;

    public float Size => _size;

    public override void _Ready()
    {
        Camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = _size,
            Near = 1,
            Far = 4000,
            Current = true,
        };
        AddChild(Camera);
        Apply();
    }

    public void JumpTo(Vector3 point) => Focus = Clamp(point);

    public void SetZoom(float size)
    {
        _targetSize = Mathf.Clamp(size, MinSize, MaxSize);
        _size = _targetSize;
        Apply();
    }

    /// <summary>Forward along the ground (screen up) and right along the ground (screen right).</summary>
    public Vector3 GroundForward => new Vector3(-Mathf.Sin(_yaw), 0, -Mathf.Cos(_yaw));
    public Vector3 GroundRight => new Vector3(Mathf.Cos(_yaw), 0, -Mathf.Sin(_yaw));

    public override void _UnhandledInput(InputEvent e)
    {
        if (!InputEnabled) return;
        switch (e)
        {
            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed:
                _targetSize = Mathf.Max(MinSize, _targetSize * 0.87f);
                break;
            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed:
                _targetSize = Mathf.Min(MaxSize, _targetSize * 1.15f);
                break;
            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.Middle:
                _dragging = mb.Pressed;
                _dragLast = mb.Position;
                break;
            case InputEventMouseMotion mm when _dragging:
            {
                Vector2 d = mm.Position - _dragLast;
                _dragLast = mm.Position;
                float unitsPerPixel = _size / GetViewport().GetVisibleRect().Size.Y;
                Focus = Clamp(Focus - GroundRight * d.X * unitsPerPixel - GroundForward * d.Y * unitsPerPixel / Mathf.Sin(Mathf.DegToRad(Pitch)));
                break;
            }
            case InputEventKey { Pressed: true, Echo: false } k when k.Keycode == Key.Q:
                _targetYaw += Mathf.Pi / 2;
                break;
            case InputEventKey { Pressed: true, Echo: false } k when k.Keycode == Key.E:
                _targetYaw -= Mathf.Pi / 2;
                break;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (InputEnabled && GetViewport().GuiGetFocusOwner() is not LineEdit)
        {
            Vector3 move = Vector3.Zero;
            if (Input.IsKeyPressed(Key.Up)) move += GroundForward;
            if (Input.IsKeyPressed(Key.Down)) move -= GroundForward;
            if (Input.IsKeyPressed(Key.Right)) move += GroundRight;
            if (Input.IsKeyPressed(Key.Left)) move -= GroundRight;

            if (Settings.Current.EdgeScroll && DisplayServer.WindowIsFocused())
            {
                // Screen coordinates, not the viewport's: in a maximised window the title bar and
                // the taskbar sit between the game and the screen's top and bottom edges, so the
                // mouse can't reach the window's edge there. Scroll when it reaches the window's
                // edge or the usable screen's edge, whichever comes first.
                Vector2I g = DisplayServer.MouseGetPosition();
                Vector2I wp = DisplayServer.WindowGetPosition(), ws = DisplayServer.WindowGetSize();
                Rect2I u = DisplayServer.ScreenGetUsableRect(DisplayServer.WindowGetCurrentScreen());
                const int edge = 6;
                bool inX = g.X >= wp.X - edge && g.X <= wp.X + ws.X + edge;
                bool inY = g.Y >= Math.Min(wp.Y, u.Position.Y) - edge && g.Y <= Math.Max(wp.Y + ws.Y, u.End.Y) + edge;
                if (inX && inY)
                {
                    if (g.X < Math.Max(wp.X, u.Position.X) + edge) move -= GroundRight;
                    if (g.X > Math.Min(wp.X + ws.X, u.End.X) - edge - 1) move += GroundRight;
                    if (g.Y < Math.Max(wp.Y, u.Position.Y) + edge) move += GroundForward;
                    if (g.Y > Math.Min(wp.Y + ws.Y, u.End.Y) - edge - 1) move -= GroundForward;
                }
            }
            if (move != Vector3.Zero)
                Focus = Clamp(Focus + move.Normalized() * _size * 1.1f * Settings.Current.ScrollSpeed * dt);
        }

        _size = Mathf.Lerp(_size, _targetSize, 1 - Mathf.Exp(-12 * dt));
        _yaw = Mathf.LerpAngle(_yaw, _targetYaw, 1 - Mathf.Exp(-10 * dt));
        Apply();
    }

    private Vector3 Clamp(Vector3 p) => new(Mathf.Clamp(p.X, 0, MapWidth), p.Y, Mathf.Clamp(p.Z, 0, MapHeight));

    private void Apply()
    {
        if (Camera is null) return;
        Camera.Size = _size;
        float pitch = Mathf.DegToRad(Pitch);
        float dist = 900;
        var offset = new Vector3(Mathf.Sin(_yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(_yaw) * Mathf.Cos(pitch)) * dist;
        Camera.GlobalPosition = Focus + offset;
        Camera.LookAt(Focus, Vector3.Up);
    }
}
