using System;
using System.Collections.Generic;
using ContingencyZero.Game.View;
using ContingencyZero.Game.View.Meshes;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using Godot;

namespace ContingencyZero.Game.Match;

/// <summary>
/// The placement preview for a build template: a translucent model and a footprint square for
/// every structure, green where the sim would accept it and red where it wouldn't. Owned by the
/// <see cref="InputController"/>; set <see cref="Items"/> each frame and clear it to hide.
/// </summary>
public partial class TemplatePreview : Node3D
{
    private World _world = null!;
    private TerrainView _terrain = null!;
    private readonly ImmediateMesh _ground = new();
    private readonly List<MeshInstance3D> _models = new();
    private readonly List<int> _modelTypes = new();
    private Shader _ghostShader = null!;

    /// <summary>What to draw: structure type, snapped world centre (XZ) and whether it can be placed.</summary>
    public readonly List<(int Type, FixVec2 Centre, bool Valid)> Items = new();

    public void Init(World world, TerrainView terrain)
    {
        _world = world;
        _terrain = terrain;
        var mat = new ShaderMaterial { Shader = new Shader { Code = GroundShader } };
        AddChild(new MeshInstance3D { Mesh = _ground, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        _ghostShader = new Shader { Code = GhostShader };
    }

    public override void _Process(double delta)
    {
        if (_world is null) return;
        _ground.ClearSurfaces();
        int verts = 0;
        if (Items.Count > 0)
        {
            _ground.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            for (int i = 0; i < Items.Count; i++)
            {
                var (type, centre, valid) = Items[i];
                UnitDef def = _world.Catalog[type];
                float x = centre.X.Raw / 65536f, z = centre.Y.Raw / 65536f;
                float y = def.NeedsWater ? _terrain.WaterHeight : _terrain.HeightAt(x, z);
                var p = new Vector3(x, y, z);
                Color c = valid ? new Color(0.3f, 1f, 0.5f, 0.9f) : new Color(1f, 0.3f, 0.25f, 0.9f);
                verts += Square(_ground, p + new Vector3(0, 0.3f, 0), def.Footprint * MapData.TileSize / 2f, 0.4f, c);
                MeshInstance3D m = Model(i, type);
                m.Visible = true;
                m.Position = p;
                ((ShaderMaterial)m.MaterialOverride).SetShaderParameter("tint", valid ? new Color(0.3f, 1f, 0.5f, 0.45f) : new Color(1f, 0.25f, 0.2f, 0.45f));
            }
            if (verts > 0) _ground.SurfaceEnd(); else _ground.ClearSurfaces();
        }
        for (int i = Items.Count; i < _models.Count; i++) _models[i].Visible = false;
    }

    private MeshInstance3D Model(int slot, int type)
    {
        while (_models.Count <= slot)
        {
            var m = new MeshInstance3D { MaterialOverride = new ShaderMaterial { Shader = _ghostShader }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
            AddChild(m);
            _models.Add(m);
            _modelTypes.Add(-1);
        }
        if (_modelTypes[slot] != type)
        {
            _models[slot].Mesh = UnitModels.For(_world.Catalog[type]).Body;
            _modelTypes[slot] = type;
        }
        return _models[slot];
    }

    private static int Square(ImmediateMesh m, Vector3 c, float half, float width, Color col)
    {
        m.SurfaceSetColor(col);
        const int segments = 4;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.Tau / segments + Mathf.Pi / 4;
            float a1 = (i + 1) * Mathf.Tau / segments + Mathf.Pi / 4;
            float s = Mathf.Sqrt2;
            Vector3 d0 = new(Mathf.Cos(a0) * s, 0, Mathf.Sin(a0) * s), d1 = new(Mathf.Cos(a1) * s, 0, Mathf.Sin(a1) * s);
            float r0 = half - width, r1 = half;
            Vector3 i0 = c + d0 * r0, o0 = c + d0 * r1, i1 = c + d1 * r0, o1 = c + d1 * r1;
            m.SurfaceAddVertex(i0); m.SurfaceAddVertex(o0); m.SurfaceAddVertex(o1);
            m.SurfaceAddVertex(i0); m.SurfaceAddVertex(o1); m.SurfaceAddVertex(i1);
        }
        return segments * 6;
    }

    private const string GroundShader = @"
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
