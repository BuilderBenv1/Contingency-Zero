using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Pathing;
using ContingencyZero.Sim.State;

namespace ContingencyZero.Sim.Missions;

/*
 Mission JSON. The game (game/src/Missions/Campaign.cs) reads the match setup keys; this class reads the rules.

 {
   "campaign": "lattice" | "crucible" | "coop",          game only: which column the mission is listed in
   "order": 1, "title": "...", "briefing": "...",         game only ("briefing" is shown as BBCode: no square brackets)
   "map": "<map key>",                                    game only
   "players": [{ "name", "team", "faction", "start", "ai": "None|Easy|Normal|Hard|Brutal" }],   game only
   "humanPlayer": 0,                                      the human's slot (single player)
   "humanPlayers": [0, 1],                                co-op: every human slot, all on one team; overrides humanPlayer
   "mode": "survival",                                    optional label for the client (SurvivalMode writes it)
   "difficulty": "Normal",                                optional label for the client (survival scores)
   "startMetal": 1000, "startEnergy": 1000, "spawnCommanders": true,                              game only
   "messages": [ "..." ],                                 may contain the placeholders below
   "objectives": [ { "id", "text", "optional"?: bool, "hidden"?: bool } ],   text may contain placeholders
   "spawns": [ { "tag"?, "type", "owner", "x", "y" } ],  units placed before tick 0 (structures snap to the grid)
   "triggers": [ { "id", "when"?, "actions": [ ... ], "repeat"?: bool, "every"?: seconds, "after"?: seconds } ]
 }

 Placeholders (messages and objective text): {wave} the wave counter, {clock} m:ss elapsed, {minutes} whole
 minutes elapsed, {countdown} m:ss until the next periodic trigger is due.

 Trigger timing
   plain:        fires once, the first tick `when` holds (after "after" seconds, if given).
   repeat:       fires every time `when` holds, at most once a second.
   every: N      periodic: fires at after, after+N, after+2N, ... seconds ("after" defaults to N) if `when` holds
                 at that moment (no `when` means always); a slot where it doesn't hold is skipped, not postponed.

 Conditions (`when`)
   time         { seconds }                                  the match clock has reached seconds
   destroyed    { tag }                                      every unit ever tagged is dead (false while none exist)
   count        { player | team, unit?, role?, atLeast = 1, x?, y?, radius? }
                                                             completed units owned by the player (or anyone on the
                                                             team), optionally of one type or role and inside a circle
   noStructures { player }
   area         { player | team, x, y, radius = 20 }         any living unit of theirs inside the circle
   fired        { trigger, after = 0 }                       another trigger fired at least `after` seconds ago
   metal        { player, atLeast }
   defeated     { player }
   wave         { atLeast }                                  the wave counter has reached atLeast
   objectivesDone                                            every required objective is complete
   not { of: c } · all { of: [c...] } · any { of: [c...] }

 Actions
   message   { message: index }
   objective { id, state: active | complete | failed | hidden }   a finished objective stays finished
   spawn     { tag?, units: [ { unit, owner, x, y, count = 1, countPerMinute?, maxCount?, after? } ], order?: { type, x, y } }
               count grows by floor(countPerMinute x minutes since the entry's `after`), capped at maxCount; an entry
               is skipped before its `after` seconds. `order` (fight | patrol | move) is given to the new units.
   attack    { tag | player, x, y, order = patrol }          sends tagged (or all the player's mobile, non-Commander)
                                                             units there; order is patrol | fight | move
   hunt      { player }                                      the player's idle mobile units fight their way to the
                                                             nearest unit of another team (survival waves)
   give      { player, metal, energy }
   bounty    { metal, energy, metalPerWave?, energyPerWave? }   give to every human player; the per-wave parts
                                                             are multiplied by the wave counter
   wave                                                      advances the wave counter (see {wave} and the wave condition)
   reveal · victory · defeat
*/

public enum ObjectiveState : byte { Hidden, Active, Complete, Failed }

public sealed class Objective
{
    public required string Id { get; init; }
    /// <summary>Display text, with placeholders already filled in. Changes over time when the template has any.</summary>
    public required string Text { get; set; }
    public ObjectiveState State;
    /// <summary>Secondary objectives don't block victory.</summary>
    public bool Optional { get; init; }
}

/// <summary>
/// A campaign mission's rules: tagged starting units, objectives, and triggers that fire actions
/// when their condition becomes true. Runs inside the sim, so it's deterministic and replays exactly.
/// The JSON vocabulary is documented at the top of this file.
/// </summary>
public sealed class MissionScript
{
    public string Title { get; private set; } = "";
    public string Briefing { get; private set; } = "";
    /// <summary>Optional label such as "survival", for the client.</summary>
    public string Mode { get; private set; } = "";
    /// <summary>Optional difficulty label (survival), for the client's scores. Not used by the rules.</summary>
    public string Difficulty { get; private set; } = "";
    /// <summary>The (first) human player's index in the match.</summary>
    public int HumanPlayer => HumanPlayers[0];
    /// <summary>Every human slot. One entry in single player; two in co-op. All on one team.</summary>
    public IReadOnlyList<int> HumanPlayers => _humans;
    private readonly List<int> _humans = new();
    /// <summary>Message texts with placeholders filled in as of the last time each one fired. The HUD reads these.</summary>
    public List<string> Messages { get; } = new();
    public List<Objective> Objectives { get; } = new();
    /// <summary>The wave counter, advanced by the "wave" action. Survival shows it on the HUD.</summary>
    public int Wave { get; private set; }

    private readonly List<string> _messageTemplates = new();
    private readonly List<string> _objectiveTemplates = new();
    private readonly List<Trigger> _triggers = new();
    private readonly List<(string Tag, string Type, int Owner, FixVec2 Pos)> _initialSpawns = new();
    private readonly Dictionary<string, List<int>> _tagged = new(StringComparer.Ordinal);
    private readonly List<int> _justSpawned = new();

    /// <summary>Messages fired so far, newest last, for the HUD log.</summary>
    public List<(int Tick, int Message)> MessageLog { get; } = new();

    private sealed class Trigger
    {
        public required string Id { get; init; }
        public JsonElement? Condition { get; init; }
        public required JsonElement[] Actions { get; init; }
        public bool Repeat { get; init; }
        /// <summary>Period in ticks; 0 for a one-shot or repeat trigger.</summary>
        public int EveryTicks { get; init; }
        /// <summary>Earliest tick it may fire.</summary>
        public int AfterTicks { get; init; }
        /// <summary>Periodic triggers: the next slot.</summary>
        public int NextDue;
        public int FiredTick = -1;
        public int TimesFired;
        /// <summary>Has a "wave" action: what {countdown} counts down to.</summary>
        public bool AdvancesWave { get; init; }
    }

    private JsonDocument? _doc;

    public static MissionScript Parse(string json)
    {
        var m = new MissionScript();
        m._doc = JsonDocument.Parse(json);
        JsonElement r = m._doc.RootElement;
        m.Title = Str(r, "title");
        m.Briefing = Str(r, "briefing");
        m.Mode = Str(r, "mode");
        m.Difficulty = Str(r, "difficulty");
        if (r.TryGetProperty("humanPlayers", out var hps) && hps.ValueKind == JsonValueKind.Array)
            foreach (var h in hps.EnumerateArray()) m._humans.Add(h.GetInt32());
        if (m._humans.Count == 0) m._humans.Add(Int(r, "humanPlayer", 0));

        if (r.TryGetProperty("messages", out var msgs))
            foreach (var msg in msgs.EnumerateArray())
            {
                string text = msg.GetString() ?? "";
                m._messageTemplates.Add(text);
                m.Messages.Add(text);
            }

        if (r.TryGetProperty("objectives", out var objs))
            foreach (var o in objs.EnumerateArray())
            {
                string text = o.GetProperty("text").GetString()!;
                m._objectiveTemplates.Add(text);
                m.Objectives.Add(new Objective
                {
                    Id = o.GetProperty("id").GetString()!,
                    Text = text,
                    Optional = o.TryGetProperty("optional", out var opt) && opt.GetBoolean(),
                    State = o.TryGetProperty("hidden", out var h) && h.GetBoolean() ? ObjectiveState.Hidden : ObjectiveState.Active,
                });
            }

        if (r.TryGetProperty("spawns", out var spawns))
            foreach (var s in spawns.EnumerateArray())
                m._initialSpawns.Add((
                    Str(s, "tag"),
                    s.GetProperty("type").GetString()!,
                    s.GetProperty("owner").GetInt32(),
                    new FixVec2(Num(s, "x"), Num(s, "y"))));

        if (r.TryGetProperty("triggers", out var triggers))
            foreach (var tr in triggers.EnumerateArray())
            {
                var actions = new List<JsonElement>();
                foreach (var a in tr.GetProperty("actions").EnumerateArray()) actions.Add(a);
                int every = Seconds(tr, "every");
                int after = tr.TryGetProperty("after", out _) ? Seconds(tr, "after") : every;
                m._triggers.Add(new Trigger
                {
                    Id = tr.GetProperty("id").GetString()!,
                    Condition = tr.TryGetProperty("when", out var when) ? when : null,
                    Actions = actions.ToArray(),
                    Repeat = tr.TryGetProperty("repeat", out var rep) && rep.GetBoolean(),
                    EveryTicks = every,
                    AfterTicks = after,
                    NextDue = after,
                    AdvancesWave = actions.Exists(a => Str(a, "type") == "wave"),
                });
            }

        m.RefreshObjectiveTexts(0);
        return m;
    }

    private static Fix64 Num(JsonElement o, string name, string fallback = "0") =>
        o.TryGetProperty(name, out var e) ? Fix64.Parse(e.GetRawText()) : Fix64.Parse(fallback);

    private static string Str(JsonElement o, string name, string fallback = "") =>
        o.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String && e.GetString() is { } s ? s : fallback;

    private static int Int(JsonElement o, string name, int fallback = 0) =>
        o.TryGetProperty(name, out var e) ? e.GetInt32() : fallback;

    /// <summary>A "seconds" number as whole ticks (floored).</summary>
    private static int Seconds(JsonElement o, string name) => (Num(o, name) * World.TicksPerSecond).ToIntFloor();

    internal void Attach(World world)
    {
        foreach (var (tag, type, owner, pos) in _initialSpawns)
            Tag(tag, world.SpawnUnit(type, owner, pos));
    }

    private void Tag(string tag, int id)
    {
        if (tag.Length == 0) return;
        if (!_tagged.TryGetValue(tag, out var list)) _tagged[tag] = list = new List<int>();
        list.Add(id);
    }

    public IReadOnlyList<int> UnitsTagged(string tag) =>
        _tagged.TryGetValue(tag, out var list) ? list : Array.Empty<int>();

    /// <summary>
    /// Tick the next periodic trigger is due, or -1 when there is none: survival's "next wave". Periodic
    /// triggers that advance the wave counter come first; others count only when there are none.
    /// </summary>
    public int NextPeriodicTick
    {
        get
        {
            int best = -1, any = -1;
            foreach (Trigger t in _triggers)
            {
                if (t.EveryTicks <= 0) continue;
                if (any < 0 || t.NextDue < any) any = t.NextDue;
                if (t.AdvancesWave && (best < 0 || t.NextDue < best)) best = t.NextDue;
            }
            return best >= 0 ? best : any;
        }
    }

    /// <summary>Whether every human slot has been knocked out (co-op plays on while one stands).</summary>
    public bool HumansDefeated(World world)
    {
        foreach (int h in _humans)
            if (!world.Players[h].Defeated) return false;
        return true;
    }

    internal void Update(World world)
    {
        if (world.IsOver) return;

        if (HumansDefeated(world))
        {
            Defeat(world);
            return;
        }

        foreach (Trigger t in _triggers)
        {
            if (world.Tick < t.AfterTicks) continue;
            if (t.EveryTicks > 0)
            {
                if (world.Tick < t.NextDue) continue;
                bool holds = t.Condition is null || Evaluate(world, t.Condition.Value);
                while (t.NextDue <= world.Tick) t.NextDue += t.EveryTicks;
                if (!holds) continue;
            }
            else
            {
                if (t.TimesFired > 0 && !t.Repeat) continue;
                if (t.Repeat && t.FiredTick >= 0 && world.Tick - t.FiredTick < World.TicksPerSecond) continue;
                if (t.Condition is not null && !Evaluate(world, t.Condition.Value)) continue;
            }
            t.FiredTick = world.Tick;
            t.TimesFired++;
            foreach (JsonElement a in t.Actions) Run(world, a);
            if (world.IsOver) return;
        }

        if (world.Tick % World.TicksPerSecond == 0) RefreshObjectiveTexts(world.Tick);
    }

    // ---- Placeholders ------------------------------------------------------------------------

    private static string Clock(int ticks)
    {
        int secs = ticks < 0 ? 0 : ticks / World.TicksPerSecond;
        return $"{secs / 60}:{secs % 60:D2}";
    }

    /// <summary>Fills {wave}, {clock}, {minutes} and {countdown} in. Display only: never feeds back into state.</summary>
    public string Resolve(string template, int tick)
    {
        if (template.IndexOf('{') < 0) return template;
        var sb = new StringBuilder(template);
        sb.Replace("{wave}", Wave.ToString());
        sb.Replace("{clock}", Clock(tick));
        sb.Replace("{minutes}", (tick / (60 * World.TicksPerSecond)).ToString());
        int next = NextPeriodicTick;
        sb.Replace("{countdown}", next < 0 ? "--" : Clock(next - tick));
        return sb.ToString();
    }

    private void RefreshObjectiveTexts(int tick)
    {
        for (int i = 0; i < Objectives.Count; i++)
            if (_objectiveTemplates[i].IndexOf('{') >= 0)
                Objectives[i].Text = Resolve(_objectiveTemplates[i], tick);
    }

    // ---- Conditions ----------------------------------------------------------------------------

    /// <summary>Whether a unit's owner is the one a condition names: "player" (one slot) or "team" (everyone on it).</summary>
    private static bool OwnedBy(World world, JsonElement c, int owner)
    {
        if (c.TryGetProperty("team", out var team)) return world.Players[owner].Team == team.GetInt32();
        return owner == Int(c, "player");
    }

    private bool Evaluate(World world, JsonElement c)
    {
        string kind = Str(c, "type");
        switch (kind)
        {
            case "time":
                return world.Tick >= Seconds(c, "seconds");

            case "destroyed":
            {
                var ids = UnitsTagged(Str(c, "tag"));
                if (ids.Count == 0) return false;
                foreach (int id in ids) if (world.Units.Alive[id]) return false;
                return true;
            }

            case "count":
            {
                string type = Str(c, "unit");
                string role = Str(c, "role");
                int need = Int(c, "atLeast", 1);
                bool inArea = c.TryGetProperty("radius", out _) || c.TryGetProperty("x", out _);
                var centre = new FixVec2(Num(c, "x"), Num(c, "y"));
                Fix64 r = Num(c, "radius", "20");
                int have = 0;
                for (int id = 0; id < world.Units.Count; id++)
                {
                    if (!world.Units.Alive[id] || !world.Units.Complete[id] || !OwnedBy(world, c, world.Units.Owner[id])) continue;
                    var def = world.Catalog[world.Units.TypeIndex[id]];
                    if (type.Length > 0 && def.Id != type) continue;
                    if (role.Length > 0 && def.Role != role) continue;
                    if (inArea && FixVec2.DistanceSquared(world.Units.Position[id], centre) > r * r) continue;
                    have++;
                }
                return have >= need;
            }

            case "noStructures":
            {
                for (int id = 0; id < world.Units.Count; id++)
                    if (world.Units.Alive[id] && OwnedBy(world, c, world.Units.Owner[id]) && world.Catalog[world.Units.TypeIndex[id]].IsStructure)
                        return false;
                return true;
            }

            case "area":
            {
                var centre = new FixVec2(Num(c, "x"), Num(c, "y"));
                Fix64 r = Num(c, "radius", "20");
                for (int id = 0; id < world.Units.Count; id++)
                {
                    if (!world.Units.Alive[id] || !OwnedBy(world, c, world.Units.Owner[id])) continue;
                    if (FixVec2.DistanceSquared(world.Units.Position[id], centre) <= r * r) return true;
                }
                return false;
            }

            case "fired":
            {
                string id = Str(c, "trigger");
                int delay = Seconds(c, "after");
                foreach (Trigger t in _triggers)
                    if (t.Id == id) return t.FiredTick >= 0 && world.Tick - t.FiredTick >= delay;
                return false;
            }

            case "metal":
                return world.Players[Int(c, "player")].Metal >= Num(c, "atLeast");

            case "defeated":
                return world.Players[Int(c, "player")].Defeated;

            case "wave":
                return Wave >= Int(c, "atLeast", 1);

            case "objectivesDone":
                foreach (Objective o in Objectives)
                    if (!o.Optional && o.State != ObjectiveState.Complete) return false;
                return true;

            case "not":
                return !Evaluate(world, c.GetProperty("of"));

            case "all":
                foreach (JsonElement sub in c.GetProperty("of").EnumerateArray())
                    if (!Evaluate(world, sub)) return false;
                return true;

            case "any":
                foreach (JsonElement sub in c.GetProperty("of").EnumerateArray())
                    if (Evaluate(world, sub)) return true;
                return false;

            default:
                throw new InvalidDataException($"Unknown mission condition '{kind}'");
        }
    }

    // ---- Actions ---------------------------------------------------------------------------------

    private void Run(World world, JsonElement a)
    {
        string kind = Str(a, "type");
        switch (kind)
        {
            case "message":
            {
                int index = Int(a, "message");
                if (index >= 0 && index < Messages.Count) Messages[index] = Resolve(_messageTemplates[index], world.Tick);
                MessageLog.Add((world.Tick, index));
                world.Events.Add(new GameEvent(GameEventType.MissionMessage, Value: index));
                break;
            }

            case "objective":
            {
                string id = Str(a, "id");
                string state = Str(a, "state", "active");
                foreach (Objective o in Objectives)
                {
                    if (o.Id != id) continue;
                    // A finished objective stays finished (revealing it again mustn't reopen it).
                    if (o.State is ObjectiveState.Complete or ObjectiveState.Failed && state is "active" or "hidden") continue;
                    o.State = state switch
                    {
                        "complete" => ObjectiveState.Complete,
                        "failed" => ObjectiveState.Failed,
                        "hidden" => ObjectiveState.Hidden,
                        _ => ObjectiveState.Active,
                    };
                }
                break;
            }

            case "spawn":
            {
                string tag = Str(a, "tag");
                _justSpawned.Clear();
                foreach (JsonElement s in a.GetProperty("units").EnumerateArray())
                {
                    int count = SpawnCount(s, world.Tick);
                    if (count <= 0) continue;
                    var pos = new FixVec2(Num(s, "x"), Num(s, "y"));
                    for (int i = 0; i < count; i++)
                    {
                        // Lay groups out in a loose block.
                        var offset = FixVec2.FromInts((i % 5) * 5 - 10, (i / 5) * 5);
                        int id = world.SpawnUnit(Str(s, "unit"), Int(s, "owner"), pos + offset);
                        Tag(tag, id);
                        _justSpawned.Add(id);
                    }
                }
                if (a.TryGetProperty("order", out var order))
                    GiveOrder(world, _justSpawned, OrderOf(Str(order, "type", "fight")), new FixVec2(Num(order, "x"), Num(order, "y")));
                break;
            }

            case "attack":
            {
                // Send tagged units (or every mobile unit of a player) to a point, fighting on the way.
                var target = new FixVec2(Num(a, "x"), Num(a, "y"));
                string tag = Str(a, "tag");
                IEnumerable<int> ids = tag.Length > 0 ? UnitsTagged(tag) : AllMobile(world, Int(a, "player"));
                GiveOrder(world, ids, OrderOf(Str(a, "order", "patrol")), target);
                break;
            }

            case "hunt":
                Hunt(world, Int(a, "player"));
                break;

            case "reveal":
                world.Vision.RevealAll = true;
                break;

            case "give":
                Give(world.Players[Int(a, "player")], Num(a, "metal"), Num(a, "energy"));
                break;

            case "bounty":
            {
                Fix64 metal = Num(a, "metal") + Num(a, "metalPerWave") * Wave;
                Fix64 energy = Num(a, "energy") + Num(a, "energyPerWave") * Wave;
                foreach (int h in _humans) Give(world.Players[h], metal, energy);
                break;
            }

            case "wave":
                Wave++;
                RefreshObjectiveTexts(world.Tick);
                break;

            case "victory":
                world.EndMatch(world.Players[HumanPlayer].Team);
                break;

            case "defeat":
                Defeat(world);
                break;

            default:
                throw new InvalidDataException($"Unknown mission action '{kind}'");
        }
    }

    /// <summary>How many units a spawn entry makes right now: count, plus growth per minute since its `after`, capped.</summary>
    public static int SpawnCount(JsonElement entry, int tick)
    {
        int after = Seconds(entry, "after");
        if (tick < after) return 0;
        int count = Int(entry, "count", 1);
        Fix64 perMinute = Num(entry, "countPerMinute");
        if (perMinute > Fix64.Zero)
        {
            Fix64 minutes = Fix64.FromRatio(tick - after, 60 * World.TicksPerSecond);
            count += (perMinute * minutes).ToIntFloor();
        }
        if (entry.TryGetProperty("maxCount", out var max)) count = Math2.Min(count, max.GetInt32());
        return count;
    }

    private static OrderType OrderOf(string name) => name switch
    {
        "fight" => OrderType.Fight,
        "move" => OrderType.Move,
        _ => OrderType.Patrol,
    };

    private static void GiveOrder(World world, IEnumerable<int> ids, OrderType type, FixVec2 target)
    {
        foreach (int id in ids)
        {
            if (!world.Units.IsValid(id)) continue;
            var def = world.Catalog[world.Units.TypeIndex[id]];
            if (!def.CanMove) continue;
            world.Units.ClearOrders(id);
            world.Units.Orders[id].Add(new Order(type, target) { Origin = target });
        }
    }

    /// <summary>
    /// Every idle mobile unit of the player is sent (fighting) to the nearest living unit of another
    /// team. Units still on their way somewhere keep going. Iterates ids in order, so it's deterministic.
    /// </summary>
    private static void Hunt(World world, int player)
    {
        int team = world.Players[player].Team;
        var targets = new List<FixVec2>();
        for (int id = 0; id < world.Units.Count; id++)
            if (world.Units.Alive[id] && world.Players[world.Units.Owner[id]].Team != team && !world.Players[world.Units.Owner[id]].Defeated)
                targets.Add(world.Units.Position[id]);
        if (targets.Count == 0) return;
        for (int id = 0; id < world.Units.Count; id++)
        {
            if (!world.Units.Alive[id] || world.Units.Owner[id] != player || !world.Units.Complete[id]) continue;
            if (world.Units.Orders[id].Count > 0) continue;
            var def = world.Catalog[world.Units.TypeIndex[id]];
            if (!def.CanMove || def.IsCommander) continue;
            FixVec2 at = world.Units.Position[id];
            FixVec2 best = targets[0];
            Fix64 bestD = FixVec2.DistanceSquared(at, best);
            for (int i = 1; i < targets.Count; i++)
            {
                Fix64 d = FixVec2.DistanceSquared(at, targets[i]);
                if (d < bestD) { bestD = d; best = targets[i]; }
            }
            world.Units.Orders[id].Add(new Order(OrderType.Fight, best) { Origin = best });
        }
    }

    private static void Give(PlayerState p, Fix64 metal, Fix64 energy)
    {
        p.Metal = Fix64.Min(p.MetalStorage, p.Metal + metal);
        p.Energy = Fix64.Min(p.EnergyStorage, p.Energy + energy);
    }

    private static IEnumerable<int> AllMobile(World world, int player)
    {
        for (int id = 0; id < world.Units.Count; id++)
            if (world.Units.Alive[id] && world.Units.Owner[id] == player && world.Units.Complete[id]
                && world.Catalog[world.Units.TypeIndex[id]].CanMove && !world.Catalog[world.Units.TypeIndex[id]].IsCommander)
                yield return id;
    }

    private void Defeat(World world)
    {
        int humanTeam = world.Players[HumanPlayer].Team;
        int? enemy = null;
        foreach (PlayerState p in world.Players)
            if (p.Team != humanTeam) { enemy = p.Team; break; }
        world.EndMatch(enemy);
    }

    public ulong StateHash()
    {
        var h = new StateHasher();
        foreach (Trigger t in _triggers) { h.Add(t.FiredTick); h.Add(t.TimesFired); h.Add(t.NextDue); }
        foreach (Objective o in Objectives) h.Add((int)o.State);
        h.Add(Wave);
        return h.Value;
    }
}
