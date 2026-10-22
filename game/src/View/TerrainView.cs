using System;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Vision;
using Godot;

namespace ContingencyZero.Game.View;

/// <summary>Colours for a map theme.</summary>
public sealed record TerrainPalette(Color Shore, Color Low, Color High, Color Peak, Color Rock, Color Water, Color WaterDeep, bool LavaWater, Color Sky);

/// <summary>
/// Draws the battlefield: a lit, vertex-coloured heightmap mesh with fog of war in its shader,
/// a water (or lava) plane, metal spot markers and a dark border beyond the map edge.
/// </summary>
public partial class TerrainView : Node3D
{
    public const float HeightScale = 1f / MapData.HeightStepsPerUnit;

    public MapData Map { get; private set; } = null!;
    private float[] _heights = Array.Empty<float>();
    private ShaderMaterial _material = null!;
    private Image _fogImage = null!;
    private ImageTexture _fogTexture = null!;
    private int _team = -1;

    public static TerrainPalette PaletteFor(string theme) => theme switch
    {
        "desert" => new TerrainPalette(new Color(0.78f, 0.68f, 0.5f), new Color(0.72f, 0.58f, 0.4f), new Color(0.64f, 0.48f, 0.33f),
            new Color(0.74f, 0.64f, 0.52f), new Color(0.55f, 0.36f, 0.25f), new Color(0.2f, 0.42f, 0.5f, 0.75f), new Color(0.08f, 0.2f, 0.3f), false,
            new Color(0.62f, 0.5f, 0.4f)),
        "lava" => new TerrainPalette(new Color(0.25f, 0.18f, 0.16f), new Color(0.24f, 0.22f, 0.22f), new Color(0.32f, 0.28f, 0.26f),
            new Color(0.45f, 0.4f, 0.38f), new Color(0.18f, 0.15f, 0.15f), new Color(1.0f, 0.38f, 0.05f, 0.95f), new Color(0.9f, 0.15f, 0.02f), true,
            new Color(0.22f, 0.12f, 0.1f)),
        "arctic" => new TerrainPalette(new Color(0.66f, 0.72f, 0.77f), new Color(0.76f, 0.8f, 0.85f), new Color(0.82f, 0.85f, 0.89f),
            new Color(0.88f, 0.9f, 0.93f), new Color(0.48f, 0.52f, 0.58f), new Color(0.35f, 0.55f, 0.7f, 0.8f), new Color(0.12f, 0.25f, 0.4f), false,
            new Color(0.6f, 0.68f, 0.76f)),
        _ => new TerrainPalette(new Color(0.76f, 0.7f, 0.5f), new Color(0.36f, 0.45f, 0.28f), new Color(0.47f, 0.5f, 0.35f),
            new Color(0.6f, 0.6f, 0.5f), new Color(0.45f, 0.43f, 0.4f), new Color(0.15f, 0.4f, 0.55f, 0.78f), new Color(0.05f, 0.18f, 0.3f), false,
            new Color(0.45f, 0.55f, 0.62f)),
    };

    public void Build(MapData map)
    {
        Map = map;
        foreach (Node c in GetChildren()) c.QueueFree();

        int vx = map.TilesX + 1, vy = map.TilesY + 1;
        _heights = new float[vx * vy];
        for (int i = 0; i < _heights.Length; i++) _heights[i] = map.Heights[i] * HeightScale;

        TerrainPalette pal = PaletteFor(map.Theme);
        AddChild(BuildGround(map, pal));
        AddChild(BuildWater(map, pal));
        AddChild(BuildBorder(map, pal));
        AddChild(BuildMetalSpots(map));
    }

    /// <summary>Ground height in world units at a point, bilinear between corners.</summary>
    public float HeightAt(float x, float z)
    {
        if (_heights.Length == 0) return 0;
        float fx = Mathf.Clamp(x / MapData.TileSize, 0, Map.TilesX - 0.001f);
        float fz = Mathf.Clamp(z / MapData.TileSize, 0, Map.TilesY - 0.001f);
        int tx = (int)fx, tz = (int)fz;
        float ux = fx - tx, uz = fz - tz;
        int w = Map.TilesX + 1;
        float h00 = _heights[tz * w + tx], h10 = _heights[tz * w + tx + 1];
        float h01 = _heights[(tz + 1) * w + tx], h11 = _heights[(tz + 1) * w + tx + 1];
        return Mathf.Lerp(Mathf.Lerp(h00, h10, ux), Mathf.Lerp(h01, h11, ux), uz);
    }

    /// <summary>Height to stand things on: the ground, or the water surface where it's flooded.</summary>
    public float SurfaceAt(float x, float z) => Mathf.Max(HeightAt(x, z), WaterHeight);

    public float WaterHeight => Map.WaterLevel * HeightScale;

    public Vector3 NormalAt(float x, float z)
    {
        const float e = 2f;
        float hl = HeightAt(x - e, z), hr = HeightAt(x + e, z);
        float hd = HeightAt(x, z - e), hu = HeightAt(x, z + e);
        return new Vector3(hl - hr, 2 * e, hd - hu).Normalized();
    }

    /// <summary>Where a screen ray first hits the ground (marching along the ray against the heightmap).</summary>
    public Vector3? RayHit(Vector3 origin, Vector3 dir)
    {
        if (dir.Y >= -0.0001f) return null;
        // Start where the ray crosses a ceiling above the highest terrain.
        float top = 120f;
        float t = (origin.Y - top) / -dir.Y;
        if (t < 0) t = 0;
        Vector3 p = origin + dir * t;
        float step = 2f;
        for (int i = 0; i < 600; i++)
        {
            float ground = SurfaceAt(p.X, p.Z);
            if (p.Y <= ground)
            {
                // Refine between the last two samples.
                Vector3 a = p - dir * step, b = p;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 m = (a + b) / 2;
                    if (m.Y <= SurfaceAt(m.X, m.Z)) b = m; else a = m;
                }
                return new Vector3(b.X, SurfaceAt(b.X, b.Z), b.Z);
            }
            p += dir * step;
        }
        // Missed: fall back to the water/ground plane.
        float tp = (origin.Y - WaterHeight) / -dir.Y;
        return origin + dir * tp;
    }

    private static float Hash(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
    }

    private MeshInstance3D BuildGround(MapData map, TerrainPalette pal)
    {
        int vx = map.TilesX + 1, vy = map.TilesY + 1;
        var verts = new Vector3[vx * vy];
        var normals = new Vector3[vx * vy];
        var colours = new Color[vx * vy];
        var uvs = new Vector2[vx * vy];
        float water = map.WaterLevel * HeightScale;

        float min = float.MaxValue, max = float.MinValue;
        foreach (float h in _heights) { min = Mathf.Min(min, h); max = Mathf.Max(max, h); }
        bool hasWater = min < water;
        // Colour bands run from the shoreline when there is water, otherwise from the lowest ground.
        float baseline = hasWater ? water : min;
        // Gentle maps shouldn't stretch the whole palette over a few metres of relief.
        float range = Mathf.Max(20, max - baseline);

        for (int y = 0; y < vy; y++)
        for (int x = 0; x < vx; x++)
        {
            int i = y * vx + x;
            float h = _heights[i];
            verts[i] = new Vector3(x * MapData.TileSize, h, y * MapData.TileSize);
            uvs[i] = new Vector2((float)x / map.TilesX, (float)y / map.TilesY);

            float hl = _heights[y * vx + Math.Max(x - 1, 0)], hr = _heights[y * vx + Math.Min(x + 1, vx - 1)];
            float hd = _heights[Math.Max(y - 1, 0) * vx + x], hu = _heights[Math.Min(y + 1, vy - 1) * vx + x];
            var n = new Vector3(hl - hr, 2 * MapData.TileSize, hd - hu).Normalized();
            normals[i] = n;

            float t = Mathf.Clamp((h - baseline) / range, 0, 1);
            Color c = hasWater && t < 0.08f ? pal.Shore.Lerp(pal.Low, t / 0.08f)
                : !hasWater && t < 0.08f ? pal.Low
                : t < 0.6f ? pal.Low.Lerp(pal.High, (t - 0.08f) / 0.52f)
                : pal.High.Lerp(pal.Peak, (t - 0.6f) / 0.4f);
            float steep = Mathf.Clamp((1 - n.Y) * 6f, 0, 1);
            c = c.Lerp(pal.Rock, steep);
            if (h < water) c = c.Darkened(0.35f);
            float noise = Hash(x, y) * 0.12f - 0.06f + Hash(x / 3, y / 3) * 0.08f - 0.04f;
            colours[i] = new Color(c.R + noise, c.G + noise, c.B + noise * 0.7f);
        }

        var indices = new int[map.TilesX * map.TilesY * 6];
        int k = 0;
        for (int y = 0; y < map.TilesY; y++)
        for (int x = 0; x < map.TilesX; x++)
        {
            int a = y * vx + x, b = a + 1, c = a + vx, d = c + 1;
            // Two triangles per tile, wound so they face up.
            indices[k++] = a; indices[k++] = b; indices[k++] = c;
            indices[k++] = b; indices[k++] = d; indices[k++] = c;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Color] = colours;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        _fogImage = Image.CreateEmpty(map.TilesX, map.TilesY, false, Image.Format.Rg8);
        _fogImage.Fill(new Color(1, 1, 0));
        _fogTexture = ImageTexture.CreateFromImage(_fogImage);

        _material = new ShaderMaterial { Shader = new Shader { Code = GroundShader } };
        _material.SetShaderParameter("fog_tex", _fogTexture);
        _material.SetShaderParameter("map_size", new Vector2(map.Width, map.Height));
        return new MeshInstance3D { Mesh = mesh, MaterialOverride = _material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    }

    /// <summary>Copies a team's fog grids into the ground shader. Pass -1 to clear the fog (spectating).</summary>
    public void UpdateFog(VisionSystem vision, int team)
    {
        if (_fogImage is null) return;
        if (team < 0)
        {
            if (_team != -2) { _fogImage.Fill(new Color(1, 1, 0)); _fogTexture.Update(_fogImage); _team = -2; }
            return;
        }
        _team = team;
        bool[] vis = vision.VisibleGrid(team);
        bool[] exp = vision.ExploredGrid(team);
        int w = vision.TilesX, h = vision.TilesY;
        byte[] data = new byte[w * h * 2];
        for (int i = 0; i < w * h; i++)
        {
            data[i * 2] = vis[i] || vision.RevealAll ? (byte)255 : (byte)0;
            data[i * 2 + 1] = exp[i] || vision.RevealAll ? (byte)255 : (byte)0;
        }
        _fogImage.SetData(w, h, false, Image.Format.Rg8, data);
        _fogTexture.Update(_fogImage);
    }

    private const string GroundShader = @"
shader_type spatial;
render_mode diffuse_lambert, specular_disabled;
uniform sampler2D fog_tex : filter_linear;
uniform vec2 map_size;
varying vec3 world_pos;
void vertex() { world_pos = VERTEX; }
void fragment() {
    vec2 uv = world_pos.xz / map_size;
    vec2 fog = texture(fog_tex, uv).rg;
    float light = mix(mix(0.18, 0.55, fog.g), 1.0, fog.r);
    // Subtle tile grid, like a planning map, only where explored.
    vec2 g = abs(fract(world_pos.xz / 8.0) - 0.5);
    float line = smoothstep(0.47, 0.5, max(g.x, g.y)) * 0.06 * fog.g;
    ALBEDO = COLOR.rgb * light * (1.0 - line);
    ROUGHNESS = 0.95;
}";

    private Node3D BuildWater(MapData map, TerrainPalette pal)
    {
        var node = new Node3D { Name = "Water" };
        float level = map.WaterLevel * HeightScale;
        bool anyWater = false;
        foreach (int h in map.Heights) if (h < map.WaterLevel) { anyWater = true; break; }
        if (!anyWater) return node;

        var plane = new PlaneMesh { Size = new Vector2(map.Width, map.Height), SubdivideWidth = 1, SubdivideDepth = 1 };
        var mat = new ShaderMaterial { Shader = new Shader { Code = pal.LavaWater ? LavaShader : WaterShader } };
        mat.SetShaderParameter("shallow", pal.Water);
        mat.SetShaderParameter("deep", pal.WaterDeep);
        var mi = new MeshInstance3D
        {
            Mesh = plane,
            MaterialOverride = mat,
            Position = new Vector3(map.Width / 2f, level + 0.05f, map.Height / 2f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        node.AddChild(mi);
        return node;
    }

    private const string WaterShader = @"
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, specular_schlick_ggx;
uniform vec4 shallow : source_color;
uniform vec4 deep : source_color;
varying vec3 wp;
void vertex() { wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment() {
    float w = sin(wp.x * 0.15 + TIME * 0.8) * sin(wp.z * 0.13 - TIME * 0.6);
    float w2 = sin((wp.x + wp.z) * 0.31 + TIME * 1.3) * 0.5;
    ALBEDO = mix(deep.rgb, shallow.rgb, 0.55 + 0.15 * w);
    ALPHA = shallow.a;
    ROUGHNESS = 0.15;
    METALLIC = 0.1;
    NORMAL = normalize(NORMAL + vec3(w * 0.08, 0.0, w2 * 0.08));
}";

    private const string LavaShader = @"
shader_type spatial;
render_mode unshaded, cull_disabled;
uniform vec4 shallow : source_color;
uniform vec4 deep : source_color;
varying vec3 wp;
void vertex() { wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment() {
    float n = sin(wp.x * 0.11 + TIME * 0.4) * sin(wp.z * 0.09 - TIME * 0.3) + 0.5 * sin((wp.x - wp.z) * 0.23 + TIME);
    vec3 c = mix(deep.rgb, shallow.rgb, 0.5 + 0.35 * n);
    ALBEDO = c * 1.4;
}";

    private Node3D BuildBorder(MapData map, TerrainPalette pal)
    {
        // A dark skirt around the map so the edge doesn't float in the void.
        float w = map.Width, h = map.Height;
        var mat = new StandardMaterial3D { AlbedoColor = pal.Sky.Darkened(0.75f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        var root = new Node3D { Name = "Border" };
        float pad = 600;
        void Add(Vector3 centre, Vector2 size) =>
            root.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = size }, MaterialOverride = mat, Position = centre });
        float y = -2;
        Add(new Vector3(w / 2, y, -pad / 2), new Vector2(w + pad * 2, pad));
        Add(new Vector3(w / 2, y, h + pad / 2), new Vector2(w + pad * 2, pad));
        Add(new Vector3(-pad / 2, y, h / 2), new Vector2(pad, h));
        Add(new Vector3(w + pad / 2, y, h / 2), new Vector2(pad, h));
        return root;
    }

    private Node3D BuildMetalSpots(MapData map)
    {
        var disc = new CylinderMesh { TopRadius = 3.6f, BottomRadius = 3.6f, Height = 0.2f, RadialSegments = 12, Rings = 1 };
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = disc, InstanceCount = map.MetalSpots.Count };
        for (int i = 0; i < map.MetalSpots.Count; i++)
        {
            var s = map.MetalSpots[i];
            float x = s.Position.X.Raw / 65536f, z = s.Position.Y.Raw / 65536f;
            mm.SetInstanceTransform(i, new Transform3D(Basis.Identity, new Vector3(x, HeightAt(x, z) + 0.08f, z)));
        }
        var mat = new ShaderMaterial { Shader = new Shader { Code = SpotShader } };
        return new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Name = "MetalSpots" };
    }

    private const string SpotShader = @"
shader_type spatial;
render_mode unshaded;
varying vec3 lp;
void vertex() { lp = VERTEX; }
void fragment() {
    float r = length(lp.xz) / 3.6;
    float ring = smoothstep(0.7, 0.78, r) * (1.0 - smoothstep(0.9, 1.0, r));
    float pulse = 0.5 + 0.5 * sin(TIME * 2.0);
    vec3 core = vec3(0.12, 0.13, 0.15) + vec3(0.25, 0.35, 0.4) * (1.0 - r) * 0.5;
    ALBEDO = mix(core, vec3(0.55, 0.85, 1.0) * (0.6 + 0.4 * pulse), ring);
}";
}
