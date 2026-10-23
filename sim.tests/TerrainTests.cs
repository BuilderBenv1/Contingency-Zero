using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using ContingencyZero.Sim.State;
using Xunit;
using static ContingencyZero.Sim.Tests.TestData;

namespace ContingencyZero.Sim.Tests;

/// <summary>Craters: explosions reshape the ground, which changes where vehicles can drive.</summary>
public class TerrainTests
{
    [Fact]
    public void A_match_reshapes_its_own_copy_of_the_map()
    {
        var map = MapData.Flat("Test", 32, 32);
        var players = new[] { new PlayerSetup("A", 0, "lattice", 0), new PlayerSetup("B", 1, "crucible", 1) };
        var world = new World(Catalog, map, 1, players, new MatchOptions(SpawnCommanders: false, RevealMap: true));
        world.DeformTerrain(At(128, 128), Fix64.FromInt(20), 40);
        Assert.True(map.Heights.All(h => h == 40), "the scenario's map must not change");
        Assert.True(world.Map.Heights.Any(h => h != 40), "the match's ground should have a crater");
    }

    [Fact]
    public void A_deep_crater_blocks_vehicles_but_not_bots()
    {
        World world = Flat();
        world.DeformTerrain(At(128, 128), Fix64.FromInt(22), 29);
        int tx = 128 / MapData.TileSize, ty = 128 / MapData.TileSize;
        // Somewhere on the bowl's slope.
        bool anyVehicleBlocked = false, allBotsFine = true;
        for (int y = ty - 3; y <= ty + 3; y++)
        for (int x = tx - 3; x <= tx + 3; x++)
        {
            if (!world.Terrain.TerrainPassable(MoveClass.Vehicle, x, y)) anyVehicleBlocked = true;
            if (!world.Terrain.TerrainPassable(MoveClass.Bot, x, y)) allBotsFine = false;
        }
        Assert.True(anyVehicleBlocked, "a Commander-sized crater should be too steep for vehicles somewhere");
        Assert.True(allBotsFine, "bots can walk through craters");
        Assert.True(world.Map.CornerHeight(tx, ty) <= 40 - 20, $"the middle should be deep, got {world.Map.CornerHeight(tx, ty)}");
        // There's a low rim just outside the bowl.
        Assert.True(world.Map.CornerHeight(tx + 3, ty) > 40, "the rim should be raised");
    }

    [Fact]
    public void Craters_never_dig_below_the_cap_and_leave_structures_level()
    {
        World world = Flat(5000, 5000);
        int solar = world.SpawnUnit("lattice_solar_collector", 0, At(160, 128));
        int footTx = world.Units.TileX[solar], footTy = world.Units.TileY[solar];
        var before = new List<int>();
        for (int y = footTy; y <= footTy + 2; y++) for (int x = footTx; x <= footTx + 2; x++) before.Add(world.Map.CornerHeight(x, y));

        for (int i = 0; i < 10; i++) world.DeformTerrain(At(128, 128), Fix64.FromInt(24), 40);
        int cap = System.Math.Min(Terrain.MaxCraterDepth, 24 * 14 / 10);
        Assert.Equal(System.Math.Max(0, 40 - cap), world.Map.CornerHeight(16, 16));

        var after = new List<int>();
        for (int y = footTy; y <= footTy + 2; y++) for (int x = footTx; x <= footTx + 2; x++) after.Add(world.Map.CornerHeight(x, y));
        Assert.Equal(before, after);
    }

    [Fact]
    public void A_commanders_death_blast_leaves_a_crater_and_a_terrain_event()
    {
        World world = Flat();
        int cmdr = world.SpawnUnit("lattice_commander", 0, At(128, 128));
        world.Kill(cmdr, leaveWreck: true, explode: true);
        world.ResolveDeaths();
        Assert.Contains(GameEventType.TerrainChanged, world.Events.Select(e => e.Type));
        Assert.True(world.Map.CornerHeight(16, 16) < 40);
    }

    [Fact]
    public void A_vehicle_paths_round_a_fresh_crater()
    {
        World world = Flat(5000, 5000);
        int tank = world.SpawnUnit("lattice_strider", 0, At(60, 128));
        world.Step(new[] { new Command(0, 0, CommandType.Move, tank, At(200, 128)) });
        for (int i = 0; i < 20; i++) world.Step();
        // Dig a wide bowl across its path, three times over so the slopes are steep.
        for (int i = 0; i < 3; i++) world.DeformTerrain(At(128, 128), Fix64.FromInt(28), 34);
        Fix64 lowest = Fix64.MaxValue;
        for (int i = 0; i < 1200 && FixVec2.Distance(world.Units.Position[tank], At(200, 128)) > Fix64.FromInt(4); i++)
        {
            world.Step();
            lowest = Fix64.Min(lowest, world.Map.GroundHeightAt(world.Units.Position[tank]));
        }
        Assert.True(FixVec2.Distance(world.Units.Position[tank], At(200, 128)) <= Fix64.FromInt(4), "the tank should still arrive");
        Assert.True(lowest > Fix64.FromInt(40 - 16), $"the tank should skirt the crater, but went down to {lowest}");
    }
}
