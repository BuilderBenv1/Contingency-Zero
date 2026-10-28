using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace ContingencyZero.Game.Audio;

/// <summary>
/// Sound effects. Most are recorded sounds designed from CC0 libraries (res://audio/sfx, built by
/// tools/audio_build.py); <see cref="Init"/> also synthesises every sound on a background thread (see
/// <see cref="SfxSynth"/>), used for kinds with no recording and when the "synthesised sounds" setting
/// is on. Playback goes through fixed pools of
/// <see cref="AudioStreamPlayer"/>s: 8 voices for interface sounds (<see cref="Play"/>) and 32 for the
/// battlefield (<see cref="PlayAt"/>), so battle noise can never cut off a click or an alert.
/// </summary>
/// <remarks>
/// Main-thread only. <see cref="Play"/> and <see cref="PlayAt"/> never block and allocate nothing per
/// call; a sound requested before its synthesis has finished is silently skipped.
/// </remarks>
public static class Sfx
{
    private const int UiVoiceCount = 8;
    private const int BattleVoiceCount = 32;
    private const int RateLimitStarts = 6;          // at most this many starts of one kind...
    private const double RateLimitWindowMs = 100.0; // ...within this window
    private const float NearDistance = 30f;         // full volume within this ground distance
    private const float ZoomInSize = 60f, ZoomOutSize = 900f;
    private const float ZoomOutDb = -9f;            // battlefield level change from fully in to fully out
    private const float InaudibleDb = -50f;

    private static readonly int KindCount = Enum.GetValues<SfxKind>().Length;

    /// <summary>Synthesis order: interface and feedback sounds first, so menus have them immediately.</summary>
    private static readonly SfxKind[] SynthesisOrder =
    {
        SfxKind.Click, SfxKind.Hover, SfxKind.Select, SfxKind.Acknowledge, SfxKind.Error, SfxKind.Alert,
        SfxKind.MissionMessage, SfxKind.BuildComplete, SfxKind.UnitReady, SfxKind.BuildStart, SfxKind.Nano,
        SfxKind.Reclaim, SfxKind.Laser, SfxKind.Cannon, SfxKind.Flak, SfxKind.Missile, SfxKind.Artillery,
        SfxKind.Bomb, SfxKind.DGun, SfxKind.ImpactSmall, SfxKind.ExplosionSmall, SfxKind.ExplosionLarge,
        SfxKind.ExplosionHuge, SfxKind.Victory, SfxKind.Defeat,
    };

    private static readonly SfxTraits[] Traits = CreateTraits();
    private static readonly double MsPerTick = 1000.0 / Stopwatch.Frequency;

    // PCM per kind and variant, published once by the synthesis thread.
    private static readonly byte[][]?[] Rendered = new byte[KindCount][][];
    // Streams and their lengths, created on the main thread the first time a kind is played.
    private static readonly AudioStream[]?[] Streams = new AudioStream[KindCount][];
    private static readonly float[] LiftDb = new float[KindCount];
    private static readonly double[]?[] LengthsMs = new double[KindCount][];

    // Rate limiter: a ring of the last few start times per kind.
    private static readonly double[] RecentStarts = CreateRecentStarts();
    private static readonly int[] RecentHead = new int[KindCount];
    private static readonly int[] LastVariant = new int[KindCount];

    private static Node? _root;
    private static bool _rootInTree;
    private static Voice[] _uiVoices = Array.Empty<Voice>();
    private static Voice[] _battleVoices = Array.Empty<Voice>();
    private static int _synthesisStarted;
    private static string? _synthesisError;
    private static uint _random = 0x9E3779B9u;
    private static Vector3 _cameraRight = new Vector3(1f, 0f, -1f).Normalized();
    private static ulong _cameraRightFrame = ulong.MaxValue;

    /// <summary>Ground distance (world units) beyond which battlefield sounds are silent.</summary>
    public static float HearingRange { get; set; } = 350f;

    /// <summary>True once every sound has been synthesised (useful for tests and loading screens).</summary>
    public static bool IsSynthesised
    {
        get
        {
            for (int k = 0; k < KindCount; k++)
                if (Streams[k] is null && Volatile.Read(ref Rendered[k]) is null) return false;
            return true;
        }
    }

    /// <summary>
    /// Call once from the root node. Creates the "SFX" and "Music" audio buses if missing (both routed
    /// to Master), starts synthesising every sound (and the music) in the background, and creates the
    /// voice pool under <paramref name="host"/>. Calling it again with a new host (e.g. after the old one
    /// was freed) rebuilds the pool; the synthesised sounds are kept.
    /// </summary>
    public static void Init(Node host)
    {
        if (_root is not null && GodotObject.IsInstanceValid(_root))
        {
            if (_root.GetParent() == host) return;
            _root.QueueFree();
        }

        AudioBuses.Ensure();
        StartSynthesis();
        MusicLibrary.Start();

        // UI voices keep working while the game is paused; battlefield voices pause with it.
        var root = new Node { Name = "SfxVoices", ProcessMode = Node.ProcessModeEnum.Always };
        var battle = new Node { Name = "Battlefield", ProcessMode = Node.ProcessModeEnum.Pausable };
        root.AddChild(battle);
        _uiVoices = CreateVoices(root, UiVoiceCount, "Ui");
        _battleVoices = CreateVoices(battle, BattleVoiceCount, "Battle");
        // Identity checks: an old pool being freed after a re-Init must not switch off the new one.
        root.TreeEntered += () => { if (_root == root) _rootInTree = true; };
        root.TreeExited += () => { if (_root == root) _rootInTree = false; };
        host.AddChild(root);
        _root = root;
        _rootInTree = root.IsInsideTree();
    }

    /// <summary>
    /// Plays a non-positional (interface) sound. Safe to call before <see cref="Init"/> (does nothing then).
    /// </summary>
    /// <param name="volumeDb">Offset added to the sound's own mix level.</param>
    /// <param name="pitchScale">Playback speed/pitch multiplier.</param>
    public static void Play(SfxKind kind, float volumeDb = 0f, float pitchScale = 1f)
    {
        if (!IsAlive()) return;
        Start(kind, _uiVoices, volumeDb, pitchScale, AudioBuses.CenterPan);
    }

    /// <summary>
    /// Plays a battlefield sound at <paramref name="position"/>, heard from <paramref name="listener"/>
    /// (the camera's focus point on the ground). The sound fades with ground distance (silent beyond
    /// <see cref="HearingRange"/>), pans left/right by its offset along the camera's right axis, and gets
    /// quieter overall as <paramref name="listenerZoom"/> (the orthographic camera size) zooms out.
    /// A kind can start at most 6 times per 100 ms; extra calls are dropped. When all 32 voices are busy
    /// the oldest one of equal or lower priority is stolen.
    /// </summary>
    public static void PlayAt(SfxKind kind, Vector3 position, Vector3 listener, float listenerZoom, float volumeDb = 0f)
    {
        if (!IsAlive()) return;

        float dx = position.X - listener.X;
        float dz = position.Z - listener.Z;
        float distance = MathF.Sqrt(dx * dx + dz * dz);
        float range = MathF.Max(HearingRange, NearDistance + 1f);
        if (!(distance < range)) return; // also rejects NaN positions

        // Amplitude falls off as the square of the remaining fraction of the range: full volume close
        // by, a soft curve through the middle distance and true silence at the edge.
        float g = 1f - Math.Clamp((distance - NearDistance) / (range - NearDistance), 0f, 1f);
        float distanceDb = 40f * MathF.Log10(MathF.Max(g, 1e-4f));

        float zoom = float.IsFinite(listenerZoom) ? listenerZoom : ZoomInSize;
        float zoomT = Math.Clamp(MathF.Log(MathF.Max(zoom, 1f) / ZoomInSize) / MathF.Log(ZoomOutSize / ZoomInSize), 0f, 1f);
        float zoomDb = ZoomOutDb * zoomT;

        // Pan by the offset along the camera's right axis, relative to half the visible width.
        Vector3 right = CameraRight();
        float lateral = dx * right.X + dz * right.Z;
        float halfWidth = MathF.Max(80f, zoom * 0.9f);
        int pan = AudioBuses.PanIndex(Math.Clamp(lateral / halfWidth, -1f, 1f));

        Start(kind, _battleVoices, volumeDb + distanceDb + zoomDb, 1f, pan);
    }

    // ---- Playback --------------------------------------------------------------------------------

    private sealed class Voice
    {
        public Voice(AudioStreamPlayer player) => Player = player;

        public readonly AudioStreamPlayer Player;
        public double StartMs;
        public double EndMs;
        public int Priority;
        public int PanIndex = AudioBuses.CenterPan;
    }

    private static bool IsAlive() => _rootInTree && _root is not null && GodotObject.IsInstanceValid(_root);

    private static void Start(SfxKind kind, Voice[] pool, float volumeDb, float pitchScale, int panIndex)
    {
        int k = (int)kind;
        if ((uint)k >= (uint)KindCount) return;
        AudioStream[]? streams = Streams[k] ?? Promote(k);
        if (streams is null) return; // still synthesising

        SfxTraits traits = Traits[k];
        float db = traits.LevelDb + LiftDb[k] + volumeDb;
        if (!(db > InaudibleDb)) return;

        double now = Stopwatch.GetTimestamp() * MsPerTick;
        int ring = k * RateLimitStarts + RecentHead[k];
        if (now - RecentStarts[ring] < RateLimitWindowMs) return; // the 6th-most-recent start was under 100 ms ago

        Voice? voice = Acquire(pool, now, traits.Priority);
        if (voice is null) return;

        RecentStarts[ring] = now;
        RecentHead[k] = (RecentHead[k] + 1) % RateLimitStarts;

        int variant = PickVariant(k, streams.Length);
        float pitch = Math.Clamp(pitchScale, 0.05f, 16f) * (1f + traits.PitchJitter * NextBipolar());

        AudioStreamPlayer player = voice.Player;
        if (voice.PanIndex != panIndex)
        {
            player.Bus = AudioBuses.PanBus(panIndex);
            voice.PanIndex = panIndex;
        }
        player.Stream = streams[variant];
        player.VolumeDb = Math.Min(db, 24f);
        player.PitchScale = pitch;
        player.Play();

        voice.StartMs = now;
        voice.EndMs = now + LengthsMs[k]![variant] / pitch;
        voice.Priority = traits.Priority;
    }

    /// <summary>A free voice, else the oldest one this priority may steal, else null.</summary>
    private static Voice? Acquire(Voice[] pool, double now, int priority)
    {
        Voice? oldest = null;
        foreach (Voice v in pool)
        {
            if (now >= v.EndMs) return v;
            if (v.Priority <= priority && (oldest is null || v.StartMs < oldest.StartMs)) oldest = v;
        }
        return oldest;
    }

    /// <summary>Random variant, never the same one twice in a row.</summary>
    private static int PickVariant(int kind, int count)
    {
        if (count <= 1) return 0;
        int v = (LastVariant[kind] + 1 + (int)(NextRandom() % (uint)(count - 1))) % count;
        LastVariant[kind] = v;
        return v;
    }

    private static uint NextRandom()
    {
        uint x = _random;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        return _random = x;
    }

    private static float NextBipolar() => (NextRandom() >> 8) * (2f / 16777216f) - 1f;

    /// <summary>The active camera's right axis flattened onto the ground, refreshed once per frame.</summary>
    private static Vector3 CameraRight()
    {
        ulong frame = Engine.GetProcessFrames();
        if (frame == _cameraRightFrame) return _cameraRight;
        _cameraRightFrame = frame;

        Camera3D? camera = _root!.GetViewport()?.GetCamera3D();
        if (camera is not null)
        {
            Vector3 x = camera.GlobalBasis.X;
            x.Y = 0f;
            float length = x.Length();
            if (length > 1e-3f) _cameraRight = x / length;
        }
        return _cameraRight;
    }

    // ---- Synthesis -------------------------------------------------------------------------------

    private static void StartSynthesis()
    {
        if (Interlocked.Exchange(ref _synthesisStarted, 1) != 0) return;
        Task.Run(() =>
        {
            foreach (SfxKind kind in SynthesisOrder) Synthesise(kind);
            foreach (SfxKind kind in Enum.GetValues<SfxKind>())
                if (Volatile.Read(ref Rendered[(int)kind]) is null) Synthesise(kind);
        });
    }

    private static void Synthesise(SfxKind kind)
    {
        try
        {
            int count = Math.Max(1, Traits[(int)kind].Variants);
            var variants = new byte[count][];
            for (int v = 0; v < count; v++)
                variants[v] = Dsp.ToLittleEndianBytes(Dsp.ToPcm16(SfxSynth.Render(kind, v)));
            Volatile.Write(ref Rendered[(int)kind], variants);
        }
        catch (Exception e)
        {
            Interlocked.CompareExchange(ref _synthesisError, $"Sound synthesis failed for {kind}: {e}", null);
        }
    }

    /// <summary>
    /// Forgets the loaded streams so the next play of each kind picks recorded or synthesised sounds
    /// afresh (after the setting changes).
    /// </summary>
    public static void ReloadSoundSet()
    {
        for (int k = 0; k < KindCount; k++) { Streams[k] = null; LengthsMs[k] = null; LiftDb[k] = 0f; }
    }

    /// <summary>The kind's recorded variants, or null if it has none or synthesised sounds are chosen.</summary>
    private static AudioStream[]? LoadRecorded(int k)
    {
        if (App.Settings.Current.SynthSounds) return null;
        string name = ((SfxKind)k).ToString();
        if (!RecordedSfx.Kinds.TryGetValue(name, out var info)) return null;
        var streams = new AudioStream[info.Variants];
        var lengths = new double[info.Variants];
        for (int v = 0; v < info.Variants; v++)
        {
            if (ResourceLoader.Load<AudioStream>($"res://audio/sfx/{name}_{v + 1}.ogg") is not { } stream) return null;
            streams[v] = stream;
            lengths[v] = stream.GetLength() * 1000.0;
        }
        LengthsMs[k] = lengths;
        LiftDb[k] = info.LiftDb;
        return Streams[k] = streams;
    }

    /// <summary>Loads a kind's recorded sounds, else wraps its finished PCM in streams (main thread, once per kind).</summary>
    private static AudioStream[]? Promote(int k)
    {
        if (LoadRecorded(k) is { } recorded) return recorded;
        byte[][]? pcm = Volatile.Read(ref Rendered[k]);
        if (pcm is null)
        {
            string? error = Interlocked.Exchange(ref _synthesisError, null);
            if (error is not null) GD.PushError(error);
            return null;
        }

        var streams = new AudioStreamWav[pcm.Length];
        var lengths = new double[pcm.Length];
        for (int v = 0; v < pcm.Length; v++)
        {
            streams[v] = WavStreams.Create(pcm[v], SfxSynth.SampleRate, stereo: false);
            lengths[v] = pcm[v].Length / 2 * 1000.0 / SfxSynth.SampleRate;
        }
        LengthsMs[k] = lengths;
        LiftDb[k] = 0f;
        Streams[k] = streams; // Rendered is kept, so the sound set can be switched back
        return streams;
    }

    private static Voice[] CreateVoices(Node parent, int count, string prefix)
    {
        var voices = new Voice[count];
        for (int i = 0; i < count; i++)
        {
            var player = new AudioStreamPlayer { Name = $"{prefix}{i}", Bus = AudioBuses.PanBus(AudioBuses.CenterPan) };
            parent.AddChild(player);
            voices[i] = new Voice(player) { EndMs = double.MinValue };
        }
        return voices;
    }

    private static SfxTraits[] CreateTraits()
    {
        var traits = new SfxTraits[Enum.GetValues<SfxKind>().Length];
        foreach (SfxKind kind in Enum.GetValues<SfxKind>()) traits[(int)kind] = SfxSynth.Traits(kind);
        return traits;
    }

    private static double[] CreateRecentStarts()
    {
        var starts = new double[Enum.GetValues<SfxKind>().Length * RateLimitStarts];
        Array.Fill(starts, double.MinValue / 2);
        return starts;
    }
}
