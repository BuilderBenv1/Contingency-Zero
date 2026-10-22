using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.State;

namespace ContingencyZero.Sim.Missions;

public enum ObjectiveState : byte { Hidden, Active, Complete, Failed }

public sealed class Objective
{
    public required string Id { get; init; }
    public required string Text { get; init; }
    public ObjectiveState State;
    /// <summary>Secondary objectives don't block victory.</summary>
    public bool Optional { get; init; }
}

/// <summary>
/// A campaign mission's rules: tagged starting units, objectives, and triggers that fire actions
/// when their condition becomes true. Runs inside the sim, so it's deterministic and replays exactly.
///
/// Conditions: time, destroyed (all units with a tag are dead), count (a player owns at least N of a
/// type), area (a player has units inside a circle), fired (another trigger fired at least N seconds
/// ago), metal (a player has at least N metal), and all / any of other conditions.
/// Actions: message, objective, spawn, attack (send tagged units to a point), reveal, give, victory, defeat.
/// </summary>
public sealed class MissionScript
{
    public string Title { get; private set; } = "";
    public string Briefing { get; private set; } = "";
    /// <summary>The human player's index in the match.</summary>
    public int HumanPlayer { get; private set; }
    public List<string> Messages { get; } = new();
    public List<Objective> Objectives { get; } = new();

    private readonly List<Trigger> _triggers = new();
    private readonly List<(string Tag, string Type, int Owner, FixVec2 Pos)> _initialSpawns = new();
    private readonly Dictionary<string, List<int>> _tagged = new(StringComparer.Ordinal);

    /// <summary>Messages fired so far, newest last, for the HUD log.</summary>
    public List<(int Tick, int Message)> MessageLog { get; } = new();

    private sealed class Trigger
    {
        public required string Id { get; init; }
        public required JsonElement Condition { get; init; }
        public required JsonElement[] Actions { get; init; }
        public bool Repeat { get; init; }
        public int FiredTick = -1;
        public int TimesFired;
    }

    private JsonDocument? _doc;

    public static MissionScript Parse(string json)
    {
        var m = new MissionScript();
        m._doc = JsonDocument.Parse(json);
        JsonElement r = m._doc.RootElement;
        m.Title = r.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
        m.Briefing = r.TryGetProperty("briefing", out var b) ? b.GetString() ?? "" : "";
        m.HumanPlayer = r.TryGetProperty("humanPlayer", out var hp) ? hp.GetInt32() : 0;

        if (r.TryGetProperty("messages", out var msgs))
            foreach (var msg in msgs.EnumerateArray()) m.Messages.Add(msg.GetString() ?? "");

        if (r.TryGetProperty("objectives", out var objs))
            foreach (var o in objs.EnumerateArray())
                m.Objectives.Add(new Objective
                {
                    Id = o.GetProperty("id").GetString()!,
                    Text = o.GetProperty("text").GetString()!,
                    Optional = o.TryGetProperty("optional", out var opt) && opt.GetBoolean(),
                    State = o.TryGetProperty("hidden", out var h) && h.GetBoolean() ? ObjectiveState.Hidden : ObjectiveState.Active,
                });

        if (r.TryGetProperty("spawns", out var spawns))
            foreach (var s in spawns.EnumerateArray())
                m._initialSpawns.Add((
                    s.TryGetProperty("tag", out var tag) ? tag.GetString() ?? "" : "",
                    s.GetProperty("type").GetString()!,
                    s.GetProperty("owner").GetInt32(),
                    new FixVec2(Num(s, "x"), Num(s, "y"))));

        if (r.TryGetProperty("triggers", out var triggers))
            foreach (var tr in triggers.EnumerateArray())
            {
                var actions = new List<JsonElement>();
                foreach (var a in tr.GetProperty("actions").EnumerateArray()) actions.Add(a);
                m._triggers.Add(new Trigger
                {
                    Id = tr.GetProperty("id").GetString()!,
                    Condition = tr.GetProperty("when"),
                    Actions = actions.ToArray(),
                    Repeat = tr.TryGetProperty("repeat", out var rep) && rep.GetBoolean(),
                });
            }

        return m;
    }

    private static Fix64 Num(JsonElement o, string name, string fallback = "0") =>
        o.TryGetProperty(name, out var e) ? Fix64.Parse(e.GetRawText()) : Fix64.Parse(fallback);

    private static string Str(JsonElement o, string name, string fallback = "") =>
        o.TryGetProperty(name, out var e) && e.GetString() is { } s ? s : fallback;

    private static int Int(JsonElement o, string name, int fallback = 0) =>
        o.TryGetProperty(name, out var e) ? e.GetInt32() : fallback;

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

    internal void Update(World world)
    {
        if (world.IsOver) return;

        if (world.Players[HumanPlayer].Defeated)
        {
            Defeat(world);
            return;
        }

        foreach (Trigger t in _triggers)
        {
            if (t.TimesFired > 0 && !t.Repeat) continue;
            if (t.Repeat && t.FiredTick >= 0 && world.Tick - t.FiredTick < World.TicksPerSecond) continue;
            if (!Evaluate(world, t.Condition)) continue;
            t.FiredTick = world.Tick;
            t.TimesFired++;
            foreach (JsonElement a in t.Actions) Run(world, a);
            if (world.IsOver) return;
        }
    }

    private bool Evaluate(World world, JsonElement c)
    {
        string kind = Str(c, "type");
        switch (kind)
        {
            case "time":
                return world.Tick >= (Num(c, "seconds") * World.TicksPerSecond).ToIntFloor();

            case "destroyed":
            {
                var ids = UnitsTagged(Str(c, "tag"));
                if (ids.Count == 0) return false;
                foreach (int id in ids) if (world.Units.Alive[id]) return false;
                return true;
            }

            case "count":
            {
                int player = Int(c, "player");
                string type = Str(c, "unit");
                string role = Str(c, "role");
                int need = Int(c, "atLeast", 1);
                int have = 0;
                for (int id = 0; id < world.Units.Count; id++)
                {
                    if (!world.Units.Alive[id] || !world.Units.Complete[id] || world.Units.Owner[id] != player) continue;
                    var def = world.Catalog[world.Units.TypeIndex[id]];
                    if (type.Length > 0 && def.Id != type) continue;
                    if (role.Length > 0 && def.Role != role) continue;
                    have++;
                }
                return have >= need;
            }

            case "noStructures":
            {
                int player = Int(c, "player");
                for (int id = 0; id < world.Units.Count; id++)
                    if (world.Units.Alive[id] && world.Units.Owner[id] == player && world.Catalog[world.Units.TypeIndex[id]].IsStructure)
                        return false;
                return true;
            }

            case "area":
            {
                int player = Int(c, "player");
                var centre = new FixVec2(Num(c, "x"), Num(c, "y"));
                Fix64 r = Num(c, "radius", "20");
                for (int id = 0; id < world.Units.Count; id++)
                {
                    if (!world.Units.Alive[id] || world.Units.Owner[id] != player) continue;
                    if (FixVec2.DistanceSquared(world.Units.Position[id], centre) <= r * r) return true;
                }
                return false;
            }

            case "fired":
            {
                string id = Str(c, "trigger");
                int delay = (Num(c, "after") * World.TicksPerSecond).ToIntFloor();
                foreach (Trigger t in _triggers)
                    if (t.Id == id) return t.FiredTick >= 0 && world.Tick - t.FiredTick >= delay;
                return false;
            }

            case "metal":
                return world.Players[Int(c, "player")].Metal >= Num(c, "atLeast");

            case "objectivesDone":
                foreach (Objective o in Objectives)
                    if (!o.Optional && o.State != ObjectiveState.Complete) return false;
                return true;

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

    private void Run(World world, JsonElement a)
    {
        string kind = Str(a, "type");
        switch (kind)
        {
            case "message":
            {
                int index = Int(a, "message");
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
                foreach (JsonElement s in a.GetProperty("units").EnumerateArray())
                {
                    int count = Int(s, "count", 1);
                    var pos = new FixVec2(Num(s, "x"), Num(s, "y"));
                    for (int i = 0; i < count; i++)
                    {
                        // Lay groups out in a loose block.
                        var offset = FixVec2.FromInts((i % 5) * 5 - 10, (i / 5) * 5);
                        Tag(tag, world.SpawnUnit(Str(s, "unit"), Int(s, "owner"), pos + offset));
                    }
                }
                break;
            }

            case "attack":
            {
                // Send tagged units (or every mobile unit of a player) to a point, fighting on the way.
                var target = new FixVec2(Num(a, "x"), Num(a, "y"));
                string tag = Str(a, "tag");
                IEnumerable<int> ids = tag.Length > 0 ? UnitsTagged(tag) : AllMobile(world, Int(a, "player"));
                foreach (int id in ids)
                {
                    if (!world.Units.IsValid(id)) continue;
                    var def = world.Catalog[world.Units.TypeIndex[id]];
                    if (!def.CanMove) continue;
                    world.Units.ClearOrders(id);
                    world.Units.Orders[id].Add(new Order(OrderType.Patrol, target) { Origin = target });
                }
                break;
            }

            case "reveal":
                world.Vision.RevealAll = true;
                break;

            case "give":
            {
                PlayerState p = world.Players[Int(a, "player")];
                p.Metal = Fix64.Min(p.MetalStorage, p.Metal + Num(a, "metal"));
                p.Energy = Fix64.Min(p.EnergyStorage, p.Energy + Num(a, "energy"));
                break;
            }

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
        foreach (Trigger t in _triggers) { h.Add(t.FiredTick); h.Add(t.TimesFired); }
        foreach (Objective o in Objectives) h.Add((int)o.State);
        return h.Value;
    }
}
