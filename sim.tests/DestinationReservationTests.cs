using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using Xunit;
using static ContingencyZero.Sim.Tests.TestData;

namespace ContingencyZero.Sim.Tests;

public class DestinationReservationTests
{
    private static FixVec2 Tile(int tx, int ty) =>
        FixVec2.FromInts(tx * MapData.TileSize + MapData.TileSize / 2, ty * MapData.TileSize + MapData.TileSize / 2);

    private static MapData Split()
    {
        MapData map = MapData.Flat("reservation-split", 32, 32);
        map.WaterLevel = 30;
        int vx = map.TilesX + 1;
        for (int y = 13; y <= 19; y++)
        for (int x = 0; x < vx; x++)
            map.Heights[y * vx + x] = 0;
        return map;
    }

    [Fact]
    public void Active_moves_claim_unique_passable_tiles_and_stop_releases_them()
    {
        World world = Flat();
        int a = world.SpawnUnit("lattice_strider", 0, At(60, 60));
        int b = world.SpawnUnit("lattice_strider", 0, At(60, 80));
        FixVec2 goal = At(140, 100);
        world.Step(new[]
        {
            new Command(0, 0, CommandType.Move, a, goal),
            new Command(0, 0, CommandType.Move, b, goal),
        });

        int claimA = world.Units.ReservedGoal[a];
        int claimB = world.Units.ReservedGoal[b];
        Assert.True(claimA >= 0);
        Assert.True(claimB >= 0);
        Assert.NotEqual(claimA, claimB);
        Assert.Equal(a, world.Units.GoalOwner(claimA));
        Assert.Equal(b, world.Units.GoalOwner(claimB));
        foreach (int unit in new[] { a, b })
        {
            FixVec2 p = world.Units.CurrentOrder(unit).Position;
            Assert.True(world.Terrain.Passable(MoveClass.Vehicle, p));
        }

        world.Step(new[]
        {
            new Command(0, 0, CommandType.Stop, a),
            new Command(0, 0, CommandType.Stop, b),
        });
        Assert.Equal(-1, world.Units.ReservedGoal[a]);
        Assert.Equal(-1, world.Units.ReservedGoal[b]);
        Assert.Equal(-1, world.Units.GoalOwner(claimA));
        Assert.Equal(-1, world.Units.GoalOwner(claimB));
    }

    [Fact]
    public void Queued_destinations_wait_and_death_releases_only_the_dead_units_claim()
    {
        World world = Flat();
        int a = world.SpawnUnit("lattice_strider", 0, At(60, 60));
        int b = world.SpawnUnit("lattice_strider", 0, At(60, 80));
        FixVec2 first = At(120, 60), second = At(180, 100);
        world.Step(new[]
        {
            new Command(0, 0, CommandType.Move, a, first),
            new Command(0, 0, CommandType.Move, a, second, Queue: true),
            new Command(0, 0, CommandType.Move, b, second),
        });

        int secondTile = world.Terrain.Index(MapData.TileOf(second.X), MapData.TileOf(second.Y));
        Assert.NotEqual(secondTile, world.Units.ReservedGoal[a]);
        Assert.Equal(secondTile, world.Units.ReservedGoal[b]);
        int claimA = world.Units.ReservedGoal[a];
        world.Kill(a);
        Assert.Equal(-1, world.Units.ReservedGoal[a]);
        Assert.Equal(-1, world.Units.GoalOwner(claimA));
        Assert.Equal(b, world.Units.GoalOwner(secondTile));
    }

    [Fact]
    public void A_land_destination_in_deep_water_spirals_to_the_near_bank()
    {
        var map = Split();
        var players = new[]
        {
            new Match.PlayerSetup("A", 0, "lattice", 0),
            new Match.PlayerSetup("B", 1, "crucible", 1),
        };
        var world = new World(Catalog, map, 1, players,
            new Match.MatchOptions(SpawnCommanders: false, RevealMap: true));
        int tank = world.SpawnUnit("lattice_strider", 0, Tile(10, 4));
        world.Step(new[] { new Command(0, 0, CommandType.Move, tank, Tile(10, 16)) });

        FixVec2 goal = world.Units.CurrentOrder(tank).Position;
        int tx = MapData.TileOf(goal.X), ty = MapData.TileOf(goal.Y);
        Assert.True(world.Terrain.Passable(MoveClass.Vehicle, tx, ty));
        Assert.True(ty < 13, $"goal row {ty} is not on the near bank");
        Assert.Equal(world.Terrain.Index(tx, ty), world.Units.ReservedGoal[tank]);
    }
}
