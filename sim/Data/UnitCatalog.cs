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
    }

    public int Count => _defs.Count;
    public UnitDef this[int index] => _defs[index];
    public int IndexOf(string id) =>
        _indexById.TryGetValue(id, out int i) ? i : throw new KeyNotFoundException($"Unknown unit id '{id}'");

    /// <summary>Loads every *.json file under a folder (recursively). Each file holds one unit object.</summary>
    public static UnitCatalog LoadDirectory(string directory)
    {
        var files = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal);
        return new UnitCatalog(files.Select(f => ParseUnit(File.ReadAllText(f), f)));
    }

    private static int AtLeastOne(int value) => value < 1 ? 1 : value;

    /// <summary>
    /// Parses one unit definition. Numbers are read from their raw JSON text with <see cref="Fix64.Parse"/>,
    /// so no floating point is involved at any stage. Rates in the file are per second.
    /// </summary>
    public static UnitDef ParseUnit(string json, string sourceName = "<inline>")
    {
        using var doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        Fix64 Num(string name, string fallback = "0") =>
            root.TryGetProperty(name, out JsonElement e) ? Fix64.Parse(e.GetRawText()) : Fix64.Parse(fallback);

        string Str(string name) =>
            root.TryGetProperty(name, out JsonElement e) && e.GetString() is { } s
                ? s
                : throw new InvalidDataException($"{sourceName}: missing '{name}'");

        Fix64 perTick(Fix64 perSecond) => perSecond / World.TicksPerSecond;

        return new UnitDef
        {
            Id = Str("id"),
            Name = Str("name"),
            Faction = Str("faction"),
            MetalCost = Num("metalCost"),
            EnergyCost = Num("energyCost"),
            BuildTime = Num("buildTime", "1"),
            MaxHealth = Num("health", "1"),
            SpeedPerTick = perTick(Num("speed")),
            SightRange = Num("sightRange", "10"),
            IsCommander = root.TryGetProperty("commander", out JsonElement c) && c.GetBoolean(),
            WeaponRange = Num("weaponRange"),
            WeaponDamage = Num("weaponDamage"),
            ReloadTicks = AtLeastOne((Num("reloadSeconds", "1") * World.TicksPerSecond + Fix64.Half).ToIntFloor()),
            Accuracy = Num("accuracy", "1"),
            BuildPowerPerTick = perTick(Num("buildPower")),
            BuildRange = Num("buildRange", "4"),
            MetalIncomePerTick = perTick(Num("metalIncome")),
            EnergyIncomePerTick = perTick(Num("energyIncome")),
            MetalStorage = Num("metalStorage"),
            EnergyStorage = Num("energyStorage"),
            WreckFraction = Num("wreckFraction", "0.6"),
        };
    }
}
