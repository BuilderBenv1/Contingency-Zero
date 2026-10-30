using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using ContingencyZero.Sim.Pathing;
using ContingencyZero.Sim.State;
using static ContingencyZero.Sim.Tests.TestData;
using Xunit;

namespace ContingencyZero.Sim.Tests;

public class ConnectivityTests
{
    /// <summary>A 32x32 flat map cut in two by a deep east-west channel across the middle.</summary>
    private static MapData Split()
    {
        MapData map = MapData.Flat("split", 32, 32);
        map.WaterLevel = 30;
        int vx = map.TilesX + 1;
        for (int y = 13; y <= 19; y++)
        for (int x = 0; x < vx; x++)
            map.Heights[y * vx + x] = 0;
        return map;
    }

    private static FixVec2 Tile(int tx, int ty) =>
        FixVec2.FromInts(tx * MapData.TileSize + MapData.TileSize / 2, ty * MapData.TileSize + MapData.TileSize / 2);

    [Fact]
    public void A_deep_channel_splits_land_into_two_zones_but_ships_have_one()
    {
        var terrain = new Terrain(Split());
        Assert.NotEqual(terrain.ZoneOf(MoveClass.Vehicle, 5, 4), terrain.ZoneOf(MoveClass.Vehicle, 5, 28));
        Assert.Equal(terrain.ZoneOf(MoveClass.Vehicle, 2, 4), terrain.ZoneOf(MoveClass.Vehicle, 29, 6));
        Assert.Equal(-1, terrain.ZoneOf(MoveClass.Vehicle, 5, 16));
        Assert.True(terrain.ZoneOf(MoveClass.Ship, 5, 16) >= 0);
    }

    [Fact]
    public void An_unreachable_goal_sends_the_mover_to_the_nearest_reachable_tile_without_a_long_search()
    {
        var terrain = new Terrain(Split());
        var pf = new Pathfinder(terrain);
        var path = pf.Find(MoveClass.Vehicle, Tile(10, 4), Tile(10, 28));
        Assert.NotEmpty(path);
        FixVec2 end = path[^1];
        int ex = MapData.TileOf(end.X), ey = MapData.TileOf(end.Y);
        Assert.Equal(terrain.ZoneOf(MoveClass.Vehicle, 10, 4), terrain.ZoneOf(MoveClass.Vehicle, ex, ey));
        Assert.True(ey < 14, $"stopped at the near bank, not across the channel (row {ey})");
        Assert.True(pf.LastExpansions < 400, $"searched {pf.LastExpansions} tiles");
    }

    [Fact]
    public void A_builder_that_cannot_reach_the_site_gives_the_order_up()
    {
        var players = new[] { new PlayerSetup("A", 0, "lattice", 0), new PlayerSetup("B", 1, "crucible", 1) };
        var world = new World(Catalog, Split(), 1, players, new MatchOptions(1000, 1000, SpawnCommanders: false, RevealMap: true));
        int ship = world.SpawnUnit("lattice_builder_sea", 0, Tile(10, 16));
        // A solar collector well inland: the ship can't get within build range.
        world.Step(new[] { new Command(0, 0, CommandType.Build, ship, Tile(10, 3), TypeIndex: Type("lattice_solar_collector")) });
        RunUntil(world, () => world.Units.Orders[ship].Count == 0, 1500);
        Assert.Empty(world.Units.Orders[ship]);
        for (int id = 0; id < world.Units.Count; id++)
            Assert.False(world.Units.Alive[id] && world.Units.TypeIndex[id] == Type("lattice_solar_collector"), "no frame should be started");

        // At the water's edge it is in range, and builds.
        world.Step(new[] { new Command(world.Tick, 0, CommandType.Build, ship, Tile(10, 11), TypeIndex: Type("lattice_wind_turbine")) });
        RunUntil(world, () => world.Units.CurrentOrder(ship).Target >= 0 && world.Units.CurrentOrder(ship).Type == OrderType.Build, 1500);
        Assert.True(world.Units.CurrentOrder(ship).Type == OrderType.Build,
            $"place {world.CanPlace(Type("lattice_wind_turbine"), 0, Tile(10, 11))}, ship at {world.Units.Position[ship]}");
    }

    [Fact]
    public void Zones_follow_the_ground_when_it_changes()
    {
        MapData map = Split();
        var terrain = new Terrain(map);
        Assert.NotEqual(terrain.ZoneOf(MoveClass.Vehicle, 5, 4), terrain.ZoneOf(MoveClass.Vehicle, 5, 28));
        // Fill the channel back in: the two halves join.
        int vx = map.TilesX + 1;
        for (int y = 13; y <= 19; y++)
        for (int x = 0; x < vx; x++)
            map.Heights[y * vx + x] = 40;
        terrain.RefreshPassability(0, 0, map.TilesX - 1, map.TilesY - 1);
        Assert.Equal(terrain.ZoneOf(MoveClass.Vehicle, 5, 4), terrain.ZoneOf(MoveClass.Vehicle, 5, 28));
    }
}
