namespace ContingencyZero.Game.Audio;

/// <summary>
/// Every sound effect the game can play. All of them are synthesised in code at startup
/// (see <see cref="SfxSynth"/>); there are no audio asset files.
/// </summary>
public enum SfxKind
{
    // UI
    Click, Hover,

    // Weapons firing
    Laser, Cannon, Artillery, Missile, Flak, Bomb, DGun,

    // Impacts, unit deaths and reactor blasts
    ImpactSmall, ExplosionSmall, ExplosionLarge, ExplosionHuge,

    // Construction
    BuildStart, BuildComplete, UnitReady, Nano, Reclaim,

    // Feedback ("Alert" = base under attack)
    Select, Acknowledge, Error, Alert, MissionMessage,

    // Match result stingers
    Victory, Defeat,
}
