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
        // Weathered steel-grey armour with a cold tint, not clean ceramic.
        Hull = Paint.Plain(0.52f, 0.54f, 0.56f),
        HullDark = Paint.Plain(0.34f, 0.36f, 0.39f),
        Mech = Paint.Plain(0.19f, 0.2f, 0.22f),
        Metal = Paint.Plain(0.4f, 0.42f, 0.44f),
        Accent = Paint.Plain(0.4f, 0.45f, 0.5f),
        Glow = Paint.Glow(0.1f, 0.42f, 1.0f),
        GlowDim = Paint.Glow(0.05f, 0.2f, 0.55f),
        Team = Paint.Team(0.8f),
        TeamDark = Paint.Team(0.5f),
        Tread = Paint.Plain(0.15f, 0.15f, 0.16f),
        Glass = Paint.Plain(0.1f, 0.16f, 0.22f),
        Solar = Paint.Plain(0.08f, 0.12f, 0.2f),
    };

    public static readonly Style Crucible = new()
    {
        IsLattice = false,
        // Scorched, oily gunmetal with a rust cast.
        Hull = Paint.Plain(0.33f, 0.3f, 0.26f),
        HullDark = Paint.Plain(0.2f, 0.18f, 0.16f),
        Mech = Paint.Plain(0.11f, 0.1f, 0.09f),
        Metal = Paint.Plain(0.4f, 0.38f, 0.35f),
        Accent = Paint.Plain(0.46f, 0.32f, 0.2f),
        Glow = Paint.Glow(1.0f, 0.24f, 0.03f),
        GlowDim = Paint.Glow(0.6f, 0.12f, 0.02f),
        Team = Paint.Team(0.75f),
        TeamDark = Paint.Team(0.45f),
        Tread = Paint.Plain(0.1f, 0.09f, 0.08f),
        Glass = Paint.Plain(0.16f, 0.11f, 0.08f),
        Solar = Paint.Plain(0.11f, 0.1f, 0.12f),
    };

    public static Style For(string faction) => faction == "crucible" ? Crucible : Lattice;
}
