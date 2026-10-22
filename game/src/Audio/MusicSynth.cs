using System;
using System.Runtime.CompilerServices;

namespace ContingencyZero.Game.Audio;

/// <summary>A rendered music track: interleaved 16-bit stereo PCM.</summary>
internal sealed class MusicRender
{
    public MusicRender(short[] interleaved, int sampleRate, int loopFrames)
    {
        Interleaved = interleaved;
        SampleRate = sampleRate;
        LoopFrames = loopFrames;
    }

    /// <summary>Left/right interleaved samples.</summary>
    public short[] Interleaved { get; }

    public int SampleRate { get; }

    /// <summary>
    /// Loop length in frames, or 0 for a one-shot track. A looping render carries a few extra frames
    /// past the loop end (a copy of the loop start) so the player's interpolator reads continuous
    /// audio across the wrap.
    /// </summary>
    public int LoopFrames { get; }

    public int Frames => Interleaved.Length / 2;
    public bool Loops => LoopFrames > 0;
    public float Seconds => (float)(LoopFrames > 0 ? LoopFrames : Frames) / SampleRate;
}

/// <summary>
/// Procedural 90s sci-fi synth score. Each track is sequenced note by note on a step grid, rendered
/// with <see cref="Instruments"/>, then run through a ping-pong delay and a reverb.
///
/// Looping tracks are seamless by construction: every note starts inside the loop, and whatever is
/// still ringing past the loop end (releases, echoes, reverb) is folded back onto the loop start,
/// which is exactly what would be sounding when playback wraps. Every per-note LFO restarts with its
/// note, so nothing drifts between repeats.
/// </summary>
internal static class MusicSynth
{
    /// <summary>Sample rate of every track (stereo).</summary>
    public const int SampleRate = 22050;

    private const float TargetRms = 0.13f;     // ≈ -17.7 dBFS
    private const float PeakCeiling = 0.95f;
    private const int LoopGuardFrames = 32;

    // ---- Menu: slow, moody pads and an echoing arpeggio ------------------------------------------

    /// <summary>
    /// Menu theme: 75 BPM, 16 bars (51.2 s) in D minor. Detuned string pads walk Dm9 – Bbmaj7 – Gm9 –
    /// A7sus4 – Dm9 – Fmaj7 – Cadd9 – A7 over a sine sub, with FM bells and a ping-ponging pulse
    /// arpeggio (entering on bar 3, so every repeat opens with a two-bar breath).
    /// </summary>
    public static MusicRender RenderMenu()
    {
        const int step = 4410;                 // one 16th note at 75 BPM
        const int bar = 16 * step;
        const int bars = 16;
        const int loop = bars * bar;
        var m = new MixBuffer(loop + 10 * SampleRate, SampleRate, 75);

        int[][] chords =
        {
            new[] { 50, 53, 57, 64 },   // Dm9     D3 F3 A3 E4
            new[] { 46, 53, 57, 62 },   // Bbmaj7  Bb2 F3 A3 D4
            new[] { 55, 58, 62, 69 },   // Gm9     G3 Bb3 D4 A4
            new[] { 57, 62, 64, 67 },   // A7sus4  A3 D4 E4 G4
            new[] { 50, 53, 57, 64 },   // Dm9
            new[] { 53, 57, 60, 64 },   // Fmaj7   F3 A3 C4 E4
            new[] { 48, 55, 62, 64 },   // Cadd9   C3 G3 D4 E4
            new[] { 57, 61, 64, 67 },   // A7      A3 C#4 E4 G4
        };
        int[] roots = { 38, 34, 43, 45, 38, 41, 36, 45 };
        int[] arpUp = { 0, 1, 2, 3, 4, 3, 2, 1 };
        int[] arpWeave = { 0, 2, 1, 3, 2, 4, 3, 1 };

        for (int c = 0; c < chords.Length; c++)
        {
            int s = c * 2 * bar;
            int[] chord = chords[c];
            Instruments.Pad(m, s, 2 * bar, chord, 0.9f, attack: 1.4f, release: 0.9f, brightness: 0.35f, reverb: 0.6f);
            Instruments.Sub(m, s, 2 * bar - step, roots[c], 0.5f, attack: 0.3f, release: 0.5f);
            Instruments.Bell(m, s, chord[^1] + 12, 0.45f, pan: -0.3f, decay: 0.9f, reverb: 0.7f, delay: 0.3f);
            Instruments.Bell(m, s + bar + 6 * step, chord[2] + 12, 0.3f, pan: 0.35f, decay: 0.9f, reverb: 0.7f, delay: 0.3f);

            int[] tones = ArpTones(chord);
            if (c >= 1)
            {
                for (int b = 0; b < 2; b++)
                {
                    int[] shape = c >= 4 && b == 1 ? arpWeave : arpUp;
                    for (int e = 0; e < 8; e++)
                    {
                        float vel = (e % 4 == 0 ? 0.75f : 0.5f) * (c == 1 && b == 0 ? 0.6f : 1f);
                        Instruments.Pluck(m, s + b * bar + e * 2 * step, 2 * step, tones[shape[e]], vel,
                            pan: e % 2 == 0 ? -0.35f : 0.35f, duty: 0.25f, brightness: 0.6f, decay: 0.25f,
                            reverb: 0.35f, delay: 0.35f);
                    }
                }
            }
            if (c >= 4)
            {
                // Second half: a quiet three-against-four sparkle an octave up.
                for (int st = 0, k = 0; st < 32; st += 3, k++)
                {
                    Instruments.Pluck(m, s + st * step, step, tones[k % tones.Length] + 12, 0.22f,
                        pan: k % 2 == 0 ? 0.6f : -0.6f, duty: 0.5f, brightness: 0.4f, decay: 0.12f,
                        reverb: 0.5f, delay: 0.4f);
                }
            }
        }

        Instruments.ApplyDelay(m, 3 * step, 0.42f, 0.55f);     // dotted eighth
        Instruments.ApplyReverb(m, 0.86f, 0.45f, 1f);
        return FinishLoop(m, loop);
    }

    // ---- Battle: driving bass, dark pads, light percussion ---------------------------------------

    /// <summary>
    /// Battle theme: 126 BPM, 24 bars (45.7 s) in E minor, in three 8-bar sections that build:
    /// A (bass pulse, kick and offbeat hats; Em–C–D–B), B (adds snare, 16th hats and an echoing
    /// arpeggio; Am–Em–F–B with a Phrygian bII), C (four-on-the-floor and a lead line, ending on the
    /// dominant B with a tom fill that drops back into A).
    /// </summary>
    public static MusicRender RenderBattle()
    {
        const int step = 2625;                 // one 16th note at 126 BPM
        const int bar = 16 * step;
        const int bars = 24;
        const int loop = bars * bar;
        var m = new MixBuffer(loop + 8 * SampleRate, SampleRate, 126);

        (int Root, bool Minor)[] prog =
        {
            (40, true), (40, true), (36, false), (38, false), (40, true), (40, true), (36, false), (35, false),
            (45, true), (45, true), (40, true), (40, true), (41, false), (41, false), (35, false), (35, false),
            (40, true), (40, true), (36, false), (36, false), (45, true), (35, false), (40, true), (35, false),
        };
        int[] arpPattern = { 0, 1, 2, 3, 2, 1, 0, 1, 2, 3, 2, 1, 0, 2, 1, 3 };

        for (int b = 0; b < bars; b++)
        {
            int s = b * bar;
            var (root, minor) = prog[b];
            int section = b / 8;

            // Bass: galloping 16ths (eighths for the first four bars), octave jump on the "e".
            for (int st = 0; st < 16; st++)
            {
                if (section == 0 && b < 4 && st % 2 == 1) continue;
                int offset = st % 4 == 2 ? 12 : 0;
                if (st == 14 && b % 2 == 1) offset = 7;
                float vel = st % 4 == 0 ? 1f : (st % 4 == 2 ? 0.8f : 0.6f);
                float accent = st % 8 == 0 ? 1f : (st % 4 == 2 ? 0.5f : 0.15f);
                Instruments.PulseBass(m, s + st * step, (int)(step * 0.8f), root + offset, vel, accent);
            }

            // Drums.
            Instruments.Kick(m, s, 0.9f);
            Instruments.Kick(m, s + 8 * step, 0.85f);
            if (section == 1 && b % 2 == 1) Instruments.Kick(m, s + 10 * step, 0.6f);
            if (section == 2)
            {
                Instruments.Kick(m, s + 4 * step, 0.8f);
                Instruments.Kick(m, s + 12 * step, 0.8f);
            }
            if (section >= 1)
            {
                Instruments.Snare(m, s + 4 * step, 0.8f);
                if (b != bars - 1) Instruments.Snare(m, s + 12 * step, 0.8f);
            }
            if (b == 7)
            {
                for (int st = 12; st < 16; st++) Instruments.Snare(m, s + st * step, 0.35f + 0.12f * (st - 12));
            }
            for (int st = 0; st < 16; st++)
            {
                bool offbeat = st % 4 == 2;
                if (section == 0 && !offbeat) continue;
                bool open = section >= 1 && st == 14 && b % 2 == 1;
                float vel = offbeat ? 0.45f : 0.24f;
                Instruments.Hat(m, s + st * step, vel, open, pan: st % 2 == 0 ? 0.25f : -0.2f);
            }
            if (b == bars - 1)
            {
                int[] toms = { 52, 50, 47, 45, 43, 40 };
                for (int k = 0; k < toms.Length; k++)
                    Instruments.Tom(m, s + (10 + k) * step, toms[k], 0.6f + 0.06f * k, pan: 0.5f - 0.2f * k);
            }

            // Arpeggio from section B on.
            if (section >= 1)
            {
                int third = minor ? 3 : 4;
                int[] tones = { root + 24, root + 24 + third, root + 31, root + 36 };
                for (int st = 0; st < 16; st++)
                {
                    Instruments.Pluck(m, s + st * step, step, tones[arpPattern[st]], st % 4 == 0 ? 0.5f : 0.32f,
                        pan: st % 2 == 0 ? -0.4f : 0.4f, duty: 0.25f, brightness: 0.8f, decay: 0.1f,
                        reverb: 0.25f, delay: 0.3f);
                }
            }
        }

        // Pads: one held chord per run of identical bars, voiced close around E3–D#4.
        for (int b = 0; b < bars;)
        {
            int run = 1;
            while (b + run < bars && prog[b + run] == prog[b]) run++;
            var (root, minor) = prog[b];
            float vel = b < 8 ? 0.65f : 0.8f;
            Instruments.Pad(m, b * bar, run * bar - step, Voicing(root, minor, 52), vel, attack: 0.25f, release: 0.5f,
                brightness: 0.3f, reverb: 0.5f);
            b += run;
        }

        // Lead line over section C.
        (int Bar, int Step, int Steps, int Midi)[] lead =
        {
            (16, 0, 8, 71), (16, 8, 4, 69), (16, 12, 4, 67),
            (17, 0, 12, 64), (17, 12, 4, 66),
            (18, 0, 8, 67), (18, 8, 4, 64), (18, 12, 4, 67),
            (19, 0, 12, 72), (19, 12, 4, 71),
            (20, 0, 8, 69), (20, 8, 4, 72), (20, 12, 4, 76),
            (21, 0, 8, 75), (21, 8, 4, 71), (21, 12, 4, 66),
            (22, 0, 8, 67), (22, 8, 8, 71),
            (23, 0, 8, 66), (23, 8, 8, 63),
        };
        foreach (var n in lead)
        {
            Instruments.Lead(m, n.Bar * bar + n.Step * step, (int)(n.Steps * step * 0.92f), n.Midi, 0.8f,
                pan: 0.1f, reverb: 0.35f, delay: 0.35f);
        }

        Instruments.ApplyDelay(m, 3 * step, 0.35f, 0.45f);
        Instruments.ApplyReverb(m, 0.75f, 0.5f, 0.9f);
        return FinishLoop(m, loop);
    }

    // ---- Result tracks ----------------------------------------------------------------------------

    /// <summary>
    /// Victory outro (~22 s, one-shot). Shadows the Victory stinger's C–Ab–Bb–C cadence with soft pads
    /// (so the two layer cleanly when played together), then holds a bright C major with a
    /// Fadd9 lift and drifting pentatonic bells before fading out.
    /// </summary>
    public static MusicRender RenderVictory()
    {
        var m = new MixBuffer(26 * SampleRate, SampleRate, 1998);
        (float At, float Len, float Attack, int[] Notes)[] chords =
        {
            (0f, 0.9f, 0.1f, new[] { 55, 60, 64 }),
            (0.9f, 0.45f, 0.08f, new[] { 56, 60, 63 }),
            (1.35f, 0.45f, 0.08f, new[] { 58, 62, 65 }),
            (1.8f, 8.2f, 0.15f, new[] { 55, 60, 64, 67, 72 }),
            (10f, 4f, 1.2f, new[] { 53, 57, 60, 67 }),
            (14f, 5f, 1.2f, new[] { 52, 55, 60, 64, 67 }),
        };
        foreach (var c in chords)
        {
            Instruments.Pad(m, m.At(c.At), m.At(c.Len), c.Notes, 0.85f, attack: c.Attack, release: 1.2f,
                brightness: 0.45f, reverb: 0.6f);
        }
        Instruments.Sub(m, m.At(1.8), m.At(17.2), 36, 0.45f, attack: 0.3f, release: 1.2f);

        int[] pentatonic = { 72, 74, 76, 79, 81, 84 };
        for (float t = 3f; t < 18f; t += 1.2f)
        {
            Instruments.Bell(m, m.At(t + m.Rng.Range(0f, 0.3f)), pentatonic[m.Rng.Int(pentatonic.Length)],
                m.Rng.Range(0.25f, 0.45f), pan: m.Rng.Range(-0.6f, 0.6f), decay: 1f, reverb: 0.7f, delay: 0.4f);
        }

        Instruments.ApplyDelay(m, m.At(0.45), 0.4f, 0.5f);
        Instruments.ApplyReverb(m, 0.85f, 0.4f, 1f);
        return FinishOneShot(m);
    }

    /// <summary>
    /// Defeat outro (~20 s, one-shot): a dark, low C minor pad moving Cm – Ab/C – Fm/C – Cm over a
    /// sub pedal, with sparse, metallic low bells, fading to silence.
    /// </summary>
    public static MusicRender RenderDefeat()
    {
        var m = new MixBuffer(24 * SampleRate, SampleRate, 1066);
        (float At, float Len, float Attack, int[] Notes)[] chords =
        {
            (0f, 6f, 0.4f, new[] { 48, 55, 60, 63 }),
            (6f, 4f, 1.2f, new[] { 48, 56, 60, 63 }),
            (10f, 4f, 1.2f, new[] { 48, 53, 56, 60 }),
            (14f, 4.5f, 1.2f, new[] { 48, 51, 55, 60 }),
        };
        foreach (var c in chords)
        {
            Instruments.Pad(m, m.At(c.At), m.At(c.Len), c.Notes, 0.9f, attack: c.Attack, release: 1.5f,
                brightness: 0.28f, reverb: 0.7f);
        }
        Instruments.Sub(m, 0, m.At(18.5), 36, 0.45f, attack: 0.5f, release: 1.5f);

        int[] bells = { 60, 63, 67, 58, 60, 56 };
        for (int k = 0; k < bells.Length; k++)
            Instruments.Bell(m, m.At(2.5 + 2.6 * k), bells[k], 0.3f, pan: k % 2 == 0 ? -0.4f : 0.4f,
                ratio: 1.4f, decay: 1.4f, reverb: 0.8f, delay: 0.3f);

        Instruments.ApplyDelay(m, m.At(0.6), 0.35f, 0.4f);
        Instruments.ApplyReverb(m, 0.9f, 0.55f, 1f);
        return FinishOneShot(m);
    }

    // ---- Helpers ----------------------------------------------------------------------------------

    /// <summary>Five ascending arpeggio tones from a chord: its notes an octave up plus the root two octaves up.</summary>
    private static int[] ArpTones(int[] chord)
    {
        var tones = new int[chord.Length + 1];
        for (int i = 0; i < chord.Length; i++) tones[i] = chord[i] + 12;
        tones[^1] = chord[0] + 24;
        Array.Sort(tones);
        return tones;
    }

    /// <summary>Close triad voicing: each chord tone placed in the octave [low, low + 12).</summary>
    private static int[] Voicing(int root, bool minor, int low)
    {
        int[] intervals = { 0, minor ? 3 : 4, 7 };
        var notes = new int[3];
        for (int i = 0; i < 3; i++)
        {
            int pc = (root + intervals[i]) % 12;
            int n = low + ((pc - low % 12) % 12 + 12) % 12;
            notes[i] = n;
        }
        Array.Sort(notes);
        return notes;
    }

    /// <summary>Gain that brings the mix to the target RMS without exceeding the peak ceiling.</summary>
    private static float LevelGain(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        float rms = MathF.Sqrt(0.5f * (Square(Dsp.Rms(left)) + Square(Dsp.Rms(right))));
        float peak = MathF.Max(Dsp.Peak(left), Dsp.Peak(right));
        if (peak < 1e-9f) return 1f;
        return MathF.Min(TargetRms / MathF.Max(rms, 1e-9f), PeakCeiling / peak);
    }

    private static float Square(float x) => x * x;

    /// <summary>
    /// Folds the tail past <paramref name="loop"/> back onto the start (making the loop seamless),
    /// levels the result and packs it with a short guard copy of the loop start.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static MusicRender FinishLoop(MixBuffer m, int loop)
    {
        for (int i = loop; i < m.Length; i++)
        {
            int j = i % loop;
            m.Left[j] += m.Left[i];
            m.Right[j] += m.Right[i];
        }
        float gain = LevelGain(m.Left.AsSpan(0, loop), m.Right.AsSpan(0, loop));
        int frames = loop + LoopGuardFrames;
        var pcm = new short[frames * 2];
        for (int i = 0; i < frames; i++)
        {
            int j = i % loop;
            pcm[2 * i] = ToShort(m.Left[j] * gain);
            pcm[2 * i + 1] = ToShort(m.Right[j] * gain);
        }
        return new MusicRender(pcm, SampleRate, loop);
    }

    /// <summary>Trims the inaudible tail, fades the last moment, levels and packs a one-shot track.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static MusicRender FinishOneShot(MixBuffer m)
    {
        int length = Math.Max(Dsp.AudibleLength(m.Left, 0.001f), Dsp.AudibleLength(m.Right, 0.001f));
        length = Math.Max(length, 1);
        var left = m.Left.AsSpan(0, length);
        var right = m.Right.AsSpan(0, length);
        int fade = Math.Min(length / 4, SampleRate / 2);
        Dsp.FadeOut(left, fade);
        Dsp.FadeOut(right, fade);
        float gain = LevelGain(left, right);
        Dsp.Scale(left, gain);
        Dsp.Scale(right, gain);
        return new MusicRender(Dsp.ToPcm16Stereo(left, right), SampleRate, 0);
    }

    private static short ToShort(float v) => (short)MathF.Round(Math.Clamp(v, -1f, 1f) * 32767f);
}
