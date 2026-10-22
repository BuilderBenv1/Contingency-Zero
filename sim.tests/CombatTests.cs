using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.State;
using Xunit;

namespace ContingencyZero.Sim.Tests;

public class CombatTests
{
    private static World NewWorld() =>
        new(TestData.Catalog, seed: 3, mapWidth: 256, mapHeight: 256, playerTeams: new[] { 0, 1 });

    [Fact]
    public void Units_in_range_fight_until_one_dies_and_leaves_a_wreck()
    {
        World world = NewWorld();
        int a = world.SpawnUnit("lattice_strider", 0, TestData.At(100, 100));
        int b = world.SpawnUnit("crucible_anvil", 1, TestData.At(110, 100));

        for (int i = 0; i < 2000 && world.Units.Alive[a] && world.Units.Alive[b]; i++) world.Step();

        Assert.True(world.Units.Alive[a] ^ world.Units.Alive[b]);
        Assert.Single(world.Wrecks);
    }

    [Fact]
    public void Units_out_of_range_hold_fire()
    {
        World world = NewWorld();
        int a = world.SpawnUnit("lattice_strider", 0, TestData.At(20, 20));
        int b = world.SpawnUnit("crucible_anvil", 1, TestData.At(200, 200));

        for (int i = 0; i < 100; i++) world.Step();

        Assert.Equal(world.Catalog[world.Units.TypeIndex[a]].MaxHealth, world.Units.Health[a]);
        Assert.Equal(world.Catalog[world.Units.TypeIndex[b]].MaxHealth, world.Units.Health[b]);
    }

    [Fact]
    public void Allies_never_shoot_each_other()
    {
        World world = new(TestData.Catalog, 3, 256, 256, new[] { 0, 0 });
        int a = world.SpawnUnit("lattice_strider", 0, TestData.At(100, 100));
        int b = world.SpawnUnit("crucible_anvil", 1, TestData.At(105, 100));

        for (int i = 0; i < 100; i++) world.Step();

        Assert.Equal(-1, world.Units.WeaponTarget[a]);
        Assert.Equal(-1, world.Units.WeaponTarget[b]);
    }

    [Fact]
    public void Attack_order_closes_into_range()
    {
        World world = NewWorld();
        int a = world.SpawnUnit("lattice_strider", 0, TestData.At(20, 100));
        int b = world.SpawnUnit("crucible_solar_collector", 1, TestData.At(120, 100));

        world.Step(new[] { new Command(0, 0, CommandType.Attack, a, TargetId: b) });
        for (int i = 0; i < 1000 && world.Units.Alive[b]; i++) world.Step();
        world.Step(); // the order clears on the tick after the target dies

        Assert.False(world.Units.Alive[b]);
        Assert.Equal(OrderType.None, world.Units.Order[a]);
    }

    [Fact]
    public void Losing_the_Commander_defeats_the_player()
    {
        World world = NewWorld();
        world.SpawnUnit("lattice_commander", 0, TestData.At(20, 20));
        int enemyCmdr = world.SpawnUnit("crucible_commander", 1, TestData.At(200, 200));
        Assert.Null(world.WinningTeam());

        world.Kill(enemyCmdr);

        Assert.True(world.Players[1].Defeated);
        Assert.Equal(0, world.WinningTeam());
    }

    [Fact]
    public void Players_cannot_command_enemy_units()
    {
        World world = NewWorld();
        int enemy = world.SpawnUnit("crucible_anvil", 1, TestData.At(50, 50));

        world.Step(new[] { new Command(0, 0, CommandType.Move, enemy, TestData.At(0, 0)) });

        Assert.Equal(OrderType.None, world.Units.Order[enemy]);
        Assert.Equal(TestData.At(50, 50), world.Units.Position[enemy]);
    }
}
