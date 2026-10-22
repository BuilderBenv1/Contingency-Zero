using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace ContingencyZero.Game.Audio;

/// <summary>
/// Synthesises the music tracks once per process on background threads and hands out the finished
/// streams. Shared by every <see cref="MusicPlayer"/>, so scene changes never regenerate anything.
/// </summary>
internal static class MusicLibrary
{
    private sealed record Rendered(byte[] Pcm, int SampleRate, int LoopFrames);

    private static readonly int TrackCount = Enum.GetValues<MusicPlayer.Track>().Length;
    private static readonly Rendered?[] Pending = new Rendered?[TrackCount];
    private static readonly AudioStreamWav?[] Streams = new AudioStreamWav?[TrackCount];
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
    public static AudioStreamWav? TryGet(MusicPlayer.Track track)
    {
        int i = (int)track;
        if ((uint)i >= (uint)TrackCount || track == MusicPlayer.Track.None) return null;
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
