using System;
using System.Collections.Generic;
using ContingencyZero.Game.Audio;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.State;
using Godot;

namespace ContingencyZero.Game.View;

/// <summary>
/// Battle effects: laser beams, muzzle flashes, shells and missiles in flight (with arcs for
/// artillery), explosions, sparks, smoke, scorch marks, nanolathe and reclaim beams, the Breach
/// Cannon bolt. Everything is drawn as camera-facing quads in two immediate meshes (additive glow
/// and alpha smoke), rebuilt every frame. Also triggers positional sounds for sim events.
/// </summary>
public partial class EffectsView : Node3D
{
    private World _world = null!;
    private TerrainView _terrain = null!;
    private UnitView _units = null!;
    private Camera3D _camera = null!;
    private Func<Vector3> _listener = () => Vector3.Zero;
    private int _viewerTeam = -1;
    private int _localPlayer = -1;

    private readonly ImmediateMesh _glowMesh = new();
    private readonly ImmediateMesh _smokeMesh = new();
    private MultiMesh _scorch = null!;
    private int _scorchNext;

    private struct Beam { public Vector3 A, B; public Color C; public float Life, Max, Width; }
    private struct Sprite { public Vector3 P, V; public Color C; public float Life, Max, Size, Grow, Gravity; public bool Smoke; }

    private readonly List<Beam> _beams = new();
    private readonly List<Sprite> _sprites = new();
    private readonly List<Beam> _nano = new();

    private static readonly Color LatticeLaser = new(0.45f, 0.8f, 1.0f);
    private static readonly Color CrucibleLaser = new(1.0f, 0.55f, 0.2f);
    private static readonly Color Fire = new(1.0f, 0.62f, 0.2f);
    private static readonly Color NanoColour = new(0.35f, 1.0f, 0.5f);
    private static readonly Color ReclaimColour = new(0.75f, 0.45f, 1.0f);
    private readonly RandomNumberGenerator _rng = new();

    public void Init(World world, TerrainView terrain, UnitView units, Camera3D camera, Func<Vector3> listener, int viewerTeam, int localPlayer)
    {
        _world = world;
        _terrain = terrain;
        _units = units;
        _camera = camera;
        _listener = listener;
        _viewerTeam = viewerTeam;
        _localPlayer = localPlayer;
        _rng.Seed = 12345;

        var glowMat = new ShaderMaterial { Shader = new Shader { Code = GlowShader } };
        var smokeMat = new ShaderMaterial { Shader = new Shader { Code = SmokeShader } };
        AddChild(new MeshInstance3D { Mesh = _glowMesh, MaterialOverride = glowMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        AddChild(new MeshInstance3D { Mesh = _smokeMesh, MaterialOverride = smokeMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });

        _scorch = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = new PlaneMesh { Size = new Vector2(1, 1) },
            InstanceCount = 160,
            VisibleInstanceCount = 0,
        };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = _scorch,
            MaterialOverride = new ShaderMaterial { Shader = new Shader { Code = ScorchShader } },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    public void SetViewerTeam(int team) => _viewerTeam = team;

    private bool Seen(FixVec2 p) =>
        _viewerTeam < 0 || _world.Vision.IsVisible(_viewerTeam, MapData.TileOf(p.X), MapData.TileOf(p.Y));

    private Vector3 Ground(FixVec2 p, float lift = 0.5f)
    {
        float x = p.X.Raw / 65536f, z = p.Y.Raw / 65536f;
        return new Vector3(x, _terrain.SurfaceAt(x, z) + lift, z);
    }

    private Vector3 Muzzle(int unit)
    {
        if (unit < 0 || unit >= _world.Units.Count) return Vector3.Zero;
        UnitDef def = _world.Catalog[_world.Units.TypeIndex[unit]];
        Vector3 p = _units.PositionOf(unit, 1f);
        float h = def.IsStructure ? 5f : Math.Max(1.2f, def.Radius.Raw / 65536f * 0.9f);
        return p + new Vector3(0, h, 0);
    }

    private Color LaserColour(int unit) =>
        unit >= 0 && unit < _world.Units.Count && _world.Players[_world.Units.Owner[unit]].Faction == "lattice" ? LatticeLaser : CrucibleLaser;

    /// <summary>Turns this tick's sim events into effects and sounds.</summary>
    public void OnTick()
    {
        _nano.Clear();
        Vector3 listener = _listener();
        float zoom = _camera.Size;

        foreach (GameEvent e in _world.Events)
        {
            switch (e.Type)
            {
                case GameEventType.WeaponFired:
                {
                    if (!Seen(e.Position) && !Seen(e.Position2)) break;
                    var kind = (WeaponKind)e.Kind;
                    Vector3 from = Muzzle(e.Unit);
                    Vector3 to = e.Other >= 0 && e.Other < _world.Units.Count ? _units.PositionOf(e.Other, 1f) + new Vector3(0, 1.2f, 0) : Ground(e.Position2, 1f);
                    Color c = LaserColour(e.Unit);
                    switch (kind)
                    {
                        case WeaponKind.Laser:
                            _beams.Add(new Beam { A = from, B = to, C = c, Life = 0.14f, Max = 0.14f, Width = 0.55f });
                            Flash(from, c, 1.6f);
                            Flash(to, c, 1.2f);
                            Sfx.PlayAt(SfxKind.Laser, from, listener, zoom, -6);
                            break;
                        case WeaponKind.Flak:
                            for (int i = 0; i < 3; i++)
                                Flash(to + RandomInSphere(2.5f), new Color(1f, 0.85f, 0.5f), 1.4f, 0.18f);
                            Sfx.PlayAt(SfxKind.Flak, from, listener, zoom, -8);
                            break;
                        case WeaponKind.Cannon:
                            Flash(from, Fire, 2.2f);
                            Smoke(from, 1.5f, 0.6f);
                            Sfx.PlayAt(SfxKind.Cannon, from, listener, zoom, -5);
                            break;
                        case WeaponKind.Artillery:
                            Flash(from, Fire, 3f);
                            Smoke(from, 2.5f, 1.2f);
                            Sfx.PlayAt(SfxKind.Artillery, from, listener, zoom, -3);
                            break;
                        case WeaponKind.Missile:
                            Flash(from, new Color(1f, 0.9f, 0.6f), 1.5f);
                            Sfx.PlayAt(SfxKind.Missile, from, listener, zoom, -7);
                            break;
                        case WeaponKind.Bomb:
                            Sfx.PlayAt(SfxKind.Bomb, from, listener, zoom, -5);
                            break;
                        case WeaponKind.DGun:
                            Flash(from, new Color(1f, 0.6f, 0.15f), 6f, 0.35f);
                            Sfx.PlayAt(SfxKind.DGun, from, listener, zoom, 2);
                            break;
                    }
                    break;
                }

                case GameEventType.Impact:
                {
                    if (!Seen(e.Position)) break;
                    var kind = (WeaponKind)e.Kind;
                    Vector3 p = Ground(e.Position, 0.6f);
                    float size = Math.Max(1.5f, e.Value);
                    if (kind == WeaponKind.Missile) size = 1.5f;
                    Explode(p, size, kind == WeaponKind.Artillery || kind == WeaponKind.Bomb || kind == WeaponKind.DGun);
                    Sfx.PlayAt(size > 4 ? SfxKind.ExplosionSmall : SfxKind.ImpactSmall, p, listener, zoom, size > 4 ? -2 : -8);
                    break;
                }

                case GameEventType.UnitDestroyed:
                {
                    if (!Seen(e.Position)) break;
                    UnitDef def = _world.Catalog[e.Value];
                    float size = def.IsStructure ? def.Footprint * 4f : Math.Max(2f, def.Radius.Raw / 65536f * 2f);
                    Vector3 p = Ground(e.Position, def.IsAir ? UnitView.AirAltitude : 1f);
                    Explode(p, size, true);
                    Debris(p, (int)Math.Clamp(size * 3, 6, 40), size);
                    Sfx.PlayAt(size > 10 ? SfxKind.ExplosionLarge : SfxKind.ExplosionSmall, p, listener, zoom, size > 10 ? 0 : -3);
                    break;
                }

                case GameEventType.Explosion:
                {
                    Vector3 p = Ground(e.Position, 1f);
                    float r = Math.Max(8, e.Value);
                    Explode(p, r * 0.8f, true);
                    Shockwave(p, r);
                    Debris(p, 60, r * 0.6f);
                    Sfx.PlayAt(SfxKind.ExplosionHuge, p, listener, zoom * 0.5f, 4);
                    break;
                }

                case GameEventType.NanoBeam:
                case GameEventType.ReclaimBeam:
                {
                    if (e.Unit < 0 || e.Unit >= _world.Units.Count || !_units.IsShown(e.Unit)) break;
                    Vector3 from = Muzzle(e.Unit);
                    Vector3 to = Ground(e.Position2, 2f);
                    if (e.Type == GameEventType.NanoBeam && e.Other >= 0 && e.Other < _world.Units.Count)
                        to = _units.PositionOf(e.Other, 1f) + new Vector3(0, 2f, 0);
                    Color c = e.Type == GameEventType.NanoBeam ? NanoColour : ReclaimColour;
                    _nano.Add(new Beam { A = from, B = to, C = c, Life = 1, Max = 1, Width = 0.35f });
                    if (_rng.Randf() < 0.25f)
                        Flash(to + RandomInSphere(1.5f), c, 0.8f, 0.3f);
                    break;
                }

                case GameEventType.BuildComplete:
                    if (e.Other == _localPlayer)
                    {
                        UnitDef def = _world.Catalog[_world.Units.TypeIndex[e.Unit]];
                        Sfx.Play(def.IsStructure ? SfxKind.BuildComplete : SfxKind.UnitReady, -6);
                    }
                    break;

                case GameEventType.BuildStarted:
                    if (_world.Units.IsValid(e.Unit) && _world.Units.Owner[e.Unit] == _localPlayer
                        && _world.Catalog[_world.Units.TypeIndex[e.Unit]].IsStructure)
                        Sfx.PlayAt(SfxKind.BuildStart, Ground(e.Position), listener, zoom, -8);
                    break;
            }
        }
    }

    private Vector3 RandomInSphere(float r) =>
        new Vector3(_rng.RandfRange(-1, 1), _rng.RandfRange(-1, 1), _rng.RandfRange(-1, 1)).LimitLength(1) * r;

    private void Flash(Vector3 p, Color c, float size, float life = 0.09f) =>
        _sprites.Add(new Sprite { P = p, C = c, Life = life, Max = life, Size = size, Grow = 0 });

    private void Smoke(Vector3 p, float size, float life) =>
        _sprites.Add(new Sprite
        {
            P = p, V = new Vector3(0, 1.5f, 0) + RandomInSphere(0.6f), C = new Color(0.35f, 0.33f, 0.31f, 0.55f),
            Life = life, Max = life, Size = size, Grow = size * 1.2f, Smoke = true,
        });

    private void Explode(Vector3 p, float size, bool scorch)
    {
        _sprites.Add(new Sprite { P = p, C = new Color(1f, 0.9f, 0.6f), Life = 0.12f, Max = 0.12f, Size = size * 1.6f });
        _sprites.Add(new Sprite { P = p, C = Fire, Life = 0.45f, Max = 0.45f, Size = size * 1.1f, Grow = size * 1.5f });
        int puffs = (int)Math.Clamp(size, 2, 10);
        for (int i = 0; i < puffs; i++)
        {
            _sprites.Add(new Sprite
            {
                P = p + RandomInSphere(size * 0.4f), V = new Vector3(0, 2f, 0) + RandomInSphere(2f),
                C = new Color(0.25f, 0.23f, 0.22f, 0.6f), Life = 1.2f + _rng.Randf(), Max = 2.2f,
                Size = size * 0.7f, Grow = size * 1.4f, Smoke = true,
            });
        }
        for (int i = 0; i < puffs * 2; i++)
        {
            _sprites.Add(new Sprite
            {
                P = p, V = RandomInSphere(size * 3f) + new Vector3(0, size * 2f, 0), C = new Color(1f, 0.7f, 0.3f),
                Life = 0.5f + _rng.Randf() * 0.4f, Max = 0.9f, Size = 0.4f, Gravity = 20f,
            });
        }
        if (scorch && size >= 2.5f) Scorch(p, size * 1.6f);
    }

    private void Debris(Vector3 p, int count, float size)
    {
        for (int i = 0; i < count; i++)
        {
            _sprites.Add(new Sprite
            {
                P = p + RandomInSphere(size * 0.3f), V = RandomInSphere(size * 2.5f) + new Vector3(0, size * 2.5f + 4, 0),
                C = new Color(1f, 0.55f, 0.2f), Life = 0.8f + _rng.Randf() * 0.8f, Max = 1.6f, Size = 0.5f + _rng.Randf() * 0.4f, Gravity = 28f,
            });
        }
    }

    private void Shockwave(Vector3 p, float radius)
    {
        for (int i = 0; i < 24; i++)
        {
            float a = i / 24f * Mathf.Tau;
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            _sprites.Add(new Sprite
            {
                P = p + dir * 2, V = dir * radius * 2.2f, C = new Color(1f, 0.8f, 0.5f), Life = 0.45f, Max = 0.45f, Size = 3f, Grow = 4f,
            });
        }
        _sprites.Add(new Sprite { P = p + new Vector3(0, 3, 0), C = new Color(1f, 0.95f, 0.8f), Life = 0.3f, Max = 0.3f, Size = radius * 1.8f });
    }

    private void Scorch(Vector3 p, float size)
    {
        int i = _scorchNext++ % _scorch.InstanceCount;
        Vector3 n = _terrain.NormalAt(p.X, p.Z);
        float yaw = _rng.Randf() * Mathf.Tau;
        Basis b = new Basis(Vector3.Up, yaw).Scaled(new Vector3(size, 1, size));
        if (n.Y < 0.999f) b = new Basis(Vector3.Up.Cross(n).Normalized(), Mathf.Acos(n.Y)) * b;
        _scorch.SetInstanceTransform(i, new Transform3D(b, new Vector3(p.X, _terrain.SurfaceAt(p.X, p.Z) + 0.12f, p.Z)));
        _scorch.SetInstanceCustomData(i, new Color(Time.GetTicksMsec() / 1000f, 0, 0, 0));
        _scorch.VisibleInstanceCount = Math.Min(_scorchNext, _scorch.InstanceCount);
    }

    public override void _Process(double delta)
    {
        if (_world is null) return;
        float dt = (float)delta;
        float alpha = GetMeta("alpha", 1f).AsSingle();
        Basis cam = _camera.GlobalTransform.Basis;
        Vector3 right = cam.X, up = cam.Y;

        _glowMesh.ClearSurfaces();
        _smokeMesh.ClearSurfaces();
        int glowVerts = 0, smokeVerts = 0;

        // Projectiles in flight.
        var projectiles = _world.Projectiles;
        bool glowBegun = false;
        void BeginGlow()
        {
            if (glowBegun) return;
            _glowMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            glowBegun = true;
        }

        foreach (Projectile p in projectiles)
        {
            if (!Seen(p.Position)) continue;
            Vector3 start = Ground(p.Start, 1.5f);
            Vector3 at = Ground(p.Position, 1.5f);
            Vector3 aim = Ground(p.Aim, 0.8f);
            Vector3 dir = (aim - at);
            float step = p.SpeedPerTick.Raw / 65536f;
            if (dir.Length() > 0.001f) at += dir.Normalized() * Math.Min(dir.Length(), step * alpha);

            float t = p.FlightTicks > 0 ? Math.Clamp((p.TicksAlive + alpha) / p.FlightTicks, 0, 1) : 1;
            switch (p.Kind)
            {
                case WeaponKind.Artillery:
                {
                    float dist = start.DistanceTo(aim);
                    at.Y += 4 * dist * 0.22f * t * (1 - t);
                    BeginGlow();
                    Quad(_glowMesh, at, right, up, 1.1f, new Color(1f, 0.75f, 0.35f)); glowVerts += 6;
                    Quad(_glowMesh, at, right, up, 2.4f, new Color(1f, 0.45f, 0.1f, 0.5f)); glowVerts += 6;
                    break;
                }
                case WeaponKind.Bomb:
                {
                    at.Y += UnitView.AirAltitude * (1 - t);
                    BeginGlow();
                    Quad(_glowMesh, at, right, up, 0.9f, new Color(1f, 0.6f, 0.3f)); glowVerts += 6;
                    break;
                }
                case WeaponKind.DGun:
                {
                    BeginGlow();
                    Quad(_glowMesh, at, right, up, 3.5f, new Color(1f, 0.85f, 0.4f)); glowVerts += 6;
                    Quad(_glowMesh, at, right, up, 7f, new Color(1f, 0.4f, 0.05f, 0.7f)); glowVerts += 6;
                    if (_rng.Randf() < 0.5f) Flash(at + RandomInSphere(2), new Color(1f, 0.6f, 0.2f), 2f, 0.2f);
                    break;
                }
                case WeaponKind.Missile:
                {
                    BeginGlow();
                    Quad(_glowMesh, at, right, up, 0.8f, new Color(1f, 0.95f, 0.8f)); glowVerts += 6;
                    if (_rng.Randf() < 0.6f) Smoke(at, 0.6f, 0.5f);
                    break;
                }
                default:
                {
                    BeginGlow();
                    Vector3 back = dir.Length() > 0.001f ? -dir.Normalized() * 2.5f : Vector3.Zero;
                    BeamQuad(_glowMesh, at + back, at, 0.35f, new Color(1f, 0.85f, 0.5f)); glowVerts += 6;
                    break;
                }
            }
        }

        // Beams.
        for (int i = _beams.Count - 1; i >= 0; i--)
        {
            Beam b = _beams[i];
            b.Life -= dt;
            if (b.Life <= 0) { _beams.RemoveAt(i); continue; }
            _beams[i] = b;
            BeginGlow();
            float k = b.Life / b.Max;
            BeamQuad(_glowMesh, b.A, b.B, b.Width, new Color(b.C, k)); glowVerts += 6;
            BeamQuad(_glowMesh, b.A, b.B, b.Width * 0.35f, new Color(1, 1, 1, k)); glowVerts += 6;
        }
        foreach (Beam b in _nano)
        {
            BeginGlow();
            float shimmer = 0.55f + 0.45f * Mathf.Sin(Time.GetTicksMsec() / 60f + b.A.X);
            BeamQuad(_glowMesh, b.A, b.B, b.Width, new Color(b.C, 0.5f * shimmer)); glowVerts += 6;
        }

        // Sprites.
        bool smokeBegun = false;
        for (int i = _sprites.Count - 1; i >= 0; i--)
        {
            Sprite s = _sprites[i];
            s.Life -= dt;
            if (s.Life <= 0) { _sprites.RemoveAt(i); continue; }
            s.V.Y -= s.Gravity * dt;
            s.P += s.V * dt;
            float ground = _terrain.SurfaceAt(s.P.X, s.P.Z);
            if (s.P.Y < ground) { s.P.Y = ground; s.V *= 0.3f; }
            _sprites[i] = s;
            float k = s.Life / s.Max;
            float size = s.Size + s.Grow * (1 - k);
            if (s.Smoke)
            {
                if (!smokeBegun) { _smokeMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles); smokeBegun = true; }
                Quad(_smokeMesh, s.P, right, up, size, new Color(s.C, s.C.A * k)); smokeVerts += 6;
            }
            else
            {
                BeginGlow();
                Quad(_glowMesh, s.P, right, up, size, new Color(s.C, s.C.A * Mathf.Min(1, k * 1.5f))); glowVerts += 6;
            }
        }
        if (_sprites.Count > 4000) _sprites.RemoveRange(0, _sprites.Count - 4000);

        if (glowBegun) { if (glowVerts > 0) _glowMesh.SurfaceEnd(); else _glowMesh.ClearSurfaces(); }
        if (smokeBegun) { if (smokeVerts > 0) _smokeMesh.SurfaceEnd(); else _smokeMesh.ClearSurfaces(); }
    }

    /// <summary>A camera-facing square with radial falloff (UV in 0..1).</summary>
    private static void Quad(ImmediateMesh m, Vector3 c, Vector3 right, Vector3 up, float size, Color col)
    {
        Vector3 r = right * size * 0.5f, u = up * size * 0.5f;
        m.SurfaceSetColor(col);
        m.SurfaceSetUV(new Vector2(0, 0)); m.SurfaceAddVertex(c - r + u);
        m.SurfaceSetUV(new Vector2(1, 0)); m.SurfaceAddVertex(c + r + u);
        m.SurfaceSetUV(new Vector2(1, 1)); m.SurfaceAddVertex(c + r - u);
        m.SurfaceSetUV(new Vector2(0, 0)); m.SurfaceAddVertex(c - r + u);
        m.SurfaceSetUV(new Vector2(1, 1)); m.SurfaceAddVertex(c + r - u);
        m.SurfaceSetUV(new Vector2(0, 1)); m.SurfaceAddVertex(c - r - u);
    }

    /// <summary>A ribbon from a to b, facing the camera, with falloff across its width (UV.x offset by 2 marks a beam).</summary>
    private void BeamQuad(ImmediateMesh m, Vector3 a, Vector3 b, float width, Color col)
    {
        Vector3 view = _camera.GlobalTransform.Basis.Z;
        Vector3 along = b - a;
        Vector3 side = along.Cross(view);
        if (side.LengthSquared() < 1e-6f) side = _camera.GlobalTransform.Basis.X;
        side = side.Normalized() * width * 0.5f;
        m.SurfaceSetColor(col);
        m.SurfaceSetUV(new Vector2(2, 0)); m.SurfaceAddVertex(a - side);
        m.SurfaceSetUV(new Vector2(2, 1)); m.SurfaceAddVertex(a + side);
        m.SurfaceSetUV(new Vector2(3, 1)); m.SurfaceAddVertex(b + side);
        m.SurfaceSetUV(new Vector2(2, 0)); m.SurfaceAddVertex(a - side);
        m.SurfaceSetUV(new Vector2(3, 1)); m.SurfaceAddVertex(b + side);
        m.SurfaceSetUV(new Vector2(3, 0)); m.SurfaceAddVertex(b - side);
    }

    private const string GlowShader = @"
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_disabled;
void fragment() {
    float a;
    if (UV.x > 1.5) {
        float d = abs(UV.y * 2.0 - 1.0);
        a = 1.0 - d * d;
    } else {
        vec2 d = UV * 2.0 - 1.0;
        a = clamp(1.0 - dot(d, d), 0.0, 1.0);
        a = a * a;
    }
    ALBEDO = COLOR.rgb * a * COLOR.a * 1.6;
}";

    private const string SmokeShader = @"
shader_type spatial;
render_mode blend_mix, unshaded, depth_draw_never, cull_disabled;
void fragment() {
    vec2 d = UV * 2.0 - 1.0;
    float a = clamp(1.0 - dot(d, d), 0.0, 1.0);
    ALBEDO = COLOR.rgb;
    ALPHA = a * a * COLOR.a;
}";

    private const string ScorchShader = @"
shader_type spatial;
render_mode blend_mix, unshaded, depth_draw_never, cull_disabled;
varying vec4 inst;
void vertex() { inst = INSTANCE_CUSTOM; }
void fragment() {
    vec2 d = UV * 2.0 - 1.0;
    float r = dot(d, d);
    float age = TIME - inst.r;
    float fade = 1.0 - clamp((age - 20.0) / 15.0, 0.0, 1.0);
    ALBEDO = vec3(0.04, 0.035, 0.03);
    ALPHA = clamp(1.0 - r, 0.0, 1.0) * 0.6 * fade;
}";
}
