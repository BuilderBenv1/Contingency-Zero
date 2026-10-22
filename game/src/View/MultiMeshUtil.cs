using Godot;

namespace ContingencyZero.Game.View;

/// <summary>
/// MultiMesh helpers. The Compatibility renderer multiplies vertex COLOR by the per-instance
/// colour even when a MultiMesh has no instance colours (giving black), so every MultiMesh that
/// draws vertex-coloured meshes carries white instance colours.
/// </summary>
public static class MultiMeshUtil
{
    public static MultiMesh Create(Mesh mesh, int count, bool customData = true)
    {
        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            UseCustomData = customData,
            Mesh = mesh,
        };
        Resize(mm, count);
        mm.VisibleInstanceCount = 0;
        return mm;
    }

    /// <summary>Sets the instance count (which clears instance data) and whitens the colours.</summary>
    public static void Resize(MultiMesh mm, int count)
    {
        mm.InstanceCount = count;
        for (int i = 0; i < count; i++) mm.SetInstanceColor(i, Colors.White);
    }

    /// <summary>Grows (doubling) to hold at least <paramref name="needed"/> instances.</summary>
    public static void Ensure(MultiMesh mm, int needed)
    {
        if (mm.InstanceCount >= needed) return;
        int size = System.Math.Max(16, mm.InstanceCount);
        while (size < needed) size *= 2;
        Resize(mm, size);
    }
}
