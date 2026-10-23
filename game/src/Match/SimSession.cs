using System;
using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Replay;

namespace ContingencyZero.Game.Match;

/// <summary>
/// Where commands for a tick come from. Single player hands local commands straight to the sim;
/// multiplayer holds a tick back until every peer's commands for it have arrived.
/// </summary>
public interface ICommandChannel
{
    /// <summary>Queues a command from the local player.</summary>
    void Submit(Command command);
    /// <summary>True if every player's commands for <paramref name="tick"/> are known.</summary>
    bool Ready(int tick);
    /// <summary>All commands to apply on <paramref name="tick"/>.</summary>
    List<Command> Take(int tick);
    /// <summary>Called after each tick with the state hash, for desync detection.</summary>
    void AfterTick(int tick, ulong hash) { }
    /// <summary>Pumps the network, if any. Called every frame.</summary>
    void Poll() { }
    /// <summary>Multiplayer clock multiplier: slightly above 1 while catching up with the host.</summary>
    double TimeScale => 1.0;
}

/// <summary>Local play: a command is applied on the next tick.</summary>
public sealed class LocalChannel : ICommandChannel
{
    private readonly List<Command> _pending = new();
    public void Submit(Command command) => _pending.Add(command);
    public bool Ready(int tick) => true;
    public List<Command> Take(int tick)
    {
        var list = _pending.Select(c => c with { Tick = tick }).ToList();
        _pending.Clear();
        return list;
    }
}

/// <summary>Replay playback: commands come from the recording.</summary>
public sealed class ReplayChannel : ICommandChannel
{
    private readonly ILookup<int, Command> _byTick;
    public int LastTick { get; }
    public ReplayChannel(MatchReplay replay)
    {
        _byTick = replay.Commands.ToLookup(c => c.Tick);
        LastTick = replay.TickCount;
    }
    public void Submit(Command command) { }
    public bool Ready(int tick) => tick < LastTick;
    public List<Command> Take(int tick) => _byTick[tick].ToList();
}

/// <summary>
/// Owns a running match: the world, the clock, where commands come from and the replay being
/// recorded. The renderer reads <see cref="World"/> and interpolates with <see cref="Alpha"/>.
/// </summary>
public sealed class SimSession
{
    public const double TickSeconds = 1.0 / World.TicksPerSecond;

    public World World { get; }
    public Scenario Scenario { get; }
    public UnitCatalog Catalog { get; }
    /// <summary>The player this machine controls, or -1 when only watching.</summary>
    public int LocalPlayer { get; }
    public ICommandChannel Channel { get; }
    public bool IsReplay => Channel is ReplayChannel;
    public bool IsMultiplayer { get; init; }

    public bool Paused { get; set; }
    /// <summary>Game speed multiplier (single player and replays only).</summary>
    public double Speed { get; set; } = 1.0;
    /// <summary>How far between the last tick and the next the clock is, 0..1, for smooth rendering.</summary>
    public float Alpha => (float)Math.Clamp(_accumulator / TickSeconds, 0, 1);
    /// <summary>True while waiting on other players' commands.</summary>
    public bool Waiting { get; private set; }

    /// <summary>Raised after every tick, before the next one starts.</summary>
    public event Action? Ticked;
    /// <summary>
    /// Raised after every <see cref="World.Step"/> this session runs, including the ticks a replay
    /// seek rushes through without rendering (which don't raise <see cref="Ticked"/>). For cheap
    /// per-tick bookkeeping such as statistics sampling.
    /// </summary>
    public event Action? Stepped;

    private readonly List<Command> _recorded = new();
    private double _accumulator;
    private int _seekTarget = -1;

    /// <summary>True while a replay is being run forward to <see cref="SeekTarget"/> (see <see cref="SeekTo"/>).</summary>
    public bool Seeking => _seekTarget > World.Tick;
    /// <summary>The tick a seek is heading for, or -1.</summary>
    public int SeekTarget => _seekTarget;

    /// <summary>
    /// Replays only: runs the sim forward to <paramref name="tick"/> as fast as it can over the next
    /// few frames, without rendering the ticks in between. Jumping backwards needs a fresh session
    /// (rebuild it from the replay and seek from the start).
    /// </summary>
    public void SeekTo(int tick)
    {
        if (!IsReplay) return;
        if (Channel is ReplayChannel rc) tick = Math.Min(tick, rc.LastTick);
        if (tick > World.Tick) _seekTarget = tick;
    }

    private void RunSeek()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (World.Tick < _seekTarget && sw.ElapsedMilliseconds < 40)
        {
            int tick = World.Tick;
            if (!Channel.Ready(tick)) { _seekTarget = -1; break; }
            List<Command> commands = Channel.Take(tick);
            _recorded.AddRange(commands);
            World.Step(commands);
            Stepped?.Invoke();
        }
        if (World.Tick >= _seekTarget)
        {
            _seekTarget = -1;
            _accumulator = 0;
            Ticked?.Invoke();
        }
    }

    public SimSession(Scenario scenario, UnitCatalog catalog, int localPlayer, ICommandChannel channel)
    {
        Scenario = scenario;
        Catalog = catalog;
        LocalPlayer = localPlayer;
        Channel = channel;
        World = scenario.CreateWorld(catalog);
    }

    /// <summary>Sends a command from the local player.</summary>
    public void Issue(Command command)
    {
        if (LocalPlayer < 0 || IsReplay) return;
        Channel.Submit(command with { Player = LocalPlayer });
    }

    /// <summary>Advances the clock and runs as many ticks as are due (at most a few per frame).</summary>
    public void Update(double delta)
    {
        Channel.Poll();
        if (_seekTarget > World.Tick) { RunSeek(); return; }
        if (Paused) return;
        _accumulator += delta * (IsMultiplayer ? Channel.TimeScale : Speed);
        int steps = 0;
        int maxSteps = Speed > 2 ? 12 : 4;
        while (_accumulator >= TickSeconds && steps < maxSteps)
        {
            int tick = World.Tick;
            if (!Channel.Ready(tick))
            {
                Waiting = true;
                _accumulator = Math.Min(_accumulator, TickSeconds);
                return;
            }
            Waiting = false;
            List<Command> commands = Channel.Take(tick);
            _recorded.AddRange(commands);
            World.Step(commands);
            Channel.AfterTick(tick, IsMultiplayer && tick % World.TicksPerSecond == 0 ? World.ComputeHash() : 0);
            Stepped?.Invoke();
            Ticked?.Invoke();
            _accumulator -= TickSeconds;
            steps++;
        }
        if (steps == maxSteps && _accumulator > TickSeconds * 2) _accumulator = TickSeconds * 2; // don't spiral
    }

    /// <summary>The match so far, as a replay.</summary>
    public MatchReplay BuildReplay(string title)
    {
        return new MatchReplay(Scenario, _recorded, World.Tick)
        {
            Title = title,
            CatalogFingerprint = Catalog.Fingerprint,
            FinalHash = World.ComputeHash(),
        };
    }
}
