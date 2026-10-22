using System;
using Godot;

namespace ContingencyZero.Game.Audio;

/// <summary>
/// Procedural background music: moody 90s sci-fi synth, generated at startup (see
/// <see cref="MusicSynth"/>) and played on the "Music" bus. Add one to the tree and call
/// <see cref="Play"/>; it keeps playing while the game is paused. Menu and Battle are seamless
/// loops; Victory and Defeat are one-shot outros that shadow the matching result stinger and fade
/// to silence. Tracks are synthesised in the background, and one requested before it is ready
/// starts the moment it is.
/// </summary>
public partial class MusicPlayer : Node
{
    /// <summary>The available tracks.</summary>
    public enum Track { None, Menu, Battle, Victory, Defeat }

    /// <summary>
    /// Victory/Defeat fade in almost instantly: they're timed to sit under the result stinger, and a
    /// slow fade would swallow their opening chords.
    /// </summary>
    private const float ResultFadeInSeconds = 0.05f;

    private Deck? _a;
    private Deck? _b;
    private Track _requested = Track.None;
    private bool _pending;

    /// <summary>Seconds a crossfade between tracks takes.</summary>
    [Export]
    public float CrossfadeSeconds { get; set; } = 1.5f;

    /// <summary>The most recently requested track.</summary>
    public Track Current => _requested;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        AudioBuses.Ensure();
        EnsureDecks();
        MusicLibrary.Start();
    }

    /// <summary>
    /// Switches to <paramref name="track"/>, crossfading over <see cref="CrossfadeSeconds"/>
    /// (<see cref="Track.None"/> fades to silence). Requesting the current track does nothing.
    /// </summary>
    public void Play(Track track)
    {
        if (track == _requested) return;
        AudioBuses.Ensure();
        EnsureDecks();
        MusicLibrary.Start();
        _requested = track;
        _pending = true;
        TryStart();
    }

    public override void _Process(double delta)
    {
        if (_a is null || _b is null) return;
        if (_pending) TryStart();
        _a.Step((float)delta);
        _b.Step((float)delta);
    }

    private void EnsureDecks()
    {
        if (_a is not null) return;
        _a = new Deck(this, "DeckA");
        _b = new Deck(this, "DeckB");
    }

    /// <summary>Starts or re-targets the requested track; leaves it pending while its stream isn't ready.</summary>
    private void TryStart()
    {
        if (!IsInsideTree()) return;
        Deck a = _a!, b = _b!;

        if (_requested == Track.None)
        {
            a.FadeTo(0f, CrossfadeSeconds);
            b.FadeTo(0f, CrossfadeSeconds);
            _pending = false;
            return;
        }

        // Already playing (perhaps fading out after a quick change of mind)? Bring it back.
        Deck? owner = a.Owns(_requested) ? a : b.Owns(_requested) ? b : null;
        if (owner is not null)
        {
            owner.FadeTo(1f, CrossfadeSeconds);
            (owner == a ? b : a).FadeTo(0f, CrossfadeSeconds);
            _pending = false;
            return;
        }

        AudioStreamWav? stream = MusicLibrary.TryGet(_requested);
        if (stream is null) return;

        Deck incoming = a.Level <= b.Level ? a : b;
        Deck outgoing = incoming == a ? b : a;
        bool result = _requested is Track.Victory or Track.Defeat;
        incoming.Start(_requested, stream, result ? ResultFadeInSeconds : CrossfadeSeconds);
        outgoing.FadeTo(0f, CrossfadeSeconds);
        _pending = false;
    }

    /// <summary>One of the two players the crossfade alternates between.</summary>
    private sealed class Deck
    {
        private readonly AudioStreamPlayer _player;
        private Track _track = Track.None;
        private float _target;
        private float _rate = 1f;

        public Deck(Node parent, string name)
        {
            _player = new AudioStreamPlayer { Name = name, Bus = AudioBuses.MusicBus, VolumeDb = -80f };
            parent.AddChild(_player);
        }

        /// <summary>Fade position, 0 (silent) to 1 (full).</summary>
        public float Level { get; private set; }

        public bool Owns(Track track) => _track == track && _player.Playing;

        public void Start(Track track, AudioStream stream, float fadeSeconds)
        {
            _player.Stop();
            _player.Stream = stream;
            _track = track;
            Level = 0f;
            Apply();
            FadeTo(1f, fadeSeconds);
            _player.Play();
        }

        public void FadeTo(float target, float seconds)
        {
            _target = target;
            _rate = 1f / MathF.Max(seconds, 0.01f);
        }

        public void Step(float delta)
        {
            if (Level == _target) return;
            Level = Level < _target ? MathF.Min(_target, Level + _rate * delta) : MathF.Max(_target, Level - _rate * delta);
            Apply();
            if (Level <= 0f && _target <= 0f)
            {
                _player.Stop();
                _track = Track.None;
            }
        }

        /// <summary>Equal-power fade curve, so a crossfade doesn't dip in the middle.</summary>
        private void Apply()
        {
            float gain = MathF.Sin(Level * MathF.PI * 0.5f);
            _player.VolumeDb = gain <= 1e-4f ? -80f : Mathf.LinearToDb(gain);
        }
    }
}
