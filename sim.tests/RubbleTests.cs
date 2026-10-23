using System.Linq;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.State;
using Xunit;
using static ContingencyZero.Sim.Tests.TestData;

namespace ContingencyZero.Sim.Tests;

/// <summary>Destroyed structures leave lasting rubble round their footprint.</summary>
public class RubbleTests
{
    [Fact]
    public void A_dead_factory_leaves_a_wreck_and_rubble_round_its_footprint()
    {
        World world = Flat(5000, 5000);
        int lab = world.SpawnUnit("lattice_bot_lab", 0, At(128, 128));
        int before = world.Features.Count;
        world.Kill(lab);

        var added = world.Features.Skip(before).ToList();
        Assert.Single(added.Where(f => f.Kind == FeatureKind.Wreck));
        var rubble = added.Where(f => f.Kind == FeatureKind.Rubble).ToList();
        Assert.True(rubble.Count >= 3, $"expected a few rubble chunks, got {rubble.Count}");
        foreach (Feature r in rubble)
        {
            Assert.True(r.Metal > Fix64.Zero);
            Assert.True(FixVec2.Distance(r.Position, At(128, 128)) < Fix64.FromInt(40), $"rubble at {r.Position} is too far from the footprint");
        }
        // Rubble is small change next to the wreck itself.
        Fix64 wreckMetal = added.First(f => f.Kind == FeatureKind.Wreck).Metal;
        Assert.True(rubble.Sum(r => r.Metal.Raw) < wreckMetal.Raw / 2);
    }

    [Fact]
    public void Mobile_units_leave_no_rubble()
    {
        World world = Flat();
        int before = world.Features.Count;
        world.Kill(world.SpawnUnit("crucible_anvil", 1, At(128, 128)));
        Assert.DoesNotContain(FeatureKind.Rubble, world.Features.Skip(before).Select(f => f.Kind));
    }

    [Fact]
    public void Rubble_can_be_reclaimed_with_an_area_order()
    {
        World world = Flat(0, 0);
        int builder = world.SpawnUnit("lattice_surveyor", 0, At(80, 128));
        world.Kill(world.SpawnUnit("lattice_bot_lab", 0, At(160, 128)));
        world.Step(new[] { new Command(0, 0, CommandType.ReclaimArea, builder, At(160, 128), Arg: 60) });
        RunUntil(world, () => world.Features.All(f => f.Gone), 6000);
        Assert.True(world.Features.All(f => f.Gone));
        Assert.True(world.Players[0].Metal > Fix64.FromInt(100));
    }
}
