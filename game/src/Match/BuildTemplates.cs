using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using Godot;
using FileAccess = Godot.FileAccess;

namespace ContingencyZero.Game.Match;

/// <summary>One structure in a template: its unit type id and where it sits, in tiles, relative to the anchor structure's footprint origin.</summary>
public sealed class TemplateItem
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("dx")] public int Dx { get; set; }
    [JsonPropertyName("dy")] public int Dy { get; set; }
}

/// <summary>A saved base layout: a name and a list of structures with tile offsets. Item 0 is the anchor (offset 0,0).</summary>
public sealed class BuildTemplate
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("items")] public List<TemplateItem> Items { get; set; } = new();

    /// <summary>The layout turned a quarter turn clockwise (as seen from above), re-anchored on item 0.</summary>
    public BuildTemplate Rotated(UnitCatalog catalog)
    {
        var items = new List<TemplateItem>();
        foreach (TemplateItem it in Items)
        {
            int fp = catalog.TryIndexOf(it.Type, out int idx) ? catalog[idx].Footprint : 1;
            // A footprint covering tiles [dx, dx+fp) x [dy, dy+fp) rotated 90 degrees about the tile corner (0,0)
            // covers [-dy-fp, -dy) x [dx, dx+fp): the maths stays on whole tiles, so nothing ever misaligns.
            items.Add(new TemplateItem { Type = it.Type, Dx = -it.Dy - fp, Dy = it.Dx });
        }
        if (items.Count > 0)
        {
            int ax = items[0].Dx, ay = items[0].Dy;
            foreach (TemplateItem it in items) { it.Dx -= ax; it.Dy -= ay; }
        }
        return new BuildTemplate { Name = Name, Items = items };
    }

    /// <summary>Builds a template from the player's selected structures. Returns null if there are none.</summary>
    public static BuildTemplate? FromStructures(World world, IEnumerable<int> ids, int owner, string name)
    {
        var structures = ids.Where(id => world.Units.IsValid(id) && world.Units.Owner[id] == owner
                                         && world.Catalog[world.Units.TypeIndex[id]].IsStructure && world.Units.TileX[id] >= 0).ToList();
        if (structures.Count == 0) return null;
        // Anchor on the structure nearest the top-left so the layout reads naturally under the cursor.
        int anchor = structures.OrderBy(id => world.Units.TileY[id]).ThenBy(id => world.Units.TileX[id]).First();
        int ax = world.Units.TileX[anchor], ay = world.Units.TileY[anchor];
        var t = new BuildTemplate { Name = name };
        t.Items.Add(new TemplateItem { Type = world.Catalog[world.Units.TypeIndex[anchor]].Id, Dx = 0, Dy = 0 });
        foreach (int id in structures.OrderBy(id => world.Units.TileY[id]).ThenBy(id => world.Units.TileX[id]))
        {
            if (id == anchor) continue;
            t.Items.Add(new TemplateItem { Type = world.Catalog[world.Units.TypeIndex[id]].Id, Dx = world.Units.TileX[id] - ax, Dy = world.Units.TileY[id] - ay });
        }
        return t;
    }
}

/// <summary>
/// Where a template would go if stamped with its anchor at a given tile: every structure with its
/// snapped centre and whether the sim would accept it there. Extractors are moved to the nearest
/// free metal spot within two tiles of where the template puts them, or skipped.
/// </summary>
public sealed class TemplatePlan
{
    public readonly List<(int Type, FixVec2 Centre, bool Valid)> Items = new();
    public int ValidCount => Items.Count(i => i.Valid);

    public static TemplatePlan Make(World world, int player, BuildTemplate template, int anchorTx, int anchorTy, Func<int, bool> canBuildType)
    {
        var plan = new TemplatePlan();
        var takenSpots = new HashSet<int>();
        foreach (TemplateItem it in template.Items)
        {
            if (!world.Catalog.TryIndexOf(it.Type, out int type)) continue;
            UnitDef def = world.Catalog[type];
            if (!def.IsStructure) continue;
            int tx = anchorTx + it.Dx, ty = anchorTy + it.Dy;
            FixVec2 centre = Terrain.FootprintCentre(tx, ty, def.Footprint);
            if (!canBuildType(type))
            {
                plan.Items.Add((type, centre, false));
                continue;
            }
            bool ok = world.CanPlace(type, player, centre, out _, out _) == PlacementResult.Ok;
            if (!ok && def.NeedsMetalSpot)
            {
                // Hunt for a free metal spot within two tiles of where the template wanted it.
                Fix64 reach = Fix64.FromInt(2 * MapData.TileSize);
                Fix64 best = Fix64.MaxValue;
                FixVec2 bestCentre = centre;
                int bestSpot = -1;
                for (int s = 0; s < world.Map.MetalSpots.Count; s++)
                {
                    if (takenSpots.Contains(s)) continue;
                    FixVec2 sp = world.Map.MetalSpots[s].Position;
                    Fix64 d = FixVec2.Distance(sp, centre);
                    if (d > reach || d >= best) continue;
                    if (world.CanPlace(type, player, sp, out int stx, out int sty) != PlacementResult.Ok) continue;
                    best = d;
                    bestCentre = Terrain.FootprintCentre(stx, sty, def.Footprint);
                    bestSpot = s;
                }
                if (bestSpot >= 0)
                {
                    takenSpots.Add(bestSpot);
                    plan.Items.Add((type, bestCentre, true));
                    continue;
                }
            }
            plan.Items.Add((type, centre, ok));
        }
        return plan;
    }
}

/// <summary>The player's saved build templates, kept in user://templates.json.</summary>
public static class TemplateStore
{
    private const string FilePath = "user://templates.json";
    private static List<BuildTemplate>? _all;

    public static List<BuildTemplate> All => _all ??= Load();
    public static event Action? Changed;

    private static List<BuildTemplate> Load()
    {
        try
        {
            if (FileAccess.FileExists(FilePath))
            {
                string text = FileAccess.GetFileAsString(FilePath);
                var list = JsonSerializer.Deserialize<List<BuildTemplate>>(text);
                if (list is not null) return list.Where(t => t.Items.Count > 0).ToList();
            }
        }
        catch (JsonException e)
        {
            GD.PushWarning($"Couldn't read templates: {e.Message}");
        }
        return new List<BuildTemplate>();
    }

    public static void Save()
    {
        using var f = FileAccess.Open(FilePath, FileAccess.ModeFlags.Write);
        f?.StoreString(JsonSerializer.Serialize(All, new JsonSerializerOptions { WriteIndented = true }));
        Changed?.Invoke();
    }

    /// <summary>Adds a template, replacing any with the same name.</summary>
    public static void Add(BuildTemplate t)
    {
        All.RemoveAll(x => string.Equals(x.Name, t.Name, StringComparison.OrdinalIgnoreCase));
        All.Add(t);
        Save();
    }

    public static void Rename(BuildTemplate t, string name)
    {
        name = name.Trim();
        if (name.Length == 0) return;
        All.RemoveAll(x => x != t && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        t.Name = name;
        Save();
    }

    public static void Delete(BuildTemplate t)
    {
        All.Remove(t);
        Save();
    }

    public static BuildTemplate? Find(string name) =>
        All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
}
