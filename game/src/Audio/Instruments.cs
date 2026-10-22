using System;
using System.Runtime.CompilerServices;

namespace ContingencyZero.Game.Audio;

/// <summary>
/// A stereo mix bus with reverb and delay sends. Instruments add notes into it; effects are applied
/// once at the end. Every stage is linear and time-invariant (per-note nonlinearity aside), which is
/// what lets <see cref="MusicSynth"/> fold a rendered tail back onto the loop start for a seamless loop.
/// </summary>
internal sealed class MixBuffer
{
    public readonly int SampleRate;
    public readonly float[] Left;
    public readonly float[] Right;
    /// <summary>Mono send into the reverb.</summary>
    public readonly float[] ReverbSend;
    /// <summary>Mono send into the ping-pong delay.</summary>
    public readonly float[] DelaySend;
    /// <summary>Seeded randomness for oscillator phases and noise.</summary>
    public readonly SynthRandom Rng;

    public MixBuffer(int length, int sampleRate, uint seed)
    {
        SampleRate = sampleRate;
        Left = new float[length];
        Right = new float[length];
        ReverbSend = new float[length];
        DelaySend = new float[length];
        Rng = new SynthRandom(seed);
    }

    public int Length => Left.Length;

    /// <summary>Sample index of a time in seconds.</summary>
    public int At(double seconds) => (int)Math.Round(seconds * SampleRate);

    /// <summary>Equal-power pan gains for pan in [-1 (left), 1 (right)].</summary>
    public static void PanGains(float pan, out float left, out float right)
    {
        float angle = (Math.Clamp(pan, -1f, 1f) + 1f) * MathF.PI * 0.25f;
        left = MathF.Cos(angle);
        right = MathF.Sin(angle);
    }

    /// <summary>Folds the left/right channels to mono.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public float[] Downmix()
    {
        var mono = new float[Length];
        for (int i = 0; i < mono.Length; i++) mono[i] = (Left[i] + Right[i]) * 0.5f;
        return mono;
    }
}

/// <summary>
/// The synth voices used for music and the Victory/Defeat stingers. Each call renders one note (or
/// chord) into a <see cref="MixBuffer"/> starting at a sample index; anything past the end is clipped.
/// </summary>
internal static class Instruments
{
    private const float Inv2Pi = 1f / Dsp.Tau;

    /// <summary>
    /// Warm 90s string-machine pad, paraphonic like the real thing: every chord note is three detuned
    /// saws (the outer two split left/right for width), all sharing one slowly breathing low-pass per
    /// side. <paramref name="bendSemitones"/> glides the whole chord over the gate.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Pad(MixBuffer m, int start, int gate, int[] notes, float velocity,
        float attack = 1.0f, float release = 0.8f, float brightness = 0.5f, float reverb = 0.5f,
        float bendSemitones = 0f)
    {
        int sr = m.SampleRate;
        int voices = notes.Length * 3;
        var freq = new float[notes.Length];
        var phase = new float[voices];
        var dt = new float[voices];
        float meanF = 0f;
        for (int n = 0; n < notes.Length; n++)
        {
            freq[n] = Dsp.MidiToHz(notes[n]);
            meanF += freq[n] / notes.Length;
        }
        for (int v = 0; v < voices; v++) phase[v] = m.Rng.Unit();

        var env = new Envelope(attack, 1.5f, 0.8f, release, gate, sr);
        int end = Math.Min(start + gate + Envelope.ReleaseSamples(release, sr), m.Length);
        float detune = MathF.Pow(2f, 7f / 1200f);
        float lfoRate = m.Rng.Range(0.15f, 0.3f), lfoPhase = m.Rng.Unit();
        float baseCut = Math.Clamp(250f + meanF * (1f + 5f * brightness), 200f, 7000f);
        float gain = 0.14f * velocity;
        var fl = new Svf();
        var fr = new Svf();

        for (int i = Math.Max(start, 0); i < end; i++)
        {
            int k = i - start;
            float e = env.Next();
            if ((k & 31) == 0)
            {
                float t = (float)k / sr;
                float bend = bendSemitones == 0f ? 1f : MathF.Pow(2f, bendSemitones * Math.Min(1f, (float)k / gate) / 12f);
                for (int n = 0; n < notes.Length; n++)
                {
                    float fb = freq[n] * bend / sr;
                    dt[3 * n] = fb / detune;
                    dt[3 * n + 1] = fb;
                    dt[3 * n + 2] = fb * detune;
                }
                float lfo = Dsp.Sin01(lfoPhase + lfoRate * t);
                float cut = baseCut * (0.45f + 0.55f * e) * (1f + 0.25f * lfo);
                fl.Set(cut, 0.9f, sr);
                fr.Set(cut, 0.9f, sr);
            }
            float sumL = 0f, sumR = 0f;
            for (int v = 0; v < voices; v += 3)
            {
                float s1 = Dsp.Saw(phase[v], dt[v]);
                float s2 = Dsp.Saw(phase[v + 1], dt[v + 1]);
                float s3 = Dsp.Saw(phase[v + 2], dt[v + 2]);
                phase[v] = Dsp.Advance(phase[v], dt[v]);
                phase[v + 1] = Dsp.Advance(phase[v + 1], dt[v + 1]);
                phase[v + 2] = Dsp.Advance(phase[v + 2], dt[v + 2]);
                sumL += s1 + 0.6f * s2;
                sumR += s3 + 0.6f * s2;
            }
            float l = fl.Process(sumL);
            float r = fr.Process(sumR);
            float a = e * gain;
            m.Left[i] += l * a;
            m.Right[i] += r * a;
            m.ReverbSend[i] += (l + r) * 0.5f * a * reverb;
        }
    }

    /// <summary>Plucky pulse-wave arpeggio note with a snappy filter envelope.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Pluck(MixBuffer m, int start, int gate, float midi, float velocity, float pan = 0f,
        float duty = 0.3f, float brightness = 1f, float decay = 0.2f, float reverb = 0.3f, float delay = 0f)
    {
        int sr = m.SampleRate;
        float f = Dsp.MidiToHz(midi);
        float dt = f / sr;
        const float release = 0.05f;
        int held = Math.Min(gate, (int)(decay * 7f * sr));
        var env = new Envelope(0.003f, decay, 0f, release, held, sr);
        int end = Math.Min(start + held + Envelope.ReleaseSamples(release, sr), m.Length);
        MixBuffer.PanGains(pan, out float gl, out float gr);
        float gain = 0.22f * velocity;
        float filterCoef = Dsp.DecayCoef(0.09f, sr);
        float fenv = 1f;
        float phase = m.Rng.Unit();
        var filter = new Svf();

        for (int i = Math.Max(start, 0); i < end; i++)
        {
            int k = i - start;
            if ((k & 15) == 0) filter.Set(f * 1.2f + 300f + 4000f * brightness * fenv, 1.1f, sr);
            fenv *= filterCoef;
            float s = filter.Process(Dsp.Pulse(phase, dt, duty));
            phase = Dsp.Advance(phase, dt);
            float v = s * env.Next() * gain;
            m.Left[i] += v * gl;
            m.Right[i] += v * gr;
            m.ReverbSend[i] += v * reverb;
            m.DelaySend[i] += v * delay;
        }
    }

    /// <summary>
    /// Two-operator FM bell. <paramref name="ratio"/> sets the modulator:carrier ratio
    /// (3.5 = glassy and inharmonic, 1.4 = dark and metallic).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Bell(MixBuffer m, int start, float midi, float velocity, float pan = 0f,
        float ratio = 3.5f, float decay = 0.9f, float reverb = 0.6f, float delay = 0f)
    {
        int sr = m.SampleRate;
        float f = Dsp.MidiToHz(midi);
        float dtc = f / sr, dtm = f * ratio / sr;
        int end = Math.Min(start + Envelope.ReleaseSamples(decay, sr), m.Length);
        MixBuffer.PanGains(pan, out float gl, out float gr);
        float ampCoef = Dsp.DecayCoef(decay, sr), idxCoef = Dsp.DecayCoef(0.18f, sr);
        float amp = 0.16f * velocity, index = 2.5f;
        float pc = 0f, pm = 0f;
        int attack = (int)(0.004f * sr);

        for (int i = Math.Max(start, 0); i < end; i++)
        {
            int k = i - start;
            float s = Dsp.Sin01(pc + (index + 0.3f) * Inv2Pi * Dsp.Sin01(pm));
            pc = Dsp.Advance(pc, dtc);
            pm = Dsp.Advance(pm, dtm);
            float v = s * (k < attack ? amp * k / attack : amp);
            amp *= ampCoef;
            index *= idxCoef;
            m.Left[i] += v * gl;
            m.Right[i] += v * gr;
            m.ReverbSend[i] += v * reverb;
            m.DelaySend[i] += v * delay;
        }
    }

    /// <summary>Round sine sub-bass with a touch of octave-up triangle for definition on small speakers.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Sub(MixBuffer m, int start, int gate, float midi, float velocity,
        float attack = 0.08f, float release = 0.3f)
    {
        int sr = m.SampleRate;
        float dt = Dsp.MidiToHz(midi) / sr;
        var env = new Envelope(attack, 1f, 1f, release, gate, sr);
        int end = Math.Min(start + gate + Envelope.ReleaseSamples(release, sr), m.Length);
        float gain = 0.3f * velocity * 0.707f;
        float phase = 0f;

        for (int i = Math.Max(start, 0); i < end; i++)
        {
            float octave = phase < 0.5f ? phase * 2f : phase * 2f - 1f;
            float s = Dsp.Sin01(phase) + 0.12f * Dsp.Triangle(octave);
            phase = Dsp.Advance(phase, dt);
            float v = s * env.Next() * gain;
            m.Left[i] += v;
            m.Right[i] += v;
        }
    }

    /// <summary>
    /// Driving resonant saw/square bass for the battle track. <paramref name="accent"/> (0..1) opens
    /// the filter envelope for the "squelch" on strong steps.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void PulseBass(MixBuffer m, int start, int gate, float midi, float velocity, float accent)
    {
        int sr = m.SampleRate;
        float f = Dsp.MidiToHz(midi);
        float dt1 = f / sr, dt2 = f * 1.004f / sr;
        const float release = 0.02f;
        var env = new Envelope(0.002f, 0.25f, 0.55f, release, gate, sr);
        int end = Math.Min(start + gate + Envelope.ReleaseSamples(release, sr), m.Length);
        float fenv = 1f, fcoef = Dsp.DecayCoef(0.07f, sr);
        float gain = 0.32f * velocity * 0.707f;
        float p1 = 0f, p2 = 0.37f;
        var filter = new Svf();

        for (int i = Math.Max(start, 0); i < end; i++)
        {
            int k = i - start;
            if ((k & 15) == 0) filter.Set(120f + f * 1.5f + (400f + 1800f * accent) * fenv, 2.6f, sr);
            fenv *= fcoef;
            float osc = Dsp.Saw(p1, dt1) + 0.7f * Dsp.Pulse(p2, dt2, 0.5f);
            p1 = Dsp.Advance(p1, dt1);
            p2 = Dsp.Advance(p2, dt2);
            float v = Dsp.SoftClip(filter.Process(osc) * 1.6f) * env.Next() * gain;
            m.Left[i] += v;
            m.Right[i] += v;
            m.ReverbSend[i] += v * 0.04f;
        }
    }

    /// <summary>Sine-sweep electronic kick with a short click.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Kick(MixBuffer m, int start, float velocity)
    {
        int sr = m.SampleRate;
        int end = Math.Min(start + (int)(0.45f * sr), m.Length);
        float gain = 0.55f * velocity * 0.707f;
        float phase = 0f, sweep = 1f, body = 1f, tick = 1f;
        float sweepCoef = Dsp.DecayCoef(0.035f, sr), bodyCoef = Dsp.DecayCoef(0.2f, sr), tickCoef = Dsp.DecayCoef(0.002f, sr);
        int attack = Math.Max(1, (int)(0.001f * sr));
        var click = new OnePole();
        click.SetCutoff(3000f, sr);

        for (int i = Math.Max(start, 0); i < end; i++)
        {
            int k = i - start;
            phase = Dsp.Advance(phase, (45f + 110f * sweep) / sr);
            float s = Dsp.Sin01(phase) * body * Math.Min(1f, (float)k / attack);
            float c = click.Highpass(m.Rng.Bipolar()) * tick * 0.4f;
            sweep *= sweepCoef;
            body *= bodyCoef;
            tick *= tickCoef;
            float v = Dsp.SoftClip((s + c) * 1.3f) * gain;
            m.Left[i] += v;
            m.Right[i] += v;
            m.ReverbSend[i] += v * 0.05f;
        }
    }

    /// <summary>90s drum-machine snare: tuned body plus high-passed noise.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Snare(MixBuffer m, int start, float velocity, float reverb = 0.25f)
    {
        int sr = m.SampleRate;
        int end = Math.Min(start + (int)(0.35f * sr), m.Length);
        float gain = 0.3f * velocity * 0.707f;
        float p1 = 0f, p2 = 0f, toneAmp = 1f, noiseAmp = 1f;
        float toneCoef = Dsp.DecayCoef(0.045f, sr), noiseCoef = Dsp.DecayCoef(0.09f, sr);
        int attack = Math.Max(1, (int)(0.0008f * sr));
        var hp = new OnePole();
        hp.SetCutoff(1400f, sr);
        var lp = new OnePole();
        lp.SetCutoff(8000f, sr);

        for (int i = Math.Max(start, 0); i < end; i++)
        {
            int k = i - start;
            float tone = (Dsp.Sin01(p1) + 0.5f * Dsp.Sin01(p2)) * toneAmp;
            p1 = Dsp.Advance(p1, 185f / sr);
            p2 = Dsp.Advance(p2, 330f / sr);
            float noise = lp.Lowpass(hp.Highpass(m.Rng.Bipolar())) * noiseAmp;
            toneAmp *= toneCoef;
            noiseAmp *= noiseCoef;
            float v = (0.55f * tone + 1.1f * noise) * gain * Math.Min(1f, (float)k / attack);
            m.Left[i] += v;
            m.Right[i] += v;
            m.ReverbSend[i] += v * reverb;
        }
    }

    /// <summary>Closed or open hi-hat from high-passed noise.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Hat(MixBuffer m, int start, float velocity, bool open, float pan = 0f)
    {
        int sr = m.SampleRate;
        float decay = open ? 0.12f : 0.028f;
        int end = Math.Min(start + (int)(decay * 7f * sr), m.Length);
        MixBuffer.PanGains(pan, out float gl, out float gr);
        float gain = 0.16f * velocity;
        var filter = new Svf();
        filter.Set(7500f, 0.9f, sr);
        float amp = 1f, coef = Dsp.DecayCoef(decay, sr);

        for (int i = Math.Max(start, 0); i < end; i++)
        {
            filter.Process(m.Rng.Bipolar());
            float v = filter.High * amp * gain;
            amp *= coef;
            m.Left[i] += v * gl;
            m.Right[i] += v * gr;
            m.ReverbSend[i] += v * 0.1f;
        }
    }

    /// <summary>Synth tom for fills: pitched sine drop plus a little noise.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Tom(MixBuffer m, int start, float midi, float velocity, float pan = 0f)
    {
        int sr = m.SampleRate;
        float f = Dsp.MidiToHz(midi);
        int end = Math.Min(start + (int)(0.5f * sr), m.Length);
        MixBuffer.PanGains(pan, out float gl, out float gr);
        float gain = 0.4f * velocity;
        float phase = 0f, glide = 1f, body = 1f, hit = 1f;
        float glideCoef = Dsp.DecayCoef(0.03f, sr), bodyCoef = Dsp.DecayCoef(0.16f, sr), hitCoef = Dsp.DecayCoef(0.02f, sr);
        var lp = new OnePole();
        lp.SetCutoff(2500f, sr);

        for (int i = Math.Max(start, 0); i < end; i++)
        {
            phase = Dsp.Advance(phase, f * (1f + 0.6f * glide) / sr);
            float v = (Dsp.Sin01(phase) * body + lp.Lowpass(m.Rng.Bipolar()) * hit * 0.5f) * gain;
            glide *= glideCoef;
            body *= bodyCoef;
            hit *= hitCoef;
            m.Left[i] += v * gl;
            m.Right[i] += v * gr;
            m.ReverbSend[i] += v * 0.2f;
        }
    }

    /// <summary>Orchestral-style timpani hit for the fanfare.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Timpani(MixBuffer m, int start, float midi, float velocity)
    {
        int sr = m.SampleRate;
        float f = Dsp.MidiToHz(midi);
        int end = Math.Min(start + (int)(1.4f * sr), m.Length);
        float gain = 0.5f * velocity * 0.707f;
        float p1 = 0f, p2 = 0f, glide = 1f, fundamental = 1f, overtone = 1f, mallet = 1f;
        float glideCoef = Dsp.DecayCoef(0.05f, sr), fundCoef = Dsp.DecayCoef(0.35f, sr);
        float overCoef = Dsp.DecayCoef(0.15f, sr), malletCoef = Dsp.DecayCoef(0.015f, sr);
        int attack = Math.Max(1, (int)(0.002f * sr));
        var lp = new OnePole();
        lp.SetCutoff(900f, sr);

        for (int i = Math.Max(start, 0); i < end; i++)
        {
            int k = i - start;
            float g = 1f + 0.08f * glide;
            p1 = Dsp.Advance(p1, f * g / sr);
            p2 = Dsp.Advance(p2, f * 1.51f * g / sr);
            float s = Dsp.Sin01(p1) * fundamental + 0.35f * Dsp.Sin01(p2) * overtone
                      + lp.Lowpass(m.Rng.Bipolar()) * mallet * 1.2f;
            glide *= glideCoef;
            fundamental *= fundCoef;
            overtone *= overCoef;
            mallet *= malletCoef;
            float v = s * gain * Math.Min(1f, (float)k / attack);
            m.Left[i] += v;
            m.Right[i] += v;
            m.ReverbSend[i] += v * 0.35f;
        }
    }

    /// <summary>
    /// Synth brass: two detuned saws with a slight pitch scoop and a filter that blooms on the attack.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Brass(MixBuffer m, int start, int gate, float midi, float velocity, float pan = 0f, float reverb = 0.3f)
    {
        int sr = m.SampleRate;
        float f = Dsp.MidiToHz(midi);
        const float release = 0.18f;
        var env = new Envelope(0.025f, 0.35f, 0.75f, release, gate, sr);
        var filterEnv = new Envelope(0.05f, 0.3f, 0.35f, release, gate, sr);
        int end = Math.Min(start + gate + Envelope.ReleaseSamples(release, sr), m.Length);
        MixBuffer.PanGains(pan, out float gl, out float gr);
        float gain = 0.16f * velocity;
        float p1 = m.Rng.Unit(), p2 = m.Rng.Unit();
        float detune = MathF.Pow(2f, 6f / 1200f);
        float scoop = 1f, scoopCoef = Dsp.DecayCoef(0.03f, sr);
        var filter = new Svf();
        float dt1 = 0f, dt2 = 0f;

        for (int i = Math.Max(start, 0); i < end; i++)
        {
            int k = i - start;
            float fe = filterEnv.Next();
            if ((k & 15) == 0)
            {
                float bend = 1f - 0.02f * scoop;
                dt1 = f * bend / detune / sr;
                dt2 = f * bend * detune / sr;
                filter.Set(f * 1.2f + (f * 5f + 1200f) * fe, 1.2f, sr);
            }
            scoop *= scoopCoef;
            float osc = Dsp.Saw(p1, dt1) + Dsp.Saw(p2, dt2);
            p1 = Dsp.Advance(p1, dt1);
            p2 = Dsp.Advance(p2, dt2);
            float v = filter.Process(osc) * env.Next() * gain;
            m.Left[i] += v * gl;
            m.Right[i] += v * gr;
            m.ReverbSend[i] += v * reverb;
        }
    }

    /// <summary>Singing saw/pulse lead with delayed vibrato.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Lead(MixBuffer m, int start, int gate, float midi, float velocity, float pan = 0f,
        float reverb = 0.35f, float delay = 0.3f)
    {
        int sr = m.SampleRate;
        float f = Dsp.MidiToHz(midi);
        const float release = 0.15f;
        var env = new Envelope(0.02f, 0.4f, 0.7f, release, gate, sr);
        int end = Math.Min(start + gate + Envelope.ReleaseSamples(release, sr), m.Length);
        MixBuffer.PanGains(pan, out float gl, out float gr);
        float gain = 0.13f * velocity;
        float p1 = 0f, p2 = 0.5f, dt = f / sr;
        var filter = new Svf();
        filter.Set(f * 3f + 900f, 1f, sr);

        for (int i = Math.Max(start, 0); i < end; i++)
        {
            int k = i - start;
            if ((k & 15) == 0)
            {
                float t = (float)k / sr;
                float depth = 0.007f * Dsp.Smoothstep((t - 0.2f) / 0.3f);
                dt = f * (1f + depth * Dsp.Sin01(5.5f * t)) / sr;
            }
            float osc = Dsp.Saw(p1, dt) + 0.5f * Dsp.Pulse(p2, dt, 0.4f);
            p1 = Dsp.Advance(p1, dt);
            p2 = Dsp.Advance(p2, dt);
            float v = filter.Process(osc) * env.Next() * gain;
            m.Left[i] += v * gl;
            m.Right[i] += v * gr;
            m.ReverbSend[i] += v * reverb;
            m.DelaySend[i] += v * delay;
        }
    }

    /// <summary>
    /// Runs the delay send through a damped ping-pong echo and mixes it back in (and a little into the
    /// reverb send, so echoes sit in the same space).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void ApplyDelay(MixBuffer m, int delaySamples, float feedback, float wet, float dampingHz = 3500f)
    {
        var bufL = new float[delaySamples];
        var bufR = new float[delaySamples];
        var dampL = new OnePole();
        var dampR = new OnePole();
        dampL.SetCutoff(dampingHz, m.SampleRate);
        dampR.SetCutoff(dampingHz, m.SampleRate);
        int idx = 0;

        for (int i = 0; i < m.Length; i++)
        {
            float dl = bufL[idx], dr = bufR[idx];
            bufL[idx] = m.DelaySend[i] + dampR.Lowpass(dr) * feedback;
            bufR[idx] = dampL.Lowpass(dl) * feedback;
            if (++idx >= delaySamples) idx = 0;
            m.Left[i] += dl * wet;
            m.Right[i] += dr * wet;
            m.ReverbSend[i] += (dl + dr) * 0.15f * wet;
        }
    }

    /// <summary>Runs the reverb send through a stereo Freeverb and mixes it back in.</summary>
    public static void ApplyReverb(MixBuffer m, float roomSize, float damping, float wet)
    {
        new Reverb(m.SampleRate, roomSize, damping).ProcessAdd(m.ReverbSend, m.Left, m.Right, wet);
    }
}
