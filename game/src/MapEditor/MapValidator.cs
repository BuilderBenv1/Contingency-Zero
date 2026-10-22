using System;
using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Pathing;
using Godot;

namespace ContingencyZero.Game.MapEditor;

/// <summary>How serious a map check result is.</summary>
public enum CheckLevel { Good, Info, Warning, Error }

/// <summary>One line of the checks panel. <see cref="Focus"/> is where to point the camera, if anywhere.</summary>
public sealed record MapCheck(CheckLevel Level, string Text, Vector2? Focus = null);

/// <summary>
/// Looks for problems that would spoil a game on the map, using the sim's own rules: the
/// <see cref="Terrain"/> passability and build limits and the <see cref="Pathfinder"/>.
/// </summary>
public static class MapValidator
{
    /// <summary>Radius around a start, in tiles, that should be mostly buildable.</summary>
    public const int BaseRadiusTiles = 6;
    /// <summary>Radius around a start, in tiles, within which metal spots count as "near".</summary>
    public const int MetalRadiusTiles = 16;
    public const int MinMetalNearStart = 3;

    public static List<MapCheck> Run(MapData map)
    {
        var checks = new List<MapCheck>();
        var terrain = new Terrain(map);
        var paths = new Pathfinder(terrain);
        int starts = map.StartPositions.Count;

        if (starts < MapEdits.MinStarts)
            checks.Add(new MapCheck(CheckLevel.Error, starts == 0
                ? "No start positions. Use the Start positions tool (0) to place at least 2."
                : "Only 1 start position. Place at least one more so two players can play."));

        // ---- Each start ----
        var metalNear = new int[starts];
        for (int i = 0; i < starts; i++)
        {
            FixVec2 sp = map.StartPositions[i];
            Vector2 p = MapEdits.ToVec(sp);
            int tx = Math.Clamp(MapData.TileOf(sp.X), 0, map.TilesX - 1), ty = Math.Clamp(MapData.TileOf(sp.Y), 0, map.TilesY - 1);
            string name = $"Start {i + 1}";

            if (map.TileDepth(tx, ty) > 0)
                checks.Add(new MapCheck(CheckLevel.Warning, $"{name} is under water. Nothing can be built there.", p));
            else if (!terrain.TerrainPassable(MoveClass.Vehicle, tx, ty))
                checks.Add(new MapCheck(CheckLevel.Warning, $"{name} is on a slope too steep for vehicles. Flatten it.", p));

            int area = 0, good = 0;
            for (int y = ty - BaseRadiusTiles; y <= ty + BaseRadiusTiles; y++)
            for (int x = tx - BaseRadiusTiles; x <= tx + BaseRadiusTiles; x++)
            {
                if ((x - tx) * (x - tx) + (y - ty) * (y - ty) > BaseRadiusTiles * BaseRadiusTiles) continue;
                area++;
                if (terrain.GroundSuits(x, y, 1, false)) good++;
            }
            if (good * 100 < area * 55)
                checks.Add(new MapCheck(CheckLevel.Warning, $"{name} has little flat, dry ground for a base ({good * 100 / Math.Max(1, area)}%). Flatten around it.", p));

            int near = 0;
            foreach (MetalSpot s in map.MetalSpots)
            {
                float d = MapEdits.ToVec(s.Position).DistanceTo(p);
                if (d <= MetalRadiusTiles * MapData.TileSize) near++;
            }
            metalNear[i] = near;
            if (near < MinMetalNearStart)
                checks.Add(new MapCheck(CheckLevel.Warning, near == 0
                    ? $"{name} has no metal spots nearby. Place {MinMetalNearStart} or more within reach."
                    : $"{name} has only {near} metal spot{(near == 1 ? "" : "s")} nearby ({MinMetalNearStart} or more is better).", p));
        }

        if (starts >= 2 && metalNear.Max() - metalNear.Min() >= 2)
            checks.Add(new MapCheck(CheckLevel.Warning, "Uneven metal near the starts: " +
                string.Join(", ", metalNear.Select((n, i) => $"{i + 1}: {n}")) + ". Symmetry helps keep maps fair."));

        // ---- Pairs: crowding and whether armies can reach each other ----
        for (int a = 0; a < starts; a++)
        for (int b = a + 1; b < starts; b++)
        {
            float d = MapEdits.ToVec(map.StartPositions[a]).DistanceTo(MapEdits.ToVec(map.StartPositions[b])) / MapData.TileSize;
            if (d < 20)
                checks.Add(new MapCheck(CheckLevel.Warning, $"Starts {a + 1} and {b + 1} are very close ({d:0} tiles apart).", MapEdits.ToVec(map.StartPositions[a])));
        }

        if (starts >= 2)
        {
            int[] bots = Regions(terrain, MoveClass.Bot);
            int[] vehicles = Regions(terrain, MoveClass.Vehicle);
            for (int i = 1; i < starts; i++)
            {
                bool botOk = SameRegion(map, terrain, paths, bots, MoveClass.Bot, 0, i);
                bool vehOk = SameRegion(map, terrain, paths, vehicles, MoveClass.Vehicle, 0, i);
                Vector2 focus = MapEdits.ToVec(map.StartPositions[i]);
                if (!botOk && !vehOk)
                    checks.Add(new MapCheck(CheckLevel.Error, $"Start {i + 1} can't be reached from start 1 by land. Only aircraft, hovercraft or ships could attack.", focus));
                else if (!vehOk)
                    checks.Add(new MapCheck(CheckLevel.Warning, $"Vehicles can't get from start 1 to start {i + 1} (bots can). Smooth a ramp or ford.", focus));
                else if (!botOk)
                    checks.Add(new MapCheck(CheckLevel.Warning, $"Bots can't get from start 1 to start {i + 1}.", focus));
            }
        }

        // ---- Metal spots ----
        if (map.MetalSpots.Count == 0)
        {
            checks.Add(new MapCheck(CheckLevel.Warning, "No metal spots. Players need metal to build anything. Use the Metal spots tool (7)."));
        }
        else
        {
            int bad = 0;
            Vector2? first = null;
            foreach (MetalSpot s in map.MetalSpots)
            {
                int sx = MapData.TileOf(s.Position.X), sy = MapData.TileOf(s.Position.Y);
                if (terrain.GroundSuits(sx, sy, 1, false)) continue;
                bad++;
                first ??= MapEdits.ToVec(s.Position);
            }
            if (bad > 0)
                checks.Add(new MapCheck(CheckLevel.Warning, $"{bad} metal spot{(bad == 1 ? " is" : "s are")} on steep ground or under water, so extractors can't be built there.", first));
        }

        // ---- The good news ----
        if (!checks.Any(c => c.Level >= CheckLevel.Warning))
            checks.Insert(0, new MapCheck(CheckLevel.Good, $"Ready to play: {starts} starts, all reachable, {map.MetalSpots.Count} metal spots."));
        if (starts >= 2)
        {
            int? route = RouteTiles(map, paths, MoveClass.Vehicle, 0, 1);
            if (route is { } r) checks.Add(new MapCheck(CheckLevel.Info, $"Vehicle route from start 1 to start 2: about {r} tiles."));
        }
        return checks;
    }

    /// <summary>
    /// Labels connected areas a mover can travel, using the same moves as the pathfinder
    /// (eight directions, no cutting corners). Impassable tiles get -1.
    /// </summary>
    public static int[] Regions(Terrain terrain, MoveClass c)
    {
        int w = terrain.TilesX, h = terrain.TilesY;
        var label = new int[w * h];
        Array.Fill(label, -1);
        var queue = new Queue<int>();
        int next = 0;
        for (int start = 0; start < label.Length; start++)
        {
            if (label[start] >= 0 || !terrain.Passable(c, start % w, start / w)) continue;
            label[start] = next;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                int cx = cur % w, cy = cur / w;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (!terrain.Passable(c, nx, ny)) continue;
                    if (dx != 0 && dy != 0 && (!terrain.Passable(c, cx + dx, cy) || !terrain.Passable(c, cx, cy + dy))) continue;
                    int n = ny * w + nx;
                    if (label[n] >= 0) continue;
                    label[n] = next;
                    queue.Enqueue(n);
                }
            }
            next++;
        }
        return label;
    }

    private static int RegionOfStart(MapData map, Terrain terrain, Pathfinder paths, int[] regions, MoveClass c, int start)
    {
        // A start on a bad tile still counts if there's usable ground close by (the pathfinder's own fallback).
        FixVec2 p = paths.NearestPassable(c, map.StartPositions[start], 6);
        int tx = Math.Clamp(MapData.TileOf(p.X), 0, map.TilesX - 1), ty = Math.Clamp(MapData.TileOf(p.Y), 0, map.TilesY - 1);
        return regions[terrain.Index(tx, ty)];
    }

    private static bool SameRegion(MapData map, Terrain terrain, Pathfinder paths, int[] regions, MoveClass c, int a, int b)
    {
        int ra = RegionOfStart(map, terrain, paths, regions, c, a), rb = RegionOfStart(map, terrain, paths, regions, c, b);
        return ra >= 0 && ra == rb;
    }

    /// <summary>Length in tiles of the pathfinder's route between two starts, or null if it can't find one.</summary>
    private static int? RouteTiles(MapData map, Pathfinder paths, MoveClass c, int a, int b)
    {
        FixVec2 from = map.StartPositions[a], to = map.StartPositions[b];
        List<FixVec2> route = paths.Find(c, from, to);
        if (route.Count == 0 || route[^1] != to) return null;
        float len = 0;
        Vector2 prev = MapEdits.ToVec(from);
        foreach (FixVec2 w in route)
        {
            Vector2 q = MapEdits.ToVec(w);
            len += prev.DistanceTo(q);
            prev = q;
        }
        return (int)MathF.Round(len / MapData.TileSize);
    }
}
