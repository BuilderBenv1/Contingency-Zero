using System;
using System.Collections.Generic;
using ContingencyZero.Game.Match;
using ContingencyZero.Game.View.Meshes;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.State;
using Godot;

namespace ContingencyZero.Game.View;

/// <summary>
/// Everything drawn on top of the battlefield for the player: selection rings, health and build
/// bars, queued order lines, rally points, radar blips, command markers and the build-placement
/// ghost.
/// </summary>
public partial class OverlayView : Node3D
{
    private World _world = null!;
    private TerrainView _terrain = null!;
    private UnitView _units = null!;
    private Selection _selection = null!;
    private Camera3D _camera = null!;
    private int _localPlayer = -1;
    private int _viewerTeam = -1;

    private readonly ImmediateMesh _ground = new();
    private readonly ImmediateMesh _bars = new();
    private readonly MeshInstance3D _ghost = new();
    private ShaderMaterial _ghostMaterial = null!;
    private int _ghostType = -1;

    public int HoverUnit { get; set; } = -1;
    public bool ShowAllHealth { get; set; }
    private readonly HashSet<int> _shown = new();

    /// <summary>Build placement preview: type, snapped centre and whether it's valid. Type -1 hides it.</summary>
    public (int Type, Vector3 Centre, bool Valid) Ghost { get; set; } = (-1, Vector3.Zero, false);
    /// <summary>Extra ghosts already queued with shift (drawn as outlines).</summary>
    public readonly List<(int Type, Vector3 Centre)> QueuedGhosts = new();
    /// <summary>A rubber-band line for line-building or a fight/attack drag, in world space.</summary>
    public (Vector3 A, Vector3 B, Color C)? DragLine { get; set; }

    private struct Marker { public Vector3 P; public Color C; public float Life; }
    private readonly List<Marker> _markers = new();

    public void Init(World world, TerrainView terrain, UnitView units, Selection selection, Camera3D camera, int localPlayer, int viewerTeam)
    {
        _world = world;
        _terrain = terrain;
        _units = units;
        _selection = selection;
        _camera = camera;
        _localPlayer = localPlayer;
        _viewerTeam = viewerTeam;
        var groundMat = new ShaderMaterial { Shader = new Shader { Code = OverlayShader } };
        var barMat = new ShaderMaterial { Shader = new Shader { Code = OverlayShader } };
        barMat.RenderPriority = 2;
        AddChild(new MeshInstance3D { Mesh = _ground, MaterialOverride = groundMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        AddChild(new MeshInstance3D { Mesh = _bars, MaterialOverride = barMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        _ghostMaterial = new ShaderMaterial { Shader = new Shader { Code = GhostShader } };
        _ghost.MaterialOverride = _ghostMaterial;
        _ghost.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(_ghost);
    }

    public void SetViewerTeam(int team) => _viewerTeam = team;

    /// <summary>A brief expanding ring where an order was given.</summary>
    public void Mark(Vector3 p, Color c) => _markers.Add(new Marker { P = p, C = c, Life = 0.6f });

    private Vector3 Ground(FixVec2 p, float lift = 0.3f)
    {
        float x = p.X.Raw / 65536f, z = p.Y.Raw / 65536f;
        return new Vector3(x, _terrain.SurfaceAt(x, z) + lift, z);
    }

    public static Color OrderColour(OrderType t) => t switch
    {
        OrderType.Move => new Color(0.4f, 1f, 0.4f),
        OrderType.Attack or OrderType.AttackGround => new Color(1f, 0.3f, 0.25f),
        OrderType.Fight => new Color(1f, 0.6f, 0.2f),
        OrderType.Build => new Color(0.4f, 0.9f, 1f),
        OrderType.Reclaim => new Color(0.8f, 0.5f, 1f),
        OrderType.Assist => new Color(1f, 0.95f, 0.4f),
        OrderType.Patrol => new Color(0.4f, 0.6f, 1f),
        OrderType.DGun => new Color(1f, 0.5f, 0.1f),
        _ => new Color(0.8f, 0.8f, 0.8f),
    };

    public override void _Process(double delta)
    {
        if (_world is null) return;
        float dt = (float)delta;
        _ground.ClearSurfaces();
        _bars.ClearSurfaces();
        int groundVerts = 0, barVerts = 0;
        _ground.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        _bars.SurfaceBegin(Mesh.PrimitiveType.Triangles);

        Basis cam = _camera.GlobalTransform.Basis;
        Vector3 right = cam.X, up = cam.Y;
        UnitStore u = _world.Units;
        bool shift = Input.IsKeyPressed(Key.Shift);
        bool allBars = ShowAllHealth != ContingencyZero.Game.App.Settings.Current.AlwaysShowHealthBars;
        _shown.Clear();
        var shown = _shown;

        foreach (var (id, pos, radius, height) in _units.VisibleUnits)
        {
            shown.Add(id);
            bool selected = _selection.Contains(id);
            bool hovered = id == HoverUnit;
            bool mine = u.Owner[id] == _localPlayer;
            UnitDef def = _world.Catalog[u.TypeIndex[id]];
            float r = Math.Max(radius, def.IsStructure ? def.Footprint * 4f : def.Radius.Raw / 65536f) + 0.6f;
            Vector3 g = new(pos.X, def.IsAir ? _terrain.SurfaceAt(pos.X, pos.Z) : pos.Y, pos.Z);

            if (selected || hovered)
            {
                Color rc = mine ? new Color(0.3f, 1f, 0.45f, selected ? 0.95f : 0.5f)
                    : _world.AreEnemies(_localPlayer < 0 ? 0 : _localPlayer, u.Owner[id]) && _localPlayer >= 0 ? new Color(1f, 0.3f, 0.25f, 0.9f)
                    : new Color(1f, 1f, 0.5f, 0.8f);
                groundVerts += Ring(_ground, g + new Vector3(0, 0.25f, 0), r, r + (selected ? 0.55f : 0.35f), rc, def.IsStructure ? 4 : 20);
                if (def.IsAir)
                    groundVerts += Line(_ground, g + new Vector3(0, 0.3f, 0), pos, 0.15f, new Color(rc, 0.4f));
            }

            Fix64 maxHp = def.MaxHealth;
            float hp = maxHp.Raw > 0 ? Math.Clamp(u.Health[id].Raw / (float)maxHp.Raw, 0, 1) : 1;
            bool frame = !u.Complete[id];
            bool show = selected || hovered || frame || (allBars && !def.IsStructure) || (hp < 0.999f && (allBars || mine || hp < 0.6f));
            if (show)
            {
                float w = Math.Clamp(r * 1.6f, 3f, 14f);
                Vector3 top = pos + new Vector3(0, height + 1.6f, 0);
                if (frame)
                {
                    float prog = (float)(u.BuildProgress[id].Raw / (double)Math.Max(1, def.BuildTime.Raw));
                    barVerts += Bar(_bars, top + up * 0.9f, right, up, w, prog, new Color(0.35f, 1f, 0.5f));
                }
                Color hc = hp > 0.6f ? new Color(0.3f, 0.95f, 0.35f) : hp > 0.3f ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.25f, 0.2f);
                barVerts += Bar(_bars, top, right, up, w, hp, hc);
                // Veteran pips.
                int rank = ContingencyZero.Sim.Combat.CombatSystem.VeteranRank(u.Kills[id]);
                for (int k = 0; k < rank; k++)
                    barVerts += Quad(_bars, top - right * (w / 2 - 0.5f - k * 1.1f) + up * 0.9f, right, up, 0.7f, 0.7f, new Color(1f, 0.85f, 0.3f));
            }

            // Queued orders for selected own units (always for one unit, or while shift is held).
            if (selected && mine && (shift || _selection.Count <= 12))
            {
                Vector3 from = g + new Vector3(0, 0.3f, 0);
                foreach (Order o in u.Orders[id])
                {
                    Vector3 to = o.Type is OrderType.Attack or OrderType.Assist && u.IsValid(o.Target)
                        ? _units.PositionOf(o.Target, GetMeta("alpha", 1f).AsSingle()) with { Y = 0 } + new Vector3(0, 0, 0)
                        : Ground(o.Position);
                    if (o.Type is OrderType.Attack or OrderType.Assist && u.IsValid(o.Target)) to = new Vector3(to.X, _terrain.SurfaceAt(to.X, to.Z) + 0.3f, to.Z);
                    Color oc = OrderColour(o.Type);
                    groundVerts += Line(_ground, from, to, 0.28f, new Color(oc, shift ? 0.8f : 0.45f));
                    groundVerts += Ring(_ground, to, 0.8f, 1.4f, new Color(oc, 0.8f), 10);
                    if (o.Type == OrderType.Build && o.TypeIndex >= 0)
                    {
                        UnitDef bd = _world.Catalog[o.TypeIndex];
                        groundVerts += Square(_ground, to, bd.Footprint * MapData.TileSize / 2f - 0.3f, 0.35f, new Color(0.4f, 0.9f, 1f, 0.7f));
                    }
                    if (o.Type == OrderType.Patrol)
                        groundVerts += Line(_ground, to, Ground(o.Origin), 0.2f, new Color(oc, 0.35f));
                    from = to;
                }
            }

            // Rally points for selected factories.
            if (selected && mine && def.IsFactory && u.HasRally[id])
            {
                Vector3 rp = Ground(u.Rally[id]);
                groundVerts += Line(_ground, g + new Vector3(0, 0.3f, 0), rp, 0.25f, new Color(1f, 1f, 0.5f, 0.6f));
                groundVerts += Ring(_ground, rp, 1f, 2f, new Color(1f, 1f, 0.5f, 0.9f), 12);
            }
        }

        // Radar blips: enemies on radar that nobody can see.
        if (_viewerTeam >= 0)
        {
            for (int id = 0; id < u.Count; id++)
            {
                if (!u.Alive[id] || shown.Contains(id)) continue;
                if (!_world.CanSee(_viewerTeam, id, radarCounts: true)) continue;
                UnitDef def = _world.Catalog[u.TypeIndex[id]];
                Vector3 p = Ground(u.Position[id], def.IsAir ? UnitView.AirAltitude : 1f);
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.GetTicksMsec() / 200f + id);
                barVerts += Quad(_bars, p, right, up, 2.2f, 2.2f, new Color(1f, 0.25f, 0.2f, 0.85f * pulse), diamond: true);
            }
        }

        // Command markers.
        for (int i = _markers.Count - 1; i >= 0; i--)
        {
            Marker m = _markers[i];
            m.Life -= dt;
            if (m.Life <= 0) { _markers.RemoveAt(i); continue; }
            _markers[i] = m;
            float k = 1 - m.Life / 0.6f;
            groundVerts += Ring(_ground, m.P + new Vector3(0, 0.3f, 0), 0.5f + k * 3f, 1.0f + k * 3.4f, new Color(m.C, 1 - k), 16);
        }

        foreach (var (type, centre) in QueuedGhosts)
        {
            UnitDef bd = _world.Catalog[type];
            groundVerts += Square(_ground, centre + new Vector3(0, 0.3f, 0), bd.Footprint * MapData.TileSize / 2f - 0.3f, 0.35f, new Color(0.4f, 0.9f, 1f, 0.6f));
        }

        if (DragLine is { } dl)
            groundVerts += Line(_ground, dl.A + new Vector3(0, 0.4f, 0), dl.B + new Vector3(0, 0.4f, 0), 0.4f, dl.C);

        // Placement ghost.
        var (gType, gCentre, gValid) = Ghost;
        if (gType >= 0)
        {
            if (gType != _ghostType)
            {
                _ghost.Mesh = UnitModels.For(_world.Catalog[gType]).Body;
                _ghostType = gType;
            }
            _ghost.Visible = true;
            _ghost.Position = gCentre;
            _ghostMaterial.SetShaderParameter("tint", gValid ? new Color(0.3f, 1f, 0.5f, 0.55f) : new Color(1f, 0.25f, 0.2f, 0.55f));
            UnitDef bd = _world.Catalog[gType];
            groundVerts += Square(_ground, gCentre + new Vector3(0, 0.3f, 0), bd.Footprint * MapData.TileSize / 2f, 0.4f,
                gValid ? new Color(0.3f, 1f, 0.5f, 0.9f) : new Color(1f, 0.3f, 0.25f, 0.9f));
            // Show weapon range for defences.
            float range = 0;
            foreach (WeaponDef w in bd.Weapons) range = Math.Max(range, w.Range.Raw / 65536f);
            if (range > 0) groundVerts += Ring(_ground, gCentre + new Vector3(0, 0.4f, 0), range - 0.3f, range, new Color(1f, 0.4f, 0.3f, 0.5f), 48);
        }
        else
        {
            _ghost.Visible = false;
        }

        if (groundVerts > 0) _ground.SurfaceEnd(); else _ground.ClearSurfaces();
        if (barVerts > 0) _bars.SurfaceEnd(); else _bars.ClearSurfaces();
    }

    private static int Ring(ImmediateMesh m, Vector3 c, float r0, float r1, Color col, int segments)
    {
        m.SurfaceSetColor(col);
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.Tau / segments + (segments == 4 ? Mathf.Pi / 4 : 0);
            float a1 = (i + 1) * Mathf.Tau / segments + (segments == 4 ? Mathf.Pi / 4 : 0);
            float s = segments == 4 ? Mathf.Sqrt2 : 1;
            Vector3 d0 = new(Mathf.Cos(a0) * s, 0, Mathf.Sin(a0) * s), d1 = new(Mathf.Cos(a1) * s, 0, Mathf.Sin(a1) * s);
            Vector3 i0 = c + d0 * r0, o0 = c + d0 * r1, i1 = c + d1 * r0, o1 = c + d1 * r1;
            m.SurfaceAddVertex(i0); m.SurfaceAddVertex(o0); m.SurfaceAddVertex(o1);
            m.SurfaceAddVertex(i0); m.SurfaceAddVertex(o1); m.SurfaceAddVertex(i1);
        }
        return segments * 6;
    }

    private static int Square(ImmediateMesh m, Vector3 c, float half, float width, Color col) => Ring(m, c, half - width, half, col, 4);

    private static int Line(ImmediateMesh m, Vector3 a, Vector3 b, float width, Color col)
    {
        Vector3 d = b - a;
        d.Y = 0;
        if (d.LengthSquared() < 0.0001f) return 0;
        Vector3 side = new Vector3(-d.Z, 0, d.X).Normalized() * width * 0.5f;
        m.SurfaceSetColor(col);
        m.SurfaceAddVertex(a - side); m.SurfaceAddVertex(a + side); m.SurfaceAddVertex(b + side);
        m.SurfaceAddVertex(a - side); m.SurfaceAddVertex(b + side); m.SurfaceAddVertex(b - side);
        return 6;
    }

    private static int Quad(ImmediateMesh m, Vector3 c, Vector3 right, Vector3 up, float w, float h, Color col, bool diamond = false)
    {
        m.SurfaceSetColor(col);
        if (diamond)
        {
            Vector3 l = c - right * w / 2, r = c + right * w / 2, t = c + up * h / 2, b = c - up * h / 2;
            m.SurfaceAddVertex(l); m.SurfaceAddVertex(t); m.SurfaceAddVertex(r);
            m.SurfaceAddVertex(l); m.SurfaceAddVertex(r); m.SurfaceAddVertex(b);
            return 6;
        }
        Vector3 rx = right * w / 2, uy = up * h / 2;
        m.SurfaceAddVertex(c - rx + uy); m.SurfaceAddVertex(c + rx + uy); m.SurfaceAddVertex(c + rx - uy);
        m.SurfaceAddVertex(c - rx + uy); m.SurfaceAddVertex(c + rx - uy); m.SurfaceAddVertex(c - rx - uy);
        return 6;
    }

    private static int Bar(ImmediateMesh m, Vector3 c, Vector3 right, Vector3 up, float w, float fill, Color col)
    {
        const float h = 0.55f;
        int n = Quad(m, c, right, up, w + 0.3f, h + 0.3f, new Color(0, 0, 0, 0.75f));
        if (fill > 0)
        {
            Vector3 left = c - right * w / 2;
            n += Quad(m, left + right * w * fill / 2, right, up, w * fill, h, col);
        }
        return n;
    }

    private const string OverlayShader = @"
shader_type spatial;
render_mode blend_mix, unshaded, depth_test_disabled, depth_draw_never, cull_disabled;
void fragment() { ALBEDO = COLOR.rgb; ALPHA = COLOR.a; }";

    private const string GhostShader = @"
shader_type spatial;
render_mode blend_mix, unshaded, depth_draw_never, cull_back;
uniform vec4 tint : source_color;
void fragment() {
    float lines = 0.75 + 0.25 * sin(TIME * 8.0 + FRAGCOORD.y * 0.5);
    ALBEDO = tint.rgb * lines;
    ALPHA = tint.a;
}";
}
