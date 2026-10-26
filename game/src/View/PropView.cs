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
    private readonly Dictionary<string, MultiMesh> _clutterBatches = new();
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
        bool desert = map.Theme == "desert";
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
                // Desert stays calm: half the scatter density, so the ground reads quiet.
                float chance = isPlant ? plant * (desert ? 0.4f : 0.8f) : mineral * (desert ? 0.5f : 0.75f);
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

        SyncClutter(lists);
        foreach (var (key, list) in lists)
        {
            MultiMesh mm = Batch(key);
            MultiMeshUtil.Ensure(mm, list.Count);
            // Desert scrub is muted toward dusty sage so the scatter doesn't sparkle yellow.
            Color tint = desert && IsPlantName(key.Item2) ? new Color(0.78f, 0.74f, 0.64f) : Colors.White;
            for (int i = 0; i < list.Count; i++)
            {
                mm.SetInstanceTransform(i, list[i]);
                mm.SetInstanceColor(i, tint);
                mm.SetInstanceCustomData(i, new Color(0.6f, 0.6f, 0.6f, 0));
            }
            mm.VisibleInstanceCount = list.Count;
        }
        foreach (var (key, mm) in _batches)
            if (!lists.ContainsKey(key)) mm.VisibleInstanceCount = 0;
    }

    // ---- Clutter: ruined walls, field walls, supply dumps and oases in hashed clusters. ----
    // A coarse grid rolls a few cluster centres per map; each gets a small settlement-like
    // arrangement on flat, dry, open ground well away from starts, metal and buildings. Purely
    // visual, like the scatter pass; placement hashes coordinates so every viewer sees the same.

    private const int ClusterCellTiles = 12; // 96 world units between cluster candidates
    private const int ClutterSalt = 900;

    private void SyncClutter(Dictionary<(int, string), List<Transform3D>> lists)
    {
        MapData map = _terrain.Map;
        foreach (Node3D old in _oasisExtras) old.QueueFree();
        _oasisExtras.Clear();
        if (map.Theme != "desert")
        {
            foreach (var (_, mm) in _clutterBatches) mm.VisibleInstanceCount = 0;
            foreach (var (_, mm) in _shadowBatches) mm.VisibleInstanceCount = 0;
            return;
        }
        var placed = new Dictionary<string, List<Transform3D>>();
        int cellsX = map.TilesX / ClusterCellTiles, cellsY = map.TilesY / ClusterCellTiles;
        int chunksX = (map.TilesX + ChunkTiles - 1) / ChunkTiles;
        for (int cy = 0; cy < cellsY; cy++)
        for (int cx = 0; cx < cellsX; cx++)
        {
            if (Hash(cx, cy, ClutterSalt) > 0.34f) continue;
            float wx = (cx + 0.25f + Hash(cx, cy, ClutterSalt + 1) * 0.5f) * ClusterCellTiles * MapData.TileSize;
            float wz = (cy + 0.25f + Hash(cx, cy, ClutterSalt + 2) * 0.5f) * ClusterCellTiles * MapData.TileSize;
            if (!ClusterSpotOK(wx, wz)) continue;
            float kind = Hash(cx, cy, ClutterSalt + 3);
            if (kind < 0.34f) ClusterVillage(placed, cx, cy, wx, wz);
            else if (kind < 0.52f) ClusterFieldWalls(placed, cx, cy, wx, wz);
            else if (kind < 0.68f) ClusterRuin(placed, cx, cy, wx, wz);
            else if (kind < 0.86f) ClusterSupplies(placed, cx, cy, wx, wz);
            else if (FindOasisSpot(cx, cy, wx, wz, out float ox, out float oz)) ClusterOasis(placed, lists, chunksX, cx, cy, ox, oz);
            else ClusterSupplies(placed, cx, cy, wx, wz);
        }
        foreach (var (name, list) in placed)
        {
            MultiMesh mm = ClutterBatch(name);
            MultiMeshUtil.Ensure(mm, list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                mm.SetInstanceTransform(i, list[i]);
                mm.SetInstanceCustomData(i, new Color(0.6f, 0.6f, 0.6f, 0));
            }
            mm.VisibleInstanceCount = list.Count;
            // Soft contact shadow under each piece, so the clutter sits in the ground.
            Vector2 fp = ClutterModels.Footprint(name);
            MultiMesh sh = ShadowBatch(name);
            MultiMeshUtil.Ensure(sh, list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var flat = new Transform3D(Basis.Identity.Scaled(new Vector3(fp.X, 1, fp.Y)), list[i].Origin + new Vector3(0, 0.08f, 0));
                sh.SetInstanceTransform(i, flat);
            }
            sh.VisibleInstanceCount = list.Count;
        }
        foreach (var (name, mm) in _clutterBatches)
            if (!placed.ContainsKey(name)) mm.VisibleInstanceCount = 0;
        foreach (var (name, mm) in _shadowBatches)
            if (!placed.ContainsKey(name)) mm.VisibleInstanceCount = 0;
    }

    /// <summary>A small settlement: one or two houses, a well, some wall runs and supplies (5-8 pieces).</summary>
    private void ClusterVillage(Dictionary<string, List<Transform3D>> placed, int cx, int cy, float wx, float wz)
    {
        int s = ClutterSalt + 150;
        PlaceClutter(placed, "clutter_well", wx + (Hash(cx, cy, s) - 0.5f) * 6f, wz + (Hash(cx, cy, s + 1) - 0.5f) * 6f,
            Hash(cx, cy, s + 2) * Mathf.Tau, 1f);
        int houses = 1 + (int)(Hash(cx, cy, s + 3) * 2f);
        for (int h = 0; h < houses; h++)
        {
            float a = Hash(cx + h * 7, cy, s + 4) * Mathf.Tau;
            float r = 11f + Hash(cx, cy + h * 5, s + 5) * 5f;
            PlaceClutter(placed, "clutter_house_ruin", wx + Mathf.Cos(a) * r, wz + Mathf.Sin(a) * r,
                Hash(cx + h, cy, s + 6) * Mathf.Tau, 1f);
        }
        int walls = 2 + (int)(Hash(cx, cy, s + 7) * 2f);
        for (int i = 0; i < walls; i++)
        {
            float a = Hash(cx + i * 3, cy, s + 8) * Mathf.Tau;
            float r = 7f + Hash(cx, cy + i * 9, s + 9) * 7f;
            PlaceClutter(placed, "clutter_wall_ruin", wx + Mathf.Cos(a) * r, wz + Mathf.Sin(a) * r,
                Mathf.Round(a / (Mathf.Pi / 2f)) * (Mathf.Pi / 2f) + (Hash(cx, cy + i, s + 10) - 0.5f) * 0.5f,
                0.9f + Hash(cx + i, cy + i, s + 11) * 0.2f);
        }
        string supply = Hash(cx, cy, s + 12) < 0.5f ? "clutter_barrels" : "clutter_crates";
        float sa = Hash(cx + 5, cy, s + 13) * Mathf.Tau;
        PlaceClutter(placed, supply, wx + Mathf.Cos(sa) * 5f, wz + Mathf.Sin(sa) * 5f, Hash(cx, cy + 5, s + 14) * Mathf.Tau, 1f);
    }

    /// <summary>Flat, dry, unblocked ground, clear of start positions and metal spots.</summary>
    private bool ClusterSpotOK(float wx, float wz)
    {
        MapData map = _terrain.Map;
        float water = _terrain.WaterHeight;
        float h0 = _terrain.HeightAt(wx, wz);
        if (h0 < water + 0.5f) return false;
        for (int i = 0; i < 4; i++)
        {
            float a = i / 4f * Mathf.Tau;
            if (Mathf.Abs(_terrain.HeightAt(wx + Mathf.Cos(a) * 6f, wz + Mathf.Sin(a) * 6f) - h0) > 1.1f) return false;
        }
        int tx = Math.Clamp((int)(wx / MapData.TileSize), 0, map.TilesX - 1);
        int ty = Math.Clamp((int)(wz / MapData.TileSize), 0, map.TilesY - 1);
        if (_blocked[ty * map.TilesX + tx]) return false;
        var p = new Vector2(wx, wz);
        foreach (var s in map.StartPositions)
            if (new Vector2(s.X.Raw / 65536f, s.Y.Raw / 65536f).DistanceTo(p) < 64f) return false;
        foreach (var m in map.MetalSpots)
            if (new Vector2(m.Position.X.Raw / 65536f, m.Position.Y.Raw / 65536f).DistanceTo(p) < 28f) return false;
        return true;
    }

    /// <summary>Oasis cluster centres often land away from the water they seek. Search a coarse
    /// grid around the candidate for a dry, flat, unblocked bank spot near water.</summary>
    private bool FindOasisSpot(int cx, int cy, float wx, float wz, out float ox, out float oz)
    {
        for (int dz = -2; dz <= 2; dz++)
        for (int dx = -2; dx <= 2; dx++)
        {
            float x = wx + dx * 64f + (Hash(cx + dx, cy + dz, ClutterSalt + 60) - 0.5f) * 48f;
            float z = wz + dz * 64f + (Hash(cx + dx, cy + dz, ClutterSalt + 61) - 0.5f) * 48f;
            if (x < 20 || z < 20 || x > _terrain.Map.Width - 20 || z > _terrain.Map.Height - 20) continue;
            if (!ClusterSpotOK(x, z) || !NearWater(x, z)) continue;
            ox = x; oz = z;
            return true;
        }
        ox = oz = 0;
        return false;
    }

    private bool NearWater(float wx, float wz)
    {
        float water = _terrain.WaterHeight;
        for (int ring = 0; ring < 6; ring++)
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * Mathf.Tau;
            float r = 24f + ring * 22f;
            if (_terrain.HeightAt(wx + Mathf.Cos(a) * r, wz + Mathf.Sin(a) * r) < water) return true;
        }
        return false;
    }

    private void PlaceClutter(Dictionary<string, List<Transform3D>> placed, string name, float wx, float wz, float yaw, float scale)
    {
        float h = _terrain.HeightAt(wx, wz);
        var basis = new Basis(Vector3.Up, yaw).Scaled(new Vector3(scale, scale, scale));
        if (!placed.TryGetValue(name, out var list)) placed[name] = list = new List<Transform3D>();
        list.Add(new Transform3D(basis, new Vector3(wx, h - 0.04f, wz)));
    }

    private void ClusterFieldWalls(Dictionary<string, List<Transform3D>> placed, int cx, int cy, float wx, float wz)
    {
        float yaw = Hash(cx, cy, ClutterSalt + 10) * Mathf.Tau;
        var dir = new Vector2(Mathf.Cos(yaw), Mathf.Sin(yaw));
        int n = 3 + (int)(Hash(cx, cy, ClutterSalt + 11) * 3f);
        for (int i = 0; i < n; i++)
        {
            float along = (i - (n - 1) / 2f) * 9.2f;
            float side = (Hash(cx * 7 + i, cy, ClutterSalt + 12) - 0.5f) * 3f;
            PlaceClutter(placed, "clutter_field_wall", wx + dir.X * along - dir.Y * side, wz + dir.Y * along + dir.X * side,
                Mathf.Atan2(-dir.Y, dir.X), 1.1f + Hash(cx + i, cy, ClutterSalt + 13) * 0.2f);
        }
    }

    private void ClusterRuin(Dictionary<string, List<Transform3D>> placed, int cx, int cy, float wx, float wz)
    {
        int n = 2 + (int)(Hash(cx, cy, ClutterSalt + 20) * 3f);
        for (int i = 0; i < n; i++)
        {
            float a = Hash(cx, cy, ClutterSalt + 21 + i) * Mathf.Tau;
            float r = 3f + Hash(cx + i, cy, ClutterSalt + 25) * 5f;
            PlaceClutter(placed, "clutter_wall_ruin", wx + Mathf.Cos(a) * r, wz + Mathf.Sin(a) * r,
                Mathf.Round(a / (Mathf.Pi / 2f)) * (Mathf.Pi / 2f) + (Hash(cx, cy + i, ClutterSalt + 26) - 0.5f) * 0.4f,
                1.1f + Hash(cx + i, cy + i, ClutterSalt + 27) * 0.3f);
        }
    }

    private void ClusterSupplies(Dictionary<string, List<Transform3D>> placed, int cx, int cy, float wx, float wz)
    {
        PlaceClutter(placed, "clutter_barrels", wx, wz, Hash(cx, cy, ClutterSalt + 40) * Mathf.Tau, 1.15f);
        int extra = 1 + (int)(Hash(cx, cy, ClutterSalt + 41) * 3f);
        for (int i = 0; i < extra; i++)
        {
            string name = Hash(cx + i, cy, ClutterSalt + 42) < 0.5f ? "clutter_crates" : "clutter_barrels";
            float a = Hash(cx + i, cy, ClutterSalt + 43) * Mathf.Tau;
            float r = 4f + Hash(cx, cy + i, ClutterSalt + 44) * 4f;
            PlaceClutter(placed, name, wx + Mathf.Cos(a) * r, wz + Mathf.Sin(a) * r,
                Hash(cx, cy, ClutterSalt + 45 + i) * Mathf.Tau, 1.05f + Hash(cx + i, cy, ClutterSalt + 46) * 0.2f);
        }
    }

    /// <summary>An oasis: a small pool on damp dark sand, ringed by palms and scrub.</summary>
    private void ClusterOasis(Dictionary<string, List<Transform3D>> placed, Dictionary<(int, string), List<Transform3D>> lists, int chunksX, int cx, int cy, float wx, float wz)
    {
        // Damp sand disc and the pool itself, as a little node placed once per cluster.
        wx0 = wx; wz0 = wz; _baseY = _terrain.HeightAt(wx, wz);
        var node = new Node3D { Position = new Vector3(wx, _baseY + 0.05f, wz) };
        node.AddChild(new MeshInstance3D { Mesh = ClutterModels.DrapedDisc(5.6f, 0.1f, poolRings, (dx, dz) => _terrain.HeightAt(wx0 + dx, wz0 + dz) - _baseY), MaterialOverride = PoolMaterial(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        AddChild(node);
        _oasisExtras.Add(node);

        int palms = 4 + (int)(Hash(cx, cy, ClutterSalt + 50) * 3f);
        for (int i = 0; i < palms; i++)
        {
            float a = Hash(cx + i, cy, ClutterSalt + 52) * Mathf.Tau;
            float r = 4.5f + Hash(cx, cy + i, ClutterSalt + 53) * 4f;
            float px = wx + Mathf.Cos(a) * r, pz = wz + Mathf.Sin(a) * r;
            float h = _terrain.HeightAt(px, pz);
            if (h < _terrain.WaterHeight + 0.3f) continue; // palms stand on the bank, not in the pool
            PlaceClutter(placed, "clutter_palm", px, pz, Hash(cx, cy + i, ClutterSalt + 55) * Mathf.Tau, 1.1f + Hash(cx + i, cy + i, ClutterSalt + 54) * 0.55f);
        }
        // Sparse reed clumps sit on the damp bank, never out in the water.
        for (int i = 0; i < 3; i++)
        {
            float a = Hash(cx + i, cy, ClutterSalt + 160) * Mathf.Tau;
            float r = 4.0f + Hash(cx, cy + i, ClutterSalt + 161) * 0.8f;
            float px = wx + Mathf.Cos(a) * r, pz = wz + Mathf.Sin(a) * r;
            if (_terrain.HeightAt(px, pz) < _terrain.WaterHeight + 0.15f) continue;
            PlaceClutter(placed, "clutter_reeds", px, pz, a, 0.8f + Hash(cx + i, cy, ClutterSalt + 162) * 0.35f);
        }
        // Scrub and stones at the water's edge.
        for (int i = 0; i < 5; i++)
        {
            string name = Hash(cx + i, cy, ClutterSalt + 56) < 0.7f ? $"prop_{_terrain.Map.Theme}_scrub_{(int)(Hash(cx, cy + i, ClutterSalt + 57) * 2f)}" : $"prop_{_terrain.Map.Theme}_pebbles_{(int)(Hash(cx, cy + i, ClutterSalt + 57) * 2f)}";
            if (NatureModels.Get(name) is null) continue;
            float a = Hash(cx + i * 2, cy, ClutterSalt + 58) * Mathf.Tau;
            float r = 7.5f + Hash(cx, cy + i * 3, ClutterSalt + 59) * 4f;
            float px = wx + Mathf.Cos(a) * r, pz = wz + Mathf.Sin(a) * r;
            float h = _terrain.HeightAt(px, pz);
            if (h < _terrain.WaterHeight + 0.25f) continue;
            PlaceClutterAt(lists, chunksX, name, px, pz, Hash(cx, cy + i, ClutterSalt + 60) * Mathf.Tau, 0.9f + Hash(cx + i, cy, ClutterSalt + 61) * 0.5f);
        }
    }

    /// <summary>Adds a scatter-pass (chunked, detail-textured) model at a spot.</summary>
    private void PlaceClutterAt(Dictionary<(int, string), List<Transform3D>> lists, int chunksX, string name, float px, float pz, float yaw, float s)
    {
        float h = _terrain.HeightAt(px, pz);
        int chunk = ((int)(pz / MapData.TileSize) / ChunkTiles) * chunksX + (int)(px / MapData.TileSize) / ChunkTiles;
        var basis = new Basis(Vector3.Up, yaw).Scaled(new Vector3(s, s, s));
        if (!lists.TryGetValue((chunk, name), out var list)) lists[(chunk, name)] = list = new List<Transform3D>();
        list.Add(new Transform3D(basis, new Vector3(px, h - 0.02f, pz)));
    }

    private readonly List<Node3D> _oasisExtras = new();
    private ShaderMaterial? _clutterMaterial;
    private ShaderMaterial? _poolMaterial;
    private static readonly (float r, Color c)[] dampRings =
    {
        (3.0f, new Color(0.66f, 0.54f, 0.39f)),
        (4.4f, new Color(0.66f, 0.54f, 0.39f)),
        (5.6f, new Color(0.66f, 0.54f, 0.39f)),
    };
    private static readonly (float r, Color c)[] poolRings =
    {
        (2.7f, new Color(0.24f, 0.37f, 0.41f)),
        (3.6f, new Color(0.42f, 0.54f, 0.52f)),
        (4.8f, new Color(0.65f, 0.56f, 0.43f)),
        (5.6f, new Color(0.7f, 0.61f, 0.47f)),
    };

    private float wx0, wz0, _baseY;
    private ShaderMaterial ClutterMaterial()
    {
        _clutterMaterial ??= new ShaderMaterial { Shader = SharedShader };
        _clutterMaterial.SetShaderParameter("fog_tex", _terrain.FogTexture);
        _clutterMaterial.SetShaderParameter("map_size", new Vector2(_terrain.Map.Width, _terrain.Map.Height));
        return _clutterMaterial;
    }

    private ShaderMaterial PoolMaterial()
    {
        _poolMaterial ??= new ShaderMaterial { Shader = PoolShader };
        _poolMaterial.SetShaderParameter("fog_tex", _terrain.FogTexture);
        _poolMaterial.SetShaderParameter("map_size", new Vector2(_terrain.Map.Width, _terrain.Map.Height));
        return _poolMaterial;
    }

    private static Shader? _poolShader;
    private static Shader PoolShader => _poolShader ??= new Shader { Code = PoolShaderCode };
    private const string PoolShaderCode = @"
shader_type spatial;
render_mode diffuse_lambert, specular_schlick_ggx, cull_disabled;
uniform sampler2D fog_tex : filter_linear;
uniform vec2 map_size = vec2(512.0);
varying vec3 pool_pos;
void vertex() { pool_pos = VERTEX; }
void fragment() {
    vec3 wp = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
    vec2 fog = texture(fog_tex, wp.xz / map_size).rg;
    float light = mix(mix(0.16, 0.5, fog.g), 1.0, fog.r);
    float r = length(pool_pos.xz);
    float water = 1.0 - smoothstep(3.0, 3.8, r);
    float wet = smoothstep(3.1, 3.8, r) * (1.0 - smoothstep(4.8, 5.6, r));
    float ripple = sin(pool_pos.x * 2.4 + TIME * 0.7) * sin(pool_pos.z * 2.0 - TIME * 0.5);
    vec3 col = mix(COLOR.rgb, vec3(0.72, 0.64, 0.5), wet * 0.32);
    col += water * pow(max(ripple, 0.0), 12.0) * 0.055;
    ALBEDO = col * light;
    ROUGHNESS = mix(0.88, 0.2, water);
    SPECULAR = mix(0.05, 0.5, water);
    NORMAL = normalize(NORMAL + vec3(ripple * 0.035 * water, 0.0, ripple * 0.025 * water));
}";

    private readonly Dictionary<string, MultiMesh> _shadowBatches = new();
    private static Shader? _shadowShader;
    private static Shader ShadowShader => _shadowShader ??= new Shader { Code = ShadowShaderCode };

    private const string ShadowShaderCode = @"
shader_type spatial;
render_mode blend_mix, unshaded, depth_draw_never, cull_disabled, shadows_disabled;
void fragment() {
    vec2 d = UV * 2.0 - 1.0;
    float a = clamp(1.0 - dot(d, d), 0.0, 1.0);
    ALBEDO = vec3(0.02, 0.02, 0.015);
    ALPHA = a * a * 0.38;
}";

    private MultiMesh ClutterBatch(string name)
    {
        if (_clutterBatches.TryGetValue(name, out var mm)) return mm;
        mm = MultiMeshUtil.Create(ClutterModels.Get(name)!, 16);
        AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = ClutterMaterial(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        _clutterBatches[name] = mm;
        return mm;
    }

    /// <summary>The soft dark disc under each clutter piece (same transforms, flattened).</summary>
    private MultiMesh ShadowBatch(string name)
    {
        if (_shadowBatches.TryGetValue(name, out var mm)) return mm;
        mm = MultiMeshUtil.Create(new PlaneMesh { Size = new Vector2(1, 1) }, 16, customData: false);
        AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = new ShaderMaterial { Shader = ShadowShader }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        _shadowBatches[name] = mm;
        return mm;
    }

    private static bool IsPlantName(string name)
    {
        foreach (string k in PlantKinds)
            if (name.Contains(k)) return true;
        return false;
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
