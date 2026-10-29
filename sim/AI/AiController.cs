using System.Collections.Generic;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using ContingencyZero.Sim.Pathing;
using ContingencyZero.Sim.State;

namespace ContingencyZero.Sim.AI;

/// <summary>
/// A computer player. It runs inside the sim on every machine, reads the world the way a player
/// would (through fog, except on Brutal), and issues ordinary <see cref="Command"/>s, so it can
/// never do anything a human couldn't. It is fully deterministic: same match, same decisions.
///
/// Each think it: keeps builders busy (metal spots, energy, factories, defences, tier 2), keeps
/// factories producing a sensible mix, gathers an army at a rally point and sends it in waves,
/// pulls the army home when the base is attacked, and on Hard and up uses the Commander's
/// Breach Cannon on anything that gets close.
/// </summary>
public sealed class AiController
{
    private readonly World _w;
    private readonly int _player;
    private readonly int _team;
    private readonly AiDifficulty _difficulty;
    private readonly DeterministicRng _rng;
    private readonly string _faction;
    private readonly int _interval;
    private readonly int _offset;

    private FixVec2 _home;
    private FixVec2 _enemyGuess;
    private int _waveSize;
    private int _wavesSent;
    private int _lastWaveTick = -100000;
    private int _scoutIndex;
    private int _preferredFactory; // 0 = vehicles, 1 = bots
    private readonly List<(int Unit, FixVec2 Pos)> _knownEnemyStructures = new();
    private bool _enemyAirSeen;
    private int _attackTargetId = -1;
    private FixVec2 _attackTargetPos;
    private bool _islandMode;
    private readonly List<Command> _out = new();

    // An attack wave first gathers at a staging point short of the target, then goes in together,
    // so fast units don't arrive alone and get picked off.
    private readonly List<int> _wave = new();
    private FixVec2 _waveStage;
    private int _waveStageTick = -1;

    public int Player => _player;
    /// <summary>Where this player's base is (its Commander's start, or its first unit).</summary>
    public FixVec2 Home => _home;
    public AiDifficulty Difficulty => _difficulty;
    public string LastPlan { get; private set; } = "";
    internal int CurrentAttackTargetId => _attackTargetId;
    internal FixVec2 CurrentAttackTarget => _attackTargetPos;
    internal bool IslandMode => _islandMode;

    public AiController(World world, int player, AiDifficulty difficulty, ulong matchSeed)
    {
        _w = world;
        _player = player;
        _team = world.Players[player].Team;
        _difficulty = difficulty;
        _faction = world.Players[player].Faction;
        _rng = new DeterministicRng(matchSeed ^ (0x9E3779B97F4A7C15UL * (ulong)(player + 1)));
        _interval = difficulty switch
        {
            AiDifficulty.Easy => 45,
            AiDifficulty.Normal => 22,
            AiDifficulty.Hard => 10,
            _ => 8,
        };
        _offset = (player * 3) % _interval;
        _waveSize = difficulty switch
        {
            AiDifficulty.Easy => 6,
            AiDifficulty.Normal => 10,
            AiDifficulty.Hard => 12,
            _ => 14,
        };
        _preferredFactory = _rng.Range(0, 2);

        _home = FindHome();
        _enemyGuess = GuessEnemyBase();
        _attackTargetPos = _enemyGuess;
        _islandMode = !LandConnected(_enemyGuess);
    }

    private bool Sees(int unit) => _difficulty == AiDifficulty.Brutal || _w.CanSee(_team, unit);

    private FixVec2 FindHome()
    {
        for (int id = 0; id < _w.Units.Count; id++)
            if (_w.Units.Alive[id] && _w.Units.Owner[id] == _player && _w.Catalog[_w.Units.TypeIndex[id]].IsCommander)
                return _w.Units.Position[id];
        for (int id = 0; id < _w.Units.Count; id++)
            if (_w.Units.Alive[id] && _w.Units.Owner[id] == _player)
                return _w.Units.Position[id];
        return new FixVec2(Fix64.FromInt(_w.MapWidth / 2), Fix64.FromInt(_w.MapHeight / 2));
    }

    /// <summary>The start position furthest from home, as a first guess at where the enemy is.</summary>
    private FixVec2 GuessEnemyBase()
    {
        FixVec2 best = new(Fix64.FromInt(_w.MapWidth) - _home.X, Fix64.FromInt(_w.MapHeight) - _home.Y);
        Fix64 bestD = Fix64.Zero;
        foreach (FixVec2 s in _w.Map.StartPositions)
        {
            Fix64 d = FixVec2.DistanceSquared(s, _home);
            if (d > bestD) { bestD = d; best = s; }
        }
        // Prefer the actual enemy Commander's start if it was ever visible.
        return best;
    }

    public ulong StateHash()
    {
        var h = new StateHasher();
        h.Add(_rng.State);
        h.Add(_waveSize);
        h.Add(_wavesSent);
        h.Add(_lastWaveTick);
        h.Add(_knownEnemyStructures.Count);
        h.Add(_wave.Count);
        h.Add(_waveStageTick);
        h.Add(_attackTargetId);
        h.Add(_attackTargetPos);
        h.Add(_islandMode);
        return h.Value;
    }

    internal void Update(List<Command> output)
    {
        if (_w.Players[_player].Defeated) return;
        if (_w.Tick % _interval != _offset) return;
        _out.Clear();
        Think();
        output.AddRange(_out);
    }

    private void Issue(CommandType type, int unit, FixVec2 pos = default, int target = -1, int typeIndex = -1, bool queue = false, int arg = 0) =>
        _out.Add(new Command(_w.Tick + 1, _player, type, unit, pos, target, typeIndex, queue, arg));

    // ---- Survey ----------------------------------------------------------------------------------

    private readonly List<int> _commanders = new();
    private readonly List<int> _builders = new();
    private readonly List<int> _factories = new();
    private readonly List<int> _army = new();
    private readonly List<int> _frames = new();
    private readonly Dictionary<string, int> _roleCount = new();
    private int _extractors;

    private int RoleCount(string role) => _roleCount.TryGetValue(role, out int n) ? n : 0;

    private void Survey()
    {
        _commanders.Clear(); _builders.Clear(); _factories.Clear(); _army.Clear(); _frames.Clear();
        _roleCount.Clear();
        _extractors = 0;
        UnitStore u = _w.Units;
        for (int id = 0; id < u.Count; id++)
        {
            if (!u.Alive[id] || u.Owner[id] != _player) continue;
            UnitDef def = _w.Catalog[u.TypeIndex[id]];
            _roleCount[def.Role] = RoleCount(def.Role) + 1;
            if (!u.Complete[id])
            {
                if (def.IsStructure) _frames.Add(id);
                continue;
            }
            if (def.ExtractorMultiplier > Fix64.Zero) _extractors++;
            if (def.IsCommander) _commanders.Add(id);
            else if (def.IsFactory) _factories.Add(id);
            else if (def.CanBuild || (def.CanReclaim && !def.IsArmed && def.CanMove)) _builders.Add(id);
            else if (def.IsArmed && def.CanMove) _army.Add(id);
        }

        // Remember enemy structures we've seen; forget ones seen to be gone.
        for (int id = 0; id < u.Count; id++)
        {
            if (!u.Alive[id] || !_w.AreEnemies(_player, u.Owner[id])) continue;
            UnitDef def = _w.Catalog[u.TypeIndex[id]];
            if (!Sees(id)) continue;
            if (def.IsAir && def.IsArmed) _enemyAirSeen = true;
            if (!def.IsStructure) continue;
            bool known = false;
            foreach (var k in _knownEnemyStructures) if (k.Unit == id) { known = true; break; }
            if (!known) _knownEnemyStructures.Add((id, u.Position[id]));
        }
        _knownEnemyStructures.RemoveAll(k => !u.Alive[k.Unit]);
    }

    // ---- Think -----------------------------------------------------------------------------------

    private void Think()
    {
        Survey();

        DefendBase(out bool underAttack);
        UseCommander(underAttack);
        RunBuilders();
        RunFactories();
        RunArmy(underAttack);
    }

    private PlayerState Me => _w.Players[_player];

    private int MaxBuilders => _difficulty switch
    {
        AiDifficulty.Easy => 2,
        AiDifficulty.Normal => 5,
        AiDifficulty.Hard => 7,
        _ => 9,
    };

    private int MaxExtractors => _difficulty == AiDifficulty.Easy ? 6 : 99;

    /// <summary>Metal per second (the player state holds per-tick values).</summary>
    private Fix64 MetalPerSecond => Me.MetalIncome * World.TicksPerSecond;
    private Fix64 EnergyPerSecond => Me.EnergyIncome * World.TicksPerSecond;

    private int Minutes => _w.Tick / (60 * World.TicksPerSecond);

    // ---- Commander -------------------------------------------------------------------------------

    private void UseCommander(bool underAttack)
    {
        if (_commanders.Count == 0) return;
        int c = _commanders[0];
        UnitStore u = _w.Units;
        UnitDef def = _w.Catalog[u.TypeIndex[c]];

        // Breach Cannon anything close (Hard and up; Normal only in self-defence).
        if (_difficulty >= AiDifficulty.Normal && Me.Energy > Fix64.FromInt(700))
        {
            Fix64 reach = Fix64.FromInt(_difficulty >= AiDifficulty.Hard ? 22 : 16);
            int victim = _w.Grid.FindNearest(u, u.Position[c], reach, e =>
                u.Alive[e] && _w.AreEnemies(_player, u.Owner[e]) && Sees(e) && !_w.Catalog[u.TypeIndex[e]].IsAir
                && u.Complete[e]);
            if (victim >= 0 && (_difficulty >= AiDifficulty.Hard || u.LastAttacker[c] >= 0))
            {
                Issue(CommandType.DGun, c, u.Position[victim]);
                return;
            }
        }

        // Keep the Commander out of fights it would lose: if hurt and the base is under attack, walk home.
        if (u.Health[c] < def.MaxHealth / 3 && FixVec2.DistanceSquared(u.Position[c], _home) > Fix64.FromInt(30 * 30))
        {
            Issue(CommandType.Move, c, _home);
            return;
        }

        if (u.Orders[c].Count == 0) GiveBuildJob(c, def, isCommander: true);
    }

    // ---- Builders --------------------------------------------------------------------------------

    private readonly List<FixVec2> _claimedSites = new();

    private void RunBuilders()
    {
        _claimedSites.Clear();
        foreach (int b in _builders)
        {
            Order o = _w.Units.CurrentOrder(b);
            if (o.Type == OrderType.Build) _claimedSites.Add(o.Position);
        }
        foreach (int c in _commanders)
        {
            Order o = _w.Units.CurrentOrder(c);
            if (o.Type == OrderType.Build) _claimedSites.Add(o.Position);
        }

        // Out of energy: pull a few builders off whatever they're doing and put up power instead.
        bool energyStall = Me.Energy < Me.EnergyStorage / 8 && Me.BuildRatio < Fix64.Parse("0.6");
        int redirected = 0;
        if (energyStall)
        {
            foreach (int b in _builders)
            {
                if (redirected >= 3) break;
                UnitDef def = _w.Catalog[_w.Units.TypeIndex[b]];
                if (!def.CanBuild || IsWorkingOnEnergy(b)) continue;
                if (TryBuild(b, def, EnergyRole())) redirected++;
            }
        }

        // Island maps are won at sea: a naval builder refits the shipyard as soon as another factory
        // can cover for it, even if it is busy helping elsewhere.
        if (_islandMode && _difficulty >= AiDifficulty.Normal && Minutes >= 8 && !energyStall
            && RoleCount("factory_adv_ship") == 0
            && RoleCount("factory_bot") + RoleCount("factory_vehicle") + RoleCount("factory_air") + RoleCount("factory_ship") >= 2)
        {
            foreach (int b in _builders)
            {
                UnitDef def = _w.Catalog[_w.Units.TypeIndex[b]];
                if (RefitRole(def) != "factory_adv_ship" || _w.Units.CurrentOrder(b).Type == OrderType.Build) continue;
                if (TryBuild(b, def, "factory_adv_ship")) break;
            }
        }

        foreach (int b in _builders)
        {
            if (_w.Units.Orders[b].Count > 0) continue;
            UnitDef def = _w.Catalog[_w.Units.TypeIndex[b]];
            if (!def.CanBuild)
            {
                // Salvagers: reclaim the nearest wreck near home.
                ReclaimNear(b, Fix64.FromInt(140));
                continue;
            }
            GiveBuildJob(b, def, isCommander: false);
        }
    }

    /// <summary>True if the builder is building, or helping build, something that makes energy.</summary>
    private bool IsWorkingOnEnergy(int builder)
    {
        UnitStore u = _w.Units;
        Order o = u.CurrentOrder(builder);
        int type = o.Type switch
        {
            OrderType.Build => o.TypeIndex,
            OrderType.Assist when u.IsValid(o.Target) => u.TypeIndex[o.Target],
            _ => -1,
        };
        if (type < 0) return false;
        UnitDef d = _w.Catalog[type];
        return d.EnergyIncomePerTick > Fix64.Zero || d.UsesWind;
    }

    private void GiveBuildJob(int builder, UnitDef def, bool isCommander)
    {
        // Help finish anything already started first -- except, when energy has run dry, only
        // help with things that make energy, or every builder ends up pouring a trickle into
        // frames that can never finish.
        bool energyStall = Me.Energy < Me.EnergyStorage / 8 && Me.BuildRatio < Fix64.Parse("0.8");

        foreach (int f in _frames)
        {
            UnitDef fdef = _w.Catalog[_w.Units.TypeIndex[f]];
            if (energyStall && fdef.EnergyIncomePerTick <= Fix64.Zero && !fdef.UsesWind) continue;
            if (FixVec2.DistanceSquared(_w.Units.Position[f], _w.Units.Position[builder]) < Fix64.FromInt(160 * 160))
            {
                Issue(CommandType.Assist, builder, target: f);
                return;
            }
        }

        string? role = ChooseStructure(builder, def, isCommander);
        if (role is not null && TryBuild(builder, def, role)) return;

        // Nothing to build: reclaim wrecks (Normal+) or help a factory.
        if (_difficulty >= AiDifficulty.Normal && ReclaimNear(builder, Fix64.FromInt(90))) return;
        if (!isCommander && _factories.Count > 0)
        {
            int f = _factories[_rng.Range(0, _factories.Count)];
            Issue(CommandType.Assist, builder, target: f);
        }
    }

    private bool ReclaimNear(int unit, Fix64 radius)
    {
        int best = -1;
        Fix64 bestD = radius * radius;
        foreach (Feature f in _w.Features)
        {
            if (f.Gone || f.Metal < Fix64.FromInt(15)) continue;
            Fix64 d = FixVec2.DistanceSquared(f.Position, _home);
            if (d < bestD) { bestD = d; best = f.Id; }
        }
        if (best < 0) return false;
        // Clear the whole patch round it (wrecks lie in heaps, structures leave rubble) in one order.
        Issue(CommandType.ReclaimArea, unit, _w.Features[best].Position, arg: 30);
        return true;
    }

    /// <summary>Picks what this builder should put up next, by role name, or null for nothing.</summary>
    private string? ChooseStructure(int builderId, UnitDef builder, bool isCommander)
    {
        bool hasRole(string r) => Option(builder, r) >= 0;
        Fix64 metal = MetalPerSecond, energy = EnergyPerSecond;
        // Includes frames and jobs handed out earlier this think.
        int factories = RoleCount("factory_bot") + RoleCount("factory_vehicle") + RoleCount("factory_air") + RoleCount("factory_ship")
                        + AdvancedFactories();
        bool advanced = builder.Tier >= 2;

        if (advanced)
        {
            // Advanced builders specialise (industry, fortification, ...): take the first job on
            // the list this one can do.
            if (hasRole("fusion_reactor") && energy < metal * 14 && RoleCount("fusion_reactor") < 4) return "fusion_reactor";
            if (hasRole("deep_extractor") && HasUpgradeableExtractor()) return "deep_extractor";
            if (hasRole("factory_gantry") && !_islandMode && _difficulty >= AiDifficulty.Hard && RoleCount("factory_gantry") == 0 && metal > Fix64.FromInt(25)) return "factory_gantry";
            if (hasRole("heavy_turret") && RoleCount("heavy_turret") < 2 + Minutes / 6) return "heavy_turret";
            if (hasRole("plasma_battery") && _difficulty == AiDifficulty.Brutal && RoleCount("plasma_battery") < 2 && metal > Fix64.FromInt(30)) return "plasma_battery";
            if (hasRole("fusion_reactor") && (energy < metal * 20 || !hasRole("heavy_turret"))) return "fusion_reactor";
            if (hasRole("heavy_turret")) return "heavy_turret";
            return energy < metal * 10 ? "solar_collector" : "metal_maker";
        }

        // Everything costs roughly ten times as much energy as metal, so keep energy income well ahead.
        bool energyLow = energy < metal * 7 + Fix64.FromInt(20) || Me.Energy < Me.EnergyStorage / 4;
        bool metalFloating = Me.Metal > Me.MetalStorage * 7 / 10;
        bool energyFloating = Me.Energy > Me.EnergyStorage * 7 / 10;
        bool energyCritical = Me.Energy < Me.EnergyStorage / 8 && (energy < metal * 9 || metalFloating || Me.BuildRatio < Fix64.Parse("0.9"));
        bool metalLow = Me.Metal < Me.MetalStorage / 5;

        // On a separated enemy island, the first factory must be able to project force across
        // water. Commanders and basic builders can all place the shipyard; FindSite handles water.
        if (_islandMode && hasRole("factory_ship") && RoleCount("factory_ship") + RoleCount("factory_adv_ship") == 0
            && (_extractors >= 2 || _w.Tick > 60 * World.TicksPerSecond))
            return "factory_ship";

        // First factory early.
        if (factories == 0 && (_extractors >= 2 || _w.Tick > 60 * World.TicksPerSecond))
            return _preferredFactory == 0 ? "factory_vehicle" : "factory_bot";

        if (energyCritical) return EnergyRole();
        // Keep energy roughly in step with metal: most things cost several times more energy.
        if (energy < metal * 5 + Fix64.FromInt(20) && !energyFloating) return EnergyRole();
        if (_extractors < MaxExtractors && (metalLow || _extractors < 4 || (!metalFloating && _rng.Range(0, 2) == 0))
            && FreeSpotNear(_w.Units.Position[builderId]) is not null)
            return "metal_extractor";
        if (energyLow && !energyFloating) return EnergyRole();
        // Turn surplus energy into metal.
        if (energyFloating && metalLow && Minutes >= 3 && RoleCount("metal_maker") < 4 + Minutes) return "metal_maker";

        // More production as the economy grows.
        int wanted = _difficulty switch
        {
            AiDifficulty.Easy => 1,
            AiDifficulty.Normal => metal > Fix64.FromInt(9) ? 2 : 1,
            _ => metal > Fix64.FromInt(18) ? 3 : metal > Fix64.FromInt(8) ? 2 : 1,
        };
        if ((factories < wanted && !metalLow) || (metalFloating && energyFloating && factories < wanted + 2))
        {
            if (_difficulty >= AiDifficulty.Hard && RoleCount("factory_air") + RoleCount("factory_adv_air") == 0 && factories >= 2) return "factory_air";
            if (RoleCount("factory_bot") + RoleCount("factory_adv_bot") == 0) return "factory_bot";
            if (RoleCount("factory_vehicle") + RoleCount("factory_adv_vehicle") == 0) return "factory_vehicle";
            return _preferredFactory == 0 ? "factory_vehicle" : "factory_bot";
        }

        // Storage when wasting.
        if (Me.MetalWasted > Fix64.Zero && RoleCount("metal_storage") < 2) return "metal_storage";
        if (Me.EnergyWasted > Fix64.Zero && energy > metal * 20 && RoleCount("metal_maker") < 4 + Minutes) return "metal_maker";

        // Defences.
        if (_difficulty >= AiDifficulty.Normal && RoleCount("light_turret") < 1 + Minutes / 3 && Minutes >= 3) return "light_turret";
        if (_enemyAirSeen && RoleCount("aa_turret") < 2 + Minutes / 6) return "aa_turret";
        if (_difficulty >= AiDifficulty.Normal && RoleCount("radar_tower") == 0 && Minutes >= 2) return "radar_tower";

        // Tier 2 once the economy can carry it.
        // A tier 1 builder can only refit its own kind of factory. Normal gets one advanced factory,
        // Hard and up a second once the first is running.
        if (!isCommander && _difficulty >= AiDifficulty.Normal && RefitRole(builder) is { } refit
            && AdvancedFactories() < (_difficulty >= AiDifficulty.Hard && Minutes >= 14 ? 2 : 1)
            && RoleCount(refit) == 0 && RefitHost(Option(builder, refit)) >= 0
            // A refit takes its factory offline: keep something else producing. On an island map the
            // navy comes first, so the shipyard is the first refit.
            && factories >= 2
            && (!_islandMode || refit == "factory_adv_ship" || RoleCount("factory_adv_ship") > 0)
            && metal > Fix64.FromInt(_difficulty == AiDifficulty.Normal ? 18 : 12))
            return refit;

        if (energy < metal * 10) return EnergyRole();
        if (energyFloating && RoleCount("metal_maker") < 4 + Minutes) return "metal_maker";
        return null;
    }

    private string EnergyRole()
    {
        Fix64 avgWind = (_w.Map.WindMin + _w.Map.WindMax) / 2;
        return avgWind >= Fix64.FromInt(13) ? "wind_turbine" : "solar_collector";
    }

    private int CountFrames(string rolePrefix)
    {
        int n = 0;
        foreach (int f in _frames)
            if (_w.Catalog[_w.Units.TypeIndex[f]].Role.StartsWith(rolePrefix, System.StringComparison.Ordinal)) n++;
        return n;
    }

    private bool HasUpgradeableExtractor()
    {
        UnitStore u = _w.Units;
        for (int id = 0; id < u.Count; id++)
            if (u.Alive[id] && u.Owner[id] == _player && u.Complete[id] && _w.Catalog[u.TypeIndex[id]].Role == "metal_extractor")
                return true;
        return false;
    }

    /// <summary>The advanced factory this builder can refit, by role, or null.</summary>
    private string? RefitRole(UnitDef builder)
    {
        foreach (int o in builder.BuildOptions)
            if (_w.Catalog[o].UpgradesFrom.Length > 0) return _w.Catalog[o].Role;
        return null;
    }

    private int AdvancedFactories() =>
        RoleCount("factory_adv_bot") + RoleCount("factory_adv_vehicle") + RoleCount("factory_adv_air") + RoleCount("factory_adv_ship");

    /// <summary>The first of our finished factories a refit of <paramref name="type"/> can go over, or -1.</summary>
    private int RefitHost(int type)
    {
        if (type < 0) return -1;
        string host = _w.Catalog[type].UpgradesFrom;
        UnitStore u = _w.Units;
        for (int id = 0; id < u.Count; id++)
            if (u.Alive[id] && u.Owner[id] == _player && u.Complete[id] && _w.Catalog[u.TypeIndex[id]].Role == host)
                return id;
        return -1;
    }

    private int Option(UnitDef builder, string role)
    {
        foreach (int o in builder.BuildOptions)
            if (_w.Catalog[o].Role == role) return o;
        return -1;
    }

    private bool TryBuild(int builder, UnitDef def, string role)
    {
        int type = Option(def, role);
        if (type < 0) return false;
        UnitDef tdef = _w.Catalog[type];
        FixVec2? site;
        if (tdef.UpgradesFrom.Length > 0)
        {
            int host = RefitHost(type);
            site = host >= 0 ? _w.Units.Position[host] : null;
        }
        else if (tdef.NeedsMetalSpot)
            site = role == "deep_extractor" ? OwnExtractorSite() : FreeSpotNear(_w.Units.Position[builder]);
        else
            site = FindSite(type, tdef);
        if (site is null) return false;
        _claimedSites.Add(site.Value);
        Issue(CommandType.Build, builder, site.Value, typeIndex: type);
        // Count it straight away so other builders deciding this think don't pick the same thing.
        _roleCount[role] = RoleCount(role) + 1;
        if (tdef.ExtractorMultiplier > Fix64.Zero && role != "deep_extractor") _extractors++;
        LastPlan = $"build {tdef.Name}";
        return true;
    }

    private FixVec2? OwnExtractorSite()
    {
        UnitStore u = _w.Units;
        int best = -1;
        Fix64 bestD = Fix64.MaxValue;
        for (int id = 0; id < u.Count; id++)
        {
            if (!u.Alive[id] || u.Owner[id] != _player || !u.Complete[id]) continue;
            if (_w.Catalog[u.TypeIndex[id]].Role != "metal_extractor") continue;
            if (IsClaimed(u.Position[id])) continue;
            Fix64 d = FixVec2.DistanceSquared(u.Position[id], _home);
            if (d < bestD) { bestD = d; best = id; }
        }
        return best >= 0 ? u.Position[best] : null;
    }

    private bool IsClaimed(FixVec2 p)
    {
        foreach (FixVec2 c in _claimedSites)
            if (FixVec2.DistanceSquared(c, p) < Fix64.FromInt(64)) return true;
        return false;
    }

    /// <summary>Nearest metal spot to <paramref name="from"/> that is free, safe-looking and not claimed.</summary>
    private FixVec2? FreeSpotNear(FixVec2 from)
    {
        int extractor = -1;
        foreach (int o in AllExtractorTypes()) { extractor = o; break; }
        if (extractor < 0) return null;

        FixVec2? best = null;
        Fix64 bestD = Fix64.MaxValue;
        Fix64 maxDist = Fix64.FromInt(_difficulty == AiDifficulty.Easy ? 160 : 400);
        foreach (MetalSpot s in _w.Map.MetalSpots)
        {
            if (IsClaimed(s.Position)) continue;
            if (FixVec2.DistanceSquared(s.Position, _home) > maxDist * maxDist) continue;
            if (_w.CanPlace(extractor, _player, s.Position) != PlacementResult.Ok) continue;
            if (EnemyNear(s.Position, Fix64.FromInt(40))) continue;
            // Weigh distance from the builder and from home so builders don't wander off.
            Fix64 d = FixVec2.DistanceSquared(s.Position, from) + FixVec2.DistanceSquared(s.Position, _home) / 2;
            if (d < bestD) { bestD = d; best = s.Position; }
        }
        return best;
    }

    private IEnumerable<int> AllExtractorTypes()
    {
        for (int i = 0; i < _w.Catalog.Count; i++)
            if (_w.Catalog[i].Role == "metal_extractor" && _w.Catalog[i].Faction == _faction) yield return i;
    }

    private bool EnemyNear(FixVec2 p, Fix64 radius)
    {
        UnitStore u = _w.Units;
        int e = _w.Grid.FindNearest(u, p, radius, id =>
            u.Alive[id] && _w.AreEnemies(_player, u.Owner[id]) && Sees(id) && _w.Catalog[u.TypeIndex[id]].IsArmed);
        return e >= 0;
    }

    /// <summary>
    /// A spot near home for a structure, searched in rings outwards. Sites sit on a grid with gaps
    /// so the base keeps walkable lanes, avoid metal spots, and lean toward the front for defences.
    /// </summary>
    private FixVec2? FindSite(int type, UnitDef tdef)
    {
        int fp = tdef.Footprint;
        int step = fp + 1;
        int hx = MapData.TileOf(_home.X), hy = MapData.TileOf(_home.Y);
        bool defence = tdef.IsArmed;
        FixVec2 toward = _enemyGuess - _home;

        for (int ring = 1; ring < 20; ring++)
        {
            FixVec2? best = null;
            Fix64 bestScore = Fix64.MaxValue;
            for (int dy = -ring; dy <= ring; dy++)
            for (int dx = -ring; dx <= ring; dx++)
            {
                if (Math2.Abs(dx) != ring && Math2.Abs(dy) != ring) continue;
                int tx = hx + dx * step - fp / 2, ty = hy + dy * step - fp / 2;
                var centre = Terrain.FootprintCentre(tx, ty, fp);
                if (IsClaimed(centre)) continue;
                if (_w.CanPlace(type, _player, centre, out int ox, out int oy) != PlacementResult.Ok) continue;
                if (!tdef.NeedsMetalSpot && _w.MetalUnderFootprint(ox - 1, oy - 1, fp + 2) > Fix64.Zero) continue;
                if (!LanesClear(ox, oy, fp)) continue;

                Fix64 score = Fix64.FromInt(dx * dx + dy * dy);
                FixVec2 off = centre - _home;
                Fix64 dot = off.X * toward.X + off.Y * toward.Y;
                // Defences lean toward the enemy; everything else leans away, so the base grows
                // the same way whichever side of the map it's on (no scan-order bias).
                if (defence) score -= dot / 4000;
                else score += dot / 16000;
                if (score < bestScore) { bestScore = score; best = centre; }
            }
            if (best is not null) return best;
        }
        return null;
    }

    /// <summary>Keeps a one-tile walkable border round new structures so units don't get boxed in.</summary>
    private bool LanesClear(int tx, int ty, int fp)
    {
        for (int y = ty - 1; y <= ty + fp; y++)
        for (int x = tx - 1; x <= tx + fp; x++)
        {
            if (x >= tx && x < tx + fp && y >= ty && y < ty + fp) continue;
            if (!_w.Terrain.InBounds(x, y)) continue;
            if (_w.Terrain.Occupant[_w.Terrain.Index(x, y)] >= 0) return false;
        }
        return true;
    }

    // ---- Factories -------------------------------------------------------------------------------

    private void RunFactories()
    {
        int builders = _builders.Count + CountQueued(isBuilder: true);

        foreach (int f in _factories)
        {
            UnitStore u = _w.Units;
            UnitDef def = _w.Catalog[u.TypeIndex[f]];
            FixVec2 rally = def.Role is "factory_ship" or "factory_adv_ship" ? NavalRallyPoint(u.Position[f]) : RallyPoint();
            if (!u.HasRally[f] || FixVec2.DistanceSquared(u.Rally[f], rally) > Fix64.FromInt(400))
                Issue(CommandType.SetRally, f, rally);
            if (u.FactoryQueue[f].Count > 0) continue;
            // Badly stalled: let the builders catch the economy up before adding more.
            if (Me.BuildRatio < Fix64.Parse(_difficulty <= AiDifficulty.Normal ? "0.5" : "0.35")) continue;

            int choice = ChooseUnit(def, ref builders);
            if (choice >= 0) Issue(CommandType.FactoryQueue, f, typeIndex: choice, arg: 1);
        }
    }

    private int AdvancedBuilders() =>
        RoleCount("builder_adv") + RoleCount("builder_adv_bot") + RoleCount("builder_adv_air") + RoleCount("builder_adv_sea");

    private int CountQueued(bool isBuilder)
    {
        int n = 0;
        foreach (int f in _factories)
            foreach (int t in _w.Units.FactoryQueue[f])
                if (_w.Catalog[t].CanBuild == isBuilder) n++;
        return n;
    }

    private int ChooseUnit(UnitDef factory, ref int builders)
    {
        int[] options = factory.BuildOptions;
        if (options.Length == 0) return -1;

        // Builders first, up to the cap. An island shipyard needs workers, but must not spend
        // its whole early production queue filling the ordinary land-builder quota.
        int builderCap = _islandMode && factory.Role is "factory_ship" or "factory_adv_ship" ? Math2.Min(2, MaxBuilders) : MaxBuilders;
        // An advanced factory's first job is its advanced builder: it unlocks this branch's tier 2 structures.
        if (AdvancedBuilders() < (_difficulty >= AiDifficulty.Hard ? 2 : 1))
            foreach (int o in options)
                if (_w.Catalog[o].CanBuild && _w.Catalog[o].Tier >= 2)
                {
                    _roleCount[_w.Catalog[o].Role] = RoleCount(_w.Catalog[o].Role) + 1;
                    return o;
                }
        if (builders < builderCap)
        {
            foreach (int o in options)
            {
                UnitDef d = _w.Catalog[o];
                if (d.CanBuild && (d.Tier >= 2 || builders < builderCap))
                {
                    if (d.Tier >= 2 && AdvancedBuilders() >= 2) continue;
                    builders++;
                    return o;
                }
            }
        }

        // Weighted pick of fighting units.
        var weights = new List<(int Type, int Weight)>();
        foreach (int o in options)
        {
            UnitDef d = _w.Catalog[o];
            if (!d.IsArmed) continue;
            int w = d.Role switch
            {
                "scout_bot" => _army.Count < 3 ? 2 : 0,
                "assault_bot" => 6,
                "mortar_bot" => 2,
                "aa_bot" => _enemyAirSeen ? 4 : 1,
                "tank" => 7,
                "artillery_vehicle" => _difficulty >= AiDifficulty.Normal ? 2 : 1,
                "flak_vehicle" => _enemyAirSeen ? 4 : 0,
                "fighter" => _enemyAirSeen ? 5 : 2,
                "bomber" => 3,
                "gunship" => 4,
                "patrol_boat" => 5,
                "destroyer" => 4,
                "battleship" => 1,
                "heavy_assault" => 6,
                "long_artillery" => 2,
                "amphibious" => 2,
                "heavy_bot" => 6,
                "sniper_bot" => 2,
                "missile_tank" => _enemyAirSeen ? 5 : 3,
                "interceptor" => _enemyAirSeen ? 5 : 1,
                "heavy_bomber" => 2,
                "cruiser" => 3,
                "missile_ship" => 2,
                "experimental" => 10,
                _ => 2,
            };
            if (_islandMode)
            {
                if (d.MoveClass == MoveClass.Ship) w *= 6;
                else if (d.MoveClass == MoveClass.Amphibious || d.IsAir) w *= 4;
                else w = Math2.Max(1, w / 4);
            }
            if (w > 0) weights.Add((o, w));
        }
        if (weights.Count == 0) return -1;
        int total = 0;
        foreach (var (_, w) in weights) total += w;
        int roll = _rng.Range(0, total);
        foreach (var (t, w) in weights)
        {
            if (roll < w) return t;
            roll -= w;
        }
        return weights[0].Type;
    }

    // ---- Army ------------------------------------------------------------------------------------

    private FixVec2 RallyPoint()
    {
        FixVec2 dir = _enemyGuess - _home;
        Fix64 len = dir.Length();
        if (len <= Fix64.Zero) return _home;
        FixVec2 p = _home + dir * (Fix64.FromInt(55) / len);
        return _w.ClampToMap(p);
    }

    private FixVec2 NavalRallyPoint(FixVec2 origin)
    {
        FixVec2 dir = _enemyGuess - origin;
        Fix64 len = dir.Length();
        FixVec2 desired = len <= Fix64.Zero ? origin : origin + dir * (Fix64.FromInt(55) / len);
        desired = _w.ClampToMap(desired);
        return _w.Pathfinder.NearestPassable(MoveClass.Ship, desired,
            Math2.Max(_w.Map.TilesX, _w.Map.TilesY));
    }

    private FixVec2 RallyPointFor(int unit)
    {
        UnitDef def = _w.Catalog[_w.Units.TypeIndex[unit]];
        return def.MoveClass == MoveClass.Ship ? NavalRallyPoint(_w.Units.Position[unit]) : RallyPoint();
    }

    private void DefendBase(out bool underAttack)
    {
        underAttack = false;
        UnitStore u = _w.Units;
        Fix64 radius = Fix64.FromInt(90);
        int intruder = _w.Grid.FindNearest(u, _home, radius, e =>
            u.Alive[e] && _w.AreEnemies(_player, u.Owner[e]) && Sees(e) && _w.Catalog[u.TypeIndex[e]].IsArmed);
        if (intruder < 0) return;
        underAttack = true;
        FixVec2 at = u.Position[intruder];
        foreach (int a in _army)
        {
            Order o = u.CurrentOrder(a);
            bool committedFar = o.Type == OrderType.Fight && FixVec2.DistanceSquared(o.Position, _home) > Fix64.FromInt(200 * 200)
                                && FixVec2.DistanceSquared(u.Position[a], _home) > Fix64.FromInt(200 * 200);
            if (committedFar) continue; // let the attack wave carry on
            if (o.Type == OrderType.Fight && FixVec2.DistanceSquared(o.Position, at) < Fix64.FromInt(30 * 30)) continue;
            Issue(CommandType.Fight, a, at);
        }
    }

    private void RunArmy(bool underAttack)
    {
        UnitStore u = _w.Units;

        if (underAttack && _waveStageTick >= 0) { _wave.Clear(); _waveStageTick = -1; } // home comes first
        if (_waveStageTick >= 0) _wave.RemoveAll(id => !u.Alive[id] || u.Owner[id] != _player);

        var waiting = new List<int>();
        foreach (int a in _army)
        {
            if (_waveStageTick >= 0 && _wave.Contains(a)) continue;
            Order o = u.CurrentOrder(a);
            if (o.Type == OrderType.None) waiting.Add(a);
        }

        // Scout early on (Normal+), but never send a land scout to an unreachable island start.
        if (_difficulty >= AiDifficulty.Normal && Minutes < 6 && waiting.Count > 0 && _w.Tick % (40 * World.TicksPerSecond) < _interval)
        {
            int scout = waiting[0];
            var starts = _w.Map.StartPositions;
            if (starts.Count > 0)
            {
                FixVec2 dest = starts[_scoutIndex++ % starts.Count];
                if (FixVec2.DistanceSquared(dest, _home) > Fix64.FromInt(100 * 100) && CanReachTarget(scout, dest))
                {
                    Issue(CommandType.Fight, scout, dest);
                    waiting.RemoveAt(0);
                }
            }
        }

        if (underAttack) return;

        // A staged wave moves in once most of it has gathered (or it has waited long enough).
        if (_waveStageTick >= 0)
        {
            int there = 0;
            foreach (int a in _wave)
                if (FixVec2.DistanceSquared(u.Position[a], _waveStage) < Fix64.FromInt(45 * 45)) there++;
            bool waitedLongEnough = _w.Tick - _waveStageTick > 70 * World.TicksPerSecond;
            if (_wave.Count == 0)
                _waveStageTick = -1;
            else if (there * 10 >= _wave.Count * 7 || waitedLongEnough)
            {
                FixVec2 target = AttackTarget(_wave);
                List<int> reachable = ReachableUnits(_wave, target);
                foreach (int a in reachable) Issue(CommandType.Fight, a, target);
                LastPlan = $"wave {_wavesSent} goes in ({reachable.Count} units)";
                _wave.Clear();
                _waveStageTick = -1;
            }
        }

        int minGap = _difficulty switch
        {
            AiDifficulty.Easy => 180,
            AiDifficulty.Normal => 100,
            _ => 60,
        } * World.TicksPerSecond;
        // Don't sit on a big army forever waiting for the wave to fill up. Only units that can
        // reach the chosen target count toward the wave; stranded land troops stay at their rally.
        int maxGap = _difficulty switch
        {
            AiDifficulty.Easy => 420,
            AiDifficulty.Normal => 240,
            _ => 150,
        } * World.TicksPerSecond;
        FixVec2 nextTarget = AttackTarget(waiting);
        List<int> ready = ReachableUnits(waiting, nextTarget);
        bool overdue = _w.Tick - _lastWaveTick >= maxGap && ready.Count >= _waveSize / 2 && ready.Count >= 6;

        if (_waveStageTick < 0 && (ready.Count >= _waveSize || overdue) && _w.Tick - _lastWaveTick >= minGap)
        {
            FixVec2? stage = !_islandMode && _difficulty >= AiDifficulty.Normal ? StagingPoint(nextTarget) : null;
            if (stage is { } sp)
            {
                _wave.Clear();
                _wave.AddRange(ready);
                _waveStage = sp;
                _waveStageTick = _w.Tick;
                foreach (int a in ready) Issue(CommandType.Fight, a, sp);
            }
            else
            {
                foreach (int a in ready) Issue(CommandType.Fight, a, nextTarget);
            }
            _lastWaveTick = _w.Tick;
            _wavesSent++;
            int growth = _difficulty switch { AiDifficulty.Easy => 2, AiDifficulty.Normal => 3, _ => 4 };
            int cap = _difficulty switch { AiDifficulty.Easy => 14, AiDifficulty.Normal => 30, _ => 45 };
            _waveSize = Math2.Min(cap, _waveSize + growth);
            LastPlan = $"attack wave {_wavesSent} ({ready.Count} units)";
            return;
        }

        // Units idling far forward (a wave that won its fight) keep pushing instead of walking home.
        if (_difficulty >= AiDifficulty.Normal)
        {
            for (int i = waiting.Count - 1; i >= 0; i--)
            {
                int a = waiting[i];
                FixVec2 rally = RallyPointFor(a);
                if (CanReachTarget(a, nextTarget)
                    && FixVec2.DistanceSquared(u.Position[a], rally) > Fix64.FromInt(130 * 130)
                    && FixVec2.DistanceSquared(u.Position[a], _home) > FixVec2.DistanceSquared(rally, _home))
                {
                    Issue(CommandType.Fight, a, nextTarget);
                    waiting.RemoveAt(i);
                }
            }
        }

        // Everyone else gathers in a loose block. Ships use a water rally and individual slots are
        // snapped to their own movement class before destination reservation spreads them apart.
        FixVec2 toward = _enemyGuess - _home;
        Fix64 tlen = toward.Length();
        FixVec2 fwd = tlen > Fix64.Zero ? toward * (Fix64.One / tlen) : new FixVec2(Fix64.Zero, Fix64.One);
        var side = new FixVec2(-fwd.Y, fwd.X);
        for (int i = 0; i < waiting.Count; i++)
        {
            int a = waiting[i];
            UnitDef def = _w.Catalog[u.TypeIndex[a]];
            FixVec2 rally = RallyPointFor(a);
            int col = i % 12 - 6, row = i / 12;
            FixVec2 slot = _w.ClampToMap(rally + side * Fix64.FromInt(col * 7) - fwd * Fix64.FromInt(row * 7));
            if (def.MoveClass == MoveClass.Ship)
                slot = _w.Pathfinder.NearestPassable(MoveClass.Ship, slot, Math2.Max(_w.Map.TilesX, _w.Map.TilesY));
            if (FixVec2.DistanceSquared(u.Position[a], slot) > Fix64.FromInt(12 * 12)
                && FixVec2.DistanceSquared(u.Position[a], rally) > Fix64.FromInt(40 * 40))
                Issue(CommandType.Move, a, slot);
        }
    }

    /// <summary>Where a wave gathers before going in: short of the target, or null on small maps.</summary>
    private FixVec2? StagingPoint(FixVec2 target)
    {
        FixVec2 dir = target - _home;
        Fix64 len = dir.Length();
        if (len < Fix64.FromInt(260)) return null;
        FixVec2 p = target - dir * (Fix64.FromInt(110) / len);
        return _w.ClampToMap(p);
    }

    /// <summary>
    /// Picks a target for these particular movers. Nearby enemies, allied focus fire and units that
    /// hurt us score best. Once picked, a target is kept until it dies or cannot be reached by this
    /// kind of wave; candidates on another land zone are never selected for that wave.
    /// </summary>
    internal FixVec2 AttackTarget(IReadOnlyList<int> movers)
    {
        UnitStore u = _w.Units;
        if (_attackTargetId >= 0)
        {
            if (!u.IsValid(_attackTargetId))
            {
                _attackTargetId = -1;
            }
            else
            {
                if (Sees(_attackTargetId)) _attackTargetPos = u.Position[_attackTargetId];
                if (CanAnyReach(movers, _attackTargetPos)) return _attackTargetPos;
            }
        }

        int best = -1;
        FixVec2 bestPos = _attackTargetPos;
        Fix64 bestScore = Fix64.MaxValue;
        for (int id = 0; id < u.Count; id++)
        {
            if (!u.Alive[id] || !_w.AreEnemies(_player, u.Owner[id])) continue;
            bool visible = Sees(id);
            FixVec2? remembered = KnownStructurePosition(id);
            if (!visible && remembered is null) continue;
            FixVec2 pos = visible ? u.Position[id] : remembered!.Value;
            if (!CanAnyReach(movers, pos)) continue;
            Fix64 score = TargetScore(id, pos);
            if (score < bestScore || (score == bestScore && id < best))
            {
                best = id;
                bestPos = pos;
                bestScore = score;
            }
        }

        if (best >= 0)
        {
            _attackTargetId = best;
            _attackTargetPos = bestPos;
            return bestPos;
        }

        // No contact yet: scout reachable start positions, nearest first, rather than sending a
        // land army toward an enemy start on the far side of deep water.
        Fix64 bestD = Fix64.MaxValue;
        bool found = false;
        foreach (FixVec2 start in _w.Map.StartPositions)
        {
            if (FixVec2.DistanceSquared(start, _home) < Fix64.FromInt(100 * 100)) continue;
            if (!CanAnyReach(movers, start)) continue;
            Fix64 d = FixVec2.DistanceSquared(start, _home);
            if (!found || d < bestD || (d == bestD && start.X < bestPos.X)
                || (d == bestD && start.X == bestPos.X && start.Y < bestPos.Y))
            {
                found = true;
                bestD = d;
                bestPos = start;
            }
        }
        if (!found && CanAnyReach(movers, _enemyGuess)) { bestPos = _enemyGuess; found = true; }
        if (found)
        {
            _attackTargetId = -1;
            _attackTargetPos = bestPos;
        }
        return bestPos;
    }

    private Fix64 TargetScore(int enemy, FixVec2 pos)
    {
        UnitStore u = _w.Units;
        UnitDef def = _w.Catalog[u.TypeIndex[enemy]];
        Fix64 score = FixVec2.DistanceSquared(pos, _home) / 64;
        if (def.IsCommander) score -= Fix64.FromInt(12000);
        if (def.IsFactory) score -= Fix64.FromInt(6000);

        for (int id = 0; id < u.Count; id++)
            if (u.Alive[id] && u.Owner[id] == _player && u.LastAttacker[id] == enemy)
            {
                score -= Fix64.FromInt(30000);
                break;
            }

        // Allied waves concentrate on one enemy instead of scattering across every known target.
        foreach (AiController other in _w.Ais)
            if (other._player != _player && other._team == _team && other._attackTargetId == enemy)
                score -= Fix64.FromInt(20000);
        return score;
    }

    private FixVec2? KnownStructurePosition(int id)
    {
        foreach (var known in _knownEnemyStructures)
            if (known.Unit == id) return known.Pos;
        return null;
    }

    private List<int> ReachableUnits(IReadOnlyList<int> movers, FixVec2 target)
    {
        var result = new List<int>();
        foreach (int id in movers)
            if (CanReachTarget(id, target)) result.Add(id);
        return result;
    }

    private bool CanAnyReach(IReadOnlyList<int> movers, FixVec2 target)
    {
        foreach (int id in movers)
            if (CanReachTarget(id, target)) return true;
        return false;
    }

    internal bool CanReachTarget(int id, FixVec2 target)
    {
        UnitStore u = _w.Units;
        if (!u.IsValid(id) || u.Owner[id] != _player) return false;
        UnitDef def = _w.Catalog[u.TypeIndex[id]];
        if (def.IsAir) return true;
        if (!def.CanMove) return false;

        int sx = MapData.TileOf(u.Position[id].X), sy = MapData.TileOf(u.Position[id].Y);
        int startZone = _w.Terrain.ZoneOf(def.MoveClass, sx, sy);
        if (startZone < 0) return false;
        int tx = MapData.TileOf(target.X), ty = MapData.TileOf(target.Y);
        if (_w.Terrain.ZoneOf(def.MoveClass, tx, ty) == startZone) return true;
        if (def.MoveClass != MoveClass.Ship) return false;

        // A ship need not occupy a land target's tile: any same-zone water tile inside weapon
        // range is a valid firing position. This also prevents cross-zone path snapping from
        // masquerading as a reachable attack order.
        Fix64 range = Fix64.Zero;
        foreach (WeaponDef weapon in def.Weapons)
            if (weapon.TargetsGround && weapon.Kind != WeaponKind.DGun && weapon.Range > range) range = weapon.Range;
        int maxRadius = Math2.Min(Math2.Max(_w.Map.TilesX, _w.Map.TilesY),
            (range / MapData.TileSize).ToIntFloor() + 2);
        Fix64 range2 = range * range;
        for (int r = 1; r <= maxRadius; r++)
        for (int y = ty - r; y <= ty + r; y++)
        for (int x = tx - r; x <= tx + r; x++)
        {
            if (Math2.Abs(x - tx) != r && Math2.Abs(y - ty) != r) continue;
            if (_w.Terrain.ZoneOf(MoveClass.Ship, x, y) != startZone) continue;
            FixVec2 water = Terrain.FootprintCentre(x, y, 1);
            if (FixVec2.DistanceSquared(water, target) <= range2) return true;
        }
        return false;
    }

    private bool LandConnected(FixVec2 target)
    {
        int tx = MapData.TileOf(target.X), ty = MapData.TileOf(target.Y);
        int hx = MapData.TileOf(_home.X), hy = MapData.TileOf(_home.Y);
        int vehicle = _w.Terrain.ZoneOf(MoveClass.Vehicle, hx, hy);
        // A bot-only reef route is not enough for the ordinary vehicle army. Treat it as island
        // mode so production also includes ships, amphibious units and aircraft.
        return vehicle >= 0 && vehicle == _w.Terrain.ZoneOf(MoveClass.Vehicle, tx, ty);
    }
}
