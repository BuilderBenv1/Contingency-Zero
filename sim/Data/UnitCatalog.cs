using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ContingencyZero.Sim.Core;

namespace ContingencyZero.Sim.Data;

/// <summary>
/// All unit types in a match, in a fixed order. The sim refers to types by index into this list.
/// Units are sorted by id when loaded so the order never depends on file system enumeration.
/// </summary>
public sealed class UnitCatalog
{
    private readonly List<UnitDef> _defs;
    private readonly Dictionary<string, int> _indexById;

    public UnitCatalog(IEnumerable<UnitDef> defs)
    {
        _defs = defs.OrderBy(d => d.Id, StringComparer.Ordinal).ToList();
        _indexById = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < _defs.Count; i++)
        {
            if (!_indexById.TryAdd(_defs[i].Id, i))
                throw new InvalidDataException($"Duplicate unit id '{_defs[i].Id}'");
        }

        foreach (UnitDef def in _defs)
        {
            var options = new int[def.BuildOptionIds.Count];
            for (int i = 0; i < options.Length; i++)
            {
                if (!_indexById.TryGetValue(def.BuildOptionIds[i], out options[i]))
                    throw new InvalidDataException($"'{def.Id}' can build unknown unit '{def.BuildOptionIds[i]}'");
            }
            def.BuildOptions = options;
        }
    }

    public int Count => _defs.Count;
    public UnitDef this[int index] => _defs[index];
    public IReadOnlyList<UnitDef> All => _defs;

    public int IndexOf(string id) =>
        _indexById.TryGetValue(id, out int i) ? i : throw new KeyNotFoundException($"Unknown unit id '{id}'");

    public bool TryIndexOf(string id, out int index) => _indexById.TryGetValue(id, out index);

    /// <summary>The faction's Commander type.</summary>
    public int CommanderOf(string faction)
    {
        for (int i = 0; i < _defs.Count; i++)
            if (_defs[i].IsCommander && _defs[i].Faction == faction) return i;
        throw new KeyNotFoundException($"No Commander for faction '{faction}'");
    }

    /// <summary>A stable fingerprint of every stat, so replays and multiplayer can refuse mismatched data.</summary>
    public ulong Fingerprint { get; private set; }

    /// <summary>Loads every *.json file under a folder (recursively). Each file holds one unit object.</summary>
    public static UnitCatalog LoadDirectory(string directory)
    {
        var files = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories)
            .OrderBy(f => f.Replace('\\', '/'), StringComparer.Ordinal)
            .ToList();
        var texts = files.Select(File.ReadAllText).ToList();
        var catalog = new UnitCatalog(texts.Select((t, i) => ParseUnit(t, files[i])));
        catalog.Fingerprint = FingerprintOf(catalog);
        return catalog;
    }

    /// <summary>Loads from (name, json) pairs, for platforms where the data lives inside a package.</summary>
    public static UnitCatalog LoadTexts(IEnumerable<(string Name, string Json)> files)
    {
        var catalog = new UnitCatalog(files.Select(f => ParseUnit(f.Json, f.Name)));
        catalog.Fingerprint = FingerprintOf(catalog);
        return catalog;
    }

    private static ulong FingerprintOf(UnitCatalog catalog)
    {
        var h = new StateHasher();
        foreach (UnitDef d in catalog._defs)
        {
            foreach (char c in d.Id) h.Add((int)c);
            h.Add(d.MetalCost); h.Add(d.EnergyCost); h.Add(d.BuildTime); h.Add(d.MaxHealth);
            h.Add(d.SpeedPerTick); h.Add(d.BuildPowerPerTick); h.Add(d.SightRange);
            foreach (WeaponDef w in d.Weapons)
            {
                h.Add((int)w.Kind); h.Add(w.Range); h.Add(w.Damage); h.Add(w.ReloadTicks); h.Add(w.Splash);
            }
            foreach (int o in d.BuildOptions) h.Add(o);
        }
        return h.Value;
    }

    private static int AtLeastOne(int value) => value < 1 ? 1 : value;
    private static int SecondsToTicks(Fix64 seconds) =>
        AtLeastOne((seconds * World.TicksPerSecond + Fix64.Half).ToIntFloor());
    private static Fix64 PerTick(Fix64 perSecond) => perSecond / World.TicksPerSecond;

    private static Fix64 Num(JsonElement obj, string name, string fallback = "0") =>
        obj.TryGetProperty(name, out JsonElement e) ? Fix64.Parse(e.GetRawText()) : Fix64.Parse(fallback);

    private static bool Bool(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.True;

    private static string Text(JsonElement obj, string name, string fallback) =>
        obj.TryGetProperty(name, out JsonElement e) && e.GetString() is { } s ? s : fallback;

    /// <summary>
    /// Parses one unit definition. Numbers are read from their raw JSON text with <see cref="Fix64.Parse"/>,
    /// so no floating point is involved at any stage. Rates in the file are per second.
    /// </summary>
    public static UnitDef ParseUnit(string json, string sourceName = "<inline>")
    {
        using var doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        string Str(string name) =>
            root.TryGetProperty(name, out JsonElement e) && e.GetString() is { } s
                ? s
                : throw new InvalidDataException($"{sourceName}: missing '{name}'");

        var weapons = new List<WeaponDef>();
        if (root.TryGetProperty("weapons", out JsonElement weaponArray))
        {
            foreach (JsonElement w in weaponArray.EnumerateArray())
            {
                string kindText = Text(w, "kind", "laser");
                if (!Enum.TryParse(kindText, ignoreCase: true, out WeaponKind kind))
                    throw new InvalidDataException($"{sourceName}: unknown weapon kind '{kindText}'");
                bool air = Bool(w, "targetsAir");
                bool groundGiven = w.TryGetProperty("targetsGround", out JsonElement g);
                weapons.Add(new WeaponDef
                {
                    Kind = kind,
                    Range = Num(w, "range"),
                    MinRange = Num(w, "minRange"),
                    Damage = Num(w, "damage"),
                    ReloadTicks = SecondsToTicks(Num(w, "reloadSeconds", "1")),
                    Accuracy = Num(w, "accuracy", "1"),
                    ProjectileSpeedPerTick = PerTick(Num(w, "projectileSpeed")),
                    Splash = Num(w, "splash"),
                    TargetsAir = air,
                    TargetsGround = groundGiven ? g.ValueKind == JsonValueKind.True : !air,
                    EnergyPerShot = Num(w, "energyPerShot"),
                });
            }
        }

        var buildOptions = new List<string>();
        if (root.TryGetProperty("buildOptions", out JsonElement optionArray))
            foreach (JsonElement o in optionArray.EnumerateArray())
                buildOptions.Add(o.GetString() ?? throw new InvalidDataException($"{sourceName}: bad build option"));

        string moveText = Text(root, "moveClass", "none");
        if (!Enum.TryParse(moveText, ignoreCase: true, out MoveClass moveClass))
            throw new InvalidDataException($"{sourceName}: unknown move class '{moveText}'");

        return new UnitDef
        {
            Id = Str("id"),
            Name = Str("name"),
            Faction = Str("faction"),
            Description = Text(root, "description", ""),
            Role = Text(root, "role", ""),
            Tier = Num(root, "tier", "1").ToIntFloor(),
            MetalCost = Num(root, "metalCost"),
            EnergyCost = Num(root, "energyCost"),
            BuildTime = Num(root, "buildTime", "1"),
            MaxHealth = Num(root, "health", "1"),
            MoveClass = moveClass,
            SpeedPerTick = PerTick(Num(root, "speed")),
            Radius = Num(root, "radius", "2"),
            SightRange = Num(root, "sightRange", "20"),
            RadarRange = Num(root, "radarRange"),
            IsCommander = Bool(root, "commander"),
            Footprint = Num(root, "footprint", "1").ToIntFloor(),
            NeedsMetalSpot = Bool(root, "needsMetalSpot"),
            NeedsWater = Bool(root, "needsWater"),
            ExtractorMultiplier = Num(root, "extractorMultiplier"),
            Weapons = weapons,
            BuildPowerPerTick = PerTick(Num(root, "buildPower")),
            BuildRange = Num(root, "buildRange", "10"),
            ReclaimMultiplier = Num(root, "reclaimMultiplier", "1"),
            BuildOptionIds = buildOptions,
            MetalIncomePerTick = PerTick(Num(root, "metalIncome")),
            EnergyIncomePerTick = PerTick(Num(root, "energyIncome")),
            UsesWind = Bool(root, "usesWind"),
            MetalStorage = Num(root, "metalStorage"),
            EnergyStorage = Num(root, "energyStorage"),
            ConvertEnergyPerTick = PerTick(Num(root, "convertEnergy")),
            ConvertMetalPerTick = PerTick(Num(root, "convertMetal")),
            WreckFraction = Num(root, "wreckFraction", "0.6"),
            DeathExplosionDamage = Num(root, "deathExplosionDamage"),
            DeathExplosionRadius = Num(root, "deathExplosionRadius"),
            SelfReclaimRange = Num(root, "selfReclaimRange"),
        };
    }
}
