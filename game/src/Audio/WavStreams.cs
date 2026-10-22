using Godot;

namespace ContingencyZero.Game.Audio;

/// <summary>Wraps synthesised 16-bit PCM in Godot <see cref="AudioStreamWav"/> resources.</summary>
internal static class WavStreams
{
    /// <summary>
    /// Creates a 16-bit stream from little-endian PCM bytes. A positive <paramref name="loopFrames"/>
    /// loops forward over [0, loopFrames); any frames after that are only read by the interpolator.
    /// </summary>
    public static AudioStreamWav Create(byte[] pcm16, int mixRate, bool stereo, int loopFrames = 0)
    {
        var stream = new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = mixRate,
            Stereo = stereo,
        };
        if (loopFrames > 0)
        {
            stream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            stream.LoopBegin = 0;
            stream.LoopEnd = loopFrames;
        }
        stream.Data = pcm16;
        return stream;
    }
}
