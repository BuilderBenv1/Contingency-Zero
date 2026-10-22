using Godot;

namespace ContingencyZero.Game.View.Meshes;

/// <summary>
/// A faction's look: its palette and the small differences in how shared parts are shaped.
///
/// Lattice (precision, information): pale ceramic panels, slim angular forms on bevelled
/// slabs, reverse-jointed legs, smooth hulls and blue glowing accents.
///
/// Crucible (armour, industry): dark scorched steel with a brown tint, chunky square blocks,
/// treads and wheels, smokestacks, rivet studs and orange glowing furnace accents.
/// </summary>
internal sealed class Style
{
    public required bool IsLattice { get; init; }

    /// <summary>Main armour panels.</summary>
    public required Paint Hull { get; init; }
    /// <summary>Secondary armour, undersides and recessed panels.</summary>
    public required Paint HullDark { get; init; }
    /// <summary>Joints, frames, interiors.</summary>
    public required Paint Mech { get; init; }
    /// <summary>Bare metal: barrels, pistons, masts.</summary>
    public required Paint Metal { get; init; }
    /// <summary>Faction detail colour (Lattice: cool grey-blue trim, Crucible: brass/rust).</summary>
    public required Paint Accent { get; init; }
    /// <summary>
    /// Faction glow (blue / orange). The unit shader treats vertex RGB as linear and emits it
    /// at 1.6×, so glow colours are authored deep (low green) to stay blue / orange after tonemapping.
    /// </summary>
    public required Paint Glow { get; init; }
    /// <summary>A dimmer glow for large lit areas.</summary>
    public required Paint GlowDim { get; init; }
    public required Paint Team { get; init; }
    public required Paint TeamDark { get; init; }
    public required Paint Tread { get; init; }
    public required Paint Glass { get; init; }
    public required Paint Solar { get; init; }

    /// <summary>Sides used for round parts: Lattice is smoother, Crucible chunkier.</summary>
    public int Round => IsLattice ? 8 : 6;

    public static readonly Style Lattice = new()
    {
        IsLattice = true,
        Hull = Paint.Plain(0.74f, 0.75f, 0.78f),
        HullDark = Paint.Plain(0.50f, 0.52f, 0.57f),
        Mech = Paint.Plain(0.24f, 0.26f, 0.30f),
        Metal = Paint.Plain(0.46f, 0.49f, 0.54f),
        Accent = Paint.Plain(0.50f, 0.58f, 0.66f),
        Glow = Paint.Glow(0.1f, 0.42f, 1.0f),
        GlowDim = Paint.Glow(0.05f, 0.2f, 0.55f),
        Team = Paint.Team(0.85f),
        TeamDark = Paint.Team(0.55f),
        Tread = Paint.Plain(0.20f, 0.21f, 0.24f),
        Glass = Paint.Plain(0.14f, 0.22f, 0.32f),
        Solar = Paint.Plain(0.10f, 0.16f, 0.30f),
    };

    public static readonly Style Crucible = new()
    {
        IsLattice = false,
        Hull = Paint.Plain(0.36f, 0.30f, 0.25f),
        HullDark = Paint.Plain(0.24f, 0.20f, 0.17f),
        Mech = Paint.Plain(0.13f, 0.12f, 0.11f),
        Metal = Paint.Plain(0.47f, 0.44f, 0.40f),
        Accent = Paint.Plain(0.52f, 0.37f, 0.22f),
        Glow = Paint.Glow(1.0f, 0.24f, 0.03f),
        GlowDim = Paint.Glow(0.6f, 0.12f, 0.02f),
        Team = Paint.Team(0.80f),
        TeamDark = Paint.Team(0.50f),
        Tread = Paint.Plain(0.12f, 0.11f, 0.10f),
        Glass = Paint.Plain(0.20f, 0.14f, 0.10f),
        Solar = Paint.Plain(0.14f, 0.12f, 0.16f),
    };

    public static Style For(string faction) => faction == "crucible" ? Crucible : Lattice;
}
