using System.Collections.Generic;
using ContingencyZero.Sim.AI;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using ContingencyZero.Sim.Replay;
using Xunit;
using static ContingencyZero.Sim.Tests.TestData;

namespace ContingencyZero.Sim.Tests;

public class AiTargetingTests
{
    private static FixVec2 Tile(int tx, int ty) =>
        FixVec2.FromInts(tx * MapData.TileSize + MapData.TileSize / 2, ty * MapData.TileSize + MapData.TileSize / 2);

    private static MapData Split()
    {
        MapData map = MapData.Flat("targeting-split", 32, 32);
        map.WaterLevel = 30;
        int vx = map.TilesX + 1;
        for (int y = 13; y <= 19; y++)
        for (int x = 0; x < vx; x++)
            map.Heights[y * vx + x] = 0;
        return map;
    }

    private static World On(MapData map, params PlayerSetup[] players) =>
        new(Catalog, map, 5, players, new MatchOptions(SpawnCommanders: false, RevealMap: true));

    private static PlayerSetup Player(string name, int team, int start = 0, AiDifficulty ai = AiDifficulty.None) =>
        new(name, team, team == 0 ? "lattice" : "crucible", start, ai);

    [Fact]
    public void Target_choice_is_near_then_sticky_until_the_target_dies()
    {
        World world = On(MapData.Flat("sticky", 32, 32), Player("A", 0), Player("B", 1));
        int army = world.SpawnUnit("lattice_strider", 0, At(50, 50));
        int selected = world.SpawnUnit("crucible_solar_collector", 1, At(120, 50));
        world.SpawnUnit("crucible_solar_collector", 1, At(190, 50));
        var ai = new AiController(world, 0, AiDifficulty.Hard, 5);

        Assert.Equal(world.Units.Position[selected], ai.AttackTarget(new[] { army }));
        int closer = world.SpawnUnit("crucible_solar_collector", 1, At(65, 50));
        Assert.Equal(world.Units.Position[selected], ai.AttackTarget(new[] { army }));
        world.Kill(selected);
        Assert.Equal(world.Units.Position[closer], ai.AttackTarget(new[] { army }));
    }

    [Fact]
    public void A_unit_that_hit_us_outscores_a_closer_enemy()
    {
        World world = On(MapData.Flat("retaliation", 32, 32), Player("A", 0), Player("B", 1));
        int army = world.SpawnUnit("lattice_strider", 0, At(50, 50));
        world.SpawnUnit("crucible_solar_collector", 1, At(90, 50));
        int attacker = world.SpawnUnit("crucible_solar_collector", 1, At(220, 50));
        world.Units.LastAttacker[army] = attacker;
        var ai = new AiController(world, 0, AiDifficulty.Hard, 5);

        Assert.Equal(world.Units.Position[attacker], ai.AttackTarget(new[] { army }));
    }

    [Fact]
    public void Land_waves_reject_other_zones_but_ships_use_firing_water()
    {
        World world = On(Split(), Player("A", 0), Player("B", 1));
        int tank = world.SpawnUnit("lattice_strider", 0, Tile(5, 4));
        int across = world.SpawnUnit("crucible_solar_collector", 1, Tile(5, 20));
        int sameSide = world.SpawnUnit("crucible_solar_collector", 1, Tile(28, 4));
        var ai = new AiController(world, 0, AiDifficulty.Hard, 5);

        Assert.False(ai.CanReachTarget(tank, world.Units.Position[across]));
        Assert.Equal(world.Units.Position[sameSide], ai.AttackTarget(new[] { tank }));

        int ship = world.SpawnUnit("lattice_patrol_boat", 0, Tile(10, 16));
        Assert.True(ai.CanReachTarget(ship, world.Units.Position[across]));
        Assert.Equal(world.Units.Position[across], ai.AttackTarget(new[] { ship }));
    }

    [Fact]
    public void Allied_AIs_focus_the_enemy_already_being_attacked()
    {
        MapData map = MapData.Flat("focus", 32, 32, startPositions: 0);
        map.StartPositions.Add(At(50, 50));
        map.StartPositions.Add(At(50, 100));
        map.StartPositions.Add(At(240, 240));
        var players = new[]
        {
            Player("A0", 0, 0, AiDifficulty.Hard),
            Player("A1", 0, 1, AiDifficulty.Hard),
            Player("B", 1, 2, AiDifficulty.Hard),
        };
        var world = new World(Catalog, map, 5, players,
            new MatchOptions(SpawnCommanders: true, RevealMap: true));
        world.Kill(2); // keep the Commander priority bonus out of this focus-fire check
        int nearA0 = world.SpawnUnit("crucible_solar_collector", 2, At(140, 50));
        world.SpawnUnit("crucible_solar_collector", 2, At(125, 100));

        Assert.Equal(world.Units.Position[nearA0], world.Ais[0].AttackTarget(new[] { 0 }));
        Assert.Equal(world.Units.Position[nearA0], world.Ais[1].AttackTarget(new[] { 1 }));
    }

    [Fact]
    public void Shoal_chain_enters_island_mode_and_produces_combat_ships()
    {
        Scenario scenario = Scenarios.AiSkirmish(LoadMap("shoal_chain"), 11, AiDifficulty.Hard, AiDifficulty.Easy);
        World world = scenario.CreateWorld(Catalog);
        Assert.True(world.Ais[0].IslandMode, "enemy start should not be land-reachable");
        RunUntil(world, () =>
        {
            bool ship = false;
            for (int id = 0; id < world.Units.Count; id++)
                if (world.Units.Alive[id] && world.Units.Complete[id] && world.Units.Owner[id] == 0
                    && world.Catalog[world.Units.TypeIndex[id]].MoveClass == MoveClass.Ship
                    && world.Catalog[world.Units.TypeIndex[id]].IsArmed) ship = true;
            return ship;
        }, 10 * 60 * World.TicksPerSecond);

        int yards = 0;
        bool combatShip = false;
        for (int id = 0; id < world.Units.Count; id++)
        {
            if (!world.Units.Alive[id] || !world.Units.Complete[id] || world.Units.Owner[id] != 0) continue;
            UnitDef def = world.Catalog[world.Units.TypeIndex[id]];
            if (def.Role == "factory_ship")
            {
                yards++;
                Assert.True(world.Units.HasRally[id], "shipyard has no rally point");
                Assert.True(world.Terrain.Passable(MoveClass.Ship, world.Units.Rally[id]), "shipyard rally is not in water");
            }
            if (def.MoveClass == MoveClass.Ship && def.IsArmed) combatShip = true;
        }
        Assert.True(yards > 0, "no completed shipyard");
        Assert.True(combatShip, "no completed combat ship");
    }
}
