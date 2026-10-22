using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.State;
using Xunit;
using static ContingencyZero.Sim.Tests.TestData;

namespace ContingencyZero.Sim.Tests;

public class CombatTests
{
    [Fact]
    public void Units_in_range_fight_until_one_dies_and_leaves_a_wreck()
    {
        World world = Flat();
        int a = world.SpawnUnit("lattice_strider", 0, At(100, 100));
        int b = world.SpawnUnit("crucible_anvil", 1, At(112, 100));

        RunUntil(world, () => !world.Units.Alive[a] || !world.Units.Alive[b], 3000);

        Assert.True(world.Units.Alive[a] ^ world.Units.Alive[b]);
        Assert.Single(world.Features);
        Assert.Equal(1, world.Players[world.Units.Alive[a] ? 0 : 1].Kills);
    }

    [Fact]
    public void Units_out_of_range_hold_still_and_fire()
    {
        World world = Flat();
        int a = world.SpawnUnit("lattice_strider", 0, At(20, 20));
        int b = world.SpawnUnit("crucible_anvil", 1, At(220, 220));

        for (int i = 0; i < 100; i++) world.Step();

        Assert.Equal(world.Catalog[world.Units.TypeIndex[a]].MaxHealth, world.Units.Health[a]);
        Assert.Equal(world.Catalog[world.Units.TypeIndex[b]].MaxHealth, world.Units.Health[b]);
    }

    [Fact]
    public void Allies_never_shoot_each_other()
    {
        World world = Flat(sameTeam: true);
        int a = world.SpawnUnit("lattice_strider", 0, At(100, 100));
        int b = world.SpawnUnit("crucible_anvil", 1, At(106, 100));

        for (int i = 0; i < 100; i++) world.Step();

        Assert.Equal(-1, world.Units.Target0[a]);
        Assert.Equal(-1, world.Units.Target0[b]);
    }

    [Fact]
    public void Attack_order_closes_into_range_and_kills()
    {
        World world = Flat();
        int a = world.SpawnUnit("lattice_strider", 0, At(30, 100));
        int b = world.SpawnUnit("crucible_solar_collector", 1, At(160, 100));

        world.Step(new[] { new Command(0, 0, CommandType.Attack, a, TargetId: b) });
        RunUntil(world, () => !world.Units.Alive[b], 2000);
        world.Step();

        Assert.False(world.Units.Alive[b]);
        Assert.Equal(OrderType.None, world.Units.CurrentOrder(a).Type);
    }

    [Fact]
    public void Ground_weapons_ignore_aircraft_and_anti_air_ignores_ground()
    {
        World world = Flat();
        int tank = world.SpawnUnit("lattice_strider", 0, At(100, 100));
        int fighter = world.SpawnUnit("crucible_fighter", 1, At(106, 100));
        int aa = world.SpawnUnit("lattice_aa_bot", 0, At(100, 106));
        int enemyTank = world.SpawnUnit("crucible_anvil", 1, At(100, 112));
        world.Units.Fire[fighter] = FireState.HoldFire;
        world.Units.Fire[enemyTank] = FireState.HoldFire;

        for (int i = 0; i < 5; i++) world.Step();

        Assert.Equal(enemyTank, world.Units.Target0[tank]);
        Assert.Equal(fighter, world.Units.Target0[aa]);
    }

    [Fact]
    public void Artillery_shells_fly_and_splash()
    {
        World world = Flat();
        int arty = world.SpawnUnit("lattice_artillery_vehicle", 0, At(40, 100));
        int t1 = world.SpawnUnit("crucible_anvil", 1, At(90, 100));
        int t2 = world.SpawnUnit("crucible_anvil", 1, At(92, 101));
        world.Units.Fire[t1] = FireState.HoldFire;
        world.Units.Fire[t2] = FireState.HoldFire;

        bool sawProjectile = false;
        for (int i = 0; i < 200; i++)
        {
            world.Step();
            if (world.Projectiles.Count > 0) sawProjectile = true;
        }

        Assert.True(sawProjectile);
        var hp = world.Catalog[Type("crucible_anvil")].MaxHealth;
        Assert.True(world.Units.Health[t1] < hp || world.Units.Health[t2] < hp);
    }

    [Fact]
    public void Breach_cannon_destroys_what_it_touches_and_costs_energy()
    {
        World world = Flat(1000, 1000);
        int cmdr = world.SpawnUnit("lattice_commander", 0, At(60, 100));
        int t1 = world.SpawnUnit("crucible_anvil", 1, At(70, 100));
        int t2 = world.SpawnUnit("crucible_anvil", 1, At(76, 100));
        int bystander = world.SpawnUnit("crucible_anvil", 1, At(70, 140));

        world.Step(new[] { new Command(0, 0, CommandType.DGun, cmdr, At(80, 100)) });
        for (int i = 0; i < 30; i++) world.Step();

        Assert.False(world.Units.Alive[t1]);
        Assert.False(world.Units.Alive[t2]);
        Assert.True(world.Units.Alive[bystander]);
        Assert.True(world.Players[0].Energy < Fix64.FromInt(700));
    }

    [Fact]
    public void Losing_the_Commander_defeats_the_player_and_ends_the_match()
    {
        World world = new(Catalog, Map.MapData.Flat("T", 32, 32), 3, new[]
        {
            new Match.PlayerSetup("A", 0, "lattice", 0),
            new Match.PlayerSetup("B", 1, "crucible", 1),
        });
        int enemyCmdr = -1;
        for (int id = 0; id < world.Units.Count; id++)
            if (world.Units.Owner[id] == 1) enemyCmdr = id;
        int enemyTank = world.SpawnUnit("crucible_anvil", 1, At(200, 200));
        Assert.Null(world.WinningTeam);

        world.Kill(enemyCmdr);
        world.Step();

        Assert.True(world.Players[1].Defeated);
        Assert.False(world.Units.Alive[enemyTank]);
        Assert.Equal(0, world.WinningTeam);
        Assert.True(world.IsOver);
    }

    [Fact]
    public void Players_cannot_command_enemy_units()
    {
        World world = Flat();
        int enemy = world.SpawnUnit("crucible_anvil", 1, At(50, 50));

        world.Step(new[] { new Command(0, 0, CommandType.Move, enemy, At(0, 0)) });

        Assert.Equal(OrderType.None, world.Units.CurrentOrder(enemy).Type);
        Assert.Equal(At(50, 50), world.Units.Position[enemy]);
    }

    [Fact]
    public void Fusion_reactor_blows_up_when_destroyed()
    {
        World world = Flat();
        int fusion = world.SpawnUnit("lattice_fusion_reactor", 0, At(100, 100));
        int nearby = world.SpawnUnit("lattice_strider", 0, At(100, 125));
        world.Units.Health[fusion] = Fix64.Zero;
        world.Step();
        Assert.False(world.Units.Alive[fusion]);
        Assert.False(world.Units.Alive[nearby]);
    }
}
