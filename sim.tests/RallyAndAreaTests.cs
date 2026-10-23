using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.State;
using Xunit;
using static ContingencyZero.Sim.Tests.TestData;

namespace ContingencyZero.Sim.Tests;

/// <summary>Factory rally rotations (up to four stops), area attack, and salvage thresholds from data.</summary>
public class RallyAndAreaTests
{
    private static readonly FixVec2[] Stops = { At(100, 200), At(200, 100), At(220, 220), At(40, 220) };

    private static (World World, int Lab) FactoryWithStops(int stops, int every = 0, int units = 8)
    {
        World world = Flat(5000, 5000);
        world.SpawnUnit("lattice_fusion_reactor", 0, At(30, 30));
        int lab = world.SpawnUnit("lattice_bot_lab", 0, At(120, 120));
        var cmds = new List<Command> { new(0, 0, CommandType.SetRally, lab, Stops[0]) };
        for (int i = 1; i < stops; i++) cmds.Add(new Command(0, 0, CommandType.SetRally, lab, Stops[i], Queue: true));
        if (every > 0) cmds.Add(new Command(0, 0, CommandType.FactoryRallyEvery, lab, Arg: every));
        cmds.Add(new Command(0, 0, CommandType.FactoryQueue, lab, TypeIndex: Type("lattice_scout_bot"), Arg: units));
        world.Step(cmds);
        return (world, lab);
    }

    /// <summary>Which stop each scout is heading for (or standing at), by index into <see cref="Stops"/>.</summary>
    private static int[] CountByStop(World world)
    {
        var counts = new int[Stops.Length];
        for (int id = 0; id < world.Units.Count; id++)
        {
            if (!world.Units.Alive[id] || world.Units.TypeIndex[id] != Type("lattice_scout_bot")) continue;
            for (int s = 0; s < Stops.Length; s++)
                if (FixVec2.Distance(world.Units.Position[id], Stops[s]) < Fix64.FromInt(16)) counts[s]++;
        }
        return counts;
    }

    [Fact]
    public void Shift_rally_points_add_stops_up_to_four_and_a_plain_rally_resets_them()
    {
        var (world, lab) = FactoryWithStops(4, units: 0);
        Assert.Equal(4, world.Units.RallyPoints(lab).Count());
        world.Step(new[] { new Command(1, 0, CommandType.SetRally, lab, At(10, 10), Queue: true) });
        Assert.Equal(4, world.Units.RallyPoints(lab).Count()); // full: the newest replaces the last
        Assert.Equal(At(10, 10), world.Units.RallyPoints(lab).Last());

        world.Step(new[] { new Command(2, 0, CommandType.SetRally, lab, At(60, 60)) });
        Assert.Equal(new[] { At(60, 60) }, world.Units.RallyPoints(lab).ToArray());

        world.Step(new[] { new Command(3, 0, CommandType.ClearRally, lab) });
        Assert.Empty(world.Units.RallyPoints(lab));
        Assert.False(world.Units.HasRally[lab]);
    }

    [Fact]
    public void Units_take_the_rally_stops_in_turn()
    {
        var (world, _) = FactoryWithStops(4, units: 8);
        RunUntil(world, () => world.Players[0].UnitsBuilt >= 8, 4000);
        for (int i = 0; i < 900; i++) world.Step();
        Assert.Equal(new[] { 2, 2, 2, 2 }, CountByStop(world));
    }

    [Fact]
    public void Every_nth_unit_rotates_through_the_extra_stops()
    {
        // Every 2nd unit goes to an extra stop, alternating between the two; the rest to the primary.
        var (world, _) = FactoryWithStops(3, every: 2, units: 8);
        RunUntil(world, () => world.Players[0].UnitsBuilt >= 8, 4000);
        for (int i = 0; i < 900; i++) world.Step();
        Assert.Equal(new[] { 4, 2, 2, 0 }, CountByStop(world));
    }

    [Fact]
    public void Rally_points_are_part_of_the_state_hash()
    {
        var (a, labA) = FactoryWithStops(1, units: 0);
        var (b, labB) = FactoryWithStops(1, units: 0);
        Assert.Equal(a.ComputeHash(), b.ComputeHash());
        b.Step(new[] { new Command(1, 0, CommandType.SetRally, labB, At(30, 200), Queue: true) });
        a.Step();
        Assert.NotEqual(a.ComputeHash(), b.ComputeHash());
    }

    [Fact]
    public void Area_attack_kills_every_enemy_in_the_circle_and_ignores_the_rest()
    {
        World world = Flat();
        var tanks = Enumerable.Range(0, 4).Select(i => world.SpawnUnit("lattice_strider", 0, At(40 + i * 6, 40))).ToList();
        var inside = new[] { At(160, 150), At(175, 160), At(150, 170) }.Select(p => world.SpawnUnit("crucible_scout_bot", 1, p)).ToList();
        int outside = world.SpawnUnit("crucible_solar_collector", 1, At(232, 40)); // well away from the circle and the route
        foreach (int t in tanks) world.Units.Fire[t] = FireState.ReturnFire; // only the area order makes them fight
        world.Step(tanks.Select(t => new Command(0, 0, CommandType.AttackArea, t, At(160, 160), Arg: 30)).ToList());
        Assert.Equal(OrderType.AttackArea, world.Units.CurrentOrder(tanks[0]).Type);

        RunUntil(world, () => tanks.All(t => world.Units.CurrentOrder(t).Type == OrderType.None), 3000);
        foreach (int e in inside) Assert.False(world.Units.IsValid(e));
        Assert.True(world.Units.IsValid(outside));
        foreach (int t in tanks) Assert.Equal(OrderType.None, world.Units.CurrentOrder(t).Type);
    }

    [Fact]
    public void Area_attack_is_refused_for_unarmed_units()
    {
        World world = Flat();
        int builder = world.SpawnUnit("lattice_surveyor", 0, At(40, 40));
        world.Step(new[] { new Command(0, 0, CommandType.AttackArea, builder, At(160, 160), Arg: 30) });
        Assert.Equal(OrderType.None, world.Units.CurrentOrder(builder).Type);
    }

    [Fact]
    public void Salvage_thresholds_come_from_unit_data()
    {
        Assert.Equal(3, Catalog[Type("crucible_anvil")].SalvageWrecks);
        Assert.Equal(4, Catalog[Type("crucible_heavy_assault")].SalvageWrecks);

        World world = Flat(0, 0);
        int heavy = Type("crucible_heavy_assault");
        int builder = world.SpawnUnit("lattice_surveyor", 0, At(60, 60));
        for (int i = 0; i < 3; i++) world.Kill(world.SpawnUnit("crucible_heavy_assault", 1, At(90 + i * 14, 60)));
        world.Step(new[] { new Command(0, 0, CommandType.ReclaimArea, builder, At(105, 60), Arg: 60) });
        RunUntil(world, () => world.Features.All(f => f.Gone), 8000);
        Assert.Equal(3, world.Players[0].SalvageProgress[heavy]);
        Assert.False(world.HasSalvaged(0, heavy), "a tier 2 design needs four wrecks");
    }
}
