using System;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.State;
using Xunit;

namespace ContingencyZero.Sim.Tests;

public class EconomyTests
{
    private static World NewWorld(int startMetal, int startEnergy) =>
        new(TestData.Catalog, seed: 1, mapWidth: 256, mapHeight: 256, playerTeams: new[] { 0, 1 }, startMetal, startEnergy);

    private static Command Build(World world, int builder, string typeId, FixVec2 at) =>
        new(world.Tick, world.Units.Owner[builder], CommandType.Build, builder, at, TypeIndex: world.Catalog.IndexOf(typeId));

    [Fact]
    public void Commander_alone_earns_income_every_tick()
    {
        World world = NewWorld(0, 0);
        world.SpawnUnit("lattice_commander", 0, TestData.At(50, 50));

        for (int i = 0; i < World.TicksPerSecond; i++) world.Step();

        // 1 metal and 25 energy per second, give or take fixed-point rounding.
        Assert.InRange(world.Players[0].Metal, Fix64.Parse("0.99"), Fix64.Parse("1.01"));
        Assert.InRange(world.Players[0].Energy, Fix64.Parse("24.99"), Fix64.Parse("25.01"));
    }

    [Fact]
    public void Rich_player_builds_at_full_speed()
    {
        World world = NewWorld(1000, 1000);
        int cmdr = world.SpawnUnit("lattice_commander", 0, TestData.At(50, 50));

        world.Step(new[] { Build(world, cmdr, "lattice_strider", TestData.At(52, 50)) });

        BuildJob job = world.Jobs[0];
        Assert.Equal(Fix64.One, world.Players[0].BuildRatio);
        Assert.Equal(Fix64.FromInt(2), job.Progress); // 30 build power / 15 ticks
    }

    [Fact]
    public void Poor_player_stalls_and_every_build_slows_by_the_same_ratio()
    {
        World world = NewWorld(0, 0);
        int cmdr = world.SpawnUnit("lattice_commander", 0, TestData.At(50, 50));

        world.Step(new[] { Build(world, cmdr, "lattice_strider", TestData.At(52, 50)) });

        // The strider wants 2.4 metal a second; the Commander makes 1, so it builds at about 1/2.4 speed.
        PlayerState p = world.Players[0];
        Assert.InRange(p.BuildRatio, Fix64.Parse("0.41"), Fix64.Parse("0.42"));
        Assert.True(world.Jobs[0].Progress < Fix64.One);
        Assert.True(p.Metal >= Fix64.Zero);
    }

    [Fact]
    public void Finished_job_spawns_the_unit_and_costs_its_price()
    {
        World world = NewWorld(1000, 1000);
        int cmdr = world.SpawnUnit("lattice_commander", 0, TestData.At(50, 50));
        world.Step(new[] { Build(world, cmdr, "lattice_strider", TestData.At(52, 50)) });

        for (int i = 0; i < 800 && !world.Jobs[0].Done; i++) world.Step();

        BuildJob job = world.Jobs[0];
        Assert.True(job.Done);
        Assert.True(world.Units.IsValid(job.ResultUnit));
        Assert.Equal(world.Catalog.IndexOf("lattice_strider"), world.Units.TypeIndex[job.ResultUnit]);
        Assert.Equal(OrderType.None, world.Units.Order[cmdr]);
    }

    [Fact]
    public void Surplus_above_storage_is_wasted()
    {
        World world = NewWorld(1490, 0);
        world.SpawnUnit("lattice_commander", 0, TestData.At(50, 50));

        for (int i = 0; i < 20 * World.TicksPerSecond; i++) world.Step();

        PlayerState p = world.Players[0];
        Assert.Equal(Fix64.FromInt(1500), p.MetalStorage); // 1000 base + 500 from the Commander
        Assert.Equal(p.MetalStorage, p.Metal);
        Assert.True(p.MetalWasted > Fix64.Zero);
    }

    [Fact]
    public void Builders_reclaim_wrecks_for_metal()
    {
        World world = NewWorld(0, 0);
        int builder = world.SpawnUnit("lattice_surveyor", 0, TestData.At(50, 50));
        int victim = world.SpawnUnit("crucible_anvil", 1, TestData.At(80, 50));
        world.Kill(victim);

        Wreck wreck = world.Wrecks[0];
        Assert.Equal(Fix64.FromInt(130) * Fix64.Parse("0.6"), wreck.Metal);

        world.Step(new[] { new Command(0, 0, CommandType.Reclaim, builder, TargetId: wreck.Id) });
        for (int i = 0; i < 300 && !wreck.Gone; i++) world.Step();

        Assert.True(wreck.Gone);
        Assert.InRange(world.Players[0].Metal, Fix64.Parse("77.9"), Fix64.Parse("78.1"));
        Assert.Equal(OrderType.None, world.Units.Order[builder]);
    }

    [Fact]
    public void Builders_cannot_build_another_factions_units()
    {
        World world = NewWorld(1000, 1000);
        int cmdr = world.SpawnUnit("lattice_commander", 0, TestData.At(50, 50));
        world.Step(new[] { Build(world, cmdr, "crucible_anvil", TestData.At(52, 50)) });
        Assert.Empty(world.Jobs);
    }

    [Fact]
    public void Unit_data_loads_and_converts_rates_to_per_tick()
    {
        var catalog = TestData.Catalog;
        Assert.Equal(10, catalog.Count);
        var strider = catalog[catalog.IndexOf("lattice_strider")];
        Assert.Equal(Fix64.FromInt(4) / World.TicksPerSecond, strider.SpeedPerTick);
        Assert.Equal(18, strider.ReloadTicks); // 1.2 s at 15 ticks a second
        Assert.True(catalog[catalog.IndexOf("crucible_commander")].IsCommander);
        Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => catalog.IndexOf("nope"));
    }
}
