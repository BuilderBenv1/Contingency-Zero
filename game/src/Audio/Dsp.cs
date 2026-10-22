using System;
using System.Runtime.CompilerServices;

namespace ContingencyZero.Game.Audio;

// Godot-free DSP building blocks used by the sound and music synthesisers. Everything here works on
// plain float buffers in the range [-1, 1] so it can be unit-tested and rendered to .wav outside the engine.

/// <summary>
/// Tiny xorshift PRNG. Synthesis is seeded so every run produces the same sounds, which keeps the
/// generated assets reproducible and testable. (The sim's determinism rules don't apply to audio;
/// this is just for repeatable output.)
/// </summary>
internal sealed class SynthRandom
{
    private uint _state;

    public SynthRandom(uint seed)
    {
        _state = (seed * 2654435761u) ^ 0x9E3779B9u;
        if (_state == 0) _state = 0x6D2B79F5u;
        Next();
        Next();
    }

    /// <summary>Next raw 32-bit value.</summary>
    public uint Next()
    {
        uint x = _state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _state = x;
        return x;
    }

    /// <summary>Uniform value in [0, 1).</summary>
    public float Unit() => (Next() >> 8) * (1f / 16777216f);

    /// <summary>Uniform value in [-1, 1): white noise.</summary>
    public float Bipolar() => Unit() * 2f - 1f;

    /// <summary>Uniform value in [min, max).</summary>
    public float Range(float min, float max) => min + (max - min) * Unit();

    /// <summary>Uniform integer in [0, maxExclusive).</summary>
    public int Int(int maxExclusive) => (int)(Next() % (uint)maxExclusive);
}

/// <summary>Stateless DSP helpers: oscillators, shaping and buffer utilities.</summary>
internal static class Dsp
{
    public const float Tau = MathF.PI * 2f;

    /// <summary>
    /// Tiny offset added inside recursive filters so decaying states never reach subnormal floats,
    /// which are dozens of times slower on x86. Far below 16-bit resolution.
    /// </summary>
    public const float AntiDenormal = 1e-18f;

    private const int SineTableSize = 4096;
    private static readonly float[] SineTable = CreateSineTable();

    private static float[] CreateSineTable()
    {
        var table = new float[SineTableSize + 1];
        for (int i = 0; i <= SineTableSize; i++) table[i] = MathF.Sin(Tau * i / SineTableSize);
        return table;
    }

    /// <summary>
    /// Fast sine of a phase measured in cycles (1.0 = one full turn), via a linearly interpolated
    /// table (error below 1e-6). Several times cheaper than <see cref="MathF.Sin"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sin01(float cycles)
    {
        float x = (cycles - MathF.Floor(cycles)) * SineTableSize;
        int i = (int)x;
        if (i >= SineTableSize) i = SineTableSize - 1;
        float a = SineTable[i];
        return a + (SineTable[i + 1] - a) * (x - i);
    }

    /// <summary>MIDI note number to frequency in Hz (A4 = 69 = 440 Hz).</summary>
    public static float MidiToHz(float midi) => 440f * MathF.Pow(2f, (midi - 69f) / 12f);

    /// <summary>Decibels to linear gain.</summary>
    public static float DbToGain(float db) => MathF.Pow(10f, db * 0.05f);

    /// <summary>Per-sample multiplier that makes a level fall by a factor of e every <paramref name="tau"/> seconds.</summary>
    public static float DecayCoef(float tau, float sampleRate) =>
        tau <= 0f ? 0f : MathF.Exp(-1f / (tau * sampleRate));

    /// <summary>Cheap tanh-like soft clipper; exactly ±1 beyond ±3.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SoftClip(float x)
    {
        if (x >= 3f) return 1f;
        if (x <= -3f) return -1f;
        float x2 = x * x;
        return x * (27f + x2) / (27f + 9f * x2);
    }

    /// <summary>Hermite smoothstep on [0, 1].</summary>
    public static float Smoothstep(float x)
    {
        x = Math.Clamp(x, 0f, 1f);
        return x * x * (3f - 2f * x);
    }

    /// <summary>Polynomial band-limited step correction for discontinuous oscillators.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float PolyBlep(float t, float dt)
    {
        if (t < dt)
        {
            t /= dt;
            return t + t - t * t - 1f;
        }
        if (t > 1f - dt)
        {
            t = (t - 1f) / dt;
            return t * t + t + t + 1f;
        }
        return 0f;
    }

    /// <summary>Band-limited sawtooth. <paramref name="phase"/> in [0, 1), <paramref name="dt"/> = frequency / sample rate.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Saw(float phase, float dt) => 2f * phase - 1f - PolyBlep(phase, dt);

    /// <summary>Band-limited pulse wave with the given duty cycle, DC-corrected.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Pulse(float phase, float dt, float duty)
    {
        float v = phase < duty ? 1f : -1f;
        v += PolyBlep(phase, dt);
        float t2 = phase - duty;
        if (t2 < 0f) t2 += 1f;
        v -= PolyBlep(t2, dt);
        return v - (2f * duty - 1f);
    }

    /// <summary>Triangle wave (naive; its harmonics fall fast enough that aliasing is inaudible here).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Triangle(float phase) => 1f - 4f * MathF.Abs(phase - 0.5f);

    /// <summary>Advances a [0, 1) oscillator phase.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Advance(float phase, float dt)
    {
        phase += dt;
        if (phase >= 1f) phase -= MathF.Floor(phase);
        return phase;
    }

    /// <summary>Largest absolute sample value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static float Peak(ReadOnlySpan<float> buffer)
    {
        float peak = 0f;
        foreach (float s in buffer)
        {
            float a = MathF.Abs(s);
            if (a > peak) peak = a;
        }
        return peak;
    }

    /// <summary>Root-mean-square level.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static float Rms(ReadOnlySpan<float> buffer)
    {
        if (buffer.Length == 0) return 0f;
        double sum = 0;
        foreach (float s in buffer) sum += s * s;
        return (float)Math.Sqrt(sum / buffer.Length);
    }

    /// <summary>Scales the buffer so its peak equals <paramref name="targetPeak"/> (no-op on silence).</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void NormalizePeak(Span<float> buffer, float targetPeak)
    {
        float peak = Peak(buffer);
        if (peak < 1e-9f) return;
        Scale(buffer, targetPeak / peak);
    }

    /// <summary>Multiplies every sample by <paramref name="gain"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Scale(Span<float> buffer, float gain)
    {
        for (int i = 0; i < buffer.Length; i++) buffer[i] *= gain;
    }

    /// <summary>Removes DC and sub-sonic drift with a one-pole high-pass.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void DcBlock(Span<float> buffer, float cutoffHz, float sampleRate)
    {
        var hp = new OnePole();
        hp.SetCutoff(cutoffHz, sampleRate);
        for (int i = 0; i < buffer.Length; i++) buffer[i] = hp.Highpass(buffer[i]);
    }

    /// <summary>Linear fade-in over the first <paramref name="samples"/> samples.</summary>
    public static void FadeIn(Span<float> buffer, int samples)
    {
        samples = Math.Min(samples, buffer.Length);
        for (int i = 0; i < samples; i++) buffer[i] *= (float)i / samples;
    }

    /// <summary>Linear fade-out over the last <paramref name="samples"/> samples.</summary>
    public static void FadeOut(Span<float> buffer, int samples)
    {
        samples = Math.Min(samples, buffer.Length);
        int start = buffer.Length - samples;
        for (int i = 0; i < samples; i++) buffer[start + i] *= 1f - (float)(i + 1) / samples;
    }

    /// <summary>
    /// Index just past the last sample louder than <paramref name="relativeThreshold"/> × peak,
    /// i.e. the length the buffer can be trimmed to without losing audible tail.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static int AudibleLength(ReadOnlySpan<float> buffer, float relativeThreshold)
    {
        float limit = Peak(buffer) * relativeThreshold;
        for (int i = buffer.Length - 1; i >= 0; i--)
            if (MathF.Abs(buffer[i]) > limit) return i + 1;
        return 0;
    }

    /// <summary>Converts float samples to clamped signed 16-bit PCM.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static short[] ToPcm16(ReadOnlySpan<float> buffer)
    {
        var pcm = new short[buffer.Length];
        for (int i = 0; i < buffer.Length; i++)
        {
            float v = Math.Clamp(buffer[i], -1f, 1f) * 32767f;
            pcm[i] = (short)MathF.Round(v);
        }
        return pcm;
    }

    /// <summary>Interleaves two float channels into clamped signed 16-bit stereo PCM.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static short[] ToPcm16Stereo(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        var pcm = new short[left.Length * 2];
        for (int i = 0; i < left.Length; i++)
        {
            pcm[2 * i] = (short)MathF.Round(Math.Clamp(left[i], -1f, 1f) * 32767f);
            pcm[2 * i + 1] = (short)MathF.Round(Math.Clamp(right[i], -1f, 1f) * 32767f);
        }
        return pcm;
    }

    /// <summary>Serialises 16-bit PCM as little-endian bytes (the layout AudioStreamWav and .wav files expect).</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static byte[] ToLittleEndianBytes(ReadOnlySpan<short> pcm)
    {
        var bytes = new byte[pcm.Length * 2];
        for (int i = 0; i < pcm.Length; i++)
        {
            ushort v = (ushort)pcm[i];
            bytes[2 * i] = (byte)v;
            bytes[2 * i + 1] = (byte)(v >> 8);
        }
        return bytes;
    }
}

/// <summary>One-pole low-pass / high-pass filter. Cheap and smooth; 6 dB/octave.</summary>
internal struct OnePole
{
    private float _a;
    private float _z;

    public void SetCutoff(float hz, float sampleRate) =>
        _a = 1f - MathF.Exp(-Dsp.Tau * Math.Clamp(hz, 1f, sampleRate * 0.49f) / sampleRate);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Lowpass(float x)
    {
        _z += _a * (x + Dsp.AntiDenormal - _z);
        return _z;
    }

    /// <summary>High-pass output (input minus the low-pass). Don't mix with <see cref="Lowpass"/> on one instance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Highpass(float x) => x - Lowpass(x);
}

/// <summary>
/// Topology-preserving-transform state-variable filter (Zavalishin / Simper). Stable under fast cutoff
/// modulation, 12 dB/octave, with simultaneous low/band/high outputs.
/// </summary>
internal struct Svf
{
    private float _k, _a1, _a2, _a3;
    private float _ic1, _ic2;

    /// <summary>Low-pass output of the last <see cref="Process"/> call.</summary>
    public float Low;
    /// <summary>Band-pass output of the last call (peak gain ≈ Q).</summary>
    public float Band;
    /// <summary>High-pass output of the last call.</summary>
    public float High;

    /// <summary>Band-pass output normalised to unity gain at the centre frequency.</summary>
    public readonly float BandNorm => _k * Band;

    /// <summary>Sets cutoff (Hz) and resonance (Q; 0.707 = no peak).</summary>
    public void Set(float hz, float q, float sampleRate)
    {
        hz = Math.Clamp(hz, 10f, sampleRate * 0.45f);
        float g = MathF.Tan(MathF.PI * hz / sampleRate);
        _k = 1f / MathF.Max(q, 0.05f);
        _a1 = 1f / (1f + g * (g + _k));
        _a2 = g * _a1;
        _a3 = g * _a2;
    }

    /// <summary>Filters one sample; returns the low-pass output and updates all three outputs.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Process(float x)
    {
        float v3 = x + Dsp.AntiDenormal - _ic2;
        float v1 = _a1 * _ic1 + _a2 * v3;
        float v2 = _ic2 + _a2 * _ic1 + _a3 * v3;
        _ic1 = 2f * v1 - _ic1;
        _ic2 = 2f * v2 - _ic2;
        Low = v2;
        Band = v1;
        High = x - _k * v1 - v2;
        return v2;
    }
}

/// <summary>
/// Gate-driven envelope: linear attack, exponential decay to sustain while the gate is held, then an
/// exponential release. Advanced one sample per <see cref="Next"/>.
/// </summary>
internal struct Envelope
{
    private readonly int _attack;
    private readonly int _gate;
    private readonly float _sustain;
    private readonly float _decayCoef;
    private readonly float _releaseCoef;
    private int _i;
    private float _level;

    /// <param name="attack">Seconds to reach full level.</param>
    /// <param name="decay">Time constant (s) of the fall towards sustain.</param>
    /// <param name="sustain">Level held while the gate is open.</param>
    /// <param name="release">Time constant (s) of the fall after the gate closes.</param>
    /// <param name="gateSamples">How long the note is held.</param>
    public Envelope(float attack, float decay, float sustain, float release, int gateSamples, float sampleRate)
    {
        _attack = Math.Max(1, (int)(attack * sampleRate));
        _gate = gateSamples;
        _sustain = sustain;
        _decayCoef = Dsp.DecayCoef(decay, sampleRate);
        _releaseCoef = Dsp.DecayCoef(release, sampleRate);
        _i = 0;
        _level = 0f;
    }

    /// <summary>Samples a release with time constant <paramref name="release"/> takes to fall 60 dB.</summary>
    public static int ReleaseSamples(float release, float sampleRate) => (int)(release * 6.91f * sampleRate) + 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Next()
    {
        if (_i < _gate)
        {
            if (_i < _attack) _level = (float)(_i + 1) / _attack;
            else _level = _sustain + (_level - _sustain) * _decayCoef;
        }
        else
        {
            _level *= _releaseCoef;
        }
        _i++;
        return _level;
    }
}

/// <summary>
/// Freeverb-style Schroeder reverb (parallel damped combs into series all-passes): mono in, stereo out.
/// Delay lengths are scaled from the classic 44.1 kHz tunings to the working sample rate. Processes
/// in blocks, one filter at a time, which keeps each delay line hot in cache.
/// </summary>
internal sealed class Reverb
{
    private static readonly int[] CombTunings = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
    private static readonly int[] AllPassTunings = { 556, 441, 341, 225 };
    private const int StereoSpread = 23;
    private const float InputGain = 0.015f;
    private const int Block = 2048;

    private readonly float[][] _combL, _combR, _apL, _apR;
    private readonly int[] _combIdxL, _combIdxR, _apIdxL, _apIdxR;
    private readonly float[] _combStoreL, _combStoreR;
    private readonly float _feedback, _damp1, _damp2;
    private readonly int _combCount;
    private readonly float[] _blockIn = new float[Block];
    private readonly float[] _blockL = new float[Block];
    private readonly float[] _blockR = new float[Block];

    /// <param name="roomSize">0..1; larger means a longer tail.</param>
    /// <param name="damping">0..1; larger means a darker tail.</param>
    /// <param name="combCount">How many of the 8 combs to use (fewer = cheaper, grainier).</param>
    public Reverb(float sampleRate, float roomSize, float damping, int combCount = 8)
    {
        float scale = sampleRate / 44100f;
        _combCount = Math.Clamp(combCount, 1, CombTunings.Length);
        _combL = new float[_combCount][];
        _combR = new float[_combCount][];
        for (int i = 0; i < _combCount; i++)
        {
            _combL[i] = new float[Math.Max(1, (int)(CombTunings[i] * scale))];
            _combR[i] = new float[Math.Max(1, (int)((CombTunings[i] + StereoSpread) * scale))];
        }
        _apL = new float[AllPassTunings.Length][];
        _apR = new float[AllPassTunings.Length][];
        for (int i = 0; i < AllPassTunings.Length; i++)
        {
            _apL[i] = new float[Math.Max(1, (int)(AllPassTunings[i] * scale))];
            _apR[i] = new float[Math.Max(1, (int)((AllPassTunings[i] + StereoSpread) * scale))];
        }
        _combIdxL = new int[_combCount];
        _combIdxR = new int[_combCount];
        _apIdxL = new int[AllPassTunings.Length];
        _apIdxR = new int[AllPassTunings.Length];
        _combStoreL = new float[_combCount];
        _combStoreR = new float[_combCount];
        _feedback = 0.7f + 0.28f * Math.Clamp(roomSize, 0f, 1f);
        _damp1 = 0.4f * Math.Clamp(damping, 0f, 1f);
        _damp2 = 1f - _damp1;
    }

    /// <summary>
    /// Runs <paramref name="input"/> through the reverb and adds the wet output, scaled by
    /// <paramref name="wet"/>, onto <paramref name="left"/> and <paramref name="right"/>. The output
    /// spans may alias the input (and each other): each block's input is copied before it is written.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void ProcessAdd(ReadOnlySpan<float> input, Span<float> left, Span<float> right, float wet)
    {
        // Fewer combs means less summed energy; keep the wet level roughly constant.
        float gain = wet * 8f / _combCount;
        for (int offset = 0; offset < input.Length; offset += Block)
        {
            int n = Math.Min(Block, input.Length - offset);
            Span<float> x = _blockIn.AsSpan(0, n), l = _blockL.AsSpan(0, n), r = _blockR.AsSpan(0, n);
            ReadOnlySpan<float> source = input.Slice(offset, n);
            for (int i = 0; i < n; i++) x[i] = source[i] * InputGain + Dsp.AntiDenormal;
            l.Clear();
            r.Clear();
            for (int c = 0; c < _combCount; c++)
            {
                Comb(_combL[c], ref _combIdxL[c], ref _combStoreL[c], x, l);
                Comb(_combR[c], ref _combIdxR[c], ref _combStoreR[c], x, r);
            }
            for (int a = 0; a < _apL.Length; a++)
            {
                AllPass(_apL[a], ref _apIdxL[a], l);
                AllPass(_apR[a], ref _apIdxR[a], r);
            }
            Span<float> outL = left.Slice(offset, n), outR = right.Slice(offset, n);
            for (int i = 0; i < n; i++)
            {
                outL[i] += l[i] * gain;
                outR[i] += r[i] * gain;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void Comb(float[] buffer, ref int index, ref float store, ReadOnlySpan<float> input, Span<float> output)
    {
        int idx = index;
        float s = store, damp1 = _damp1, damp2 = _damp2, feedback = _feedback;
        int length = buffer.Length;
        for (int i = 0; i < input.Length; i++)
        {
            float o = buffer[idx];
            s = o * damp2 + s * damp1;
            buffer[idx] = input[i] + s * feedback;
            if (++idx >= length) idx = 0;
            output[i] += o;
        }
        index = idx;
        store = s;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void AllPass(float[] buffer, ref int index, Span<float> io)
    {
        int idx = index;
        int length = buffer.Length;
        for (int i = 0; i < io.Length; i++)
        {
            float buffered = buffer[idx];
            float x = io[i];
            buffer[idx] = x + buffered * 0.5f;
            if (++idx >= length) idx = 0;
            io[i] = buffered - x;
        }
        index = idx;
    }
}
