using System;
using Godot;
using ContingencyZero.Sim.Data;

namespace ContingencyZero.Game.View.Meshes;

/// <summary>
/// Hand-built models made in Blender (tools/models/*.py) that replace a unit's procedural one.
/// A model is res://models/{unit id}.glb holding a "Body" node and optionally a "Turret" node
/// (whose position is the turret pivot), with the same vertex colour convention as the
/// procedural models plus UVs for its baked detail maps res://models/{unit id}_body.png and
/// _turret.png (R = ambient occlusion, G = worn edges, B = grime). A spinning part is a node
/// named "Rotor_X", "Rotor_Y" or "Rotor_Z" (spin axis, in game axes) with a _rotor.png map.
/// Mobile units are authored at radius 1 and scaled; structures are authored in world units.
/// </summary>
internal static class CustomModels
{
    /// <summary>Builds the unit's hand-made model at the given scale, or returns null if it has none.</summary>
    public static UnitModel? TryLoad(UnitDef def, float scale)
    {
        string path = $"res://models/{def.Id}.glb";
        if (!ResourceLoader.Exists(path) || ResourceLoader.Load<PackedScene>(path) is not { } scene) return null;
        Node root = scene.Instantiate();
        try
        {
            var body = Find(root, "Body");
            if (body is null) return null;
            var turret = Find(root, "Turret");
            var rotor = Find(root, "Rotor_X") ?? Find(root, "Rotor_Y") ?? Find(root, "Rotor_Z");
            ArrayMesh bodyMesh = Rebuild(body.Mesh, scale, out Vector3 bMin, out Vector3 bMax);
            ArrayMesh? turretMesh = null;
            Vector3 pivot = Vector3.Zero, tMax = Vector3.Zero;
            if (turret is not null)
            {
                turretMesh = Rebuild(turret.Mesh, scale, out _, out tMax);
                pivot = turret.Position * scale;
            }
            ArrayMesh? rotorMesh = null;
            Vector3 rotorPivot = Vector3.Zero, rotorAxis = Vector3.Up, rMax = Vector3.Zero;
            if (rotor is not null)
            {
                rotorMesh = Rebuild(rotor.Mesh, scale, out _, out rMax);
                rotorPivot = rotor.Position * scale;
                string axis = rotor.Name.ToString()[^1..];
                rotorAxis = axis == "X" ? Vector3.Right : axis == "Z" ? Vector3.Back : Vector3.Up;
            }
            float height = Math.Max(bMax.Y, turret is null ? 0 : pivot.Y + tMax.Y);
            if (rotor is not null) height = Math.Max(height, rotorPivot.Y + rMax.Length());
            float reach = Math.Max(Math.Max(-bMin.X, bMax.X), Math.Max(-bMin.Z, bMax.Z));
            return new UnitModel
            {
                Body = bodyMesh,
                Turret = turretMesh,
                TurretPivot = pivot,
                Height = Math.Max(0.5f, height),
                Radius = def.IsStructure ? (Math.Max(1, def.Footprint) * 4f - 0.5f + 0.5f) * 1.12f : Math.Max(scale, reach * 0.9f),
                Rotor = rotorMesh,
                RotorPivot = rotorPivot,
                RotorAxis = rotorAxis,
                RotorDetail = rotor is null ? null : ResourceLoader.Load<Texture2D>($"res://models/{def.Id}_rotor.png"),
                BodyDetail = ResourceLoader.Load<Texture2D>($"res://models/{def.Id}_body.png"),
                TurretDetail = turret is null ? null : ResourceLoader.Load<Texture2D>($"res://models/{def.Id}_turret.png"),
            };
        }
        finally
        {
            root.Free();
        }
    }

    internal static MeshInstance3D? Find(Node n, string name)
    {
        if (n is MeshInstance3D mi && n.Name == name) return mi;
        foreach (Node c in n.GetChildren())
            if (Find(c, name) is { } hit) return hit;
        return null;
    }

    /// <summary>Copies the first surface into a material-less mesh, scaled into world units.</summary>
    internal static ArrayMesh Rebuild(Mesh mesh, float scale, out Vector3 min, out Vector3 max)
    {
        var arrays = mesh.SurfaceGetArrays(0);
        var verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        max = -min;
        for (int i = 0; i < verts.Length; i++)
        {
            verts[i] *= scale;
            min = min.Min(verts[i]);
            max = max.Max(verts[i]);
        }
        var outArrays = new Godot.Collections.Array();
        outArrays.Resize((int)Mesh.ArrayType.Max);
        outArrays[(int)Mesh.ArrayType.Vertex] = verts;
        foreach (var t in new[] { Mesh.ArrayType.Normal, Mesh.ArrayType.Color, Mesh.ArrayType.TexUV, Mesh.ArrayType.Index })
            outArrays[(int)t] = arrays[(int)t];
        var result = new ArrayMesh();
        result.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, outArrays);
        return result;
    }
}
