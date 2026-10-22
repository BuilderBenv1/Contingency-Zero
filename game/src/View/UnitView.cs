using System;
using System.Collections.Generic;
using ContingencyZero.Game.View.Meshes;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.State;
using Godot;

namespace ContingencyZero.Game.View;

/// <summary>
/// Draws every unit through MultiMeshes, one batch per unit type: body, turret, rotor, plus a
/// translucent "nanoframe" batch for things under construction. Positions are interpolated
/// between sim ticks; headings and turret aim are smoothed here (the sim has no facing).
/// Enemy units only show where the local team can see them.
/// </summary>
public partial class UnitView : Node3D
{
    public const float AirAltitude = 22f;

    private World _world = null!;
    private TerrainView _terrain = null!;
    private int _viewerTeam = -1;

    // Interpolation snapshots, refreshed after every tick.
    private Vector2[] _prev = Array.Empty<Vector2>();
    private Vector2[] _curr = Array.Empty<Vector2>();
    private float[] _heading = Array.Empty<float>();
    private float[] _turretYaw = Array.Empty<float>();
    private float[] _hitFlash = Array.Empty<float>();
    private Fix64Cache _lastHealth = new();

    private readonly Dictionary<int, Batch> _batches = new();
    private ShaderMaterial _unitMaterial = null!;
    private ShaderMaterial _frameMaterial = null!;
    private float _time;

    /// <summary>Screen-pick data, rebuilt every frame: interpolated world position and pick radius of each visible unit.</summary>
    public readonly List<(int Id, Vector3 Pos, float Radius, float Height)> VisibleUnits = new();

    private sealed class Batch
    {
        public required UnitModel Model;
        public required MultiMesh Body;
        public MultiMesh? Turret;
        public MultiMesh? Rotor;
        public required MultiMesh Frame;
        public int Count;
        public int FrameCount;
    }

    /// <summary>Small helper so health changes (for hit flashes) can be noticed without the sim's help.</summary>
    private sealed class Fix64Cache
    {
        public long[] Values = Array.Empty<long>();
    }

    public void Init(World world, TerrainView terrain, int viewerTeam)
    {
        _world = world;
        _terrain = terrain;
        _viewerTeam = viewerTeam;
        _unitMaterial = new ShaderMaterial { Shader = new Shader { Code = UnitShader } };
        _frameMaterial = new ShaderMaterial { Shader = new Shader { Code = FrameShader } };
        Snapshot();
        Snapshot();
    }

    public void SetViewerTeam(int team) => _viewerTeam = team;

    /// <summary>Called after each sim tick: shift current positions to previous and read the new ones.</summary>
    public void Snapshot()
    {
        UnitStore u = _world.Units;
        int n = u.Count;
        if (_curr.Length < n)
        {
            int size = Math.Max(n * 2, 256);
            Array.Resize(ref _prev, size);
            Array.Resize(ref _curr, size);
            Array.Resize(ref _heading, size);
            Array.Resize(ref _turretYaw, size);
            Array.Resize(ref _hitFlash, size);
            Array.Resize(ref _lastHealth.Values, size);
        }
        for (int id = 0; id < n; id++)
        {
            var p = new Vector2(u.Position[id].X.Raw / 65536f, u.Position[id].Y.Raw / 65536f);
            bool fresh = _lastHealth.Values[id] == 0 && _curr[id] == Vector2.Zero;
            _prev[id] = fresh ? p : _curr[id];
            _curr[id] = p;
            long h = u.Health[id].Raw;
            if (!fresh && h < _lastHealth.Values[id] && u.Complete[id]) _hitFlash[id] = 1f;
            _lastHealth.Values[id] = h == 0 ? 1 : h;
            if (fresh) _heading[id] = u.Owner[id] % 2 == 0 ? Mathf.Pi : 0;
        }
    }

    public Vector3 PositionOf(int id, float alpha)
    {
        Vector2 p = _prev[id].Lerp(_curr[id], alpha);
        UnitDef def = _world.Catalog[_world.Units.TypeIndex[id]];
        float y = Altitude(def, p, id);
        return new Vector3(p.X, y, p.Y);
    }

    private float Altitude(UnitDef def, Vector2 p, int id)
    {
        if (def.IsAir) return _terrain.SurfaceAt(p.X, p.Y) + AirAltitude + Mathf.Sin(_time * 1.7f + id) * 0.6f;
        if (def.MoveClass == MoveClass.Ship) return _terrain.WaterHeight;
        // Shipyards and other water structures float on the surface, not on the seabed.
        if (def.NeedsWater) return Mathf.Max(_terrain.HeightAt(p.X, p.Y), _terrain.WaterHeight);
        if (def.MoveClass == MoveClass.Hover) return _terrain.SurfaceAt(p.X, p.Y) + 0.4f;
        return _terrain.HeightAt(p.X, p.Y);
    }

    public bool IsShown(int id)
    {
        if (_viewerTeam < 0) return true;
        return _world.CanSee(_viewerTeam, id);
    }

    public override void _Process(double delta)
    {
        if (_world is null) return;
        _time += (float)delta;
        Render(GetMeta("alpha", 1f).AsSingle(), (float)delta);
    }

    private Batch GetBatch(int type)
    {
        if (_batches.TryGetValue(type, out var b)) return b;
        UnitModel model = UnitModels.For(_world.Catalog[type]);
        MultiMesh Make(Mesh mesh, Material mat)
        {
            var mm = MultiMeshUtil.Create(mesh, 16);
            AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = mat });
            return mm;
        }
        b = new Batch
        {
            Model = model,
            Body = Make(model.Body, _unitMaterial),
            Turret = model.Turret is null ? null : Make(model.Turret, _unitMaterial),
            Rotor = model.Rotor is null ? null : Make(model.Rotor, _unitMaterial),
            Frame = Make(model.Body, _frameMaterial),
        };
        _batches[type] = b;
        return b;
    }

    private static void Ensure(MultiMesh mm, int needed) => MultiMeshUtil.Ensure(mm, needed);

    private void Render(float alpha, float delta)
    {
        foreach (var b in _batches.Values) { b.Count = 0; b.FrameCount = 0; }
        VisibleUnits.Clear();

        UnitStore u = _world.Units;

        // Size every batch first: growing a MultiMesh clears what was already written to it.
        var need = new Dictionary<int, (int Done, int Frames)>();
        for (int id = 0; id < u.Count; id++)
        {
            if (!u.Alive[id]) continue;
            need.TryGetValue(u.TypeIndex[id], out var n);
            need[u.TypeIndex[id]] = u.Complete[id] ? (n.Done + 1, n.Frames) : (n.Done, n.Frames + 1);
        }
        foreach (var (type, n) in need)
        {
            Batch nb = GetBatch(type);
            Ensure(nb.Body, n.Done);
            if (nb.Turret is not null) Ensure(nb.Turret, n.Done);
            if (nb.Rotor is not null) Ensure(nb.Rotor, n.Done);
            Ensure(nb.Frame, n.Frames);
        }

        for (int id = 0; id < u.Count; id++)
        {
            if (!u.Alive[id]) continue;
            if (!IsShown(id)) continue;

            int type = u.TypeIndex[id];
            UnitDef def = _world.Catalog[type];
            Batch b = GetBatch(type);

            Vector2 p2 = _prev[id].Lerp(_curr[id], alpha);
            Vector3 pos = new(p2.X, Altitude(def, p2, id), p2.Y);

            // Heading: face the direction of travel; structures face south (their door side).
            if (!def.IsStructure)
            {
                Vector2 mv = _curr[id] - _prev[id];
                if (mv.LengthSquared() > 0.0004f)
                {
                    float target = Mathf.Atan2(-mv.X, -mv.Y);
                    float turn = (def.IsAir ? 4f : 6f) * delta;
                    _heading[id] = Mathf.RotateToward(_heading[id], target, turn);
                }
            }
            float heading = def.IsStructure ? 0 : _heading[id];

            Basis basis = Basis.FromEuler(new Vector3(0, heading, 0));
            if (!def.IsStructure && !def.IsAir && def.MoveClass != MoveClass.Ship && def.MoveClass != MoveClass.Bot)
            {
                // Vehicles tilt with the ground.
                Vector3 n = _terrain.NormalAt(p2.X, p2.Y);
                if (n.Y < 0.999f)
                {
                    Vector3 axis = Vector3.Up.Cross(n).Normalized();
                    float ang = Mathf.Acos(Mathf.Clamp(n.Y, -1, 1));
                    basis = new Basis(axis, ang) * basis;
                }
            }
            if (def.IsAir)
            {
                // Bank a little into turns.
                Vector2 mv = _curr[id] - _prev[id];
                basis = basis * Basis.FromEuler(new Vector3(-Mathf.Clamp(mv.Length() * 0.15f, 0, 0.25f), 0, 0));
            }

            Color team = App.UiTheme.TeamColour(u.Owner[id]);
            _hitFlash[id] = Mathf.Max(0, _hitFlash[id] - delta * 5f);

            if (!u.Complete[id])
            {
                float progress = (float)(u.BuildProgress[id].Raw / (double)Math.Max(1, def.BuildTime.Raw));
                b.Frame.SetInstanceTransform(b.FrameCount, new Transform3D(basis, pos));
                b.Frame.SetInstanceCustomData(b.FrameCount, new Color(progress, b.Model.Height, team.R, 0));
                b.FrameCount++;
                VisibleUnits.Add((id, pos, b.Model.Radius, b.Model.Height));
                continue;
            }

            float flash = _hitFlash[id];
            b.Body.SetInstanceTransform(b.Count, new Transform3D(basis, pos));
            b.Body.SetInstanceCustomData(b.Count, new Color(team.R, team.G, team.B, flash));

            if (b.Turret is not null)
            {
                float yaw = TurretYaw(id, pos, heading, delta);
                Transform3D t = new Transform3D(basis, pos) * new Transform3D(Basis.FromEuler(new Vector3(0, yaw - heading, 0)), b.Model.TurretPivot);
                b.Turret.SetInstanceTransform(b.Count, t);
                b.Turret.SetInstanceCustomData(b.Count, new Color(team.R, team.G, team.B, flash));
            }
            if (b.Rotor is not null)
            {
                float spin = _time * (def.UsesWind ? 1.5f + _world.Wind.Raw / 65536f * 0.12f : 6f) + id;
                Transform3D t = new Transform3D(basis, pos) * new Transform3D(new Basis(b.Model.RotorAxis.Normalized(), spin), b.Model.RotorPivot);
                b.Rotor.SetInstanceTransform(b.Count, t);
                b.Rotor.SetInstanceCustomData(b.Count, new Color(team.R, team.G, team.B, flash));
            }
            b.Count++;
            VisibleUnits.Add((id, pos, b.Model.Radius, b.Model.Height));
        }

        foreach (var b in _batches.Values)
        {
            b.Body.VisibleInstanceCount = b.Count;
            if (b.Turret is not null) b.Turret.VisibleInstanceCount = b.Count;
            if (b.Rotor is not null) b.Rotor.VisibleInstanceCount = b.Count;
            b.Frame.VisibleInstanceCount = b.FrameCount;
        }
    }

    private float TurretYaw(int id, Vector3 pos, float heading, float delta)
    {
        UnitStore u = _world.Units;
        int target = u.Target0[id] >= 0 ? u.Target0[id] : u.Target1[id];
        float want = heading;
        if (target >= 0 && target < u.Count && u.Alive[target])
        {
            Vector2 tp = _curr[target];
            want = Mathf.Atan2(-(tp.X - pos.X), -(tp.Y - pos.Z));
        }
        else
        {
            Order o = u.CurrentOrder(id);
            if (o.Type == OrderType.AttackGround)
                want = Mathf.Atan2(-(o.Position.X.Raw / 65536f - pos.X), -(o.Position.Y.Raw / 65536f - pos.Z));
        }
        _turretYaw[id] = Mathf.RotateToward(_turretYaw[id], want, 5f * delta);
        return _turretYaw[id];
    }

    // Vertex alpha: 1 = plain, 0 = team colour (modulated by RGB brightness), 0.5 = glowing light.
    public const string UnitShader = @"
shader_type spatial;
render_mode diffuse_lambert, specular_schlick_ggx;
varying vec4 inst;
void vertex() { inst = INSTANCE_CUSTOM; }
// Note: the Compatibility renderer lights in display (sRGB) space, so colours are used as authored.
void fragment() {
    vec3 base = COLOR.rgb;
    float a = COLOR.a;
    vec3 col;
    vec3 emit = vec3(0.0);
    if (a < 0.25) {
        float lum = dot(COLOR.rgb, vec3(0.333));
        col = inst.rgb * (0.25 + 0.85 * lum);
    } else if (a < 0.75) {
        col = base * 0.3;
        emit = base * 1.6;
    } else {
        col = base;
    }
    col = mix(col, vec3(1.0), inst.a * 0.55);
    ALBEDO = col;
    EMISSION = emit + vec3(1.0, 0.9, 0.8) * inst.a * 0.25;
    ROUGHNESS = 0.55;
    METALLIC = 0.25;
}";

    /// <summary>
    /// Under construction: the finished part (below the progress line) is solid but tinted green,
    /// the rest is a faint hologram, with a bright scanline at the build height.
    /// </summary>
    public const string FrameShader = @"
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, unshaded;
varying vec4 inst;
varying float ly;
void vertex() { inst = INSTANCE_CUSTOM; ly = VERTEX.y; }
void fragment() {
    float h = max(inst.g, 0.5) * inst.r;
    float built = step(ly, h);
    float scan = 1.0 - smoothstep(0.0, 0.6, abs(ly - h));
    vec3 holo = vec3(0.3, 1.0, 0.55);
    vec3 solid = mix(COLOR.rgb, holo, 0.35) * 0.8;
    ALBEDO = mix(holo * 0.6, solid, built) + holo * scan * 1.5;
    float flicker = 0.85 + 0.15 * sin(TIME * 12.0 + ly * 3.0);
    ALPHA = mix(0.18 * flicker, 0.92, built) + scan * 0.5;
}";
}
