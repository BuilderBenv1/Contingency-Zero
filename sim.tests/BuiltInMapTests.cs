using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Pathing;
using Xunit;
using static ContingencyZero.Sim.Tests.TestData;

namespace ContingencyZero.Sim.Tests;

/// <summary>Checks every map in data/maps, so a new or regenerated map can't ship broken.</summary>
public class BuiltInMapTests
{
    /// <summary>Naval maps: the starts are joined for walkers (bots, amphibious) but not for vehicles.</summary>
    private static readonly HashSet<string> WalkersOnly = new(StringComparer.Ordinal) { "shoal_chain" };

    private static List<string> MapNames() =>
        Directory.GetFiles(Path.Combine(DataDirectory(), "maps"), "*.json")
            .Select(p => Path.GetFileNameWithoutExtension(p)!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

    private static (int X, int Y) Tile(FixVec2 p) => (MapData.TileOf(p.X), MapData.TileOf(p.Y));

    /// <summary>Tiles a mover can reach from a start, stepping like the pathfinder (8 ways, no cutting corners).</summary>
    private static bool[] Reach(Terrain t, MoveClass c, (int X, int Y) from)
    {
        var seen = new bool[t.TilesX * t.TilesY];
        if (!t.TerrainPassable(c, from.X, from.Y)) return seen;
        var queue = new Queue<(int X, int Y)>();
        seen[t.Index(from.X, from.Y)] = true;
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if ((dx == 0 && dy == 0) || !t.TerrainPassable(c, nx, ny) || seen[t.Index(nx, ny)]) continue;
                if (dx != 0 && dy != 0 && (!t.TerrainPassable(c, nx, y) || !t.TerrainPassable(c, x, ny))) continue;
                seen[t.Index(nx, ny)] = true;
                queue.Enqueue((nx, ny));
            }
        }
        return seen;
    }

    [Fact]
    public void Every_built_in_map_loads_and_round_trips()
    {
        List<string> names = MapNames();
        Assert.True(names.Count >= 14, $"expected at least 14 built-in maps, found {names.Count}");
        foreach (string name in names)
        {
            MapData map = LoadMap(name);
            Assert.True(map.StartPositions.Count >= 2, $"{name}: fewer than two starts");
            Assert.NotEmpty(map.MetalSpots);
            Assert.True(map.WindMin <= map.WindMax, $"{name}: wind range is backwards");
            Assert.Equal(map.Fingerprint(), MapData.FromJson(map.ToJson()).Fingerprint());
        }
    }

    [Fact]
    public void Every_start_is_passable_with_room_to_build()
    {
        foreach (string name in MapNames())
        {
            MapData map = LoadMap(name);
            var terrain = new Terrain(map);
            for (int i = 0; i < map.StartPositions.Count; i++)
            {
                var (sx, sy) = Tile(map.StartPositions[i]);
                Assert.True(terrain.TerrainPassable(MoveClass.Bot, sx, sy), $"{name}: start {i} is not passable");
                int sites = 0;
                for (int ty = sy - 6; ty <= sy + 6; ty++)
                for (int tx = sx - 6; tx <= sx + 6; tx++)
                    if (terrain.GroundSuits(tx, ty, 3, needsWater: false)) sites++;
                Assert.True(sites >= 20, $"{name}: start {i} has room for only {sites} factory sites");
            }
        }
    }

    [Fact]
    public void Every_metal_spot_can_take_an_extractor()
    {
        foreach (string name in MapNames())
        {
            MapData map = LoadMap(name);
            var terrain = new Terrain(map);
            foreach (MetalSpot s in map.MetalSpots)
            {
                var (x, y) = Tile(s.Position);
                Assert.True(terrain.GroundSuits(x, y, 1, needsWater: false), $"{name}: metal spot at tile {x},{y} can't be built on");
                Assert.True(s.Yield > Fix64.Zero, $"{name}: metal spot at tile {x},{y} yields nothing");
            }
        }
    }

    [Fact]
    public void Every_start_reaches_every_other()
    {
        foreach (string name in MapNames())
        {
            MapData map = LoadMap(name);
            var terrain = new Terrain(map);
            var pathfinder = new Pathfinder(terrain);
            MoveClass[] classes = WalkersOnly.Contains(name)
                ? new[] { MoveClass.Bot, MoveClass.Amphibious }
                : new[] { MoveClass.Bot, MoveClass.Vehicle };
            foreach (MoveClass c in classes)
            {
                bool[] seen = Reach(terrain, c, Tile(map.StartPositions[0]));
                for (int i = 1; i < map.StartPositions.Count; i++)
                {
                    var (x, y) = Tile(map.StartPositions[i]);
                    Assert.True(seen[terrain.Index(x, y)], $"{name}: {c} can't get from start 0 to start {i}");
                    // The real pathfinder has a search budget: make sure it still finds the way.
                    List<FixVec2> path = pathfinder.Find(c, map.StartPositions[0], map.StartPositions[i]);
                    Assert.True(path.Count > 0 && path[^1] == map.StartPositions[i],
                        $"{name}: pathfinder gives up on {c} from start 0 to start {i}");
                }
            }
        }
    }

    [Fact]
    public void Starts_get_the_same_metal_nearby()
    {
        foreach (string name in MapNames())
        {
            MapData map = LoadMap(name);
            var totals = new List<Fix64>();
            foreach (FixVec2 start in map.StartPositions)
            {
                Fix64 total = Fix64.Zero;
                Fix64 r2 = Fix64.FromInt(11 * MapData.TileSize * 11 * MapData.TileSize);
                foreach (MetalSpot s in map.MetalSpots)
                    if (FixVec2.DistanceSquared(s.Position, start) <= r2) total += s.Yield;
                totals.Add(total);
            }
            Assert.True(totals.All(t => t == totals[0]), $"{name}: metal near each start differs ({string.Join(", ", totals)})");
            Assert.True(totals[0] >= Fix64.FromInt(8), $"{name}: only {totals[0]} metal/s near each start");
        }
    }
}
