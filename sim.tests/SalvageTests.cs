using System.Linq;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.State;
using Xunit;
using static ContingencyZero.Sim.Tests.TestData;

namespace ContingencyZero.Sim.Tests;

/// <summary>Salvage: reclaiming enough enemy wrecks of a design lets your factories build it.</summary>
public class SalvageTests
{
    private static World ReclaimEnemyWrecks(int count, out int builder)
    {
        World world = Flat(0, 0);
        builder = world.SpawnUnit("lattice_surveyor", 0, At(60, 60));
        for (int i = 0; i < count; i++)
            world.Kill(world.SpawnUnit("crucible_anvil", 1, At(90 + i * 12, 60)));
        world.Step(new[] { new Command(0, 0, CommandType.ReclaimArea, builder, At(100, 60), Arg: 60) });
        RunUntil(world, () => world.Features.All(f => f.Gone), 4000);
        return world;
    }

    [Fact]
    public void Three_enemy_wrecks_unlock_the_design_and_raise_events()
    {
        World world = ReclaimEnemyWrecks(3, out _);
        int anvil = Type("crucible_anvil");
        Assert.Equal(3, world.Players[0].SalvageProgress[anvil]);
        Assert.True(world.HasSalvaged(0, anvil));
        Assert.False(world.HasSalvaged(1, anvil), "the Crucible already builds the Anvil; only the enemy salvages it");
    }

    [Fact]
    public void Two_wrecks_are_not_enough()
    {
        World world = ReclaimEnemyWrecks(2, out _);
        Assert.Equal(2, world.Players[0].SalvageProgress[Type("crucible_anvil")]);
        Assert.False(world.HasSalvaged(0, Type("crucible_anvil")));
    }

    [Fact]
    public void A_salvaged_tank_can_be_queued_at_a_vehicle_plant_but_not_a_bot_lab()
    {
        World world = ReclaimEnemyWrecks(3, out _);
        world.Players[0].Metal = Fix64.FromInt(5000);
        world.Players[0].Energy = Fix64.FromInt(5000);
        world.SpawnUnit("lattice_fusion_reactor", 0, At(30, 220));
        int plant = world.SpawnUnit("lattice_vehicle_plant", 0, At(150, 150));
        int lab = world.SpawnUnit("lattice_bot_lab", 0, At(60, 150));
        int anvil = Type("crucible_anvil");

        Assert.Contains(anvil, world.SalvagedOptions(world.Catalog[world.Units.TypeIndex[plant]], 0));
        Assert.DoesNotContain(anvil, world.SalvagedOptions(world.Catalog[world.Units.TypeIndex[lab]], 0));

        world.Step(new[]
        {
            new Command(0, 0, CommandType.FactoryQueue, plant, TypeIndex: anvil, Arg: 1),
            new Command(0, 0, CommandType.FactoryQueue, lab, TypeIndex: anvil, Arg: 1),
        });
        Assert.Single(world.Units.FactoryQueue[plant]);
        Assert.Empty(world.Units.FactoryQueue[lab]);

        RunUntil(world, () => world.Players[0].UnitsBuilt >= 1, 3000);
        bool built = false;
        for (int id = 0; id < world.Units.Count; id++)
            if (world.Units.Alive[id] && world.Units.Owner[id] == 0 && world.Units.TypeIndex[id] == anvil && world.Units.Complete[id]) built = true;
        Assert.True(built, "the Lattice plant should have produced a salvaged Anvil");
    }

    [Fact]
    public void Own_faction_wrecks_and_structures_do_not_count()
    {
        World world = Flat(0, 0);
        int builder = world.SpawnUnit("lattice_surveyor", 0, At(60, 60));
        for (int i = 0; i < 3; i++) world.Kill(world.SpawnUnit("lattice_strider", 1, At(90 + i * 12, 60)));
        world.Kill(world.SpawnUnit("crucible_solar_collector", 1, At(120, 120)));
        world.Step(new[] { new Command(0, 0, CommandType.ReclaimArea, builder, At(100, 80), Arg: 80) });
        RunUntil(world, () => world.Features.All(f => f.Gone), 4000);
        Assert.True(world.Players[0].SalvageProgress.All(n => n == 0));
    }
}
