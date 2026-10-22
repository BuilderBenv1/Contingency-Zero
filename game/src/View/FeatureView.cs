using System;
using System.Collections.Generic;
using ContingencyZero.Game.View.Meshes;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.State;
using Godot;

namespace ContingencyZero.Game.View;

/// <summary>
/// Trees, rocks and wrecks. Rebuilt only when the set of features changes (one is reclaimed,
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
        if (_world.Features.Count != _lastCount || checksum != _lastChecksum)
        {
            _lastCount = _world.Features.Count;
            _lastChecksum = checksum;
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
