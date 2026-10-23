using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.State;
using Xunit;
using static ContingencyZero.Sim.Tests.TestData;

namespace ContingencyZero.Sim.Tests;

/// <summary>Area orders (reclaim / repair a circle) and factory ferry rally points.</summary>
public class OrderTests
{
    [Fact]
    public void Area_reclaim_clears_every_wreck_in_the_circle_and_leaves_the_rest()
    {
        World world = Flat(0, 0);
        int builder = world.SpawnUnit("lattice_surveyor", 0, At(60, 60));
        foreach (var p in new[] { At(100, 60), At(120, 80), At(90, 100) })
            world.Kill(world.SpawnUnit("crucible_anvil", 1, p));
        int farAway = world.SpawnUnit("crucible_anvil", 1, At(230, 230));
        world.Kill(farAway);
        Feature far = world.Features[^1];

        world.Step(new[] { new Command(0, 0, CommandType.ReclaimArea, builder, At(105, 80), Arg: 40) });
        Assert.Equal(OrderType.ReclaimArea, world.Units.CurrentOrder(builder).Type);

        RunUntil(world, () => world.Units.CurrentOrder(builder).Type == OrderType.None, 3000);
        Assert.Equal(OrderType.None, world.Units.CurrentOrder(builder).Type);
        int inside = 0;
        foreach (Feature f in world.Features)
            if (f.Id != far.Id && !f.Gone) inside++;
        Assert.Equal(0, inside);
        Assert.False(far.Gone, "the wreck outside the circle must be left alone");
        Assert.True(world.Players[0].Metal > Fix64.FromInt(100));
    }

    [Fact]
    public void Area_repair_heals_every_damaged_friendly_unit_in_the_circle()
    {
        World world = Flat(1000, 1000);
        int builder = world.SpawnUnit("lattice_surveyor", 0, At(60, 60));
        int a = world.SpawnUnit("lattice_strider", 0, At(100, 60));
        int b = world.SpawnUnit("lattice_strider", 0, At(110, 90));
        int outside = world.SpawnUnit("lattice_strider", 0, At(240, 240));
        foreach (int id in new[] { a, b, outside }) world.Units.Health[id] = Fix64.FromInt(50);

        world.Step(new[] { new Command(0, 0, CommandType.RepairArea, builder, At(105, 75), Arg: 40) });
        RunUntil(world, () => world.Units.CurrentOrder(builder).Type == OrderType.None, 3000);

        Fix64 max = world.Catalog[Type("lattice_strider")].MaxHealth;
        Assert.Equal(max, world.Units.Health[a]);
        Assert.Equal(max, world.Units.Health[b]);
        Assert.Equal(Fix64.FromInt(50), world.Units.Health[outside]);
    }

    [Fact]
    public void Every_third_unit_ferries_to_the_secondary_rally_point()
    {
        World world = Flat(5000, 5000);
        world.SpawnUnit("lattice_fusion_reactor", 0, At(30, 220));
        int lab = world.SpawnUnit("lattice_bot_lab", 0, At(100, 100));
        world.Step(new[]
        {
            new Command(0, 0, CommandType.SetRally, lab, At(100, 200)),
            new Command(0, 0, CommandType.SetRally, lab, At(200, 100), Arg: 1),
            new Command(0, 0, CommandType.FactoryRallyEvery, lab, Arg: 3),
            new Command(0, 0, CommandType.FactoryQueue, lab, TypeIndex: Type("lattice_scout_bot"), Arg: 6),
        });
        RunUntil(world, () => world.Players[0].UnitsBuilt >= 6, 3000);
        for (int i = 0; i < 900; i++) world.Step();

        int primary = 0, secondary = 0;
        for (int id = 0; id < world.Units.Count; id++)
        {
            if (!world.Units.Alive[id] || world.Units.TypeIndex[id] != Type("lattice_scout_bot")) continue;
            if (FixVec2.Distance(world.Units.Position[id], At(100, 200)) < Fix64.FromInt(14)) primary++;
            else if (FixVec2.Distance(world.Units.Position[id], At(200, 100)) < Fix64.FromInt(14)) secondary++;
        }
        Assert.Equal(4, primary);
        Assert.Equal(2, secondary);
    }
}
