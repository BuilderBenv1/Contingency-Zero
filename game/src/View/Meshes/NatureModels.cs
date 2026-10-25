using System.Collections.Generic;
using Godot;

namespace ContingencyZero.Game.View.Meshes;

/// <summary>
/// Hand-built nature and debris models (tools/models/features/*.py): trees, rocks, wrecks,
/// rubble and small ground props, at res://models/features/{name}.glb with a "Body" node and a
/// {name}_body.png detail map. Authored in world units. Cached; null when a model doesn't exist,
/// so callers fall back to the procedural meshes.
/// </summary>
internal static class NatureModels
{
    private static readonly Dictionary<string, (ArrayMesh Mesh, Texture2D? Detail, float Size)?> Cache = new();

    public static (ArrayMesh Mesh, Texture2D? Detail, float Size)? Get(string name)
    {
        if (Cache.TryGetValue(name, out var hit)) return hit;
        (ArrayMesh, Texture2D?, float)? result = null;
        string path = $"res://models/features/{name}.glb";
        if (ResourceLoader.Exists(path) && ResourceLoader.Load<PackedScene>(path) is { } scene)
        {
            Node root = scene.Instantiate();
            if (CustomModels.Find(root, "Body") is { } body)
            {
                ArrayMesh mesh = CustomModels.Rebuild(body.Mesh, 1f, out Vector3 min, out Vector3 max);
                result = (mesh, ResourceLoader.Load<Texture2D>($"res://models/features/{name}_body.png"),
                          Mathf.Max(max.X - min.X, max.Z - min.Z));
            }
            root.Free();
        }
        Cache[name] = result;
        return result;
    }
}
