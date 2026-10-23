using System;
using System.Collections.Generic;
using ContingencyZero.Game.View.Meshes;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.State;
using Godot;

namespace ContingencyZero.Game.View;

/// <summary>
/// Trees, rocks, wrecks, and the foundations and rubble that fallen structures leave. Rebuilt only when the set of features changes (one is reclaimed,
/// a wreck appears). Features in unexplored ground are hidden; wrecks shrink as they're stripped.
/// </summary>
public partial class FeatureView : Node3D
{
    private World _world = null!;
    private TerrainView _terrain = null!;
    private int _viewerTeam = -1;
    private readonly Dictionary<string, MultiMesh> _batches = new();
    private ShaderMaterial _material = null!;
    private int _lastCount = -1;
    private long _lastChecksum;
    private int _lastTerrain = -1;
    private float _timer;

    public void Init(World world, TerrainView terrain, int viewerTeam)
    {
        _world = world;
        _terrain = terrain;
        _viewerTeam = viewerTeam;
        _material = new ShaderMaterial { Shader = new Shader { Code = UnitView.UnitShader } };
        Rebuild();
    }

    public void SetViewerTeam(int team) { _viewerTeam = team; Rebuild(); }

    public override void _Process(double delta)
    {
        if (_world is null) return;
        _timer -= (float)delta;
        if (_timer > 0) return;
        _timer = 0.25f;
        long checksum = 0;
        foreach (Feature f in _world.Features)
            checksum = checksum * 31 + (f.Gone ? 1 : (f.Metal.Raw >> 18) + (f.Energy.Raw >> 18) + 7);
        // Rebuild when features change, or a crater moved the ground under them.
        if (_world.Features.Count != _lastCount || checksum != _lastChecksum || _terrain.Revision != _lastTerrain)
        {
            _lastCount = _world.Features.Count;
            _lastChecksum = checksum;
            _lastTerrain = _terrain.Revision;
            Rebuild();
        }
    }

    private MultiMesh Batch(string key, Func<Mesh> mesh)
    {
        if (_batches.TryGetValue(key, out var mm)) return mm;
        mm = MultiMeshUtil.Create(mesh(), 64);
        AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = _material });
        _batches[key] = mm;
        return mm;
    }

    /// <summary>Lowest ground under a square, so a slab never floats over a dip.</summary>
    private float LowestUnder(float x, float z, float half)
    {
        float h = _terrain.HeightAt(x, z);
        for (int i = -1; i <= 1; i++)
        for (int j = -1; j <= 1; j++)
            h = Mathf.Min(h, _terrain.HeightAt(x + i * half * 0.8f, z + j * half * 0.8f));
        return h;
    }

    private static float Hash(int i) => ((uint)(i * 2654435761u) >> 8) / 16777216f;

    private void Rebuild()
    {
        var pending = new Dictionary<string, (Func<Mesh> Make, List<Transform3D> Xf)>();
        string theme = _terrain.Map.Theme;
        var neutral = new Color(0.6f, 0.6f, 0.6f, 0);

        foreach (Feature f in _world.Features)
        {
            if (f.Gone) continue;
            float x = f.Position.X.Raw / 65536f, z = f.Position.Y.Raw / 65536f;
            if (_viewerTeam >= 0 && !_world.Vision.IsExplored(_viewerTeam, MapData.TileOf(f.Position.X), MapData.TileOf(f.Position.Y)))
                continue;

            string key;
            Func<Mesh> make;
            float scale = 1f;
            int variant = (int)(Hash(f.Id) * 3) % 3;
            switch (f.Kind)
            {
                case FeatureKind.Tree:
                    key = $"tree{variant}";
                    make = () => UnitModels.Tree(theme, variant);
                    scale = 0.85f + Hash(f.Id + 17) * 0.4f;
                    break;
                case FeatureKind.Rock:
                    key = $"rock{variant}";
                    make = () => UnitModels.Rock(theme, variant);
                    scale = 0.7f + Math.Min(1.2f, f.Metal.Raw / 65536f / 60f);
                    break;
                case FeatureKind.Rubble:
                    key = $"rubble{variant}";
                    make = () => UnitModels.Rubble(variant);
                    scale = 0.8f + Hash(f.Id + 23) * 0.5f;
                    break;
                case FeatureKind.Wreck when f.SourceTypeIndex >= 0 && _world.Catalog[f.SourceTypeIndex].IsStructure:
                {
                    // A fallen structure: its broken foundation, square to the grid like the building was.
                    int width = _world.Catalog[f.SourceTypeIndex].Footprint * MapData.TileSize;
                    key = $"foundation{width}_{variant}";
                    make = () => UnitModels.Foundation(width, variant);
                    float turn = (int)(Hash(f.Id + 5) * 4) * (Mathf.Pi / 2);
                    float left = Math.Clamp(f.Metal.Raw / (float)Math.Max(1, (_world.Catalog[f.SourceTypeIndex].MetalCost * _world.Catalog[f.SourceTypeIndex].WreckFraction).Raw), 0.35f, 1f);
                    var fb = new Basis(Vector3.Up, turn).Scaled(new Vector3(1, 0.4f + 0.6f * left, 1)); // stripped ruins sink into the ground
                    if (!pending.TryGetValue(key, out var fe)) pending[key] = fe = (make, new List<Transform3D>());
                    fe.Xf.Add(new Transform3D(fb, new Vector3(x, LowestUnder(x, z, width / 2f), z)));
                    continue;
                }
                default:
                {
                    float size = MathF.Round(Math.Clamp(f.Radius.Raw / 65536f * 2f, 3f, 24f));
                    key = $"wreck{size}";
                    make = () => UnitModels.Wreck(size);
                    scale = 1f;
                    break;
                }
            }

            float yaw = Hash(f.Id + 5) * Mathf.Tau;
            var basis = new Basis(Vector3.Up, yaw).Scaled(new Vector3(scale, scale, scale));
            if (!pending.TryGetValue(key, out var entry)) pending[key] = entry = (make, new List<Transform3D>());
            entry.Xf.Add(new Transform3D(basis, new Vector3(x, _terrain.HeightAt(x, z), z)));
        }

        foreach (var (key, entry) in pending)
        {
            MultiMesh mm = Batch(key, entry.Make);
            MultiMeshUtil.Ensure(mm, entry.Xf.Count);
            for (int i = 0; i < entry.Xf.Count; i++)
            {
                mm.SetInstanceTransform(i, entry.Xf[i]);
                mm.SetInstanceCustomData(i, neutral);
            }
        }
        foreach (var kv in _batches)
            kv.Value.VisibleInstanceCount = pending.TryGetValue(kv.Key, out var e) ? e.Xf.Count : 0;
    }
}
