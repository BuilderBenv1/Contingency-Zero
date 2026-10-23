using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using ContingencyZero.Sim.Pathing;
using ContingencyZero.Sim.Replay;

namespace ContingencyZero.Sim.Missions;

/// <summary>
/// Survival: one or two players hold their start positions against waves of a "Horde" that grow in
/// size and tier until the last Commander falls. There is no victory; the score is minutes survived.
/// <see cref="Build"/> writes the whole thing as an ordinary mission script (see MissionScript.cs), so
/// it runs inside the sim, replays, and works over the network like any other mission.
///
/// Contract for <see cref="Build"/>'s players list:
///   * every entry but the last is a defender: a human (Ai None) or, in tests, an AI standing in for
///     one. Defenders share one team and each has a distinct, valid start index.
///   * the last entry is the Horde slot: Ai None, on a team of its own. Its faction is the faction the
///     waves use. It has no Commander and never builds anything; the script spawns its units.
/// Defenders' Commanders are written into the script's spawns, so the scenario must use
/// SpawnCommanders: false (<see cref="Options"/> does). <see cref="CreateScenario"/> does all of this.
/// </summary>
public static class SurvivalMode
{
    public const string HordeName = "The Horde";
    public const string ModeKey = "survival";
    public const int StartMetal = 1500;
    public const int StartEnergy = 1500;
    /// <summary>At most this many spawn lanes (directions the waves come from).</summary>
    public const int MaxLanes = 3;

    /// <summary>Seconds between waves.</summary>
    public static int WaveSeconds(AiDifficulty difficulty) => difficulty switch
    {
        AiDifficulty.Easy => 100,
        AiDifficulty.Normal => 75,
        AiDifficulty.Hard => 55,
        _ => 40,
    };

    /// <summary>The Horde slot the caller appends to the defenders.</summary>
    public static PlayerSetup HordeSetup(string faction, int team, int startIndex = 0) =>
        new(HordeName, team, faction, startIndex, AiDifficulty.None);

    /// <summary>
    /// The defenders plus a Horde slot: the Horde fights with <paramref name="hordeFaction"/> (default:
    /// the opposite of the first defender's), on the first free team, from a start the defenders don't use.
    /// </summary>
    public static IReadOnlyList<PlayerSetup> Players(IReadOnlyList<PlayerSetup> defenders, MapData map, string? hordeFaction = null)
    {
        if (defenders.Count == 0) throw new ArgumentException("Survival needs at least one defender", nameof(defenders));
        var list = new List<PlayerSetup>(defenders);
        string faction = hordeFaction ?? (defenders[0].Faction == "lattice" ? "crucible" : "lattice");
        int team = 0;
        bool used;
        do
        {
            used = false;
            foreach (PlayerSetup d in defenders) if (d.Team == team) used = true;
            if (used) team++;
        } while (used);
        int start = 0;
        for (int i = 0; i < map.StartPositions.Count; i++)
        {
            bool taken = false;
            foreach (PlayerSetup d in defenders) if (d.StartIndex == i) taken = true;
            if (!taken) { start = i; break; }
        }
        list.Add(HordeSetup(faction, team, start));
        return list;
    }

    /// <summary>Match options survival needs: Commander win condition, Commanders placed by the script.</summary>
    public static MatchOptions Options(AiDifficulty difficulty) =>
        new(StartMetal, StartEnergy, WinCondition.Commander, SpawnCommanders: false);

    /// <summary>Everything in one call: the defenders get a Horde slot, a script and options.</summary>
    public static Scenario CreateScenario(MapData map, IReadOnlyList<PlayerSetup> defenders, AiDifficulty difficulty, int seed, string? hordeFaction = null)
    {
        IReadOnlyList<PlayerSetup> players = Players(defenders, map, hordeFaction);
        string json = Build(map, players, difficulty, seed);
        return new Scenario(map, (ulong)seed, players, Options(difficulty), new List<Spawn>(), json);
    }

    // ---- Tuning ------------------------------------------------------------------------------

    private sealed record Tuning(
        int WaveSeconds,
        /// <summary>Base wave size, in tenths of the Easy size.</summary>
        int Size,
        /// <summary>Growth of the main assault entry per minute, in hundredths of a unit.</summary>
        int Growth,
        /// <summary>Tier start times, in hundredths of the Normal times.</summary>
        int TierPace,
        int BountyMetal,
        int BountyEnergy);

    private static Tuning TuningFor(AiDifficulty d) => d switch
    {
        AiDifficulty.Easy => new(100, 10, 25, 125, 150, 300),
        AiDifficulty.Normal => new(75, 13, 35, 100, 140, 280),
        AiDifficulty.Hard => new(55, 17, 50, 85, 120, 240),
        _ => new(40, 22, 70, 70, 100, 200),
    };

    // Normal-difficulty tier starts, in seconds.
    private const int TankStart = 120, MortarStart = 240, ArtilleryStart = 360, AirStart = 720,
        BomberStart = 840, HeavyStart = 1080, LongArtilleryStart = 1260, ExperimentalStart = 1500;

    // ---- Lanes -------------------------------------------------------------------------------

    /// <summary>Where a wave lands and where it goes. Ground waves are laid out in a block below the point.</summary>
    private sealed record Lane(FixVec2 Point, string Direction, FixVec2 Target, bool FromStart);

    /// <summary>The block MissionScript lays a group out in: rows of five, five apart, up to this many rows.</summary>
    private const int BlockHalfWidth = 12, BlockDepth = 32;
    /// <summary>Spawn points stay this far inside the map edge.</summary>
    private const int EdgeInset = 72;
    private const int LaneSeparation = 160;

    private static bool BlockPassable(Terrain terrain, FixVec2 p)
    {
        for (int dy = -4; dy <= BlockDepth; dy += 4)
        for (int dx = -BlockHalfWidth; dx <= BlockHalfWidth; dx += 4)
        {
            FixVec2 q = p + FixVec2.FromInts(dx, dy);
            if (q.X < Fix64.Zero || q.Y < Fix64.Zero || q.X >= Fix64.FromInt(terrain.Map.Width) || q.Y >= Fix64.FromInt(terrain.Map.Height)) return false;
            if (!terrain.Passable(MoveClass.Bot, q) || !terrain.Passable(MoveClass.Vehicle, q)) return false;
        }
        return true;
    }

    /// <summary>The nearest point to <paramref name="around"/> (within 64 units) whose block is open ground, or null.</summary>
    private static FixVec2? FindBlock(Terrain terrain, FixVec2 around)
    {
        for (int r = 0; r <= 64; r += 8)
        for (int dy = -r; dy <= r; dy += 8)
        for (int dx = -r; dx <= r; dx += 8)
        {
            if (Math2.Max(Math2.Abs(dx), Math2.Abs(dy)) != r) continue;
            FixVec2 p = around + FixVec2.FromInts(dx, dy);
            if (BlockPassable(terrain, p)) return p;
        }
        return null;
    }

    private static bool Reachable(Pathfinder pathfinder, FixVec2 from, FixVec2 to)
    {
        List<FixVec2> path = pathfinder.Find(MoveClass.Vehicle, from, to);
        FixVec2 end = path.Count > 0 ? path[^1] : from;
        Fix64 slack = Fix64.FromInt(24);
        return FixVec2.DistanceSquared(end, to) <= slack * slack;
    }

    private static string DirectionOf(FixVec2 from, FixVec2 to)
    {
        long dx = (to.X - from.X).Raw, dy = (to.Y - from.Y).Raw; // Y runs south
        long ax = dx < 0 ? -dx : dx, ay = dy < 0 ? -dy : dy;
        if (ax > 2 * ay) return dx > 0 ? "east" : "west";
        if (ay > 2 * ax) return dy > 0 ? "south" : "north";
        return (dy > 0 ? "south" : "north") + "-" + (dx > 0 ? "east" : "west");
    }

    private static Lane[] ChooseLanes(MapData map, List<FixVec2> defenderStarts, int seed)
    {
        var terrain = new Terrain(map);
        var pathfinder = new Pathfinder(terrain);
        FixVec2 home = Centroid(defenderStarts);
        int w = map.Width, h = map.Height;
        Fix64 minDistance = Fix64.FromInt(Math2.Min(w, h) * 2 / 5);

        // Candidates: the start positions nobody defends, then points just inside the map edge.
        var candidates = new List<(FixVec2 Point, bool FromStart)>();
        foreach (FixVec2 s in map.StartPositions)
            if (!defenderStarts.Contains(s)) candidates.Add((s, true));
        int[] ex = { w / 2, w - EdgeInset, w - EdgeInset, w - EdgeInset, w / 2, EdgeInset, EdgeInset, EdgeInset };
        int[] ey = { EdgeInset, EdgeInset, h / 2, h - EdgeInset, h - EdgeInset, h - EdgeInset, h / 2, EdgeInset };
        for (int i = 0; i < 8; i++) candidates.Add((FixVec2.FromInts(ex[i], ey[i]), false));

        var lanes = new List<Lane>();
        var accepted = new List<FixVec2>();
        bool Accept((FixVec2 Point, bool FromStart) c, bool checkReach)
        {
            foreach (FixVec2 d in defenderStarts)
                if (FixVec2.DistanceSquared(c.Point, d) < minDistance * minDistance) return false;
            FixVec2? block = FindBlock(terrain, c.Point);
            if (block is null) return false;
            FixVec2 p = block.Value;
            Fix64 sep = Fix64.FromInt(LaneSeparation);
            foreach (FixVec2 a in accepted)
                if (FixVec2.DistanceSquared(a, p) < sep * sep) return false;
            // Nearest defender start is the reachability goal; the wave's target rotates over defenders below.
            FixVec2 goal = Nearest(defenderStarts, p);
            if (checkReach && !Reachable(pathfinder, p, goal)) return false;
            accepted.Add(p);
            lanes.Add(new Lane(p, DirectionOf(home, p), goal, c.FromStart));
            return true;
        }

        // Starts first, then edge points farthest from home first.
        foreach (var c in candidates)
            if (c.FromStart && lanes.Count < MaxLanes) Accept(c, true);
        var edges = candidates.FindAll(c => !c.FromStart);
        edges.Sort((a, b) => FixVec2.DistanceSquared(b.Point, home).CompareTo(FixVec2.DistanceSquared(a.Point, home)));
        foreach (var c in edges)
            if (lanes.Count < MaxLanes) Accept(c, true);

        // A map where nothing reaches the base by road: take the farthest open ground and let the pathfinder cope.
        if (lanes.Count == 0)
        {
            edges.Sort((a, b) => FixVec2.DistanceSquared(b.Point, home).CompareTo(FixVec2.DistanceSquared(a.Point, home)));
            foreach (var c in candidates) if (lanes.Count == 0) Accept(c, false);
        }
        if (lanes.Count == 0)
            throw new InvalidDataException($"{map.Name}: no open ground far enough from the defenders for survival waves");

        // Waves rotate over the defenders so a second base is not left alone; the seed picks who is hit first.
        for (int i = 0; i < lanes.Count; i++)
            lanes[i] = lanes[i] with { Target = defenderStarts[(i + seed) % defenderStarts.Count] };
        return lanes.ToArray();
    }

    private static FixVec2 Centroid(List<FixVec2> points)
    {
        Fix64 x = Fix64.Zero, y = Fix64.Zero;
        foreach (FixVec2 p in points) { x += p.X; y += p.Y; }
        return new FixVec2(x / points.Count, y / points.Count);
    }

    private static FixVec2 Nearest(List<FixVec2> points, FixVec2 to)
    {
        FixVec2 best = points[0];
        foreach (FixVec2 p in points)
            if (FixVec2.DistanceSquared(p, to) < FixVec2.DistanceSquared(best, to)) best = p;
        return best;
    }

    // ---- The script ----------------------------------------------------------------------------

    /// <summary>
    /// The survival mission script for a map, a player list (see the class summary for the contract),
    /// a difficulty and a seed. Pure and deterministic: the same inputs give the same JSON.
    /// </summary>
    public static string Build(MapData map, IReadOnlyList<PlayerSetup> players, AiDifficulty difficulty, int seed)
    {
        if (players.Count < 2) throw new ArgumentException("Survival needs a defender and the Horde slot", nameof(players));
        int horde = players.Count - 1;
        PlayerSetup hordeSetup = players[horde];
        if (hordeSetup.Ai != AiDifficulty.None) throw new ArgumentException("The Horde slot must have no AI; the script drives it", nameof(players));
        var defenderStarts = new List<FixVec2>();
        var defenders = new List<int>();
        for (int i = 0; i < horde; i++)
        {
            PlayerSetup p = players[i];
            if (p.Team == hordeSetup.Team) throw new ArgumentException($"{p.Name} shares the Horde's team", nameof(players));
            if (p.Team != players[0].Team) throw new ArgumentException("Defenders must share one team", nameof(players));
            if (p.StartIndex < 0 || p.StartIndex >= map.StartPositions.Count)
                throw new ArgumentException($"{p.Name} has no start position {p.StartIndex} on {map.Name}", nameof(players));
            defenderStarts.Add(map.StartPositions[p.StartIndex]);
            defenders.Add(i);
        }

        Tuning t = TuningFor(difficulty);
        Lane[] lanes = ChooseLanes(map, defenderStarts, Math2.Abs(seed));
        string f = hordeSetup.Faction;
        string tank = f == "lattice" ? "lattice_strider" : "crucible_anvil";
        int Tier(int normalSeconds) => normalSeconds * t.TierPace / 100;
        int Size(int tenthsAtEasy) => Math2.Max(1, tenthsAtEasy * t.Size / 100);
        int period = lanes.Length * t.WaveSeconds;

        var messages = new List<string>
        {
            $"Oversight online. {HordeName} is coming and it will not stop. Build Metal Extractors and power, get a factory up and ring the base with turrets. First wave in {{countdown}}. Every wave you break pays a salvage bounty.",
            "Artillery signatures in the next waves. Push out and kill the guns or they will shell you from range.",
            "Air signatures: gunships and bombers next. Build anti-air turrets and keep flak with the army.",
            "Heavy assault units are rolling in. Thicker armour: bring artillery and Heavy Turrets.",
            "Experimental signature detected. Nothing you have will stop it alone. Mass everything.",
            "Five minutes held.",
            "Ten minutes held. The waves are getting bigger.",
            "Fifteen minutes held.",
            "Twenty minutes held. Few last this long.",
            "Thirty minutes held. Oversight is recording this.",
            "Forty-five minutes held.",
            "One hour held. There is no end to them; there is only how long you stand.",
        };
        int laneMessage = messages.Count;
        foreach (Lane lane in lanes) messages.Add($"Wave {{wave}} incoming from the {lane.Direction}.");

        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            w.WriteStartObject();
            w.WriteString("title", "Survival");
            w.WriteString("briefing", $"Hold {map.Name} against {HordeName} for as long as you can. Waves come every {t.WaveSeconds} seconds and grow without limit. There is no victory, only the clock.");
            w.WriteString("mode", ModeKey);
            w.WriteNumber("humanPlayer", defenders[0]);
            w.WriteStartArray("humanPlayers");
            foreach (int d in defenders) w.WriteNumberValue(d);
            w.WriteEndArray();

            w.WriteStartArray("messages");
            foreach (string m in messages) w.WriteStringValue(m);
            w.WriteEndArray();

            w.WriteStartArray("objectives");
            Objective(w, "hold", "Hold out  ·  wave {wave}  ·  {clock} survived  ·  next wave in {countdown}", false);
            Objective(w, "ten", "Hold for 10 minutes", true);
            Objective(w, "twenty", "Hold for 20 minutes", true);
            Objective(w, "thirty", "Hold for 30 minutes", true);
            w.WriteEndArray();

            // Defenders' Commanders (the match itself spawns none, so the Horde has none).
            w.WriteStartArray("spawns");
            foreach (int d in defenders)
            {
                w.WriteStartObject();
                w.WriteString("tag", "commander");
                w.WriteString("type", players[d].Faction + "_commander");
                w.WriteNumber("owner", d);
                WriteXY(w, map.StartPositions[players[d].StartIndex]);
                w.WriteEndObject();
            }
            w.WriteEndArray();

            w.WriteStartArray("triggers");
            TimeTrigger(w, "intro", 1, 0);
            TimeTrigger(w, "tierArtillery", Tier(ArtilleryStart) - 60, 1);
            TimeTrigger(w, "tierAir", Tier(AirStart) - 60, 2);
            TimeTrigger(w, "tierHeavy", Tier(HeavyStart) - 60, 3);
            TimeTrigger(w, "tierExperimental", Tier(ExperimentalStart) - 60, 4);
            TimeTrigger(w, "held5", 300, 5);
            TimeTrigger(w, "held10", 600, 6, "ten");
            TimeTrigger(w, "held15", 900, 7);
            TimeTrigger(w, "held20", 1200, 8, "twenty");
            TimeTrigger(w, "held30", 1800, 9, "thirty");
            TimeTrigger(w, "held45", 2700, 10);
            TimeTrigger(w, "held60", 3600, 11);

            for (int i = 0; i < lanes.Length; i++)
            {
                Lane lane = lanes[i];
                w.WriteStartObject();
                w.WriteString("id", $"lane{i}");
                w.WriteNumber("every", period);
                w.WriteNumber("after", (i + 1) * t.WaveSeconds);
                w.WriteStartArray("actions");
                w.WriteStartObject(); w.WriteString("type", "wave"); w.WriteEndObject();
                w.WriteStartObject(); w.WriteString("type", "message"); w.WriteNumber("message", laneMessage + i); w.WriteEndObject();
                w.WriteStartObject();
                w.WriteString("type", "bounty");
                w.WriteNumber("metal", t.BountyMetal);
                w.WriteNumber("energy", t.BountyEnergy);
                w.WriteNumber("metalPerWave", t.BountyMetal / 10);
                w.WriteNumber("energyPerWave", t.BountyEnergy / 10);
                w.WriteEndObject();

                w.WriteStartObject();
                w.WriteString("type", "spawn");
                w.WriteString("tag", $"lane{i}");
                w.WriteStartArray("units");
                // (unit, base count, growth in hundredths per minute, max, first appears at)
                Entry(w, $"{f}_assault_bot", horde, lane.Point, Size(30), t.Growth, 30, 0);
                Entry(w, $"{f}_scout_bot", horde, lane.Point, 2, 0, 2, 0);
                Entry(w, tank, horde, lane.Point, Size(12), t.Growth * 60 / 100, 20, Tier(TankStart));
                Entry(w, $"{f}_mortar_bot", horde, lane.Point, 1, t.Growth * 30 / 100, 8, Tier(MortarStart));
                Entry(w, $"{f}_artillery_vehicle", horde, lane.Point, Size(8), t.Growth * 25 / 100, 8, Tier(ArtilleryStart));
                Entry(w, $"{f}_gunship", horde, lane.Point, Size(8), t.Growth * 30 / 100, 10, Tier(AirStart));
                Entry(w, $"{f}_flak_vehicle", horde, lane.Point, 1, t.Growth * 20 / 100, 6, Tier(AirStart));
                Entry(w, $"{f}_bomber", horde, lane.Point, Size(6), t.Growth * 25 / 100, 8, Tier(BomberStart));
                Entry(w, $"{f}_heavy_assault", horde, lane.Point, Size(8), t.Growth * 30 / 100, 10, Tier(HeavyStart));
                Entry(w, $"{f}_long_artillery", horde, lane.Point, 1, t.Growth * 15 / 100, 5, Tier(LongArtilleryStart));
                Entry(w, $"{f}_experimental", horde, lane.Point, 1, t.Growth * 8 / 100, 4, Tier(ExperimentalStart));
                w.WriteEndArray(); // units
                w.WriteStartObject("order");
                w.WriteString("type", "fight");
                WriteXY(w, lane.Target);
                w.WriteEndObject(); // order
                w.WriteEndObject(); // spawn
                w.WriteEndArray(); // actions
                w.WriteEndObject(); // trigger
            }
            w.WriteEndArray(); // triggers
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteXY(Utf8JsonWriter w, FixVec2 p)
    {
        w.WritePropertyName("x");
        w.WriteRawValue(MapData.FixToDecimalText(p.X));
        w.WritePropertyName("y");
        w.WriteRawValue(MapData.FixToDecimalText(p.Y));
    }

    private static void Objective(Utf8JsonWriter w, string id, string text, bool optional)
    {
        w.WriteStartObject();
        w.WriteString("id", id);
        w.WriteString("text", text);
        if (optional) w.WriteBoolean("optional", true);
        w.WriteEndObject();
    }

    private static void TimeTrigger(Utf8JsonWriter w, string id, int seconds, int message, string? completes = null)
    {
        w.WriteStartObject();
        w.WriteString("id", id);
        w.WriteStartObject("when");
        w.WriteString("type", "time");
        w.WriteNumber("seconds", Math2.Max(1, seconds));
        w.WriteEndObject();
        w.WriteStartArray("actions");
        w.WriteStartObject(); w.WriteString("type", "message"); w.WriteNumber("message", message); w.WriteEndObject();
        if (completes is not null)
        {
            w.WriteStartObject();
            w.WriteString("type", "objective");
            w.WriteString("id", completes);
            w.WriteString("state", "complete");
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void Entry(Utf8JsonWriter w, string unit, int owner, FixVec2 at, int count, int growthHundredths, int max, int after)
    {
        w.WriteStartObject();
        w.WriteString("unit", unit);
        w.WriteNumber("owner", owner);
        WriteXY(w, at);
        w.WriteNumber("count", count);
        if (growthHundredths > 0)
        {
            w.WritePropertyName("countPerMinute");
            w.WriteRawValue($"{growthHundredths / 100}.{growthHundredths % 100:D2}");
        }
        w.WriteNumber("maxCount", max);
        if (after > 0) w.WriteNumber("after", after);
        w.WriteEndObject();
    }
}
