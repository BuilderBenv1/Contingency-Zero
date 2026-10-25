using System;
using System.Collections.Generic;
using ContingencyZero.Game.View.Meshes;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Map;
using Godot;

namespace ContingencyZero.Game.View;

/// <summary>
/// Small hand-built ground props (grass tufts, stones, scrub, cinders) scattered over the map so
/// the ground carries the same modelled detail as the units. Purely visual: placement comes from
/// a hash of each tile, never from the sim. Vegetation grows on low ground, stones on high ground
/// and shores; nothing on cliffs, under water or under buildings. Re-settled when craters reshape
/// the ground or buildings come and go. Darkened by the fog of war like the ground.
/// </summary>
public partial class PropView : Node3D
{
    private const int ChunkTiles = 32;
    private World _world = null!;
    private TerrainView _terrain = null!;
    private readonly List<(string Name, bool Plant)> _props = new();
    private readonly Dictionary<(int Chunk, string Name), MultiMesh> _batches = new();
    private readonly Dictionary<string, ShaderMaterial> _materials = new();
    private int _lastTerrain = -1;
    private long _lastStructures = -1;
    private float _timer;
    private bool[] _blocked = Array.Empty<bool>();

    // Kinds the nature builder makes, sorted into plants (low ground) and minerals (high ground, shores).
    private static readonly string[] PlantKinds = { "grass", "bush", "fern", "scrub", "cactus", "weed", "twig", "stump" };
    private static readonly string[] MineralKinds = { "pebbles", "stone", "snowlump", "ice", "ash", "cinder", "chip" };

    public void Init(World world, TerrainView terrain)
    {
        _world = world;
        _terrain = terrain;
        string theme = terrain.Map.Theme;
        foreach (var (kinds, plant) in new[] { (PlantKinds, true), (MineralKinds, false) })
            foreach (string k in kinds)
                for (int v = 0; v < 3; v++)
                    if (NatureModels.Get($"prop_{theme}_{k}_{v}") is not null)
                        _props.Add(($"prop_{theme}_{k}_{v}", plant));
        Rebuild();
    }

    public override void _Process(double delta)
    {
        if (_world is null || _props.Count == 0) return;
        _timer -= (float)delta;
        if (_timer > 0) return;
        _timer = 0.5f;
        long structures = StructureChecksum();
        if (_terrain.Revision != _lastTerrain || structures != _lastStructures) Rebuild();
    }

    private long StructureChecksum()
    {
        long sum = 17;
        var u = _world.Units;
        for (int id = 0; id < u.Count; id++)
            if (u.Alive[id] && _world.Catalog[u.TypeIndex[id]].IsStructure)
                sum = sum * 31 + id * 7 + (u.Position[id].X.Raw >> 16) + (u.Position[id].Y.Raw >> 16) * 1013;
        return sum;
    }

    private static float Hash(int a, int b, int c)
    {
        uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791);
        h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
        return (h & 0xFFFFFF) / 16777216f;
    }

    private void Rebuild()
    {
        _lastTerrain = _terrain.Revision;
        _lastStructures = StructureChecksum();
        MapData map = _terrain.Map;
        int tx = map.TilesX, ty = map.TilesY;

        // Tiles under buildings (plus a one-tile apron) stay bare.
        if (_blocked.Length != tx * ty) _blocked = new bool[tx * ty];
        else Array.Clear(_blocked);
        var u = _world.Units;
        for (int id = 0; id < u.Count; id++)
        {
            if (!u.Alive[id]) continue;
            var def = _world.Catalog[u.TypeIndex[id]];
            if (!def.IsStructure) continue;
            int cx = u.Position[id].X.ToIntFloor() / MapData.TileSize, cy = u.Position[id].Y.ToIntFloor() / MapData.TileSize;
            int r = def.Footprint / 2 + 1;
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
                if (x >= 0 && y >= 0 && x < tx && y < ty) _blocked[y * tx + x] = true;
        }

        var lists = new Dictionary<(int, string), List<Transform3D>>();
        float water = _terrain.WaterHeight;
        int chunksX = (tx + ChunkTiles - 1) / ChunkTiles;
        for (int y = 0; y < ty; y++)
        for (int x = 0; x < tx; x++)
        {
            if (_blocked[y * tx + x]) continue;
            var (a, b) = _terrain.WeightsAt(x, y);
            if (b.Y > 0.4f) continue; // cliffs
            float plant = a.Y, mineral = a.X + b.X * 0.8f + 0.12f;
            int chunk = (y / ChunkTiles) * chunksX + x / ChunkTiles;
            for (int slot = 0; slot < 3; slot++)
            {
                float roll = Hash(x, y, slot);
                bool isPlant = slot < 2;
                float chance = isPlant ? plant * 0.8f : mineral * 0.75f;
                if (roll > chance) continue;
                float px = (x + Hash(x, y, slot + 10)) * MapData.TileSize, pz = (y + Hash(x, y, slot + 20)) * MapData.TileSize;
                float h = _terrain.HeightAt(px, pz);
                if (h < water + 0.05f) continue;
                string? name = Pick(isPlant, Hash(x, y, slot + 30));
                if (name is null) continue;
                float s = 0.75f + Hash(x, y, slot + 40) * 0.6f;
                var basis = new Basis(Vector3.Up, Hash(x, y, slot + 50) * Mathf.Tau).Scaled(new Vector3(s, s, s));
                if (!lists.TryGetValue((chunk, name), out var list)) lists[(chunk, name)] = list = new List<Transform3D>();
                list.Add(new Transform3D(basis, new Vector3(px, h - 0.02f, pz)));
            }
        }

        foreach (var (key, list) in lists)
        {
            MultiMesh mm = Batch(key);
            MultiMeshUtil.Ensure(mm, list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                mm.SetInstanceTransform(i, list[i]);
                mm.SetInstanceCustomData(i, new Color(0.6f, 0.6f, 0.6f, 0));
            }
            mm.VisibleInstanceCount = list.Count;
        }
        foreach (var (key, mm) in _batches)
            if (!lists.ContainsKey(key)) mm.VisibleInstanceCount = 0;
    }

    private string? Pick(bool plant, float roll)
    {
        int n = 0;
        foreach (var p in _props) if (p.Plant == plant) n++;
        if (n == 0) return null;
        int want = Math.Min(n - 1, (int)(roll * n));
        foreach (var p in _props)
            if (p.Plant == plant && want-- == 0) return p.Name;
        return null;
    }

    private MultiMesh Batch((int Chunk, string Name) key)
    {
        if (_batches.TryGetValue(key, out var mm)) return mm;
        var model = NatureModels.Get(key.Name)!.Value;
        mm = MultiMeshUtil.Create(model.Mesh, 32);
        if (!_materials.TryGetValue(key.Name, out var mat))
        {
            mat = new ShaderMaterial { Shader = SharedShader };
            mat.SetShaderParameter("fog_tex", _terrain.FogTexture);
            mat.SetShaderParameter("map_size", new Vector2(_terrain.Map.Width, _terrain.Map.Height));
            if (model.Detail is not null)
            {
                mat.SetShaderParameter("detail_tex", model.Detail);
                mat.SetShaderParameter("detailed", true);
            }
            _materials[key.Name] = mat;
        }
        AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        _batches[key] = mm;
        return mm;
    }

    private static Shader? _shader;
    private static Shader SharedShader => _shader ??= new Shader { Code = PropShader };

    // Vertex colour as in the unit models (alpha 0.5 = glow), the baked detail map, and the
    // ground's fog of war so props dim with the land under them.
    private const string PropShader = @"
shader_type spatial;
render_mode diffuse_lambert, specular_disabled, cull_disabled;
uniform sampler2D fog_tex : filter_linear;
uniform vec2 map_size = vec2(512.0);
uniform sampler2D detail_tex : filter_linear_mipmap;
uniform bool detailed = false;
void fragment() {
    vec3 wp = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
    vec2 fog = texture(fog_tex, wp.xz / map_size).rg;
    float light = mix(mix(0.16, 0.5, fog.g), 1.0, fog.r);
    vec3 col = COLOR.rgb;
    vec3 emit = vec3(0.0);
    if (COLOR.a > 0.25 && COLOR.a < 0.75) { emit = col * 1.4 * fog.g; col *= 0.3; }
    if (detailed) {
        vec3 d = texture(detail_tex, UV).rgb;
        col *= mix(0.4, 1.0, d.r);
        col = mix(col, col * 1.25, d.g * 0.5);
        col = mix(col, vec3(0.2, 0.16, 0.12), d.b * 0.35);
    }
    ALBEDO = col * light;
    EMISSION = emit;
    ROUGHNESS = 0.9;
}";
}
