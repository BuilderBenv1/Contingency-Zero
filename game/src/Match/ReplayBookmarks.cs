using System;
using System.Collections.Generic;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Replay;
using ContingencyZero.Sim.State;

namespace ContingencyZero.Game.Match;

/// <summary>
/// Watches a running match and notes the moments worth jumping to in its replay: Commanders under
/// attack or killed, factories lost, enemy waves arriving at a base, players knocked out and the
/// end of the game. Call <see cref="OnTick"/> after every sim tick and <see cref="AttachTo"/> when
/// the replay is saved. Read-only: it never touches the world.
/// </summary>
public sealed class ReplayBookmarks
{
    /// <summary>Ticks between "Commander under attack" bookmarks for one player.</summary>
    private const int CommanderAlertCooldown = 20 * World.TicksPerSecond;
    /// <summary>How often the wave check runs.</summary>
    private const int WaveInterval = World.TicksPerSecond;
    /// <summary>Enemy mobile units near a base that count as a wave.</summary>
    private const int WaveSize = 8;
    /// <summary>Big battle: at least this many completed units destroyed within <see cref="BattleWindow"/> seconds.</summary>
    private const int BattleDeaths = 12;
    private const int BattleWindow = 10;
    /// <summary>At most one big-battle bookmark this often.</summary>
    private const int BattleCooldown = 30 * World.TicksPerSecond;
    /// <summary>Samples kept per player for the wave check: 10 seconds at one per second.</summary>
    private const int WaveHistory = 10;
    /// <summary>Cell size for the structure grid; "near" means within this many map units, give or take.</summary>
    private const int WaveReach = 60;
    private const int WaveCell = 32;

    public List<ReplayBookmark> Items { get; } = new();

    private int _players = -1;
    private int _lastTick = -1;
    private int[] _lastCommanderAlert = Array.Empty<int>();
    private int[] _commanderId = Array.Empty<int>();
    private int[] _commanderHealthRaw = Array.Empty<int>();   // Fix64 raw, clamped to int range
    private int[][] _nearby = Array.Empty<int[]>();          // ring buffer per player
    private int _nearbyIndex;
    private bool _gameOver;
    private readonly int[] _deathsPerSecond = new int[BattleWindow];
    private int _deathSecond = -1;
    private int _lastBattle = -BattleCooldown;
    private bool _battleArmed = true;
    private readonly HashSet<(int, int)> _cells = new();
    private readonly List<int> _defeatedThisTick = new();

    /// <summary>Call once per sim tick with the world after that tick.</summary>
    public void OnTick(World world)
    {
        if (_players != world.Players.Length) Reset(world);
        if (world.Tick == _lastTick) return;
        _lastTick = world.Tick;
        if (_gameOver) return;

        int tick = world.Tick - 1; // the tick whose events we are reading
        UnitStore units = world.Units;
        UnitCatalog catalog = world.Catalog;

        _defeatedThisTick.Clear();
        foreach (GameEvent e in world.Events)
            if (e.Type == GameEventType.PlayerDefeated && e.Other >= 0) _defeatedThisTick.Add(e.Other);

        foreach (GameEvent e in world.Events)
        {
            switch (e.Type)
            {
                case GameEventType.UnitDestroyed when e.Unit >= 0 && e.Value >= 0 && e.Value < catalog.Count:
                {
                    UnitDef def = catalog[e.Value];
                    if (units.Complete[e.Unit]) CountDeath(world.Tick);
                    int owner = e.Other;
                    if (owner < 0 || owner >= _players) break;
                    string who = world.Players[owner].Name;
                    if (def.IsCommander && units.Complete[e.Unit])
                    {
                        bool out_ = _defeatedThisTick.Contains(owner);
                        Add(tick, ReplayBookmarkKind.CommanderKilled, out_ ? $"{who}'s Commander killed - {who} is out" : $"{who}'s Commander killed", owner);
                        _commanderId[owner] = -1;
                    }
                    else if (def.IsFactory && units.Complete[e.Unit] && !_defeatedThisTick.Contains(owner) && !world.Players[owner].Defeated)
                    {
                        Add(tick, ReplayBookmarkKind.FactoryDestroyed, $"{who}'s {def.Name} destroyed", owner);
                    }
                    break;
                }
                case GameEventType.UnitDamaged when e.Unit >= 0 && e.Unit < units.Count:
                {
                    int owner = units.Owner[e.Unit];
                    if (catalog[units.TypeIndex[e.Unit]].IsCommander && units.Alive[e.Unit])
                        CommanderAttacked(world, tick, owner);
                    break;
                }
                case GameEventType.PlayerDefeated when e.Other >= 0 && e.Other < _players:
                {
                    // A Commander kill on the same tick already says it.
                    bool said = false;
                    foreach (ReplayBookmark b in Items)
                        if (b.Tick == tick && b.Kind == ReplayBookmarkKind.CommanderKilled && b.Player == e.Other) said = true;
                    if (!said) Add(tick, ReplayBookmarkKind.PlayerDefeated, $"{world.Players[e.Other].Name} defeated", e.Other);
                    break;
                }
                case GameEventType.GameOver:
                {
                    Add(tick, ReplayBookmarkKind.GameOver, GameOverText(world, e.Value), -1);
                    _gameOver = true;
                    break;
                }
            }
        }
        if (_gameOver) return;

        // The sim doesn't raise UnitDamaged for every hit, so also watch each Commander's health.
        for (int p = 0; p < _players; p++)
        {
            int id = _commanderId[p];
            if (id < 0 || !units.IsValid(id) || !catalog[units.TypeIndex[id]].IsCommander || units.Owner[id] != p)
            {
                id = FindCommander(world, p);
                _commanderId[p] = id;
                _commanderHealthRaw[p] = id >= 0 ? Raw(units.Health[id]) : 0;
                continue;
            }
            int health = Raw(units.Health[id]);
            if (health < _commanderHealthRaw[p]) CommanderAttacked(world, tick, p);
            _commanderHealthRaw[p] = health;
        }

        if (world.Tick % WaveInterval == 0) CheckWaves(world, tick);
        CheckBattle(world, tick);
    }

    private void CountDeath(int worldTick)
    {
        AdvanceDeathClock(worldTick);
        _deathsPerSecond[_deathSecond % BattleWindow]++;
    }

    /// <summary>Moves the per-second death ring buffer up to the current second, clearing skipped seconds.</summary>
    private void AdvanceDeathClock(int worldTick)
    {
        int second = worldTick / World.TicksPerSecond;
        if (_deathSecond < 0) { _deathSecond = second; return; }
        for (int s = _deathSecond + 1; s <= second && s - _deathSecond <= BattleWindow; s++) _deathsPerSecond[s % BattleWindow] = 0;
        if (second - _deathSecond > BattleWindow) Array.Clear(_deathsPerSecond);
        _deathSecond = Math.Max(_deathSecond, second);
    }

    /// <summary>
    /// A big battle: many deaths in the last ten seconds. Fires once per fight: it re-arms when the
    /// killing has died down to under half the threshold, and not more than every 30 seconds.
    /// </summary>
    private void CheckBattle(World world, int tick)
    {
        AdvanceDeathClock(world.Tick);
        int recent = 0;
        foreach (int d in _deathsPerSecond) recent += d;
        if (!_battleArmed)
        {
            if (recent < BattleDeaths / 2) _battleArmed = true;
            return;
        }
        if (recent < BattleDeaths || tick - _lastBattle < BattleCooldown) return;
        _battleArmed = false;
        _lastBattle = tick;
        Add(Math.Max(0, tick - BattleWindow * World.TicksPerSecond / 2), ReplayBookmarkKind.BigBattle, $"Big battle: {recent} units down in {BattleWindow}s", -1);
    }

    /// <summary>Copies the bookmarks into a replay about to be saved.</summary>
    public void AttachTo(MatchReplay replay)
    {
        replay.Bookmarks.Clear();
        replay.Bookmarks.AddRange(System.Linq.Enumerable.OrderBy(Items, b => b.Tick)); // stable: same-tick marks keep their order
    }

    /// <summary>What a replay's "game over" bookmark says, for share text and the end screen.</summary>
    public static string GameOverText(World world, int winningTeam)
    {
        if (winningTeam < 0) return "Game over: draw";
        var names = new List<string>();
        foreach (PlayerState p in world.Players)
            if (p.Team == winningTeam) names.Add(p.Name);
        string who = names.Count == 1 ? names[0] : $"Team {winningTeam + 1} ({string.Join(", ", names)})";
        return $"Game over: {who} wins";
    }

    private void Reset(World world)
    {
        _players = world.Players.Length;
        _lastCommanderAlert = new int[_players];
        _commanderId = new int[_players];
        _commanderHealthRaw = new int[_players];
        _nearby = new int[_players][];
        for (int p = 0; p < _players; p++)
        {
            _lastCommanderAlert[p] = -CommanderAlertCooldown;
            _commanderId[p] = -1;
            _nearby[p] = new int[WaveHistory];
        }
        _nearbyIndex = 0;
        _gameOver = world.IsOver;
        Array.Clear(_deathsPerSecond);
        _deathSecond = -1;
        _lastBattle = -BattleCooldown;
        _battleArmed = true;
    }

    private void Add(int tick, ReplayBookmarkKind kind, string text, int player) =>
        Items.Add(new ReplayBookmark(Math.Max(0, tick), kind, text, player));

    private void CommanderAttacked(World world, int tick, int owner)
    {
        if (owner < 0 || owner >= _players || world.Players[owner].Defeated) return;
        if (tick - _lastCommanderAlert[owner] < CommanderAlertCooldown) return;
        _lastCommanderAlert[owner] = tick;
        Add(tick, ReplayBookmarkKind.CommanderAttacked, $"{world.Players[owner].Name}'s Commander under attack", owner);
    }

    private static int FindCommander(World world, int player)
    {
        UnitStore u = world.Units;
        for (int id = 0; id < u.Count; id++)
            if (u.Alive[id] && u.Owner[id] == player && world.Catalog[u.TypeIndex[id]].IsCommander) return id;
        return -1;
    }

    private static int Raw(Fix64 v) => (int)Math.Clamp(v.Raw, int.MinValue, int.MaxValue);

    /// <summary>
    /// Once a second: for each player, how many enemy mobile combat units are within reach of one
    /// of their structures. A jump from none ten seconds ago to a group now is a wave arriving.
    /// </summary>
    private void CheckWaves(World world, int tick)
    {
        UnitStore u = world.Units;
        UnitCatalog catalog = world.Catalog;
        int oldest = _nearbyIndex; // slot about to be overwritten holds the sample from WaveHistory seconds ago
        for (int p = 0; p < _players; p++)
        {
            PlayerState player = world.Players[p];
            int count = 0;
            if (!player.Defeated)
            {
                _cells.Clear();
                for (int id = 0; id < u.Count; id++)
                {
                    if (!u.Alive[id] || u.Owner[id] != p || !catalog[u.TypeIndex[id]].IsStructure) continue;
                    _cells.Add((u.Position[id].X.ToIntFloor() / WaveCell, u.Position[id].Y.ToIntFloor() / WaveCell));
                }
                if (_cells.Count > 0)
                {
                    int reach = (WaveReach + WaveCell - 1) / WaveCell;
                    for (int id = 0; id < u.Count; id++)
                    {
                        if (!u.Alive[id] || !u.Complete[id]) continue;
                        if (world.Players[u.Owner[id]].Team == player.Team) continue;
                        UnitDef def = catalog[u.TypeIndex[id]];
                        if (!def.CanMove || !def.IsArmed) continue;
                        int cx = u.Position[id].X.ToIntFloor() / WaveCell, cy = u.Position[id].Y.ToIntFloor() / WaveCell;
                        bool near = false;
                        for (int dx = -reach; dx <= reach && !near; dx++)
                            for (int dy = -reach; dy <= reach; dy++)
                                if (_cells.Contains((cx + dx, cy + dy))) { near = true; break; }
                        if (near) count++;
                    }
                }
            }
            int before = _nearby[p][oldest];
            _nearby[p][oldest] = count;
            if (count >= WaveSize && before == 0 && !player.Defeated)
                Add(tick, ReplayBookmarkKind.Wave, $"Wave hits {player.Name} ({count} units)", p);
        }
        _nearbyIndex = (_nearbyIndex + 1) % WaveHistory;
    }
}
