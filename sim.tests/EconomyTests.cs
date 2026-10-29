using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.State;
using Xunit;
using static ContingencyZero.Sim.Tests.TestData;

namespace ContingencyZero.Sim.Tests;

public class EconomyTests
{
    private static Command Build(World w, int builder, string typeId, FixVec2 at, bool queue = false) =>
        new(w.Tick, w.Units.Owner[builder], CommandType.Build, builder, at, TypeIndex: Type(typeId), Queue: queue);

    [Fact]
    public void Commander_alone_earns_income_every_tick()
    {
        World world = Flat(0, 0);
        world.SpawnUnit("lattice_commander", 0, At(50, 50));

        for (int i = 0; i < World.TicksPerSecond; i++) world.Step();

        // 1.5 metal and 25 energy per second, give or take fixed-point rounding.
        Assert.InRange(world.Players[0].Metal, Fix64.Parse("1.49"), Fix64.Parse("1.51"));
        Assert.InRange(world.Players[0].Energy, Fix64.Parse("24.99"), Fix64.Parse("25.01"));
    }

    [Fact]
    public void Builder_walks_to_the_site_puts_up_a_nanoframe_and_finishes_it()
    {
        World world = Flat();
        int cmdr = world.SpawnUnit("lattice_commander", 0, At(40, 40));
        world.Step(new[] { Build(world, cmdr, "lattice_solar_collector", At(120, 40)) });

        int frame = -1;
        RunUntil(world, () =>
        {
            Order o = world.Units.CurrentOrder(cmdr);
            if (o.Type == OrderType.Build && o.Target >= 0) frame = o.Target;
            return frame >= 0;
        }, 600);
        Assert.True(frame >= 0, "the builder never started the frame");
        Assert.False(world.Units.Complete[frame]);
        Assert.True(world.Units.Position[cmdr].X > Fix64.FromInt(80), "the builder should have walked over");

        RunUntil(world, () => world.Units.Complete[frame], 2000);
        world.Step(); // the builder notices on the next tick
        Assert.True(world.Units.Complete[frame]);
        Assert.Equal(world.Catalog[Type("lattice_solar_collector")].MaxHealth, world.Units.Health[frame]);
        Assert.Equal(OrderType.None, world.Units.CurrentOrder(cmdr).Type);
    }

    [Fact]
    public void A_builder_assisting_another_builder_works_on_its_frame()
    {
        World world = Flat();
        world.SpawnUnit("lattice_fusion_reactor", 0, At(30, 220));
        int a = world.SpawnUnit("lattice_builder_vehicle", 0, At(60, 60));
        int b = world.SpawnUnit("lattice_builder_vehicle", 0, At(130, 110));
        world.Step(new[] { Build(world, a, "lattice_vehicle_plant", At(100, 60)) });
        RunUntil(world, () => world.Units.CurrentOrder(a).Type == OrderType.Build && world.Units.CurrentOrder(a).Target >= 0, 600);
        int frame = world.Units.CurrentOrder(a).Target;
        Assert.Equal(OrderType.Build, world.Units.CurrentOrder(a).Type);
        Assert.True(frame >= 0, "the first builder never started the frame");

        world.Step(new[] { new Command(world.Tick, 0, CommandType.Assist, b, TargetId: a) });
        Fix64 power = world.Catalog[Type("lattice_builder_vehicle")].BuildPowerPerTick;
        // The helper drives over to the frame, then both builders' power goes in every tick.
        RunUntil(world, () => EconomySystemProbe.InRange(world, b, frame), 900);
        Assert.False(world.Units.Complete[frame], "the frame finished before the helper arrived");
        world.Step();
        Fix64 before = world.Units.BuildProgress[frame];
        world.Step();
        Fix64 gained = world.Units.BuildProgress[frame] - before;
        Assert.True(gained >= power * Fix64.FromInt(2) - Fix64.One, $"frame gained {gained}, expected two builders' worth ({power * Fix64.FromInt(2)})");
    }

    [Fact]
    public void Poor_player_stalls_and_every_build_slows_by_the_same_ratio()
    {
        World world = Flat(0, 0);
        int cmdr = world.SpawnUnit("lattice_commander", 0, At(50, 50));
        world.Step(new[] { Build(world, cmdr, "lattice_solar_collector", At(60, 50)) });
        RunUntil(world, () => world.Units.CurrentOrder(cmdr).Target >= 0, 200);
        world.Step();

        // A solar wants 150 metal over 2000 work; at 150 build power that's 11.25 metal/s against 1.5 income.
        PlayerState p = world.Players[0];
        Assert.InRange(p.BuildRatio, Fix64.Parse("0.12"), Fix64.Parse("0.15"));
        Assert.True(p.Metal >= Fix64.Zero);
    }

    [Fact]
    public void Factory_builds_its_queue_and_sends_units_to_the_rally_point()
    {
        World world = Flat();
        world.SpawnUnit("lattice_fusion_reactor", 0, At(30, 220));
        int lab = world.SpawnUnit("lattice_bot_lab", 0, At(100, 100));
        world.Step(new[]
        {
            new Command(0, 0, CommandType.FactoryQueue, lab, TypeIndex: Type("lattice_assault_bot"), Arg: 2),
            new Command(0, 0, CommandType.SetRally, lab, At(100, 200)),
        });

        RunUntil(world, () => world.Players[0].UnitsBuilt >= 2, 1500);
        Assert.Equal(2, world.Players[0].UnitsBuilt);
        Assert.Empty(world.Units.FactoryQueue[lab]);

        for (int i = 0; i < 600; i++) world.Step();
        int bots = 0;
        for (int id = 0; id < world.Units.Count; id++)
        {
            if (!world.Units.Alive[id] || world.Units.TypeIndex[id] != Type("lattice_assault_bot")) continue;
            bots++;
            Assert.True(FixVec2.Distance(world.Units.Position[id], At(100, 200)) < Fix64.FromInt(12),
                $"bot {id} is at {world.Units.Position[id]}");
        }
        Assert.Equal(2, bots);
    }

    [Fact]
    public void Repeat_keeps_a_factory_producing()
    {
        World world = Flat();
        world.SpawnUnit("crucible_fusion_reactor", 1, At(30, 220));
        int lab = world.SpawnUnit("crucible_bot_lab", 1, At(100, 100));
        world.Step(new[]
        {
            new Command(0, 1, CommandType.FactoryQueue, lab, TypeIndex: Type("crucible_scout_bot"), Arg: 1),
            new Command(0, 1, CommandType.FactoryRepeat, lab, Arg: 1),
        });
        RunUntil(world, () => world.Players[1].UnitsBuilt >= 3, 2000);
        Assert.True(world.Players[1].UnitsBuilt >= 3);
        Assert.Single(world.Units.FactoryQueue[lab]);
    }

    [Fact]
    public void Surplus_above_storage_is_wasted()
    {
        World world = Flat(1490, 0);
        world.SpawnUnit("lattice_commander", 0, At(50, 50));

        for (int i = 0; i < 20 * World.TicksPerSecond; i++) world.Step();

        PlayerState p = world.Players[0];
        Assert.Equal(Fix64.FromInt(1500), p.MetalStorage); // 1000 base + 500 from the Commander
        Assert.Equal(p.MetalStorage, p.Metal);
        Assert.True(p.MetalWasted > Fix64.Zero);
    }

    [Fact]
    public void Builders_reclaim_wrecks_for_metal()
    {
        World world = Flat(0, 0);
        int builder = world.SpawnUnit("lattice_surveyor", 0, At(50, 50));
        int victim = world.SpawnUnit("crucible_anvil", 1, At(90, 50));
        world.Kill(victim);

        Feature wreck = world.Features[^1];
        Fix64 expected = world.Catalog[Type("crucible_anvil")].MetalCost * Fix64.Parse("0.6");
        Assert.Equal(expected, wreck.Metal);

        world.Step(new[] { new Command(0, 0, CommandType.Reclaim, builder, TargetId: wreck.Id) });
        RunUntil(world, () => wreck.Gone, 600);

        Assert.True(wreck.Gone);
        Assert.InRange(world.Players[0].Metal, expected - Fix64.Parse("0.1"), expected + Fix64.Parse("0.1"));
        RunUntil(world, () => world.Units.CurrentOrder(builder).Type == OrderType.None, 30);
        Assert.Equal(OrderType.None, world.Units.CurrentOrder(builder).Type);
    }

    [Fact]
    public void Extractors_only_go_on_metal_spots_and_earn_the_spot_yield()
    {
        var map = Map.MapData.Flat("Spots", 32, 32);
        map.MetalSpots.Add(new Map.MetalSpot(At(100, 100), Fix64.FromInt(3)));
        var world = new World(Catalog, map, 1, new[] { new Match.PlayerSetup("A", 0, "lattice", 0) },
            new Match.MatchOptions(SpawnCommanders: false));
        int ex = Type("lattice_metal_extractor");

        Assert.Equal(PlacementResult.NeedsMetalSpot, world.CanPlace(ex, 0, At(40, 40)));
        Assert.Equal(PlacementResult.Ok, world.CanPlace(ex, 0, At(100, 100)));

        int id = world.SpawnUnit(ex, 0, At(100, 100));
        world.Step();
        Assert.InRange(world.Players[0].MetalIncome * World.TicksPerSecond, Fix64.Parse("2.99"), Fix64.Parse("3.01"));
        Assert.Equal(PlacementResult.Occupied, world.CanPlace(ex, 0, At(100, 100)));
        Assert.Equal(PlacementResult.Ok, world.CanPlace(Type("lattice_deep_extractor"), 0, world.Units.Position[id]));
    }

    [Fact]
    public void Wind_turbines_follow_the_map_wind()
    {
        World world = Flat(0, 0);
        world.SpawnUnit("lattice_wind_turbine", 0, At(60, 60));
        world.Step();
        Fix64 perSecond = world.Players[0].EnergyIncome * World.TicksPerSecond;
        Assert.InRange(perSecond, world.Map.WindMin, world.Map.WindMax);
    }

    [Fact]
    public void Metal_makers_turn_spare_energy_into_metal()
    {
        World world = Flat(0, 900);
        world.SpawnUnit("lattice_metal_maker", 0, At(60, 60));
        for (int i = 0; i < World.TicksPerSecond; i++) world.Step();
        Assert.InRange(world.Players[0].Metal, Fix64.Parse("0.98"), Fix64.Parse("1.01"));
        Assert.True(world.Players[0].Energy < Fix64.FromInt(900));
    }

    [Fact]
    public void Metal_makers_leave_half_the_energy_storage_for_building()
    {
        World world = Flat(0, 900);
        world.SpawnUnit("lattice_metal_maker", 0, At(60, 60));
        for (int i = 0; i < 60 * World.TicksPerSecond; i++) world.Step();
        PlayerState p = world.Players[0];
        // Conversion stops at the reserve line instead of draining storage.
        Assert.InRange(p.Energy, p.EnergyStorage / 2 - Fix64.One, p.EnergyStorage / 2 + Fix64.One);
        Fix64 made = p.Metal;
        for (int i = 0; i < 10 * World.TicksPerSecond; i++) world.Step();
        Assert.True(p.Metal - made < Fix64.Parse("0.01"), "makers kept converting below the reserve");
    }

    [Fact]
    public void A_frame_a_hair_short_of_done_finishes_even_while_stalled()
    {
        // Regression: while stalled, fixed-point rounding could leave a frame one step short of
        // its build time forever, showing 100% but never completing.
        World world = Flat(0, 0);
        int cmdr = world.SpawnUnit("lattice_commander", 0, At(40, 40));
        world.Step(new[] { Build(world, cmdr, "lattice_solar_collector", At(70, 40)) });
        int frame = -1;
        RunUntil(world, () =>
        {
            Order o = world.Units.CurrentOrder(cmdr);
            if (o.Type == OrderType.Build && o.Target >= 0) frame = o.Target;
            return frame >= 0;
        }, 600);
        Assert.True(frame >= 0);
        Fix64 buildTime = world.Catalog[world.Units.TypeIndex[frame]].BuildTime;
        world.Units.BuildProgress[frame] = buildTime - Fix64.FromRaw(1);
        RunUntil(world, () => world.Units.Complete[frame], 60);
        Assert.True(world.Units.Complete[frame], "the frame stuck just short of finished");
    }
}

internal static class EconomySystemProbe
{
    public static bool InRange(World world, int builder, int target) =>
        ContingencyZero.Sim.Economy.EconomySystem.InBuildRange(world, builder, world.Catalog[world.Units.TypeIndex[builder]], target);
}
