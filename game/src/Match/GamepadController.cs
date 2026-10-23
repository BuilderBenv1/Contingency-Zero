using System;
using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Controls;
using ContingencyZero.Game.UI;
using ContingencyZero.Game.View;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using Godot;

namespace ContingencyZero.Game.Match;

/// <summary>
/// Plays a match with a controller (Steam Deck, Xbox or PlayStation pads) by driving a virtual
/// mouse cursor, so every bit of mouse code in <see cref="InputController"/> keeps working:
///   left stick   move the cursor (accelerates; slows over units and snaps onto one when let go)
///   right stick  pan the camera                 LT / RT     zoom out / in
///   A            click: select, confirm, place (hold A and move for a box, or a row of structures)
///   B            cancel the armed order, else deselect
///   X            order at the cursor (the right click: move, attack, assist, reclaim, rally)
///   Y            radial order menu (stick picks, A or releasing Y confirms, B closes)
///   LB           tap: next control group; hold: shift (add to selection, queue orders, keep placing)
///   RB           select all army, again for all builders, again for the next idle builder
///   D-pad        walk the build menu of the selected builder / factory, A picks
///   L3           select every unit of the type under the cursor (the double-click)
///   Menu (Start) pause menu        View (Back)  tap: Commander; hold: steer on the minimap
/// Joypad events are taken here (so Godot's ui_* focus actions don't fight the cursor) unless a
/// menu is open, when <see cref="MenuNavigator"/> and the GUI get them. Button prompts show while
/// the pad was the last thing used (or always, per Settings).
/// </summary>
public partial class GamepadController : Node
{
    public static GamepadController? Current { get; private set; }

    private const float Deadzone = 0.18f;
    private const float SlowSpeed = 240f;   // logical pixels per second at full tilt, before acceleration
    private const float FastSpeed = 1300f;  // after about half a second at full tilt
    private const float RampTime = 0.5f;
    private const float SnapRadius = 34f;
    private const double HoldTime = 0.35;

    private InputController _input = null!;
    private SimSession _session = null!;
    private CameraRig _rig = null!;
    private Selection _selection = null!;
    private MatchScene? _match;
    private GamepadOverlay _overlay = null!;
    private int _device;
    private float _ramp;
    private bool _wasMoving;
    private Vector2? _snapTarget;

    private bool _aHeld, _lbHeld, _lbUsed;
    private int _rbStep;
    private double _rbAt = -100;
    private double _yDownAt;
    private double _viewDownAt = -1;
    private bool _navigatingCard;
    private Vector2 _beforeMinimap;
    private double _lastPadUse = -100;

    /// <summary>The command card, for d-pad navigation (found in the HUD).</summary>
    public CommandCard? Card { get; set; }
    /// <summary>Where the virtual cursor is, in viewport (logical) coordinates.</summary>
    public Vector2 Cursor { get; private set; }
    /// <summary>True while the player is using the pad (the drawn cursor shows; real mouse use hides it).</summary>
    public bool Active { get; private set; }
    public bool HintsVisible => !Inert && (MenuNavigator.GamepadLast || Settings.Current.GamepadHints) && !MenuNavigator.MenuOpen;

    // Radial order menu.
    public bool RadialOpen { get; private set; }
    public Vector2 RadialCentre { get; private set; }
    public int RadialHover { get; private set; } = -1;
    public IReadOnlyList<RadialItem> RadialItems => _radial;
    private readonly List<RadialItem> _radial = new();

    // Minimap steering.
    public bool MinimapMode { get; private set; }
    public Rect2 MinimapRect { get; private set; }

    public bool NavigatingCard => _navigatingCard && Card?.NavButton is not null;
    private bool Inert => _session is null || _match is null || _match.Kind == MatchKind.MenuBackground || !IsInsideTree();
    private static double Now => Time.GetTicksMsec() / 1000.0;

    public void Init(InputController input, SimSession session, CameraRig rig, Selection selection)
    {
        _input = input;
        _session = session;
        _rig = rig;
        _selection = selection;
        _selection.Changed += () => { if (_navigatingCard && Card?.NavButton is null) _navigatingCard = false; };
    }

    public override void _Ready()
    {
        for (Node? p = GetParent(); p is not null; p = p.GetParent())
            if (p is MatchScene m) { _match = m; break; }
        if (_match is not null && _match.Kind != MatchKind.MenuBackground) Current = this;
        Cursor = GetViewport().GetVisibleRect().Size / 2;
        var layer = new CanvasLayer { Layer = 16, Name = "GamepadLayer" };
        AddChild(layer);
        _overlay = new GamepadOverlay { Pad = this, MouseFilter = Control.MouseFilterEnum.Ignore };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(_overlay);
    }

    public override void _ExitTree()
    {
        if (Current == this) Current = null;
    }

    // ---- Events ----------------------------------------------------------------------------------

    public override void _Input(InputEvent e)
    {
        if (Inert) return;
        if (e is InputEventJoypadButton or InputEventJoypadMotion && MenuNavigator.MenuOpen)
        {
            // Menus have the pad; Menu still closes the pause menu.
            if (e is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.Start }) { PressKey(Key.F10); GetViewport().SetInputAsHandled(); }
            if (_aHeld) { _aHeld = false; Click(MouseButton.Left, false); }
            return;
        }
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
            case InputEventMouseMotion mm when Active && !SyntheticInput.IsSynthetic(mm):
                // The OS pointer warp that follows our own moves lands on the cursor; anything else is the real mouse.
                if (mm.Position.DistanceTo(Cursor) > 2f && mm.Relative.LengthSquared() > 1) Deactivate();
                break;
        }
    }

    private void Touch(int device)
    {
        _device = device;
        _lastPadUse = Now;
        MenuNavigator.MarkGamepad();
        if (!Active)
        {
            Active = true;
            Vector2 m = GetViewport().GetMousePosition();
            MoveCursor(GetViewport().GetVisibleRect().HasPoint(m) ? m : Cursor, force: true);
        }
    }

    private void Deactivate()
    {
        Active = false;
        CloseRadial();
        ExitMinimap(restore: false);
        LeaveCardNavigation();
    }

    private void OnButton(JoyButton b, bool pressed)
    {
        if (RadialOpen)
        {
            if (b is JoyButton.A or JoyButton.B or JoyButton.Y) { RadialButton(b, pressed); return; }
            if (!pressed || b is JoyButton.LeftShoulder or JoyButton.RightShoulder) return;
            CloseRadial(); // anything else closes the wheel and does its usual job
        }
        if (MinimapMode && b is JoyButton.B or JoyButton.Back && pressed) { ExitMinimap(restore: true); return; }

        if (NavigatingCard)
        {
            switch (b)
            {
                case JoyButton.A when pressed:
                    Card!.NavPress();
                    // A structure was picked: back to the cursor to place it. Factories stay for more.
                    if (_input.Mode is ClickMode.Place or ClickMode.PlaceTemplate) LeaveCardNavigation();
                    return;
                case JoyButton.A: return;
                case JoyButton.B when pressed: LeaveCardNavigation(); return;
                case JoyButton.B: return;
                case JoyButton.DpadUp or JoyButton.DpadDown or JoyButton.DpadLeft or JoyButton.DpadRight: break;
                default: LeaveCardNavigation(); break;
            }
        }

        switch (b)
        {
            case JoyButton.A:
                if (_aHeld == pressed) return;
                _aHeld = pressed;
                if (_lbHeld) _lbUsed = true;
                Click(MouseButton.Left, pressed, _lbHeld);
                return;
            case JoyButton.B when pressed:
                if (_aHeld) { _aHeld = false; Click(MouseButton.Left, false); }
                if (_input.Mode != ClickMode.Select) _input.SetMode(ClickMode.Select);
                else if (_selection.Count > 0) { _selection.Clear(); Audio.Sfx.Play(Audio.SfxKind.Hover, -6); }
                return;
            case JoyButton.X:
                if (_lbHeld) _lbUsed = true;
                Click(MouseButton.Right, pressed, _lbHeld);
                return;
            case JoyButton.Y when pressed:
                if (_input.Mode == ClickMode.PlaceTemplate) { _input.RotateTemplate(); return; }
                OpenRadial();
                return;
            case JoyButton.LeftShoulder:
                if (pressed) { _lbHeld = true; _lbUsed = false; }
                else
                {
                    _lbHeld = false;
                    if (!_lbUsed) _input.CycleGroup();
                }
                return;
            case JoyButton.RightShoulder when pressed:
                if (_lbHeld) _lbUsed = true;
                CycleSelectSet();
                return;
            case JoyButton.LeftStick when pressed:
                // Two quick clicks: every unit of that type on screen.
                Click(MouseButton.Left, true); Click(MouseButton.Left, false);
                Click(MouseButton.Left, true); Click(MouseButton.Left, false);
                return;
            case JoyButton.RightStick when pressed:
                PressKey(Key.E); // turn the view a quarter
                return;
            case JoyButton.DpadUp when pressed: Nav(0, -1); return;
            case JoyButton.DpadDown when pressed: Nav(0, 1); return;
            case JoyButton.DpadLeft when pressed: Nav(-1, 0); return;
            case JoyButton.DpadRight when pressed: Nav(1, 0); return;
            case JoyButton.Start when pressed:
                PressKey(Key.F10); // the keyboard's pause-menu key; MatchScene handles it
                return;
            case JoyButton.Back:
                if (pressed) _viewDownAt = Now;
                else if (_viewDownAt >= 0)
                {
                    if (!MinimapMode) _input.SelectCommander();
                    _viewDownAt = -1;
                }
                return;
        }
    }

    private static void PressKey(Key k, bool shift = false, bool ctrl = false)
    {
        SyntheticInput.Key(k, true, shift, ctrl);
        SyntheticInput.Key(k, false, shift, ctrl);
    }

    private void Post(string text) => _match?.Hud?.Post(text, UiTheme.Amber, 3);

    /// <summary>RB: all army, then all builders, then the next idle builder, then round again.</summary>
    private void CycleSelectSet()
    {
        _rbStep = Now - _rbAt < 2.0 ? (_rbStep + 1) % 3 : 0;
        _rbAt = Now;
        switch (_rbStep)
        {
            case 0:
            {
                var army = _input.OwnUnits(d => d.CanMove && !d.CanBuild && d.IsArmed).ToList();
                _selection.Set(army);
                Post(army.Count == 0 ? "No army yet" : $"Army: {army.Count} units");
                break;
            }
            case 1:
            {
                var builders = _input.OwnUnits(d => d.CanBuild && d.CanMove).ToList();
                _selection.Set(builders);
                Post($"Builders: {builders.Count}");
                break;
            }
            default:
                _input.SelectNextIdleBuilder();
                break;
        }
        Audio.Sfx.Play(Audio.SfxKind.Select, -10);
    }

    // ---- Build menu (d-pad over the command card) -------------------------------------------------

    private CommandCard? FindCard()
    {
        if (Card is not null && IsInstanceValid(Card)) return Card;
        Card = _match?.Hud is { } hud ? FindFirst<CommandCard>(hud) : null;
        return Card;
    }

    private void Nav(int dx, int dy)
    {
        CommandCard? card = FindCard();
        if (card is null) return;
        if (!NavigatingCard)
        {
            if (!card.NavMove(0, 0)) { Post("Select a builder or factory to open its build menu"); return; }
            _navigatingCard = true;
            // Start on the build grid rather than the order buttons (orders live on the Y menu).
            for (int i = 0; i < 6 && card.NavButton?.GetParent() is not GridContainer; i++) card.NavMove(0, 1);
            if (card.NavButton?.GetParent() is not GridContainer)
                for (int i = 0; i < 6; i++) card.NavMove(0, -1);
            Audio.Sfx.Play(Audio.SfxKind.Hover);
            return;
        }
        if (card.NavMove(dx, dy)) Audio.Sfx.Play(Audio.SfxKind.Hover, -4);
    }

    private void LeaveCardNavigation()
    {
        if (!_navigatingCard) return;
        _navigatingCard = false;
        if (Card is not null && IsInstanceValid(Card)) Card.NavClear();
    }

    // ---- Radial order menu -------------------------------------------------------------------------

    public sealed record RadialItem(string Label, bool Enabled, Action Run);

    private void OpenRadial()
    {
        _radial.Clear();
        var mine = _selection.Ids.Where(id => _session.World.Units.IsValid(id) && _session.World.Units.Owner[id] == _session.LocalPlayer).ToList();
        var defs = mine.Select(id => _session.World.Catalog[_session.World.Units.TypeIndex[id]]).ToList();
        bool mobile = defs.Any(d => d.CanMove), armed = defs.Any(d => d.IsArmed);
        void Add(string label, bool ok, Action run) => _radial.Add(new RadialItem(label, ok, run));
        Add("Move", mobile, () => _input.SetMode(ClickMode.Move));
        Add("Attack-move", mobile && armed, () => _input.SetMode(ClickMode.Fight));
        Add("Patrol", mobile, () => _input.SetMode(ClickMode.Patrol));
        Add("Guard", mobile, () => _input.SetMode(ClickMode.Assist));
        Add("Reclaim", defs.Any(d => d.CanReclaim), () => _input.SetMode(ClickMode.Reclaim));
        Add("Stop", mine.Count > 0, _input.StopSelection);
        Add("D-Gun", defs.Any(d => d.HasDGun), () => _input.SetMode(ClickMode.DGun));
        Add(FireLabel(mine), armed, () => _input.CycleFireState(mine));
        Add("Make group", mine.Count > 0, MakeGroup);
        Add("Same type", mine.Count > 0, () => PressKey(Key.Z, ctrl: true));
        RadialOpen = true;
        RadialHover = -1;
        _yDownAt = Now;
        Vector2 size = GetViewport().GetVisibleRect().Size;
        const float r = GamepadOverlay.RadialRadius + 70;
        RadialCentre = new Vector2(Mathf.Clamp(Cursor.X, r, size.X - r), Mathf.Clamp(Cursor.Y, r, size.Y - r));
        Audio.Sfx.Play(Audio.SfxKind.Hover);
    }

    private string FireLabel(List<int> mine)
    {
        int armed = mine.FirstOrDefault(id => _session.World.Catalog[_session.World.Units.TypeIndex[id]].IsArmed, -1);
        if (armed < 0) return "Fire state";
        return _session.World.Units.Fire[armed] switch
        {
            Sim.State.FireState.HoldFire => "Fire: hold",
            Sim.State.FireState.ReturnFire => "Fire: return",
            _ => "Fire: at will",
        };
    }

    /// <summary>Saves the selection as the lowest empty control group (Ctrl+number, so the usual notice shows).</summary>
    private void MakeGroup()
    {
        for (int g = 1; g <= 9; g++)
        {
            if (_selection.Group(g).Any(_session.World.Units.IsValid)) continue;
            PressKey(Key.Key0 + g, ctrl: true);
            return;
        }
        Post("All nine groups are in use (LB cycles them)");
    }

    private void RadialButton(JoyButton b, bool pressed)
    {
        switch (b)
        {
            case JoyButton.A when pressed:
                ConfirmRadial();
                return;
            case JoyButton.Y when !pressed:
                // Hold-and-flick: releasing Y on a highlighted item picks it. A quick tap leaves the menu open.
                if (Now - _yDownAt > HoldTime && RadialHover >= 0) ConfirmRadial();
                return;
            case JoyButton.Y when pressed:
            case JoyButton.B when pressed:
                CloseRadial();
                return;
        }
    }

    private void ConfirmRadial()
    {
        if (RadialHover < 0 || RadialHover >= _radial.Count) return;
        RadialItem item = _radial[RadialHover];
        if (!item.Enabled) { Audio.Sfx.Play(Audio.SfxKind.Error, -6); return; }
        CloseRadial();
        Audio.Sfx.Play(Audio.SfxKind.Click);
        item.Run();
    }

    private void CloseRadial()
    {
        RadialOpen = false;
        RadialHover = -1;
    }

    // ---- Minimap steering --------------------------------------------------------------------------

    private void EnterMinimap()
    {
        Minimap? mm = _match?.Hud is { } hud ? FindFirst<Minimap>(hud) : null;
        if (mm is null || !mm.IsVisibleInTree()) return;
        MinimapRect = mm.GetGlobalRect();
        MinimapMode = true;
        _beforeMinimap = Cursor;
        if (_aHeld) { _aHeld = false; Click(MouseButton.Left, false); }
        MoveCursor(MinimapRect.GetCenter());
        Audio.Sfx.Play(Audio.SfxKind.Hover);
    }

    private void ExitMinimap(bool restore)
    {
        if (!MinimapMode) return;
        MinimapMode = false;
        if (_aHeld) { _aHeld = false; Click(MouseButton.Left, false); }
        if (restore) MoveCursor(_beforeMinimap);
    }

    // ---- Clicks and the cursor ---------------------------------------------------------------------

    private void Click(MouseButton button, bool pressed, bool shift = false)
    {
        SyntheticInput.MouseButton(GetViewport(), Cursor, button, pressed, shift);
    }

    /// <summary>Moves the virtual cursor and tells the game about it as a mouse move.</summary>
    public void MoveCursor(Vector2 to, bool force = false)
    {
        Rect2 bounds = MinimapMode ? MinimapRect.Grow(-1) : new Rect2(Vector2.Zero, GetViewport().GetVisibleRect().Size - Vector2.One);
        to = new Vector2(Mathf.Clamp(to.X, bounds.Position.X, bounds.End.X), Mathf.Clamp(to.Y, bounds.Position.Y, bounds.End.Y));
        if (!force && to.DistanceSquaredTo(Cursor) < 0.0001f) return;
        Vector2 rel = to - Cursor;
        Cursor = to;
        SyntheticInput.MouseMove(GetViewport(), to, rel);
        // Keep the OS pointer with us so hover, tooltips and edge-scrolling agree (harmless where unsupported).
        GetViewport().WarpMouse(to);
    }

    // ---- Per frame ---------------------------------------------------------------------------------

    public override void _Process(double delta)
    {
        if (Inert) return;
        _overlay.QueueRedraw();
        if (!Active || MenuNavigator.MenuOpen) return;
        float dt = (float)Math.Min(delta, 0.1);

        if (_viewDownAt >= 0 && Now - _viewDownAt > HoldTime && !MinimapMode)
        {
            EnterMinimap();
            _viewDownAt = -1;
        }

        Vector2 ls = Stick(JoyAxis.LeftX, JoyAxis.LeftY);
        if (RadialOpen)
        {
            if (ls.Length() > 0.5f && _radial.Count > 0)
            {
                // Item 0 is straight up, then clockwise.
                float a = Mathf.PosMod(Mathf.Atan2(ls.X, -ls.Y), Mathf.Tau);
                float step = Mathf.Tau / _radial.Count;
                int hover = (int)Mathf.PosMod(Mathf.Round(a / step), _radial.Count);
                if (hover != RadialHover) Audio.Sfx.Play(Audio.SfxKind.Hover, -8);
                RadialHover = hover;
            }
        }
        else
        {
            MoveWithStick(ls, dt);
        }

        // Right stick: pan the camera.
        Vector2 rs = Stick(JoyAxis.RightX, JoyAxis.RightY);
        if (rs != Vector2.Zero && _rig.InputEnabled)
        {
            float mag = Mathf.Min(1, rs.Length());
            Vector3 move = (_rig.GroundRight * rs.X - _rig.GroundForward * rs.Y).Normalized() * mag * mag;
            _rig.JumpTo(_rig.Focus + move * _rig.Size * 1.2f * Settings.Current.ScrollSpeed * dt);
            if (Active) SyntheticInput.MouseMove(GetViewport(), Cursor); // ghosts and hover follow the ground under the cursor
        }

        // Triggers: zoom.
        float zoomOut = Axis(JoyAxis.TriggerLeft), zoomIn = Axis(JoyAxis.TriggerRight);
        if ((zoomOut > Deadzone || zoomIn > Deadzone) && _rig.InputEnabled)
        {
            _rig.SetZoom(_rig.Size * (1 + (zoomOut - zoomIn) * 1.6f * dt));
            SyntheticInput.MouseMove(GetViewport(), Cursor);
        }
    }

    private void MoveWithStick(Vector2 ls, float dt)
    {
        if (ls == Vector2.Zero)
        {
            _ramp = 0;
            if (_wasMoving) _snapTarget = MinimapMode || _aHeld || _input.Mode is ClickMode.Place or ClickMode.PlaceTemplate ? null : NearestUnitOnScreen(Cursor, SnapRadius);
            _wasMoving = false;
            if (_snapTarget is { } t)
            {
                Vector2 next = Cursor.Lerp(t, 1 - Mathf.Exp(-18 * dt));
                if (next.DistanceTo(t) < 0.75f) { next = t; _snapTarget = null; }
                MoveCursor(next);
            }
            return;
        }
        _snapTarget = null;
        _wasMoving = true;
        float mag = Mathf.Min(1, ls.Length());
        // Acceleration: holding the stick near full tilt ramps from fine to fast.
        _ramp = mag > 0.85f ? Mathf.Min(1, _ramp + dt / RampTime) : Mathf.Max(0, _ramp - dt * 3);
        float speed = Mathf.Lerp(SlowSpeed, FastSpeed, _ramp * _ramp) * mag * mag;
        if (MinimapMode) speed *= 0.5f;
        // Friction over units makes them easier to land on (not while dragging a box).
        else if (!_aHeld && _input.UnitUnderMouse(Cursor) >= 0) speed *= 0.45f;
        Vector2 dir = ls / Math.Max(1, ls.Length());
        MoveCursor(Cursor + dir * speed * dt);

        // Pushing against the screen edge scrolls, as the mouse would.
        Vector2 size = GetViewport().GetVisibleRect().Size;
        if (!MinimapMode && _rig.InputEnabled)
        {
            Vector3 push = Vector3.Zero;
            if (Cursor.X <= 1 && dir.X < -0.3f) push -= _rig.GroundRight;
            if (Cursor.X >= size.X - 2 && dir.X > 0.3f) push += _rig.GroundRight;
            if (Cursor.Y <= 1 && dir.Y < -0.3f) push += _rig.GroundForward;
            if (Cursor.Y >= size.Y - 2 && dir.Y > 0.3f) push -= _rig.GroundForward;
            if (push != Vector3.Zero) _rig.JumpTo(_rig.Focus + push.Normalized() * _rig.Size * 1.1f * Settings.Current.ScrollSpeed * dt);
        }
    }

    /// <summary>The screen position of the visible unit nearest a point, within a radius.</summary>
    private Vector2? NearestUnitOnScreen(Vector2 at, float radius)
    {
        if (_match is null) return null;
        Camera3D cam = _rig.Camera;
        Vector2? best = null;
        float bestD = radius;
        foreach (var (_, pos, _, height) in _match.Units.VisibleUnits)
        {
            Vector3 p = pos + Vector3.Up * height * 0.4f;
            if (cam.IsPositionBehind(p)) continue;
            Vector2 s = cam.UnprojectPosition(p);
            float d = s.DistanceTo(at);
            if (d < bestD) { bestD = d; best = s; }
        }
        return best;
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
        _lastPadUse = Now;
        // Rescale so motion starts from zero at the edge of the deadzone.
        return v.Normalized() * Mathf.Min(1, (v.Length() - Deadzone) / (1 - Deadzone));
    }

    private static T? FindFirst<T>(Node n) where T : class
    {
        foreach (Node c in n.GetChildren())
        {
            if (c is T t) return t;
            if (FindFirst<T>(c) is { } found) return found;
        }
        return null;
    }

    // ---- Prompts ----------------------------------------------------------------------------------

    /// <summary>The prompt strip for what the pad does right now, as (button, label) pairs.</summary>
    public List<(string Button, string Label)> Hints()
    {
        if (RadialOpen) return new() { ("LS", "Choose"), ("A", "Confirm"), ("B", "Close") };
        if (MinimapMode) return new() { ("LS", "Steer"), ("A", "Jump the camera here"), ("B", "Back") };
        if (NavigatingCard) return new() { ("DPAD", "Choose"), ("A", "Pick"), ("B", "Back to the cursor") };
        switch (_input.Mode)
        {
            case ClickMode.Place:
                return new() { ("A", "Place (hold: a row)"), ("LB", "Hold: keep placing"), ("B", "Cancel"), ("RS", "Camera"), ("LT", "Zoom") };
            case ClickMode.PlaceTemplate:
                return new() { ("A", "Stamp"), ("Y", "Turn"), ("LB", "Hold: keep placing"), ("B", "Cancel") };
            case ClickMode.Select:
                break;
            default:
                return new() { ("A", "Confirm at cursor"), ("LB", "Hold: queue"), ("B", "Cancel") };
        }
        var h = new List<(string, string)> { ("A", "Select"), ("X", "Order"), ("Y", "Orders"), ("B", "Deselect") };
        if (Card?.IsVisibleInTree() == true) h.Add(("DPAD", "Build"));
        h.Add(("LB", "Groups"));
        h.Add(("RB", "Army / builders"));
        h.Add(("VIEW", "Commander"));
        h.Add(("START", "Menu"));
        return h;
    }
}

/// <summary>Draws the virtual cursor, the radial order menu and the button prompts.</summary>
public partial class GamepadOverlay : Control
{
    public const float RadialRadius = 138f;
    public GamepadController? Pad;

    public override void _Draw()
    {
        if (Pad is null) return;
        Font font = UiTheme.Theme.DefaultFont ?? ThemeDB.FallbackFont;
        if (Pad.HintsVisible)
        {
            // Centred above the selection panel, between the minimap and the command card.
            // Wraps onto two rows when the gap is narrow (1280x800 with a big interface).
            float left = 256, right = Size.X - 476, room = right - left - 20, cx = (left + right) / 2;
            var hints = Pad.Hints();
            if (PadGlyphs.StripWidth(hints) <= room)
                PadGlyphs.Strip(this, hints, new Vector2(cx, Size.Y - 196), centred: true);
            else
            {
                int half = (hints.Count + 1) / 2;
                PadGlyphs.Strip(this, hints.Take(half).ToList(), new Vector2(cx, Size.Y - 230), centred: true);
                PadGlyphs.Strip(this, hints.Skip(half).ToList(), new Vector2(cx, Size.Y - 196), centred: true);
            }
        }
        if (Pad.MinimapMode)
            DrawRect(Pad.MinimapRect.Grow(3), UiTheme.Amber, false, 2);
        if (Pad.RadialOpen) DrawRadial(font);
        if (Pad.Active && !Controls.MenuNavigator.MenuOpen && !Pad.RadialOpen) DrawCursor();
    }

    private void DrawCursor()
    {
        Vector2 c = Pad!.Cursor;
        var ring = new Color(1f, 0.95f, 0.6f, 0.95f);
        DrawArc(c, 10, 0, Mathf.Tau, 28, new Color(0, 0, 0, 0.8f), 4.5f);
        DrawArc(c, 10, 0, Mathf.Tau, 28, ring, 2.2f);
        DrawCircle(c, 2.4f, ring);
        foreach (Vector2 d in new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right })
        {
            DrawLine(c + d * 12, c + d * 19, new Color(0, 0, 0, 0.8f), 4);
            DrawLine(c + d * 12, c + d * 19, ring, 2);
        }
    }

    private void DrawRadial(Font font)
    {
        var items = Pad!.RadialItems;
        if (items.Count == 0) return;
        Vector2 c = Pad.RadialCentre;
        DrawCircle(c, RadialRadius + 58, new Color(0, 0, 0, 0.45f));
        DrawArc(c, RadialRadius + 58, 0, Mathf.Tau, 64, UiTheme.PanelEdge, 1.5f);
        float step = Mathf.Tau / items.Count;
        for (int i = 0; i < items.Count; i++)
        {
            float a = i * step; // 0 = up, clockwise
            var dir = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
            Vector2 p = c + dir * RadialRadius;
            bool hover = i == Pad.RadialHover;
            var item = items[i];
            const int fs = 16;
            Vector2 sz = font.GetStringSize(item.Label, HorizontalAlignment.Left, -1, fs);
            var r = new Rect2(p - new Vector2(sz.X / 2 + 12, 17), new Vector2(sz.X + 24, 34));
            Color bg = hover ? new Color(0.3f, 0.28f, 0.14f, 0.97f) : new Color(0.1f, 0.1f, 0.095f, 0.94f);
            Color edge = hover ? UiTheme.Amber : UiTheme.PanelEdge;
            DrawStyleBox(UiTheme.Box(bg, edge, hover ? 2 : 1, 3, 0), r);
            Color ink = !item.Enabled ? new Color(0.45f, 0.45f, 0.42f) : hover ? Colors.White : UiTheme.Ink;
            DrawString(font, new Vector2(p.X - sz.X / 2, p.Y + fs * 0.36f), item.Label, HorizontalAlignment.Left, -1, fs, ink);
        }
        if (Pad.RadialHover >= 0)
        {
            float a = Pad.RadialHover * step;
            var dir = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
            DrawLine(c + dir * 16, c + dir * (RadialRadius - 26), UiTheme.Amber, 3);
        }
        DrawCircle(c, 14, new Color(0.1f, 0.1f, 0.095f, 0.95f));
        DrawArc(c, 14, 0, Mathf.Tau, 24, UiTheme.Amber, 2);
        string title = "ORDERS";
        Vector2 ts = font.GetStringSize(title, HorizontalAlignment.Left, -1, 13);
        DrawString(font, c + new Vector2(-ts.X / 2, -RadialRadius - 30), title, HorizontalAlignment.Left, -1, 13, UiTheme.Dim);
    }
}
