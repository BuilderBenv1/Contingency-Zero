using Godot;

namespace ContingencyZero.Game.Audio;

/// <summary>
/// The background bed of a battlefield: a quiet, seamless loop per map theme (desert and temperate
/// wind, arctic gale with blowing snow, lava crackle and boil), from res://audio/ambience. Plays on
/// the effects bus, so the effects volume sets it too.
/// </summary>
public static class Ambience
{
    /// <summary>Playback level of the loops (they are all built to the same loudness).</summary>
    private const float LevelDb = -3f;

    /// <summary>A looping player for the theme, or null if the theme has no ambience.</summary>
    public static AudioStreamPlayer? Make(string theme)
    {
        string path = $"res://audio/ambience/{theme}.ogg";
        if (!ResourceLoader.Exists(path) || ResourceLoader.Load<AudioStreamOggVorbis>(path) is not { } stream) return null;
        stream.Loop = true;
        var player = new AudioStreamPlayer
        {
            Name = "Ambience", Stream = stream, Bus = AudioBuses.Sfx, VolumeDb = LevelDb, Autoplay = true,
            ProcessMode = Node.ProcessModeEnum.Always,
        };
        return player;
    }
}
