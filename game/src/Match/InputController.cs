using System;
using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Game.Audio;
using ContingencyZero.Game.View;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.State;
using Godot;

namespace ContingencyZero.Game.Match;

/// <summary>What the next left click means.</summary>
public enum ClickMode
{
    Select,
    Move,
    Fight,
    Patrol,
    Reclaim,
    Assist,
    DGun,
    Place,
    /// <summary>Stamping a saved build template.</summary>
    PlaceTemplate,
}

/// <summary>
/// Turns mouse and keyboard into selections and commands, Total Annihilation style: left click
/// selects (drag for a box, double-click for all of a type, shift adds), right click does the
/// sensible thing for what's under the cursor (move, attack, reclaim, assist, rally), and letter
/// hotkeys arm a command for the next click (A attack-move, S stop, D Breach Cannon, R reclaim,
/// G guard, P patrol, M move, F fire state). Ctrl+number saves a group, number recalls it.
/// In reclaim and guard modes a left drag on the ground orders work on a whole area. Ctrl+T saves
/// the selected structures as a build template; a template is stamped like a single structure,
/// with R turning it. Shift+right click on the ground sets a factory's secondary (ferry) rally.
/// A gamepad drives all of this through a virtual cursor (see <see cref="GamepadController"/>).
/// </summary>
public partial class InputController : Node
{
    /// <summary>Smallest drag, in world units, that counts as an area order rather than a click.</summary>
    public const float MinAreaRadius = 6f;

    private SimSession _session = null!;
    private CameraRig _rig = null!;
    private TerrainView _terrain = null!;
    private UnitView _units = null!;
    private OverlayView _overlay = null!;
    private Selection _selection = null!;
    private TemplatePreview _preview = null!;

    public ClickMode Mode { get; private set; } = ClickMode.Select;
    public int PlaceType { get; private set; } = -1;
    /// <summary>The template being stamped (already turned), when <see cref="Mode"/> is PlaceTemplate.</summary>
    public BuildTemplate? Template { get; private set; }
    /// <summary>Radius of the area drag in progress (reclaim / guard modes), or 0.</summary>
    public float AreaRadius { get; private set; }
    public GamepadController Gamepad { get; private set; } = null!;

    public event Action? ModeChanged;
    /// <summary>Raised for UI-facing messages ("Not enough energy", "Can't build there").</summary>
    public event Action<string>? Notice;
    /// <summary>Ctrl+T (or the command card button) with own structures selected: the UI should ask for a name.</summary>
    public event Action<IReadOnlyList<int>>? TemplateSaveRequested;

    private bool _boxing;
    private Vector2 _boxStart;
    private Vector2 _mouse;
    private ulong _lastClickMs;
    private int _lastClickUnit = -1;
    private readonly ulong[] _lastGroupPress = new ulong[10];
    private Vector3? _lineStart;
    private Vector3? _areaStart;
    private TemplatePlan? _plan;
    private int _lastGroupCycled;
    private int _lastIdleBuilder = -1;

    private World World => _session.World;
    private int Me => _session.LocalPlayer;

    /// <summary>The rubber-band rectangle being dragged, in screen space, if any.</summary>
    public Rect2? Box => _boxing && (_mouse - _boxStart).Length() > 6 ? new Rect2(_boxStart, _mouse - _boxStart).Abs() : null;

    public void Init(SimSession session, CameraRig rig, TerrainView terrain, UnitView units, OverlayView overlay, Selection selection)
    {
        _session = session;
        _rig = rig;
        _terrain = terrain;
        _units = units;
        _overlay = overlay;
        _selection = selection;
        _preview = new TemplatePreview { Name = "TemplatePreview" };
        AddChild(_preview);
        _preview.Init(session.World, terrain);
        Gamepad = new GamepadController { Name = "Gamepad" };
        AddChild(Gamepad);
        Gamepad.Init(this, session, rig, selection);
    }

    public void SetMode(ClickMode mode, int placeType = -1)
    {
        Mode = mode;
        PlaceType = mode == ClickMode.Place ? placeType : -1;
        if (mode != ClickMode.Place) { _overlay.Ghost = (-1, Vector3.Zero, false); _overlay.QueuedGhosts.Clear(); }
        if (mode != ClickMode.PlaceTemplate) { Template = null; _plan = null; _preview.Items.Clear(); }
        _lineStart = null;
        _areaStart = null;
        AreaRadius = 0;
        _overlay.DragLine = null;
        _overlay.DragCircle = null;
        ModeChanged?.Invoke();
    }

    public void BeginPlacement(int type) => SetMode(ClickMode.Place, type);

    /// <summary>Arms a template for stamping with the next click.</summary>
    public void BeginTemplatePlacement(BuildTemplate template)
    {
        if (template.Items.Count == 0) return;
        if (!MySelected().Any(id => World.Catalog[World.Units.TypeIndex[id]].CanBuild))
        {
            Notice?.Invoke("Select a builder first");
            return;
        }
        SetMode(ClickMode.PlaceTemplate);
        Template = template;
        UpdateGhost();
        ModeChanged?.Invoke();
    }

    /// <summary>Turns the armed template a quarter turn (R while placing).</summary>
    public void RotateTemplate()
    {
        if (Mode != ClickMode.PlaceTemplate || Template is null) return;
        Template = Template.Rotated(World.Catalog);
        UpdateGhost();
        ModeChanged?.Invoke();
    }

    /// <summary>Asks the UI to save the selected structures as a template (Ctrl+T).</summary>
    public void RequestTemplateSave()
    {
        var structures = MySelected().Where(id => World.Catalog[World.Units.TypeIndex[id]].IsStructure && World.Units.Complete[id]).ToList();
        if (structures.Count == 0)
        {
            Notice?.Invoke("Select some of your structures first, then press Ctrl+T");
            Sfx.Play(SfxKind.Error, -6);
            return;
        }
        TemplateSaveRequested?.Invoke(structures);
    }

    /// <summary>What the HUD shows under the top bar for the current mode.</summary>
    public string HintText()
    {
        switch (Mode)
        {
            case ClickMode.Move: return "MOVE: click the ground";
            case ClickMode.Fight:
                return AreaRadius >= MinAreaRadius ? $"ATTACK AREA: {AreaRadius:0} units"
                    : "ATTACK: click an enemy, the ground to attack-move (Ctrl: fire at the ground), or drag a circle to clear an area";
            case ClickMode.Patrol: return "PATROL: click the far end of the route";
            case ClickMode.Reclaim:
                return AreaRadius >= MinAreaRadius ? $"RECLAIM AREA: {AreaRadius:0} units" : "RECLAIM: click a wreck, rock or tree, or drag a circle to clear an area";
            case ClickMode.Assist:
                return AreaRadius >= MinAreaRadius ? $"REPAIR AREA: {AreaRadius:0} units" : "GUARD / ASSIST: click a friendly unit, or drag a circle to repair and help build everything in it";
            case ClickMode.DGun: return "BREACH CANNON: click a target (500 energy)";
            case ClickMode.Place:
                return PlaceType >= 0 ? $"BUILD {World.Catalog[PlaceType].Name.ToUpperInvariant()}: click to place, drag for a row, shift to keep placing, right-click to cancel" : "";
            case ClickMode.PlaceTemplate:
            {
                if (Template is null) return "";
                int ok = _plan?.ValidCount ?? 0, total = _plan?.Items.Count ?? Template.Items.Count;
                string fit = ok == total ? $"{total} structures" : $"{ok} of {total} structures fit";
                return $"TEMPLATE {Template.Name.ToUpperInvariant()}: click to place {fit}, R turns it, shift keeps placing, right-click cancels";
            }
            default: return "";
        }
    }

    // ---- Picking ---------------------------------------------------------------------------------

    private Vector3? GroundUnderMouse(Vector2 screen)
    {
        Camera3D cam = _rig.Camera;
        return _terrain.RayHit(cam.ProjectRayOrigin(screen), cam.ProjectRayNormal(screen));
    }

    /// <summary>Where a screen point meets the ground, for other input systems (gamepad, scripts).</summary>
    public Vector3? GroundAt(Vector2 screen) => GroundUnderMouse(screen);

    /// <summary>The visible unit closest to the cursor, within its on-screen size.</summary>
    public int UnitUnderMouse(Vector2 screen)
    {
        Camera3D cam = _rig.Camera;
        float pxPerUnit = GetViewport().GetVisibleRect().Size.Y / cam.Size;
        int best = -1;
        float bestD = float.MaxValue;
        foreach (var (id, pos, radius, height) in _units.VisibleUnits)
        {
            if (cam.IsPositionBehind(pos)) continue;
            Vector2 s = cam.UnprojectPosition(pos + new Vector3(0, height * 0.45f, 0));
            float d = s.DistanceTo(screen);
            float reach = Math.Max(10, (radius + 1) * pxPerUnit);
            if (d < reach && d < bestD) { bestD = d; best = id; }
        }
        return best;
    }

    private int FeatureUnderMouse(Vector3 ground)
    {
        int best = -1;
        float bestD = float.MaxValue;
        foreach (Feature f in World.Features)
        {
            if (f.Gone) continue;
            float x = f.Position.X.Raw / 65536f, z = f.Position.Y.Raw / 65536f;
            float d = new Vector2(x - ground.X, z - ground.Z).Length();
            float r = f.Radius.Raw / 65536f + 1.5f;
            if (d < r && d < bestD) { bestD = d; best = f.Id; }
        }
        return best;
    }

    private static FixVec2 ToFix(Vector3 p) => new(Fix64.FromRaw((long)(p.X * 65536)), Fix64.FromRaw((long)(p.Z * 65536)));

    // ---- Input -----------------------------------------------------------------------------------

    private int[]? _salvageSeen;
    private double _salvageTimer;

    /// <summary>Watches the local player's salvage progress and announces each newly unlocked design.</summary>
    public override void _Process(double delta)
    {
        if (_session is null || Me < 0 || Me >= World.Players.Length) return;
        _salvageTimer -= delta;
        if (_salvageTimer > 0) return;
        _salvageTimer = 0.25;
        int[] progress = World.Players[Me].SalvageProgress;
        if (_salvageSeen is null || _salvageSeen.Length != progress.Length) { _salvageSeen = (int[])progress.Clone(); return; }
        for (int t = 0; t < progress.Length; t++)
        {
            if (progress[t] == _salvageSeen[t]) continue;
            int needed = World.SalvageNeeded(t);
            UnitDef def = World.Catalog[t];
            if (progress[t] >= needed && _salvageSeen[t] < needed)
            {
                Notice?.Invoke($"Salvage complete: your factories can now build the enemy {def.Name}");
                Sfx.Play(SfxKind.BuildComplete, -4);
            }
            else
            {
                Notice?.Invoke($"Salvage: {def.Name} {progress[t]}/{needed}");
            }
            _salvageSeen[t] = progress[t];
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_session is null) return;
        if (e is InputEventMouseMotion mm)
        {
            _mouse = mm.Position;
            if (!_boxing) _overlay.HoverUnit = UnitUnderMouse(mm.Position);
            _overlay.HoverFeature = !_boxing && _overlay.HoverUnit < 0 && GroundUnderMouse(mm.Position) is { } hg ? FeatureUnderMouse(hg) : -1;
            UpdateGhost();
            UpdateAreaDrag();
            return;
        }

        if (e is InputEventMouseButton mb)
        {
            _mouse = mb.Position;
            if (mb.ButtonIndex == MouseButton.Left) OnLeft(mb);
            else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed) OnRight(mb);
            return;
        }

        if (e is InputEventKey { Pressed: true } k) OnKey(k);
    }

    private void OnLeft(InputEventMouseButton mb)
    {
        bool shift = mb.ShiftPressed;
        if (Mode == ClickMode.Place)
        {
            Vector3? g = GroundUnderMouse(mb.Position);
            if (g is null) return;
            if (mb.Pressed)
            {
                _lineStart = g;
                return;
            }
            // Released: place one, or a row if dragged.
            Vector3 start = _lineStart ?? g.Value;
            _lineStart = null;
            _overlay.DragLine = null;
            PlaceRow(start, g.Value, shift);
            if (!shift) SetMode(ClickMode.Select);
            return;
        }

        if (Mode == ClickMode.PlaceTemplate)
        {
            if (!mb.Pressed) return;
            StampTemplate(shift);
            if (!shift) SetMode(ClickMode.Select);
            return;
        }

        if (Mode is ClickMode.Reclaim or ClickMode.Assist or ClickMode.Fight)
        {
            // Press starts a possible area drag; release decides between an area order and a plain click.
            if (mb.Pressed)
            {
                _areaStart = GroundUnderMouse(mb.Position);
                AreaRadius = 0;
                return;
            }
            Vector3? start = _areaStart;
            _areaStart = null;
            _overlay.DragCircle = null;
            float radius = AreaRadius;
            AreaRadius = 0;
            if (start is { } s && radius >= MinAreaRadius) IssueArea(s, radius, shift);
            else ExecuteMode(mb.Position, shift);
            if (!shift) SetMode(ClickMode.Select); else ModeChanged?.Invoke();
            return;
        }

        if (Mode != ClickMode.Select)
        {
            if (!mb.Pressed) return;
            ExecuteMode(mb.Position, shift);
            if (!shift) SetMode(ClickMode.Select);
            return;
        }

        if (mb.Pressed)
        {
            _boxing = true;
            _boxStart = mb.Position;
            return;
        }

        if (!_boxing) return;
        Rect2? dragged = Box; // read before clearing _boxing, which makes Box null
        _boxing = false;
        if (dragged is { } box && box.Size.Length() > 6)
        {
            BoxSelect(box, shift);
            return;
        }

        int hit = UnitUnderMouse(mb.Position);
        ulong now = Time.GetTicksMsec();
        bool doubleClick = hit >= 0 && hit == _lastClickUnit && now - _lastClickMs < 350;
        _lastClickMs = now;
        _lastClickUnit = hit;

        if (hit < 0)
        {
            if (!shift) _selection.Clear();
            return;
        }
        if (doubleClick || mb.CtrlPressed)
        {
            SelectAllOfTypeOnScreen(World.Units.TypeIndex[hit], World.Units.Owner[hit], shift);
            return;
        }
        if (shift && World.Units.Owner[hit] == Me) _selection.Toggle(hit);
        else _selection.Set(new[] { hit });
        Sfx.Play(SfxKind.Select, -10);
    }

    private void BoxSelect(Rect2 box, bool add)
    {
        Camera3D cam = _rig.Camera;
        var inBox = new List<int>();
        foreach (var (id, pos, _, _) in _units.VisibleUnits)
        {
            if (World.Units.Owner[id] != Me && Me >= 0) continue;
            if (cam.IsPositionBehind(pos)) continue;
            if (box.HasPoint(cam.UnprojectPosition(pos))) inBox.Add(id);
        }
        // Prefer mobile units: structures only get picked up if nothing else is in the box.
        var mobile = inBox.Where(id => World.Catalog[World.Units.TypeIndex[id]].CanMove).ToList();
        var chosen = mobile.Count > 0 ? mobile : inBox;
        if (add) _selection.Add(chosen); else _selection.Set(chosen);
        if (chosen.Count > 0) Sfx.Play(SfxKind.Select, -10);
    }

    private void SelectAllOfTypeOnScreen(int type, int owner, bool add)
    {
        Rect2 screen = GetViewport().GetVisibleRect();
        Camera3D cam = _rig.Camera;
        var ids = _units.VisibleUnits
            .Where(v => World.Units.TypeIndex[v.Id] == type && World.Units.Owner[v.Id] == owner && World.Units.Complete[v.Id]
                        && !cam.IsPositionBehind(v.Pos) && screen.HasPoint(cam.UnprojectPosition(v.Pos)))
            .Select(v => v.Id);
        if (add) _selection.Add(ids); else _selection.Set(ids);
    }

    private List<int> MySelected() =>
        _selection.Ids.Where(id => World.Units.IsValid(id) && World.Units.Owner[id] == Me).ToList();

    private void OnRight(InputEventMouseButton mb)
    {
        if (Mode != ClickMode.Select)
        {
            SetMode(ClickMode.Select);
            return;
        }
        var mine = MySelected();
        if (mine.Count == 0 || Me < 0) return;
        bool queue = mb.ShiftPressed;

        int hit = UnitUnderMouse(mb.Position);
        Vector3? g = GroundUnderMouse(mb.Position);
        if (g is null) return;

        bool onlyFactories = mine.All(id => World.Catalog[World.Units.TypeIndex[id]].IsFactory);
        if (mb.CtrlPressed && onlyFactories)
        {
            ClearRallies();
            return;
        }
        if (mb.CtrlPressed || mb.AltPressed)
        {
            GroupMove(mine, g.Value, CommandType.Fight, queue);
            return;
        }

        // Clicked an enemy: attack it.
        if (hit >= 0 && World.AreEnemies(Me, World.Units.Owner[hit]))
        {
            int armed = 0;
            foreach (int id in mine)
            {
                UnitDef def = World.Catalog[World.Units.TypeIndex[id]];
                if (def.IsArmed && !def.IsFactory)
                {
                    Issue(new Command(0, Me, CommandType.Attack, id, TargetId: hit, Queue: queue));
                    armed++;
                }
            }
            var unarmed = mine.Where(id => !World.Catalog[World.Units.TypeIndex[id]].IsArmed && World.Catalog[World.Units.TypeIndex[id]].CanMove).ToList();
            if (unarmed.Count > 0) GroupMove(unarmed, g.Value, CommandType.Move, queue);
            if (armed > 0) { _overlay.Mark(g.Value, new Color(1, 0.3f, 0.25f)); Sfx.Play(SfxKind.Acknowledge, -8); }
            return;
        }

        // Clicked a friendly unit: builders help or repair, everyone else guards it.
        if (hit >= 0 && !World.AreEnemies(Me, World.Units.Owner[hit]) && !mine.Contains(hit))
        {
            UnitDef target = World.Catalog[World.Units.TypeIndex[hit]];
            foreach (int id in mine)
            {
                UnitDef def = World.Catalog[World.Units.TypeIndex[id]];
                if (def.IsFactory) continue;
                if (def.CanAssist || (def.CanMove && !target.IsStructure))
                    Issue(new Command(0, Me, CommandType.Assist, id, TargetId: hit, Queue: queue));
                else if (def.CanMove)
                    Issue(new Command(0, Me, CommandType.Move, id, ToFix(g.Value), Queue: queue));
            }
            _overlay.Mark(g.Value, new Color(1f, 0.95f, 0.4f));
            Sfx.Play(SfxKind.Acknowledge, -8);
            return;
        }

        // Clicked a wreck, rock or tree: reclaimers strip it.
        int feature = FeatureUnderMouse(g.Value);
        if (feature >= 0 && mine.Any(id => World.Catalog[World.Units.TypeIndex[id]].CanReclaim))
        {
            var others = new List<int>();
            foreach (int id in mine)
            {
                UnitDef def = World.Catalog[World.Units.TypeIndex[id]];
                if (def.CanReclaim) Issue(new Command(0, Me, CommandType.Reclaim, id, TargetId: feature, Queue: queue));
                else if (def.CanMove) others.Add(id);
            }
            if (others.Count > 0) GroupMove(others, g.Value, CommandType.Move, queue);
            _overlay.Mark(g.Value, new Color(0.8f, 0.5f, 1f));
            Sfx.Play(SfxKind.Acknowledge, -8);
            return;
        }

        // Ground: factories set rally points (shift adds another stop to the rotation, up to four), everything else moves.
        int factories = 0;
        foreach (int id in mine)
            if (World.Catalog[World.Units.TypeIndex[id]].IsFactory)
            {
                Issue(new Command(0, Me, CommandType.SetRally, id, ToFix(g.Value), Queue: queue));
                factories++;
            }
        var movers = mine.Where(id => World.Catalog[World.Units.TypeIndex[id]].CanMove).ToList();
        if (movers.Count > 0) GroupMove(movers, g.Value, CommandType.Move, queue);
        _overlay.Mark(g.Value, factories > 0 && movers.Count == 0 && queue ? new Color(0.4f, 0.9f, 1f) : new Color(0.4f, 1f, 0.4f));
        if (factories > 0 && queue && movers.Count == 0) Notice?.Invoke("Rally stop added: new units take the stops in turn (Ctrl+right-click clears)");
        Sfx.Play(SfxKind.Acknowledge, -8);
    }

    /// <summary>
    /// Sends a group to a point in a loose formation that keeps their relative layout, so they
    /// don't all fight over the same spot.
    /// </summary>
    private void GroupMove(List<int> ids, Vector3 target, CommandType type, bool queue)
    {
        if (ids.Count == 0) return;
        if (ids.Count == 1)
        {
            Issue(new Command(0, Me, type, ids[0], ToFix(target), Queue: queue));
            return;
        }
        float spacing = 0;
        Vector2 centroid = Vector2.Zero;
        foreach (int id in ids)
        {
            spacing = Math.Max(spacing, World.Catalog[World.Units.TypeIndex[id]].Radius.Raw / 65536f * 2.4f);
            centroid += new Vector2(World.Units.Position[id].X.Raw, World.Units.Position[id].Y.Raw) / 65536f;
        }
        centroid /= ids.Count;
        spacing = Math.Max(spacing, 4f);
        int cols = (int)Math.Ceiling(Math.Sqrt(ids.Count));

        // Facing of the formation: from the group toward the target.
        Vector2 dir = new Vector2(target.X, target.Z) - centroid;
        if (dir.LengthSquared() < 1) dir = new Vector2(0, -1);
        dir = dir.Normalized();
        Vector2 side = new(-dir.Y, dir.X);

        // Order units by where they stand across and along the direction of travel.
        var sorted = ids.OrderBy(id =>
        {
            var p = new Vector2(World.Units.Position[id].X.Raw, World.Units.Position[id].Y.Raw) / 65536f - centroid;
            return -p.Dot(dir);
        }).ToList();
        for (int row = 0; row * cols < sorted.Count; row++)
        {
            var rowIds = sorted.Skip(row * cols).Take(cols).OrderBy(id =>
            {
                var p = new Vector2(World.Units.Position[id].X.Raw, World.Units.Position[id].Y.Raw) / 65536f - centroid;
                return p.Dot(side);
            }).ToList();
            for (int c = 0; c < rowIds.Count; c++)
            {
                float across = (c - (rowIds.Count - 1) / 2f) * spacing;
                float along = -row * spacing;
                Vector2 spot = new Vector2(target.X, target.Z) + side * across + dir * along;
                Issue(new Command(0, Me, type, rowIds[c], new FixVec2(Fix64.FromRaw((long)(spot.X * 65536)), Fix64.FromRaw((long)(spot.Y * 65536))), Queue: queue));
            }
        }
    }

    private void ExecuteMode(Vector2 screen, bool queue)
    {
        var mine = MySelected();
        Vector3? g = GroundUnderMouse(screen);
        if (g is null || mine.Count == 0) return;
        int hit = UnitUnderMouse(screen);

        switch (Mode)
        {
            case ClickMode.Move:
                GroupMove(mine.Where(id => World.Catalog[World.Units.TypeIndex[id]].CanMove).ToList(), g.Value, CommandType.Move, queue);
                _overlay.Mark(g.Value, new Color(0.4f, 1f, 0.4f));
                break;
            case ClickMode.Fight:
                if (hit >= 0 && World.AreEnemies(Me, World.Units.Owner[hit]))
                {
                    foreach (int id in mine)
                        if (World.Catalog[World.Units.TypeIndex[id]].IsArmed)
                            Issue(new Command(0, Me, CommandType.Attack, id, TargetId: hit, Queue: queue));
                }
                else if (Input.IsKeyPressed(Key.Ctrl))
                {
                    foreach (int id in mine)
                        if (World.Catalog[World.Units.TypeIndex[id]].IsArmed)
                            Issue(new Command(0, Me, CommandType.Attack, id, ToFix(g.Value), Queue: queue)); // attack ground
                }
                else
                {
                    GroupMove(mine.Where(id => World.Catalog[World.Units.TypeIndex[id]].CanMove).ToList(), g.Value, CommandType.Fight, queue);
                }
                _overlay.Mark(g.Value, new Color(1f, 0.55f, 0.2f));
                break;
            case ClickMode.Patrol:
                foreach (int id in mine)
                    if (World.Catalog[World.Units.TypeIndex[id]].CanMove)
                        Issue(new Command(0, Me, CommandType.Patrol, id, ToFix(g.Value), Queue: queue));
                _overlay.Mark(g.Value, new Color(0.4f, 0.6f, 1f));
                break;
            case ClickMode.Reclaim:
            {
                int f = FeatureUnderMouse(g.Value);
                if (f < 0) { Notice?.Invoke("Click a wreck, rock or tree to reclaim it, or drag a circle to clear an area"); Sfx.Play(SfxKind.Error, -6); return; }
                foreach (int id in mine)
                    if (World.Catalog[World.Units.TypeIndex[id]].CanReclaim)
                        Issue(new Command(0, Me, CommandType.Reclaim, id, TargetId: f, Queue: queue));
                _overlay.Mark(g.Value, new Color(0.8f, 0.5f, 1f));
                break;
            }
            case ClickMode.Assist:
                if (hit < 0 || World.AreEnemies(Me, World.Units.Owner[hit])) { Sfx.Play(SfxKind.Error, -6); return; }
                foreach (int id in mine)
                    if (id != hit) Issue(new Command(0, Me, CommandType.Assist, id, TargetId: hit, Queue: queue));
                _overlay.Mark(g.Value, new Color(1f, 0.95f, 0.4f));
                break;
            case ClickMode.DGun:
                foreach (int id in mine)
                    if (World.Catalog[World.Units.TypeIndex[id]].HasDGun)
                        Issue(new Command(0, Me, CommandType.DGun, id, ToFix(g.Value), Queue: queue));
                _overlay.Mark(g.Value, new Color(1f, 0.5f, 0.1f));
                break;
        }
        Sfx.Play(SfxKind.Acknowledge, -8);
    }

    // ---- Area orders -----------------------------------------------------------------------------

    private static Color AreaColour(ClickMode mode) => mode switch
    {
        ClickMode.Reclaim => new Color(0.8f, 0.5f, 1f, 0.8f),
        ClickMode.Fight => new Color(1f, 0.35f, 0.25f, 0.85f),
        _ => new Color(1f, 0.95f, 0.4f, 0.8f),
    };

    private void UpdateAreaDrag()
    {
        if (_areaStart is not { } start || Mode is not (ClickMode.Reclaim or ClickMode.Assist or ClickMode.Fight)) return;
        if (!Input.IsMouseButtonPressed(MouseButton.Left)) return;
        Vector3? g = GroundUnderMouse(_mouse);
        if (g is null) return;
        float r = new Vector2(g.Value.X - start.X, g.Value.Z - start.Z).Length();
        bool was = AreaRadius >= MinAreaRadius;
        AreaRadius = r;
        _overlay.DragCircle = r >= MinAreaRadius ? (start, r, AreaColour(Mode)) : null;
        if (was || r >= MinAreaRadius) ModeChanged?.Invoke();
    }

    /// <summary>
    /// Area order for the current mode: reclaim (Reclaim), repair / help build (Assist) or attack
    /// every enemy (Fight) inside a circle, given to every selected unit that can do it.
    /// </summary>
    public void IssueArea(Vector3 centre, float radius, bool queue) => IssueArea(AreaTypeFor(Mode), centre, radius, queue);

    /// <summary>The area command a click mode drags out.</summary>
    public static CommandType AreaTypeFor(ClickMode mode) => mode switch
    {
        ClickMode.Reclaim => CommandType.ReclaimArea,
        ClickMode.Fight => CommandType.AttackArea,
        _ => CommandType.RepairArea,
    };

    /// <summary>Issues an area command (ReclaimArea, RepairArea or AttackArea) to every selected unit able to carry it out.</summary>
    public void IssueArea(CommandType type, Vector3 centre, float radius, bool queue)
    {
        var mine = MySelected();
        int sent = 0;
        int r = (int)Math.Round(Math.Clamp(radius, MinAreaRadius, 200f));
        foreach (int id in mine)
        {
            UnitDef def = World.Catalog[World.Units.TypeIndex[id]];
            bool able = type switch
            {
                CommandType.ReclaimArea => def.CanReclaim && def.CanMove,
                CommandType.AttackArea => def.IsArmed && def.CanMove,
                _ => def.CanAssist && def.CanMove,
            };
            if (!able) continue;
            Issue(new Command(0, Me, type, id, ToFix(centre), Arg: r, Queue: queue));
            sent++;
        }
        Color colour = type switch
        {
            CommandType.ReclaimArea => AreaColour(ClickMode.Reclaim),
            CommandType.AttackArea => AreaColour(ClickMode.Fight),
            _ => AreaColour(ClickMode.Assist),
        };
        if (sent == 0)
        {
            Notice?.Invoke(type == CommandType.AttackArea ? "No armed units selected" : "No builders selected");
            Sfx.Play(SfxKind.Error, -6);
            return;
        }
        _overlay.Mark(centre, colour);
        Sfx.Play(SfxKind.Acknowledge, -8);
    }

    // ---- Building --------------------------------------------------------------------------------

    private void PlaceRow(Vector3 a, Vector3 b, bool queue)
    {
        var builders = MySelected().Where(id => World.Catalog[World.Units.TypeIndex[id]].CanBuild).ToList();
        if (builders.Count == 0 || PlaceType < 0) return;
        UnitDef def = World.Catalog[PlaceType];
        float step = def.Footprint * MapData.TileSize + (def.Footprint >= 2 ? MapData.TileSize : 0);
        Vector3 d = b - a;
        int count = Math.Max(1, (int)(d.Length() / step) + 1);
        if (count > 20) count = 20;
        bool first = true;
        int placed = 0;
        for (int i = 0; i < count; i++)
        {
            Vector3 p = count == 1 ? a : a + d.Normalized() * step * i;
            FixVec2 fp = ToFix(p);
            if (World.CanPlace(PlaceType, Me, fp) != PlacementResult.Ok) continue;
            // The first builder takes the job; the rest help it.
            Issue(new Command(0, Me, CommandType.Build, builders[0], fp, TypeIndex: PlaceType, Queue: queue || !first));
            for (int k = 1; k < builders.Count; k++)
                Issue(new Command(0, Me, CommandType.Build, builders[k], fp, TypeIndex: PlaceType, Queue: queue || !first));
            first = false;
            placed++;
        }
        if (placed == 0)
        {
            Notice?.Invoke(def.NeedsMetalSpot ? "Extractors must go on a metal spot" : def.NeedsWater ? "This must be built in deep water" : "Can't build there");
            Sfx.Play(SfxKind.Error, -6);
        }
        else
        {
            Sfx.Play(SfxKind.Acknowledge, -8);
        }
    }

    /// <summary>Issues queued Build orders for every structure of the armed template that fits.</summary>
    private void StampTemplate(bool queue)
    {
        var builders = MySelected().Where(id => World.Catalog[World.Units.TypeIndex[id]].CanBuild).ToList();
        if (builders.Count == 0 || Template is null) return;
        UpdateGhost();
        if (_plan is null || _plan.ValidCount == 0)
        {
            Notice?.Invoke("None of the template fits there");
            Sfx.Play(SfxKind.Error, -6);
            return;
        }
        bool first = true;
        int skipped = 0;
        foreach (var (type, centre, valid) in _plan.Items)
        {
            if (!valid) { skipped++; continue; }
            foreach (int b in builders)
                Issue(new Command(0, Me, CommandType.Build, b, centre, TypeIndex: type, Queue: queue || !first));
            first = false;
        }
        if (skipped > 0) Notice?.Invoke($"{skipped} structure{(skipped == 1 ? "" : "s")} of the template skipped: no room or no metal spot");
        Sfx.Play(SfxKind.Acknowledge, -8);
    }

    private void UpdateGhost()
    {
        if (Mode == ClickMode.PlaceTemplate) { UpdateTemplateGhost(); return; }
        if (Mode != ClickMode.Place || PlaceType < 0) return;
        Vector3? g = GroundUnderMouse(_mouse);
        if (g is null) return;
        UnitDef def = World.Catalog[PlaceType];
        FixVec2 fp = ToFix(g.Value);
        var result = World.CanPlace(PlaceType, Me, fp, out int tx, out int ty);
        FixVec2 c = Terrain.FootprintCentre(tx, ty, def.Footprint);
        float x = c.X.Raw / 65536f, z = c.Y.Raw / 65536f;
        float y = def.NeedsWater ? _terrain.WaterHeight : _terrain.HeightAt(x, z);
        _overlay.Ghost = (PlaceType, new Vector3(x, y, z), result == PlacementResult.Ok);
        if (_lineStart is { } ls && Input.IsMouseButtonPressed(MouseButton.Left))
            _overlay.DragLine = (ls, g.Value, new Color(0.4f, 0.9f, 1f, 0.7f));
    }

    private void UpdateTemplateGhost()
    {
        _preview.Items.Clear();
        _plan = null;
        if (Template is null || Template.Items.Count == 0) return;
        Vector3? g = GroundUnderMouse(_mouse);
        if (g is null) return;
        int bid = MySelected().FirstOrDefault(id => World.Catalog[World.Units.TypeIndex[id]].CanBuild, -1);
        if (bid < 0) return;
        UnitDef builder = World.Catalog[World.Units.TypeIndex[bid]];
        int anchorFp = World.Catalog.TryIndexOf(Template.Items[0].Type, out int at) ? World.Catalog[at].Footprint : 1;
        var (tx, ty) = Terrain.FootprintOrigin(ToFix(g.Value), anchorFp);
        _plan = TemplatePlan.Make(World, Me, Template, tx, ty, type => Array.IndexOf(builder.BuildOptions, type) >= 0);
        _preview.Items.AddRange(_plan.Items);
    }

    // ---- Keys ------------------------------------------------------------------------------------

    private void OnKey(InputEventKey k)
    {
        Key key = k.Keycode;
        if (key >= Key.Key0 && key <= Key.Key9)
        {
            int g = (int)(key - Key.Key0);
            if (k.CtrlPressed) { _selection.SaveGroup(g); Notice?.Invoke($"Group {g} set"); }
            else if (k.ShiftPressed) _selection.AddToGroup(g);
            else RecallGroup(g);
            return;
        }

        var mine = MySelected();
        switch (key)
        {
            case Key.Escape when Mode != ClickMode.Select:
                SetMode(ClickMode.Select);
                GetViewport().SetInputAsHandled();
                return;
            case Key.R when Mode == ClickMode.PlaceTemplate:
                RotateTemplate();
                GetViewport().SetInputAsHandled();
                return;
            case Key.T when k.CtrlPressed:
                RequestTemplateSave();
                GetViewport().SetInputAsHandled();
                return;
            case Key.A when k.CtrlPressed:
                _selection.Set(OwnUnits(def => def.CanMove && !def.CanBuild && def.IsArmed));
                return;
            case Key.Z when k.CtrlPressed:
            {
                var types = mine.Select(id => World.Units.TypeIndex[id]).ToHashSet();
                _selection.Set(OwnUnits(def => true).Where(id => types.Contains(World.Units.TypeIndex[id])));
                return;
            }
            case Key.B when k.CtrlPressed:
                _selection.Set(OwnUnits(def => def.CanBuild && def.CanMove));
                return;
            case Key.C when k.CtrlPressed:
            case Key.Home:
                SelectCommander();
                return;
            case Key.A when mine.Count > 0: SetMode(ClickMode.Fight); return;
            case Key.M when mine.Count > 0: SetMode(ClickMode.Move); return;
            case Key.P when mine.Count > 0: SetMode(ClickMode.Patrol); return;
            case Key.R when mine.Count > 0: SetMode(ClickMode.Reclaim); return;
            case Key.G when mine.Count > 0: SetMode(ClickMode.Assist); return;
            case Key.D when mine.Any(id => World.Catalog[World.Units.TypeIndex[id]].HasDGun): SetMode(ClickMode.DGun); return;
            case Key.S when mine.Count > 0:
                StopSelection();
                return;
            case Key.F when mine.Count > 0:
                CycleFireState(mine);
                return;
            case Key.Tab:
                _overlay.ShowAllHealth = !_overlay.ShowAllHealth;
                return;
        }
    }

    /// <summary>Selects a control group; pressed twice quickly, also jumps the camera to it.</summary>
    public void RecallGroup(int g)
    {
        var ids = _selection.Group(g).Where(World.Units.IsValid).ToList();
        if (ids.Count == 0) return;
        _selection.Set(ids);
        ulong now = Time.GetTicksMsec();
        if (now - _lastGroupPress[g] < 350) CentreOn(ids);
        _lastGroupPress[g] = now;
    }

    /// <summary>Selects the next non-empty control group after the last one cycled (gamepad LB).</summary>
    public void CycleGroup()
    {
        for (int step = 1; step <= 10; step++)
        {
            int g = (_lastGroupCycled + step) % 10;
            var ids = _selection.Group(g).Where(World.Units.IsValid).ToList();
            if (ids.Count == 0) continue;
            _lastGroupCycled = g;
            _selection.Set(ids);
            CentreOn(ids);
            Notice?.Invoke($"Group {g}");
            Sfx.Play(SfxKind.Select, -10);
            return;
        }
        Notice?.Invoke("No control groups yet (Ctrl+1..9 to make one)");
    }

    /// <summary>Selects and looks at the next builder with nothing to do (gamepad RB).</summary>
    public void SelectNextIdleBuilder()
    {
        var idle = OwnUnits(def => def.CanBuild).Where(id => World.Units.Orders[id].Count == 0).ToList();
        if (idle.Count == 0) { Notice?.Invoke("No idle builders"); return; }
        int next = idle.FirstOrDefault(id => id > _lastIdleBuilder, idle[0]);
        _lastIdleBuilder = next;
        _selection.Set(new[] { next });
        CentreOn(new[] { next });
        Sfx.Play(SfxKind.Select, -10);
    }

    public void SelectCommander()
    {
        var c = OwnUnits(def => def.IsCommander).ToList();
        if (c.Count > 0) { _selection.Set(c); CentreOn(c); }
    }

    public void StopSelection()
    {
        var mine = MySelected();
        if (mine.Count == 0) return;
        foreach (int id in mine) Issue(new Command(0, Me, CommandType.Stop, id));
        Sfx.Play(SfxKind.Acknowledge, -8);
    }

    public void CycleFireState(List<int>? ids = null)
    {
        ids ??= MySelected();
        if (ids.Count == 0) return;
        var current = World.Units.Fire[ids[0]];
        int next = ((int)current + 2) % 3; // fire at will -> return fire -> hold fire -> fire at will
        foreach (int id in ids) Issue(new Command(0, Me, CommandType.SetFireState, id, Arg: next));
        Notice?.Invoke(next switch { 0 => "Hold fire", 1 => "Return fire", _ => "Fire at will" });
    }

    public IEnumerable<int> OwnUnits(Func<UnitDef, bool> filter)
    {
        UnitStore u = World.Units;
        for (int id = 0; id < u.Count; id++)
            if (u.Alive[id] && u.Complete[id] && u.Owner[id] == Me && filter(World.Catalog[u.TypeIndex[id]]))
                yield return id;
    }

    public void CentreOn(IEnumerable<int> ids)
    {
        var list = ids.Where(World.Units.IsValid).ToList();
        if (list.Count == 0) return;
        Vector3 sum = Vector3.Zero;
        foreach (int id in list) sum += _units.PositionOf(id, 1f);
        _rig.JumpTo(sum / list.Count);
    }

    public void Issue(Command c) => _session.Issue(c);

    /// <summary>Issues a command to every selected own unit that can take it (for command card buttons).</summary>
    public void IssueToSelection(CommandType type, int arg = 0)
    {
        foreach (int id in MySelected()) Issue(new Command(0, Me, type, id, Arg: arg));
        Sfx.Play(SfxKind.Acknowledge, -8);
    }

    /// <summary>Queue (count &gt; 0) or remove (count &lt; 0) units at every selected factory that can build the type.</summary>
    public void FactoryQueue(int type, int count)
    {
        foreach (int id in MySelected())
        {
            UnitDef def = World.Catalog[World.Units.TypeIndex[id]];
            if (def.IsFactory && (def.BuildOptions.Contains(type) || World.SalvagedOptions(def, Me).Contains(type)))
                Issue(new Command(0, Me, CommandType.FactoryQueue, id, TypeIndex: type, Arg: count));
        }
        Sfx.Play(count > 0 ? SfxKind.Click : SfxKind.Hover, -6);
    }

    /// <summary>Forgets every rally point of the selected factories.</summary>
    public void ClearRallies()
    {
        int n = 0;
        foreach (int id in MySelected())
            if (World.Catalog[World.Units.TypeIndex[id]].IsFactory) { Issue(new Command(0, Me, CommandType.ClearRally, id)); n++; }
        if (n == 0) return;
        Notice?.Invoke("Rally points cleared");
        Sfx.Play(SfxKind.Click, -6);
    }

    /// <summary>Sets how selected factories spread units over their rally stops: 0 in turn, N every Nth unit to the next extra stop.</summary>
    public void SetFerryEvery(int every)
    {
        foreach (int id in MySelected())
            if (World.Catalog[World.Units.TypeIndex[id]].IsFactory)
                Issue(new Command(0, Me, CommandType.FactoryRallyEvery, id, Arg: every));
        Sfx.Play(SfxKind.Click, -6);
    }
}
