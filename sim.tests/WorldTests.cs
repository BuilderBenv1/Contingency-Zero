using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using ContingencyZero.Sim.State;
using Xunit;
using static ContingencyZero.Sim.Tests.TestData;

namespace ContingencyZero.Sim.Tests;

public class PathingTests
{
    /// <summary>A flat map split by a wall of deep water with a gap at the north end.</summary>
    private static MapData RiverMap()
    {
        var map = MapData.Flat("River", 32, 32, height: 40);
        map.WaterLevel = 30;
        for (int vy = 6; vy <= 32; vy++)
        for (int vx = 15; vx <= 17; vx++)
            map.SetCornerHeight(vx, vy, 0);
        return map;
    }

    private static World World(MapData map) =>
        new(Catalog, map, 1, new[] { new PlayerSetup("A", 0, "lattice", 0), new PlayerSetup("B", 1, "crucible", 1) },
            new MatchOptions(SpawnCommanders: false, RevealMap: true));

    [Fact]
    public void Vehicles_path_round_deep_water()
    {
        World world = World(RiverMap());
        int tank = world.SpawnUnit("lattice_strider", 0, At(60, 200));
        world.Step(new[] { new Command(0, 0, CommandType.Move, tank, At(200, 200)) });

        bool wentNorth = false;
        RunUntil(world, () =>
        {
            if (world.Units.Position[tank].Y < Fix64.FromInt(60)) wentNorth = true;
            return world.Units.CurrentOrder(tank).Type == OrderType.None;
        }, 3000);

        Assert.True(wentNorth, "the tank should have gone round through the gap");
        Assert.True(FixVec2.Distance(world.Units.Position[tank], At(200, 200)) < Fix64.FromInt(6));
    }

    [Fact]
    public void Hovercraft_and_amphibious_units_cross_water_ships_stay_in_it()
    {
        World world = World(RiverMap());
        var t = world.Terrain;
        int river = 16, row = 25;
        Assert.False(t.TerrainPassable(Data.MoveClass.Vehicle, river, row));
        Assert.True(t.TerrainPassable(Data.MoveClass.Amphibious, river, row));
        Assert.True(t.TerrainPassable(Data.MoveClass.Hover, river, row));
        Assert.True(t.TerrainPassable(Data.MoveClass.Ship, river, row));
        Assert.False(t.TerrainPassable(Data.MoveClass.Ship, 5, row));
    }

    [Fact]
    public void Structures_block_paths_and_units_walk_round_them()
    {
        World world = World(MapData.Flat("Wall", 32, 32));
        for (int y = 0; y < 26; y++)
            world.SpawnUnit("lattice_light_turret", 0, FixVec2.FromInts(128 + 4, y * 8 + 4));
        int bot = world.SpawnUnit("lattice_assault_bot", 0, At(60, 60));
        world.Step(new[] { new Command(0, 0, CommandType.Move, bot, At(200, 60)) });

        RunUntil(world, () => world.Units.CurrentOrder(bot).Type == OrderType.None, 3000);
        Assert.True(FixVec2.Distance(world.Units.Position[bot], At(200, 60)) < Fix64.FromInt(6));
    }

    [Fact]
    public void Crowds_spread_out_instead_of_stacking()
    {
        World world = World(MapData.Flat("Crowd", 32, 32));
        var ids = new int[12];
        var commands = new Command[12];
        for (int i = 0; i < 12; i++)
        {
            ids[i] = world.SpawnUnit("lattice_strider", 0, At(40 + i * 3, 40));
            commands[i] = new Command(0, 0, CommandType.Move, ids[i], At(150, 150));
        }
        world.Step(commands);
        for (int i = 0; i < 900; i++) world.Step();

        for (int i = 0; i < 12; i++)
        for (int j = i + 1; j < 12; j++)
            Assert.True(FixVec2.Distance(world.Units.Position[ids[i]], world.Units.Position[ids[j]]) > Fix64.FromInt(2),
                $"units {i} and {j} overlap");
    }
}

public class VisionTests
{
    [Fact]
    public void Fog_hides_distant_enemies_until_something_sees_them()
    {
        var world = new World(Catalog, MapData.Flat("Fog", 32, 32), 1,
            new[] { new PlayerSetup("A", 0, "lattice", 0), new PlayerSetup("B", 1, "crucible", 1) },
            new MatchOptions(SpawnCommanders: false));
        world.SpawnUnit("lattice_strider", 0, At(30, 30));
        int enemy = world.SpawnUnit("crucible_anvil", 1, At(220, 220));
        world.Step();
        Assert.False(world.CanSee(0, enemy));

        int scout = world.SpawnUnit("lattice_scout_bot", 0, At(200, 200));
        world.Step(); world.Step(); world.Step();
        Assert.True(world.CanSee(0, enemy));
        Assert.True(world.Vision.IsExplored(0, 27, 27));
        world.Kill(scout);
        world.Step(); world.Step(); world.Step();
        Assert.False(world.CanSee(0, enemy));
        Assert.True(world.Vision.IsExplored(0, 27, 27));
    }

    [Fact]
    public void Radar_shows_contacts_beyond_sight()
    {
        var world = new World(Catalog, MapData.Flat("Radar", 32, 32), 1,
            new[] { new PlayerSetup("A", 0, "lattice", 0), new PlayerSetup("B", 1, "crucible", 1) },
            new MatchOptions(SpawnCommanders: false));
        world.SpawnUnit("lattice_radar_tower", 0, At(40, 40));
        int enemy = world.SpawnUnit("crucible_anvil", 1, At(150, 150));
        world.Step(); world.Step(); world.Step();
        Assert.False(world.CanSee(0, enemy));
        Assert.True(world.CanSee(0, enemy, radarCounts: true));
    }
}

public class MapTests
{
    [Theory]
    [InlineData("proving_grounds")]
    [InlineData("ashfall_plains")]
    [InlineData("twin_rivers")]
    [InlineData("crater_basin")]
    [InlineData("coastal_divide")]
    [InlineData("frostline")]
    public void Built_in_maps_load_and_round_trip(string name)
    {
        MapData map = LoadMap(name);
        Assert.True(map.StartPositions.Count >= 2);
        Assert.NotEmpty(map.MetalSpots);
        MapData again = MapData.FromJson(map.ToJson());
        Assert.Equal(map.Fingerprint(), again.Fingerprint());
    }

    [Theory]
    [InlineData("proving_grounds")]
    [InlineData("twin_rivers")]
    [InlineData("crater_basin")]
    [InlineData("frostline")]
    public void Every_start_can_put_down_a_factory_and_reach_the_others(string name)
    {
        MapData map = LoadMap(name);
        var players = new PlayerSetup[map.StartPositions.Count];
        for (int i = 0; i < players.Length; i++) players[i] = new PlayerSetup($"P{i}", i, i % 2 == 0 ? "lattice" : "crucible", i);
        var world = new World(Catalog, map, 1, players);
        int lab = Type("lattice_vehicle_plant");
        for (int i = 0; i < players.Length; i++)
        {
            FixVec2 s = map.StartPositions[i];
            bool placed = false;
            for (int dy = -40; dy <= 40 && !placed; dy += 8)
            for (int dx = -40; dx <= 40 && !placed; dx += 8)
                placed = world.CanPlace(lab, i, s + FixVec2.FromInts(dx, dy)) == PlacementResult.Ok;
            Assert.True(placed, $"start {i} has no room for a factory");
        }
        var path = world.Pathfinder.Find(Data.MoveClass.Vehicle, map.StartPositions[0], map.StartPositions[1]);
        Assert.Equal(map.StartPositions[1], path[^1]);
    }

    [Fact]
    public void Fix_values_survive_json_exactly()
    {
        Assert.Equal("1.5", MapData.FixToDecimalText(Fix64.Parse("1.5")));
        Fix64 odd = Fix64.FromRaw(123457);
        Assert.Equal(odd, Fix64.Parse(MapData.FixToDecimalText(odd)));
        Assert.Equal(-odd, Fix64.Parse(MapData.FixToDecimalText(-odd)));
    }
}
