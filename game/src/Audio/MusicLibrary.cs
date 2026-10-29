using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace ContingencyZero.Game.Audio;

/// <summary>
/// Hands out the music. Normally that is the recorded orchestral music in res://audio/music (CC0, built
/// by tools/music_build.py): menu and battle tracks as shuffled, crossfaded playlists, plus one-shot
/// victory and defeat outros. The original synthesised tracks (<see cref="MusicSynth"/>) are rendered
/// once per process on background threads as the fallback, and are used when the "original
/// synthesised audio" setting is on. Shared by every <see cref="MusicPlayer"/>.
/// </summary>
internal static class MusicLibrary
{
    private sealed record Rendered(byte[] Pcm, int SampleRate, int LoopFrames);

    private static readonly int TrackCount = Enum.GetValues<MusicPlayer.Track>().Length;
    private static readonly Rendered?[] Pending = new Rendered?[TrackCount];
    private static readonly AudioStreamWav?[] Streams = new AudioStreamWav?[TrackCount];
    private static readonly AudioStream?[] Recorded = new AudioStream?[TrackCount];

    /// <summary>Seconds of crossfade between playlist tracks.</summary>
    private const float PlaylistFadeSeconds = 3f;
    private static int _started;
    private static string? _error;

    /// <summary>Starts synthesis (idempotent). Menu and Battle render in parallel; the result tracks follow.</summary>
    public static void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0) return;
        Task.Run(() =>
        {
            Render(MusicPlayer.Track.Menu, MusicSynth.RenderMenu);
            Render(MusicPlayer.Track.Victory, MusicSynth.RenderVictory);
        });
        Task.Run(() =>
        {
            Render(MusicPlayer.Track.Battle, MusicSynth.RenderBattle);
            Render(MusicPlayer.Track.Defeat, MusicSynth.RenderDefeat);
        });
    }

    /// <summary>The stream for a track, or null while it is still being synthesised. Main thread only.</summary>
    public static AudioStream? TryGet(MusicPlayer.Track track)
    {
        int i = (int)track;
        if ((uint)i >= (uint)TrackCount || track == MusicPlayer.Track.None) return null;
        if (!App.Settings.Current.SynthSounds && (Recorded[i] ??= LoadRecorded(track)) is { } recorded) return recorded;
        if (Streams[i] is { } ready) return ready;

        Rendered? rendered = Volatile.Read(ref Pending[i]);
        if (rendered is null)
        {
            string? error = Interlocked.Exchange(ref _error, null);
            if (error is not null) GD.PushError(error);
            return null;
        }
        Streams[i] = WavStreams.Create(rendered.Pcm, rendered.SampleRate, stereo: true, rendered.LoopFrames);
        Pending[i] = null; // the stream holds its own copy
        return Streams[i];
    }

    /// <summary>The recorded music for a track, or null if its files are missing.</summary>
    private static AudioStream? LoadRecorded(MusicPlayer.Track track)
    {
        string prefix = track switch
        {
            MusicPlayer.Track.Menu => "menu",
            MusicPlayer.Track.Battle => "battle",
            MusicPlayer.Track.Victory => "victory",
            MusicPlayer.Track.Defeat => "defeat",
            _ => "",
        };
        if (prefix.Length == 0) return null;
        if (track is MusicPlayer.Track.Victory or MusicPlayer.Track.Defeat)
            return Load($"res://audio/music/{prefix}.ogg");

        var tracks = new System.Collections.Generic.List<AudioStream>();
        for (int k = 1; k <= 16; k++)
            if (Load($"res://audio/music/{prefix}_{k}.ogg") is { } s) tracks.Add(s);
        if (tracks.Count == 0) return null;
        var playlist = new AudioStreamPlaylist { Shuffle = true, Loop = true, FadeTime = PlaylistFadeSeconds, StreamCount = tracks.Count };
        for (int k = 0; k < tracks.Count; k++) playlist.SetListStream(k, tracks[k]);
        return playlist;
    }

    private static AudioStream? Load(string path) =>
        ResourceLoader.Exists(path) ? ResourceLoader.Load<AudioStream>(path) : null;

    private static void Render(MusicPlayer.Track track, Func<MusicRender> render)
    {
        try
        {
            MusicRender r = render();
            var rendered = new Rendered(Dsp.ToLittleEndianBytes(r.Interleaved), r.SampleRate, r.LoopFrames);
            Volatile.Write(ref Pending[(int)track], rendered);
        }
        catch (Exception e)
        {
            Interlocked.CompareExchange(ref _error, $"Music synthesis failed for {track}: {e}", null);
        }
    }
}
