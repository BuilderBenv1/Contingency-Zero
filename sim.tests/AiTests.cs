using System.Collections.Generic;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Match;
using ContingencyZero.Sim.Replay;
using Xunit;
using Xunit.Abstractions;

namespace ContingencyZero.Sim.Tests;

public class AiTests
{
    private readonly ITestOutputHelper _out;
    public AiTests(ITestOutputHelper output) => _out = output;

    private static int Count(World w, int player, System.Func<Data.UnitDef, bool> pred)
    {
        int n = 0;
        for (int id = 0; id < w.Units.Count; id++)
            if (w.Units.Alive[id] && w.Units.Complete[id] && w.Units.Owner[id] == player && pred(w.Catalog[w.Units.TypeIndex[id]]))
                n++;
        return n;
    }

    [Theory]
    [InlineData(AiDifficulty.Easy)]
    [InlineData(AiDifficulty.Normal)]
    [InlineData(AiDifficulty.Hard)]
    [InlineData(AiDifficulty.Brutal)]
    public void Ai_builds_an_economy_and_an_army(AiDifficulty difficulty)
    {
        var scenario = Scenarios.AiSkirmish(TestData.LoadMap("ashfall_plains"), 5, difficulty, difficulty);
        World world = scenario.CreateWorld(TestData.Catalog);
        for (int i = 0; i < 6 * 60 * World.TicksPerSecond; i++) world.Step();

        int extractors = Count(world, 0, d => d.ExtractorMultiplier > Fix64.Zero);
        int factories = Count(world, 0, d => d.IsFactory);
        int army = Count(world, 0, d => d.IsArmed && d.CanMove && !d.IsCommander);
        _out.WriteLine($"{difficulty}: {extractors} extractors, {factories} factories, {army} army, " +
                       $"metal {world.Players[0].MetalIncome * World.TicksPerSecond}/s, energy {world.Players[0].EnergyIncome * World.TicksPerSecond}/s");
        Assert.True(extractors >= 4, $"only {extractors} extractors");
        Assert.True(factories >= 1, "no factory");
        Assert.True(army >= 3, $"only {army} army units");
    }

    [Theory]
    [InlineData(11UL)]
    [InlineData(12UL)]
    public void Hard_beats_easy(ulong seed)
    {
        var scenario = Scenarios.AiSkirmish(TestData.LoadMap("proving_grounds"), seed, AiDifficulty.Hard, AiDifficulty.Easy);
        World world = scenario.CreateWorld(TestData.Catalog);
        int limit = 30 * 60 * World.TicksPerSecond;
        int t = TestData.RunUntil(world, () => world.IsOver, limit);
        _out.WriteLine($"seed {seed}: over={world.IsOver} winner={world.WinningTeam} after {t / World.TicksPerSecond / 60} min");
        Assert.True(world.IsOver, "the game didn't finish in 30 minutes");
        Assert.Equal(0, world.WinningTeam);
    }
}
