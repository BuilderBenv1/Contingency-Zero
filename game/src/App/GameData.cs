using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using Godot;
using FileAccess = Godot.FileAccess;

namespace ContingencyZero.Game.App;

/// <summary>A map the player can pick: built in (embedded) or made in the editor (user://maps).</summary>
public sealed record MapEntry(string Key, string Name, bool BuiltIn, Func<MapData> Load);

/// <summary>
/// Loads the game's data: units, maps and missions ship as resources embedded in the assembly,
/// user-made maps live in user://maps. Everything is loaded once and cached.
/// </summary>
public static class GameData
{
    private static UnitCatalog? _catalog;
    private static readonly Dictionary<string, string> _embedded = LoadEmbedded();

    public const string UserMapsDir = "user://maps";
    public const string ReplaysDir = "user://replays";

    private static Dictionary<string, string> LoadEmbedded()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        Assembly asm = typeof(GameData).Assembly;
        foreach (string name in asm.GetManifestResourceNames())
        {
            if (!name.StartsWith("data/", StringComparison.Ordinal)) continue;
            using Stream? s = asm.GetManifestResourceStream(name);
            if (s is null) continue;
            using var reader = new StreamReader(s);
            result[name.Replace('\\', '/')] = reader.ReadToEnd();
        }
        return result;
    }

    public static UnitCatalog Catalog => _catalog ??= UnitCatalog.LoadTexts(
        _embedded.Where(kv => kv.Key.StartsWith("data/units/", StringComparison.Ordinal))
                 .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                 .Select(kv => (kv.Key, kv.Value)));

    public static string? Embedded(string path) => _embedded.TryGetValue(path, out var text) ? text : null;

    public static IEnumerable<string> EmbeddedUnder(string prefix) =>
        _embedded.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal);

    /// <summary>Built-in skirmish maps in a sensible order, then the player's own maps.</summary>
    public static List<MapEntry> Maps()
    {
        string[] order = { "proving_grounds", "ashfall_plains", "twin_rivers", "crater_basin", "coastal_divide", "frostline" };
        var list = new List<MapEntry>();
        var builtIn = EmbeddedUnder("data/maps/").ToList();
        foreach (string key in order.Concat(builtIn.Select(p => Path.GetFileNameWithoutExtension(p) ?? "")).Where(k => k.Length > 0).Distinct())
        {
            string path = $"data/maps/{key}.json";
            if (!_embedded.ContainsKey(path)) continue;
            MapData header = MapData.FromJson(_embedded[path]);
            list.Add(new MapEntry(key!, header.Name, true, () => MapData.FromJson(_embedded[path])));
        }

        DirAccess.MakeDirRecursiveAbsolute(UserMapsDir);
        using var dir = DirAccess.Open(UserMapsDir);
        if (dir is not null)
        {
            foreach (string file in dir.GetFiles().Where(f => f.EndsWith(".json")).OrderBy(f => f, StringComparer.Ordinal))
            {
                string full = $"{UserMapsDir}/{file}";
                try
                {
                    string text = FileAccess.GetFileAsString(full);
                    MapData header = MapData.FromJson(text);
                    list.Add(new MapEntry("user:" + file, header.Name + "  (yours)", false, () => MapData.FromJson(FileAccess.GetFileAsString(full))));
                }
                catch (Exception e)
                {
                    GD.PushWarning($"Skipping broken map {file}: {e.Message}");
                }
            }
        }
        return list;
    }

    public static void SaveUserMap(MapData map, string fileName)
    {
        DirAccess.MakeDirRecursiveAbsolute(UserMapsDir);
        using var f = FileAccess.Open($"{UserMapsDir}/{fileName}", FileAccess.ModeFlags.Write);
        f?.StoreString(map.ToJson());
    }

    public static string SafeFileName(string name)
    {
        var chars = name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        string s = new string(chars).Trim('_');
        return (s.Length == 0 ? "map" : s) + ".json";
    }
}
