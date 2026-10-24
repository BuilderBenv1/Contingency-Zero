using Godot;

namespace ContingencyZero.Game.View.Meshes;

/// <summary>
/// The procedural 3D model of one unit type: a static body plus optional independently moving
/// parts. All meshes use the vertex colour convention described in <see cref="UnitModels"/>
/// (alpha 1 = plain, 0 = team colour, 0.5 = glow) and carry one surface with no material.
/// </summary>
public sealed class UnitModel
{
    /// <summary>Everything that doesn't rotate independently. Faces -Z, ground at y = 0.</summary>
    public required ArrayMesh Body { get; init; }

    /// <summary>Optional part that yaws to face the weapon target; authored around its own origin, barrels along -Z.</summary>
    public ArrayMesh? Turret { get; init; }

    /// <summary>Where the turret's origin sits in body space.</summary>
    public Vector3 TurretPivot { get; init; }

    /// <summary>Top of the model in world units (for health bars).</summary>
    public float Height { get; init; }

    /// <summary>Rough footprint radius in world units (for selection rings).</summary>
    public float Radius { get; init; }

    /// <summary>Optional spinning part (turbine blades, radar dish, rotors, extractor heads).</summary>
    public ArrayMesh? Rotor { get; init; }

    /// <summary>Where the rotor's origin sits in body space.</summary>
    public Vector3 RotorPivot { get; init; }

    /// <summary>Local axis the rotor spins around.</summary>
    public Vector3 RotorAxis { get; init; }

    /// <summary>Baked detail maps for hand-made models (R = occlusion, G = worn edges, B = grime); null for procedural ones.</summary>
    public Texture2D? BodyDetail { get; init; }
    public Texture2D? TurretDetail { get; init; }
}
