using System;
using Godot;

namespace ContingencyZero.Game.Audio;

/// <summary>
/// Creates the audio buses the sound system plays on, unless the project's bus layout already
/// defines them: "SFX" and "Music" (both into Master), plus four fixed-pan sub-buses feeding "SFX"
/// that give battlefield sounds a left/right position. Plain AudioStreamPlayers can't pan, and
/// AudioStreamPlayer3D doesn't suit an orthographic camera hundreds of units above the ground, so a
/// positional sound is simply played on the sub-bus nearest its pan.
/// </summary>
internal static class AudioBuses
{
    public const string Master = "Master";
    public const string Sfx = "SFX";
    public const string Music = "Music";

    /// <summary>Pan of each battlefield bus, left to right. The centre one is SFX itself.</summary>
    private static readonly float[] PanPositions = { -0.7f, -0.35f, 0f, 0.35f, 0.7f };
    private static readonly string[] PanBusNames = { "SFX_PanL2", "SFX_PanL1", Sfx, "SFX_PanR1", "SFX_PanR2" };

    private static StringName[]? _panBuses;
    private static StringName? _musicBus;

    /// <summary>Index of the centre (unpanned) bus for <see cref="PanBus"/>.</summary>
    public const int CenterPan = 2;

    /// <summary>The "Music" bus name.</summary>
    public static StringName MusicBus => _musicBus ??= new StringName(Music);

    /// <summary>Nearest pan-bus index for a pan in [-1 (left), 1 (right)].</summary>
    public static int PanIndex(float pan) =>
        Math.Clamp((int)MathF.Round(pan * 2f) + CenterPan, 0, PanPositions.Length - 1);

    /// <summary>Bus name for a pan-bus index from <see cref="PanIndex"/>. Cached, so assigning it doesn't allocate.</summary>
    public static StringName PanBus(int index)
    {
        if (_panBuses is null)
        {
            _panBuses = new StringName[PanBusNames.Length];
            for (int i = 0; i < PanBusNames.Length; i++) _panBuses[i] = new StringName(PanBusNames[i]);
        }
        return _panBuses[index];
    }

    /// <summary>Creates any missing buses. Safe to call repeatedly.</summary>
    public static void Ensure()
    {
        int sfx = EnsureBus(Sfx, Master, out bool createdSfx);
        if (createdSfx)
        {
            // Hundreds of overlapping weapons and explosions must never clip the output.
            AudioServer.AddBusEffect(sfx, new AudioEffectHardLimiter { CeilingDb = -0.5f, PreGainDb = 0f, Release = 0.12f });
        }
        EnsureBus(Music, Master, out _);

        for (int i = 0; i < PanBusNames.Length; i++)
        {
            float pan = PanPositions[i];
            if (pan == 0f) continue;
            int index = EnsureBus(PanBusNames[i], Sfx, out bool created);
            if (!created) continue;
            AudioServer.AddBusEffect(index, new AudioEffectPanner { Pan = pan });
            // Godot's panner moves energy across rather than attenuating, so a panned mono sound is
            // slightly louder overall than a centred one; trim it back to equal power.
            AudioServer.SetBusVolumeDb(index, -10f * MathF.Log10(1f + pan * pan));
        }
    }

    /// <summary>Returns the index of bus <paramref name="name"/>, creating it (sending to <paramref name="send"/>) if missing.</summary>
    private static int EnsureBus(string name, string send, out bool created)
    {
        int index = AudioServer.GetBusIndex(name);
        created = index < 0;
        if (!created) return index;

        // New buses go last, so they always come after (and can send to) the buses created before them.
        AudioServer.AddBus();
        index = AudioServer.BusCount - 1;
        AudioServer.SetBusName(index, name);
        AudioServer.SetBusSend(index, send);
        return index;
    }
}
