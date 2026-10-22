using System;
using System.Runtime.CompilerServices;

namespace ContingencyZero.Game.Audio;

/// <summary>Playback metadata for one <see cref="SfxKind"/>.</summary>
/// <param name="Variants">How many slightly different renders exist; playback picks one at random.</param>
/// <param name="LevelDb">Mix level applied at playback (every render is normalised to -1 dBFS peak).</param>
/// <param name="PitchJitter">Random pitch variation per play, as a fraction (0.05 = ±5%).</param>
/// <param name="Priority">Voice-stealing priority: a sound can only steal a voice of equal or lower priority.</param>
internal readonly record struct SfxTraits(int Variants, float LevelDb, float PitchJitter, int Priority);

/// <summary>
/// Synthesises every sound effect from scratch: noise, filtered oscillators, pitch and amplitude
/// envelopes, soft-clip distortion and a little reverb. Pure C# with no Godot dependency, so the
/// sounds can be rendered to .wav and inspected outside the engine.
/// </summary>
internal static class SfxSynth
{
    /// <summary>Sample rate of every rendered effect (mono).</summary>
    public const int SampleRate = 22050;

    private const float Sr = SampleRate;
    private const float Dt = 1f / SampleRate;
    private const float TargetPeak = 0.89f; // -1 dBFS

    /// <summary>
    /// Playback metadata (variants, mix level, pitch jitter, priority) for a sound. Mix levels were
    /// set by measuring each render's K-weighted 100 ms loudness and placing it at a common reference
    /// (-16 dB) plus a per-sound intent (UI quieter, big explosions louder), capped at -1 dB so a
    /// normalised render never peaks above -2 dBFS before the caller's own offset.
    /// </summary>
    public static SfxTraits Traits(SfxKind kind) => kind switch
    {
        SfxKind.Click => new(1, -7.5f, 0f, 3),
        SfxKind.Hover => new(1, -20f, 0f, 3),

        SfxKind.Laser => new(3, -9f, 0.05f, 0),
        SfxKind.Cannon => new(3, -11.5f, 0.05f, 0),
        SfxKind.Artillery => new(3, -8.5f, 0.05f, 1),
        SfxKind.Missile => new(3, -7.5f, 0.05f, 0),
        SfxKind.Flak => new(3, -10f, 0.05f, 0),
        SfxKind.Bomb => new(3, -9.5f, 0.05f, 1),
        SfxKind.DGun => new(3, -7f, 0.03f, 2),

        SfxKind.ImpactSmall => new(3, -12f, 0.08f, 0),
        SfxKind.ExplosionSmall => new(3, -13.5f, 0.07f, 1),
        SfxKind.ExplosionLarge => new(3, -9f, 0.06f, 2),
        SfxKind.ExplosionHuge => new(2, -1.5f, 0.04f, 3),

        SfxKind.BuildStart => new(1, -10f, 0.03f, 1),
        SfxKind.BuildComplete => new(1, -11f, 0f, 2),
        SfxKind.UnitReady => new(1, -12.5f, 0f, 2),
        SfxKind.Nano => new(3, -21f, 0.1f, 0),
        SfxKind.Reclaim => new(2, -11.5f, 0.06f, 0),

        SfxKind.Select => new(1, -7.5f, 0f, 3),
        SfxKind.Acknowledge => new(1, -15f, 0f, 3),
        SfxKind.Error => new(1, -9.5f, 0f, 3),
        SfxKind.Alert => new(1, -8.5f, 0f, 3),
        SfxKind.MissionMessage => new(1, -13f, 0f, 3),

        SfxKind.Victory => new(1, -2f, 0f, 3),
        SfxKind.Defeat => new(1, -3f, 0f, 3),
        _ => new(1, -12f, 0f, 0),
    };

    /// <summary>
    /// Renders one variant of a sound as mono float samples at <see cref="SampleRate"/>, trimmed,
    /// de-clicked and normalised to -1 dBFS. Deterministic: the same arguments give the same samples.
    /// </summary>
    public static float[] Render(SfxKind kind, int variant)
    {
        var rng = new SynthRandom((uint)kind * 7919u + (uint)variant * 104729u + 17u);
        float[] buffer = kind switch
        {
            SfxKind.Click => Click(rng),
            SfxKind.Hover => Hover(),
            SfxKind.Laser => Laser(rng),
            SfxKind.Cannon => Cannon(rng),
            SfxKind.Artillery => Artillery(rng),
            SfxKind.Missile => Missile(rng),
            SfxKind.Flak => Flak(rng),
            SfxKind.Bomb => Bomb(rng),
            SfxKind.DGun => DGun(rng),
            SfxKind.ImpactSmall => ImpactSmall(rng),
            SfxKind.ExplosionSmall => ExplosionSmall(rng),
            SfxKind.ExplosionLarge => ExplosionLarge(rng),
            SfxKind.ExplosionHuge => ExplosionHuge(rng),
            SfxKind.BuildStart => BuildStart(rng),
            SfxKind.BuildComplete => BuildComplete(),
            SfxKind.UnitReady => UnitReady(),
            SfxKind.Nano => Nano(rng),
            SfxKind.Reclaim => Reclaim(rng),
            SfxKind.Select => Select(),
            SfxKind.Acknowledge => Acknowledge(),
            SfxKind.Error => Error(),
            SfxKind.Alert => Alert(),
            SfxKind.MissionMessage => MissionMessage(rng),
            SfxKind.Victory => Victory(),
            SfxKind.Defeat => Defeat(),
            _ => new float[64],
        };
        return Finish(buffer);
    }

    // ---- Shared helpers --------------------------------------------------------------------------
    //
    // The render loops below are marked AggressiveOptimization: each runs once at startup, so under
    // .NET tiered compilation they would otherwise execute as unoptimised tier-0 code (2–4x slower).

    private static float[] Alloc(float seconds) => new float[(int)(seconds * Sr)];

    /// <summary>Exponential decay with time constant <paramref name="tau"/>.</summary>
    private static float Env(float t, float tau)
    {
        if (t <= 0f) return 1f;
        float x = t / tau;
        return x > 80f ? 0f : MathF.Exp(-x);   // past -695 dB it's silence; avoids subnormal floats
    }

    /// <summary>Linear ramp from 0 to 1 over <paramref name="seconds"/>.</summary>
    private static float Attack(float t, float seconds) => t <= 0f ? 0f : (t >= seconds ? 1f : t / seconds);

    /// <summary>Smooth on/off gate for a segment [start, end) with short ramps.</summary>
    private static float Gate(float t, float start, float end, float rampIn, float rampOut) =>
        Dsp.Smoothstep((t - start) / rampIn) * Dsp.Smoothstep((end - t) / rampOut);

    /// <summary>Trims inaudible tail, removes DC and clicks, and normalises.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] Finish(float[] buffer)
    {
        Dsp.DcBlock(buffer, 12f, Sr);
        int length = Math.Max(Dsp.AudibleLength(buffer, 0.001f), 64);
        if (length < buffer.Length) Array.Resize(ref buffer, length);
        Dsp.FadeIn(buffer, 8);
        Dsp.FadeOut(buffer, Math.Min(length / 5, (int)(0.06f * Sr)));
        Dsp.NormalizePeak(buffer, TargetPeak);
        return buffer;
    }

    /// <summary>Adds a mono reverb tail in place.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void AddReverb(float[] buffer, float roomSize, float damping, float wet)
    {
        // Left and right both alias the mono buffer, so each receives half the wet signal.
        new Reverb(Sr, roomSize, damping, combCount: 6).ProcessAdd(buffer, buffer, buffer, wet * 0.5f);
    }

    /// <summary>Adds one low-passed slap-back echo (distant terrain reflection).</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void AddEcho(float[] buffer, float delaySeconds, float gain, float lowpassHz)
    {
        int delay = (int)(delaySeconds * Sr);
        var dry = (float[])buffer.Clone();
        var lp = new OnePole();
        lp.SetCutoff(lowpassHz, Sr);
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] += lp.Lowpass(i >= delay ? dry[i - delay] : 0f) * gain;
    }

    /// <summary>Adds a feedback echo (each repeat <paramref name="feedback"/> times quieter).</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void AddFeedbackEcho(float[] buffer, float delaySeconds, float feedback)
    {
        int delay = (int)(delaySeconds * Sr);
        for (int i = delay; i < buffer.Length; i++) buffer[i] += buffer[i - delay] * feedback;
    }

    /// <summary>
    /// Sparse random impulses ("debris") whose density follows <paramref name="density"/> per sample.
    /// </summary>
    private static float Debris(SynthRandom rng, float density) =>
        rng.Unit() < density ? rng.Bipolar() : 0f;

    // ---- UI ----------------------------------------------------------------------------------------

    /// <summary>Crisp tick: a short sine blip on top of a burst of band-passed noise.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] Click(SynthRandom rng)
    {
        var buf = Alloc(0.06f);
        var bp = new Svf();
        bp.Set(3200f, 1.2f, Sr);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i * Dt;
            float tone = Dsp.Sin01(1650f * t) * Env(t, 0.011f);
            bp.Process(rng.Bipolar() * Env(t, 0.0025f));
            buf[i] = 0.7f * tone + 0.9f * bp.BandNorm;
        }
        return buf;
    }

    /// <summary>Barely-there high blip with a slight upward lift.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] Hover()
    {
        var buf = Alloc(0.05f);
        float phase = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i * Dt;
            phase = Dsp.Advance(phase, (2200f + 10000f * t) * Dt);
            buf[i] = Dsp.Sin01(phase) * Attack(t, 0.002f) * Env(t, 0.012f);
        }
        return buf;
    }

    // ---- Weapons -----------------------------------------------------------------------------------

    /// <summary>Bright zap: a pulse wave diving from ~2.5 kHz to ~260 Hz, filter tracking the pitch.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] Laser(SynthRandom rng)
    {
        var buf = Alloc(0.26f);
        float f0 = 2500f * rng.Range(0.85f, 1.15f), f1 = 260f * rng.Range(0.8f, 1.25f);
        float sweep = rng.Range(0.035f, 0.055f), decay = rng.Range(0.05f, 0.065f);
        float duty = rng.Range(0.22f, 0.4f);
        float p1 = 0f, p2 = 0f;
        var lp = new Svf();
        var hp = new OnePole();
        hp.SetCutoff(2000f, Sr);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i * Dt;
            float f = f1 + (f0 - f1) * Env(t, sweep);
            float dt = f * Dt;
            if ((i & 7) == 0) lp.Set(Math.Min(7000f, f * 3f), 1.6f, Sr);
            float osc = 0.7f * Dsp.Pulse(p1, dt, duty) + 0.5f * Dsp.Sin01(p2);
            p1 = Dsp.Advance(p1, dt);
            p2 = Dsp.Advance(p2, dt * 0.5f);
            float body = lp.Process(osc) * Attack(t, 0.001f) * Env(t, decay);
            float spark = hp.Highpass(rng.Bipolar()) * Env(t, 0.003f) * 0.35f;
            buf[i] = body + spark;
        }
        return buf;
    }

    /// <summary>Thump (falling sine) plus a high-passed crack and a short low-passed body.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] Cannon(SynthRandom rng)
    {
        var buf = Alloc(0.5f);
        float pitch = rng.Range(0.9f, 1.12f);
        float crackTau = rng.Range(0.01f, 0.016f), bodyTau = rng.Range(0.08f, 0.11f);
        float phase = 0f;
        var crackHp = new OnePole();
        crackHp.SetCutoff(1500f, Sr);
        var body = new Svf();
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i * Dt;
            float f = (45f + 130f * Env(t, 0.03f)) * pitch;
            phase = Dsp.Advance(phase, f * Dt);
            float thump = Dsp.Sin01(phase) * Attack(t, 0.0015f) * Env(t, 0.12f);
            float crack = crackHp.Highpass(rng.Bipolar()) * Env(t, crackTau);
            if ((i & 7) == 0) body.Set(300f + 2500f * Env(t, 0.04f), 0.8f, Sr);
            float rumble = body.Process(rng.Bipolar()) * 2.5f * Env(t, bodyTau);
            buf[i] = Dsp.SoftClip((thump + 0.7f * crack + 0.9f * rumble) * 1.6f);
        }
        return buf;
    }

    /// <summary>Deep boom: a very low falling sine and rumble, a distant crack and a terrain slap-back.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] Artillery(SynthRandom rng)
    {
        var buf = Alloc(1.3f);
        float pitch = rng.Range(0.88f, 1.1f);
        float phase = 0f;
        var crack = new Svf();
        crack.Set(rng.Range(750f, 1100f), 0.8f, Sr);
        var body = new Svf();
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i * Dt;
            float f = (32f + 90f * Env(t, 0.06f)) * pitch;
            phase = Dsp.Advance(phase, f * Dt);
            float boom = Dsp.Sin01(phase) * Attack(t, 0.002f) * Env(t, 0.3f);
            crack.Process(rng.Bipolar());
            if ((i & 7) == 0) body.Set(120f + 900f * Env(t, 0.08f), 0.7f, Sr);
            float rumble = body.Process(rng.Bipolar()) * 4f * Attack(t, 0.003f) * Env(t, 0.25f);
            buf[i] = Dsp.SoftClip((1.2f * boom + rumble + 1.5f * crack.BandNorm * Env(t, 0.02f)) * 1.8f);
        }
        AddEcho(buf, rng.Range(0.11f, 0.15f), 0.28f, 1200f);
        return buf;
    }

    /// <summary>Launch whoosh: band-passed noise sweeping up then down, motor crackle and an ignition pop.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] Missile(SynthRandom rng)
    {
        var buf = Alloc(0.75f);
        float peakF = rng.Range(2200f, 3000f), sweepLen = rng.Range(0.5f, 0.6f);
        var bp = new Svf();
        var crackleHp = new OnePole();
        crackleHp.SetCutoff(2500f, Sr);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i * Dt;
            float shape = MathF.Sin(MathF.PI * Math.Min(t / sweepLen, 1f));
            if ((i & 7) == 0) bp.Set(500f + peakF * shape, 2.4f, Sr);
            bp.Process(rng.Bipolar());
            float amp = Dsp.Smoothstep(t / 0.05f) * Env(t - 0.08f, 0.2f);
            float whoosh = bp.BandNorm * 3.5f * amp;
            float crackle = crackleHp.Highpass(Debris(rng, 0.03f)) * amp;
            float ignition = Dsp.Sin01(110f * t) * Env(t, 0.03f) * 0.6f;
            buf[i] = whoosh + crackle + ignition;
        }
        return buf;
    }

    /// <summary>Rapid pops: 3–4 band-passed noise bursts with little thumps, 55–90 ms apart.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] Flak(SynthRandom rng)
    {
        var buf = Alloc(0.5f);
        int pops = 3 + rng.Int(2);
        var times = new float[pops];
        float at = 0f;
        for (int k = 0; k < pops; k++)
        {
            times[k] = at;
            at += rng.Range(0.055f, 0.09f);
        }
        var bp = new Svf();
        bp.Set(rng.Range(1100f, 1600f), 1f, Sr);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i * Dt;
            float excite = 0f, thump = 0f;
            for (int k = 0; k < pops; k++)
            {
                float tk = t - times[k];
                if (tk < 0f || tk > 0.2f) continue;
                float a = 1f - 0.15f * k;
                excite += rng.Bipolar() * Env(tk, 0.018f) * a;
                thump += Dsp.Sin01(260f * tk) * Env(tk, 0.025f) * a;
            }
            bp.Process(excite);
            buf[i] = Dsp.SoftClip((bp.BandNorm * 2.5f + 0.6f * thump) * 1.5f);
        }
        return buf;
    }

    /// <summary>Heavy, whistle-less thud: driven low sine and dark rumble.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] Bomb(SynthRandom rng)
    {
        var buf = Alloc(1.1f);
        float pitch = rng.Range(0.9f, 1.1f);
        float phase = 0f;
        var body = new Svf();
        var transient = new OnePole();
        transient.SetCutoff(2000f, Sr);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i * Dt;
            float f = (28f + 70f * Env(t, 0.08f)) * pitch;
            phase = Dsp.Advance(phase, f * Dt);
            float thud = Dsp.Sin01(phase) * Attack(t, 0.004f) * Env(t, 0.28f);
            if ((i & 7) == 0) body.Set(80f + 700f * Env(t, 0.05f), 0.8f, Sr);
            float rumble = body.Process(rng.Bipolar()) * 5f * Attack(t, 0.004f) * Env(t, 0.22f);
            float hit = transient.Lowpass(rng.Bipolar()) * Env(t, 0.008f) * 1.5f;
            buf[i] = Dsp.SoftClip((1.4f * thud + rumble + hit) * 2.5f);
        }
        return buf;
    }

    /// <summary>
    /// D-gun: big crackling plasma roar. A noise-modulated saw/pulse drone that sags in pitch, a
    /// sweeping hiss and sample-and-hold "electric" crackle, all driven hard into the clipper.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] DGun(SynthRandom rng)
    {
        var buf = Alloc(1.0f);
        float baseF = rng.Range(62f, 78f);
        float p1 = 0f, p2 = 0f, p3 = 0f;
        var hiss = new Svf();
        var crackleHp = new OnePole();
        crackleHp.SetCutoff(1800f, Sr);
        var wobble = new OnePole();
        wobble.SetCutoff(180f, Sr);
        var output = new Svf();
        output.Set(4200f, 0.7f, Sr);
        float held = 0f;
        int holdCount = 0;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i * Dt;
            float amp = Attack(t, 0.015f) * (t < 0.35f ? 1f : Env(t - 0.35f, 0.14f));
            float fm = Math.Clamp(wobble.Lowpass(rng.Bipolar()) * 6f, -0.9f, 0.9f);
            float dt = baseF * (1f - 0.35f * t / 0.9f) * (1f + 0.3f * fm) * Dt;
            float drone = Dsp.Saw(p1, dt) + 0.7f * Dsp.Saw(p2, dt * 1.503f) + 0.6f * Dsp.Pulse(p3, dt * 0.5f, 0.5f);
            p1 = Dsp.Advance(p1, dt);
            p2 = Dsp.Advance(p2, dt * 1.503f);
            p3 = Dsp.Advance(p3, dt * 0.5f);
            if ((i & 7) == 0) hiss.Set(600f + 2400f * Env(t, 0.25f), 0.7f, Sr);
            hiss.Process(rng.Bipolar());
            if (--holdCount <= 0)
            {
                holdCount = 3 + rng.Int(12);
                held = rng.Unit() < 0.35f ? rng.Bipolar() : 0f;
            }
            float crackle = crackleHp.Highpass(held) * 1.2f;
            float x = (0.8f * drone + 1.8f * hiss.BandNorm + crackle) * amp;
            buf[i] = output.Process(Dsp.SoftClip(x * 3f));
        }
        return buf;
    }

    // ---- Impacts and explosions --------------------------------------------------------------------

    /// <summary>Shell hit: band-passed noise snap, a small thump and a brief metallic ring.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] ImpactSmall(SynthRandom rng)
    {
        var buf = Alloc(0.3f);
        float ring = rng.Range(0.9f, 1.15f);
        var bp = new Svf();
        bp.Set(rng.Range(1800f, 2600f), 0.9f, Sr);
        float phase = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i * Dt;
            bp.Process(rng.Bipolar());
            float hit = bp.BandNorm * Env(t, 0.02f) * 2.5f;
            phase = Dsp.Advance(phase, (70f + 80f * Env(t, 0.02f)) * Dt);
            float thump = Dsp.Sin01(phase) * Env(t, 0.04f) * 0.6f;
            float metal = (Dsp.Sin01(1730f * ring * t) + 0.7f * Dsp.Sin01(2690f * ring * t)
                           + 0.4f * Dsp.Sin01(4120f * ring * t)) * Env(t, 0.05f) * 0.25f;
            buf[i] = Dsp.SoftClip((hit + thump + metal) * 1.4f);
        }
        return buf;
    }

    /// <summary>Small blast: closing low-pass noise burst over a sub thump, softly clipped.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] ExplosionSmall(SynthRandom rng)
    {
        var buf = Alloc(0.5f);
        float tau = rng.Range(0.1f, 0.14f), bright = rng.Range(3000f, 4500f);
        float phase = 0f;
        var lp = new Svf();
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i * Dt;
            if ((i & 7) == 0) lp.Set(250f + bright * Env(t, 0.05f), 0.8f, Sr);
            float noise = lp.Process(rng.Bipolar()) * 3f * Attack(t, 0.001f) * Env(t, tau);
            phase = Dsp.Advance(phase, (40f + 90f * Env(t, 0.04f)) * Dt);
            float sub = Dsp.Sin01(phase) * Attack(t, 0.001f) * Env(t, 0.12f);
            buf[i] = Dsp.SoftClip((1.2f * noise + sub) * 2.2f);
        }
        return buf;
    }

    /// <summary>
    /// Large, crunchy blast: crack, closing noise body, sub drop and debris crackle, driven and
    /// partly bit-crushed for 90s-sampler grit, then a short reverb.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] ExplosionLarge(SynthRandom rng)
    {
        var buf = Alloc(1.4f);
        float bodyTau = rng.Range(0.24f, 0.32f);
        float phase = 0f;
        var crackHp = new OnePole();
        crackHp.SetCutoff(2000f, Sr);
        var body = new Svf();
        var debris = new Svf();
        debris.Set(rng.Range(1500f, 2200f), 1.5f, Sr);
        float held = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i * Dt;
            float crack = crackHp.Highpass(rng.Bipolar()) * Env(t, 0.01f);
            if ((i & 7) == 0) body.Set(150f + 3500f * Env(t, 0.09f), 1f, Sr);
            float noise = body.Process(rng.Bipolar()) * 3f * Attack(t, 0.002f) * Env(t, bodyTau);
            phase = Dsp.Advance(phase, (30f + 80f * Env(t, 0.07f)) * Dt);
            float sub = Dsp.Sin01(phase) * Attack(t, 0.002f) * Env(t, 0.3f);
            debris.Process(t > 0.04f ? Debris(rng, 0.006f * Env(t - 0.04f, 0.35f)) * 2f * Env(t, 0.3f) : 0f);
            float x = Dsp.SoftClip((noise + 0.8f * crack + sub + debris.BandNorm) * 3.5f);
            // Crunch: hold every other sample (11 kHz) and quantise to 7 bits, blended with the clean signal.
            if ((i & 1) == 0) held = MathF.Round(x * 64f) / 64f;
            buf[i] = 0.55f * held + 0.45f * x;
        }
        AddReverb(buf, 0.6f, 0.5f, 0.25f);
        return buf;
    }

    /// <summary>
    /// Huge, rolling detonation (fusion reactors, Commanders): two blasts, brown-noise rumble with a
    /// slow random tremor, a very low sub drop, long debris rain and a big reverb tail.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] ExplosionHuge(SynthRandom rng)
    {
        var buf = Alloc(3.2f);
        float second = rng.Range(0.16f, 0.3f);
        float phase = 0f, brown = 0f;
        var crackHp = new OnePole();
        crackHp.SetCutoff(1800f, Sr);
        var tremor = new OnePole();
        tremor.SetCutoff(7f, Sr);
        var rumble = new Svf();
        var debris = new Svf();
        debris.Set(rng.Range(1200f, 1800f), 1.2f, Sr);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i * Dt;
            float t2 = t - second;
            float amp = Attack(t, 0.005f) * Env(t, 0.8f) + (t2 > 0f ? 0.6f * Attack(t2, 0.005f) * Env(t2, 0.6f) : 0f);
            brown = brown * 0.995f + rng.Bipolar() * 0.1f;
            float roll = 1f + Math.Clamp(tremor.Lowpass(rng.Bipolar()) * 25f, -0.7f, 0.7f);
            if ((i & 7) == 0)
            {
                float flash = Env(t, 0.04f) + (t2 > 0f ? 0.6f * Env(t2, 0.04f) : 0f);
                rumble.Set(60f + 600f * Env(t, 0.25f) + 1500f * flash, 0.9f, Sr);
            }
            float body = rumble.Process(brown) * 1.6f * amp * roll;
            phase = Dsp.Advance(phase, (22f + 60f * Env(t, 0.15f)) * Dt);
            float sub = Dsp.Sin01(phase) * Attack(t, 0.005f) * Env(t, 0.6f);
            float crack = crackHp.Highpass(rng.Bipolar()) * (Env(t, 0.015f) + (t2 > 0f ? 0.6f * Env(t2, 0.015f) : 0f));
            debris.Process(Debris(rng, 0.008f * Env(t, 0.7f)) * 2f * Env(t, 0.6f));
            buf[i] = Dsp.SoftClip((body + sub + 0.7f * crack + debris.BandNorm * 1.2f) * 2f);
        }
        AddReverb(buf, 0.85f, 0.6f, 0.35f);
        return buf;
    }

    // ---- Construction ------------------------------------------------------------------------------

    /// <summary>Soft mechanical whirr: a rising, gear-chattering filtered saw with a little air.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] BuildStart(SynthRandom rng)
    {
        var buf = Alloc(0.55f);
        float phase = 0f;
        var lp = new Svf();
        var air = new Svf();
        air.Set(1200f, 2f, Sr);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i * Dt;
            float u = t / 0.55f;
            float dt = (80f + 60f * u) * Dt;
            if ((i & 15) == 0) lp.Set(500f + 400f * u, 1.5f, Sr);
            float whirr = lp.Process(Dsp.Saw(phase, dt)) * (0.6f + 0.4f * Dsp.Sin01(28f * t));
            phase = Dsp.Advance(phase, dt);
            air.Process(rng.Bipolar());
            float env = Dsp.Smoothstep(t / 0.06f) * Dsp.Smoothstep((0.55f - t) / 0.15f);
            buf[i] = (whirr + air.BandNorm * 0.3f) * env;
        }
        return buf;
    }

    /// <summary>Pleasant synthetic two-note chime (E5 then B5), FM bells with a soft echo.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] BuildComplete()
    {
        var buf = Alloc(1.2f);
        AddChimeNote(buf, 0f, Dsp.MidiToHz(76));
        AddChimeNote(buf, 0.13f, Dsp.MidiToHz(83));
        AddFeedbackEcho(buf, 0.16f, 0.25f);
        return buf;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void AddChimeNote(float[] buf, float start, float f)
    {
        int s0 = (int)(start * Sr);
        for (int i = s0; i < buf.Length; i++)
        {
            float t = (i - s0) * Dt;
            float index = 1.2f * Env(t, 0.12f);
            float bell = Dsp.Sin01(f * t + index * Dsp.Sin01(3f * f * t));
            float shimmer = Dsp.Sin01(2.76f * f * t) * 0.12f * Env(t, 0.08f);
            buf[i] += (bell + shimmer) * Attack(t, 0.002f) * Env(t, 0.32f) * 0.5f;
        }
    }

    /// <summary>Short rising blip: a triangle/pulse glide up ~1.4 octaves.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] UnitReady()
    {
        var buf = Alloc(0.3f);
        float phase = 0f;
        var lp = new OnePole();
        lp.SetCutoff(4000f, Sr);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i * Dt;
            float dt = 520f * MathF.Pow(2f, 1.4f * Math.Min(t / 0.12f, 1f)) * Dt;
            float osc = Dsp.Triangle(phase) + 0.3f * Dsp.Pulse(phase, dt, 0.5f);
            phase = Dsp.Advance(phase, dt);
            float env = Attack(t, 0.002f) * (t < 0.15f ? 1f : Env(t - 0.15f, 0.04f));
            buf[i] = lp.Lowpass(osc) * env;
        }
        return buf;
    }

    /// <summary>Soft sparkle: a few random high sine grains over airy noise, windowed so retriggers blend.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] Nano(SynthRandom rng)
    {
        var buf = Alloc(0.4f);
        const int grains = 6;
        var at = new float[grains];
        var freq = new float[grains];
        var amp = new float[grains];
        for (int k = 0; k < grains; k++)
        {
            at[k] = rng.Range(0f, 0.28f);
            freq[k] = rng.Range(2500f, 6000f);
            amp[k] = rng.Range(0.3f, 1f);
        }
        var air = new Svf();
        air.Set(5000f, 3f, Sr);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i * Dt;
            float s = 0f;
            for (int k = 0; k < grains; k++)
            {
                float tk = t - at[k];
                if (tk <= 0f) continue;
                s += Dsp.Sin01(freq[k] * tk) * Attack(tk, 0.003f) * Env(tk, 0.03f) * amp[k];
            }
            air.Process(rng.Bipolar());
            float window = Dsp.Sin01(0.5f * t / 0.4f);
            buf[i] = (0.5f * s + air.BandNorm * 0.8f) * window;
        }
        return buf;
    }

    /// <summary>Suction (falling resonant noise swelling in) ending in a soft crunch.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] Reclaim(SynthRandom rng)
    {
        var buf = Alloc(0.55f);
        float cut = rng.Range(0.34f, 0.4f);
        var bp = new Svf();
        var crunch = new Svf();
        crunch.Set(rng.Range(1000f, 1400f), 1.2f, Sr);
        float slurp = 0f, thump = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i * Dt;
            if ((i & 7) == 0) bp.Set(300f + 2600f * Env(t, 0.2f), 3f, Sr);
            float amp = t < cut ? (t / cut) * (t / cut) : Env(t - cut, 0.02f);
            bp.Process(rng.Bipolar());
            slurp = Dsp.Advance(slurp, (200f + 900f * Env(t, 0.25f)) * Dt);
            float suction = (bp.BandNorm * 3f + Dsp.Sin01(slurp) * 0.2f) * amp;
            float tc = t - (cut - 0.02f);
            crunch.Process(tc > 0f ? Debris(rng, 0.03f * Env(tc, 0.06f)) * 3f : 0f);
            thump = Dsp.Advance(thump, 90f * Dt);
            float body = tc > 0f ? Dsp.Sin01(thump) * Attack(tc, 0.002f) * Env(tc, 0.05f) * 0.5f : 0f;
            buf[i] = suction + crunch.BandNorm * 2f + body;
        }
        return buf;
    }

    // ---- Feedback ----------------------------------------------------------------------------------

    /// <summary>Short soft beep.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] Select()
    {
        var buf = Alloc(0.14f);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i * Dt;
            float s = Dsp.Sin01(1250f * t) + 0.25f * Dsp.Sin01(2500f * t);
            buf[i] = s * Attack(t, 0.002f) * Env(t, 0.025f);
        }
        return buf;
    }

    /// <summary>Two-tone "affirmative" chirp, each tone gliding slightly upward.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] Acknowledge()
    {
        var buf = Alloc(0.22f);
        float phase = 0f;
        var lp = new Svf();
        lp.Set(3000f, 0.8f, Sr);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i * Dt;
            bool first = t < 0.07f;
            float start = first ? 0f : 0.075f, end = first ? 0.065f : 0.18f;
            float baseF = first ? 950f : 1420f;
            float dt = baseF * (1f + 0.05f * Math.Clamp((t - start) / (end - start), 0f, 1f)) * Dt;
            float osc = lp.Process(Dsp.Pulse(phase, dt, 0.5f));
            phase = Dsp.Advance(phase, dt);
            buf[i] = osc * Gate(t, start, end, 0.003f, 0.015f);
        }
        return buf;
    }

    /// <summary>Low "bzzt-bzzt": two pulses of beating square/saw through a dark filter.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] Error()
    {
        var buf = Alloc(0.34f);
        float p1 = 0f, p2 = 0f;
        var lp = new Svf();
        lp.Set(900f, 1f, Sr);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i * Dt;
            float dt1 = 98f * Dt, dt2 = 104f * Dt;
            float osc = Dsp.Pulse(p1, dt1, 0.5f) + 0.8f * Dsp.Saw(p2, dt2);
            p1 = Dsp.Advance(p1, dt1);
            p2 = Dsp.Advance(p2, dt2);
            float gate = Gate(t, 0f, 0.13f, 0.005f, 0.01f) + Gate(t, 0.17f, 0.32f, 0.005f, 0.012f);
            buf[i] = lp.Process(osc) * gate;
        }
        return buf;
    }

    /// <summary>Urgent two-tone klaxon (hi-lo-hi-lo) with horn-like formants and a little vibrato.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] Alert()
    {
        var buf = Alloc(0.82f);
        float phase = 0f;
        var f1 = new Svf();
        f1.Set(900f, 2f, Sr);
        var f2 = new Svf();
        f2.Set(1800f, 3f, Sr);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i * Dt;
            int segment = Math.Min((int)(t / 0.2f), 3);
            float start = segment * 0.2f;
            float baseF = segment % 2 == 0 ? 740f : 587f;
            float dt = baseF * (1f + 0.006f * Dsp.Sin01(6f * t)) * Dt;
            float osc = Dsp.Saw(phase, dt) + 0.6f * Dsp.Pulse(phase, dt, 0.5f);
            phase = Dsp.Advance(phase, dt);
            f1.Process(osc);
            f2.Process(osc);
            float horn = f1.BandNorm + 0.6f * f2.BandNorm + 0.3f * osc;
            buf[i] = Dsp.SoftClip(horn * 1.8f) * Gate(t, start, start + 0.19f, 0.008f, 0.015f);
        }
        return buf;
    }

    /// <summary>
    /// Radio chatter: a roger-beep, a burst of formant-filtered "voice" babble over static, a squelch
    /// tail, all band-limited to 300–3000 Hz and bit-crushed like a field radio.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] MissionMessage(SynthRandom rng)
    {
        var buf = Alloc(0.42f);
        float voicePhase = 0f, flutter = 1f, formant = 1200f;
        int flutterCount = 0, formantCount = 0;
        var voice = new Svf();
        var stat = new Svf();
        stat.Set(1800f, 0.7f, Sr);
        var squelchHp = new OnePole();
        squelchHp.SetCutoff(2000f, Sr);
        var bandHp = new OnePole();
        bandHp.SetCutoff(300f, Sr);
        var bandLp = new Svf();
        bandLp.Set(3000f, 0.7f, Sr);
        float held = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i * Dt;
            float beep = Dsp.Sin01(1200f * t) * Gate(t, 0f, 0.07f, 0.003f, 0.008f) * 0.8f;

            if (--formantCount <= 0)
            {
                formantCount = (int)(rng.Range(0.05f, 0.08f) * Sr);
                formant = rng.Range(600f, 2200f);
                voice.Set(formant, 4f, Sr);
            }
            if (--flutterCount <= 0)
            {
                flutterCount = (int)(0.03f * Sr);
                flutter = rng.Range(0.3f, 1f);
            }
            float vdt = (140f + 20f * Dsp.Sin01(3f * t)) * Dt;
            voice.Process(Dsp.Saw(voicePhase, vdt));
            voicePhase = Dsp.Advance(voicePhase, vdt);
            stat.Process(rng.Bipolar());
            float chatter = (voice.BandNorm * 2.6f + stat.BandNorm * 1f) * flutter * Gate(t, 0.09f, 0.33f, 0.01f, 0.01f);
            float squelch = squelchHp.Highpass(rng.Bipolar()) * 0.6f * Gate(t, 0.33f, 0.37f, 0.002f, 0.02f);

            float x = bandLp.Process(bandHp.Highpass(beep + chatter + squelch));
            if (i % 3 == 0) held = MathF.Round(x * 16f) / 16f;
            buf[i] = held;
        }
        return buf;
    }

    // ---- Match result stingers ---------------------------------------------------------------------

    /// <summary>
    /// Triumphant synth-brass fanfare (~4 s): a triplet pickup into a rising I–bVI–bVII–I cadence
    /// (C–Ab–Bb–C) under a lead climbing G5–Ab5–Bb5 to a held C6, with timpani on each chord.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] Victory()
    {
        var m = new MixBuffer((int)(5.2f * Sr), SampleRate, 1997);

        (float At, float Len, float Midi)[] lead =
        {
            (0f, 0.13f, 67), (0.15f, 0.13f, 72), (0.3f, 0.13f, 76),
            (0.45f, 0.42f, 79), (0.9f, 0.42f, 80), (1.35f, 0.42f, 82), (1.8f, 1.5f, 84),
        };
        foreach (var n in lead)
        {
            Instruments.Lead(m, m.At(n.At), m.At(n.Len), n.Midi, 0.95f, 0f, reverb: 0.3f, delay: 0f);
            Instruments.Brass(m, m.At(n.At), m.At(n.Len), n.Midi - 12, 0.5f, 0f, reverb: 0.3f);
        }

        (float At, float Len, int Bass, int[] Notes)[] chords =
        {
            (0.45f, 0.43f, 48, new[] { 60, 64, 67 }),
            (0.9f, 0.43f, 44, new[] { 56, 60, 63 }),
            (1.35f, 0.43f, 46, new[] { 58, 62, 65 }),
            (1.8f, 1.5f, 48, new[] { 55, 60, 64, 67 }),
        };
        foreach (var c in chords)
        {
            for (int k = 0; k < c.Notes.Length; k++)
                Instruments.Brass(m, m.At(c.At), m.At(c.Len), c.Notes[k], 0.7f, (k - 1.5f) * 0.3f);
            Instruments.Sub(m, m.At(c.At), m.At(c.Len), c.Bass - 12, 0.8f, attack: 0.01f, release: 0.3f);
            Instruments.Timpani(m, m.At(c.At), c.Bass, 1f);
        }
        foreach (float pickup in new[] { 0f, 0.15f, 0.3f })
            Instruments.Timpani(m, m.At(pickup), 43, 0.45f);

        Instruments.ApplyReverb(m, 0.7f, 0.4f, 1f);
        return m.Downmix();
    }

    /// <summary>
    /// Falling C-minor arpeggio (G5 down to C4) in a soft square with echoes, settling on a low
    /// C-minor pad that sags a semitone as if powering down.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float[] Defeat()
    {
        var m = new MixBuffer((int)(4.6f * Sr), SampleRate, 1066);
        int[] notes = { 79, 75, 72, 67, 63, 60 };
        for (int k = 0; k < notes.Length; k++)
        {
            Instruments.Pluck(m, m.At(0.19 * k), m.At(0.4), notes[k], 0.9f - 0.06f * k, 0.4f - 0.16f * k,
                duty: 0.5f, brightness: 0.45f, decay: 0.45f, reverb: 0.4f, delay: 0.35f);
        }
        Instruments.Pad(m, m.At(1.1), m.At(1.5), new[] { 48, 51, 55 }, 1.3f, attack: 0.25f, release: 0.5f,
            brightness: 0.25f, reverb: 0.6f, bendSemitones: -1f);
        Instruments.Sub(m, m.At(1.1), m.At(1.5), 36, 0.8f, attack: 0.2f, release: 0.5f);

        Instruments.ApplyDelay(m, m.At(0.285), 0.35f, 0.5f);
        Instruments.ApplyReverb(m, 0.75f, 0.5f, 1f);
        return m.Downmix();
    }
}
