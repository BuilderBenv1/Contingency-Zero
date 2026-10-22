using ContingencyZero.Sim.Core;
using Xunit;

namespace ContingencyZero.Sim.Tests;

public class RngTests
{
    [Fact]
    public void Same_seed_gives_same_sequence()
    {
        var a = new DeterministicRng(42);
        var b = new DeterministicRng(42);
        for (int i = 0; i < 1000; i++) Assert.Equal(a.NextUInt(), b.NextUInt());
    }

    [Fact]
    public void Different_seeds_diverge()
    {
        var a = new DeterministicRng(1);
        var b = new DeterministicRng(2);
        Assert.NotEqual(a.NextUInt(), b.NextUInt());
    }

    [Fact]
    public void Range_stays_in_bounds()
    {
        var rng = new DeterministicRng(7);
        for (int i = 0; i < 10_000; i++)
        {
            int v = rng.Range(-3, 5);
            Assert.InRange(v, -3, 4);
        }
    }

    [Fact]
    public void NextFix_is_in_zero_to_one()
    {
        var rng = new DeterministicRng(9);
        for (int i = 0; i < 10_000; i++)
        {
            Fix64 v = rng.NextFix();
            Assert.True(v >= Fix64.Zero && v < Fix64.One);
        }
    }

    [Fact]
    public void Chance_roughly_matches_its_probability()
    {
        var rng = new DeterministicRng(11);
        int hits = 0;
        for (int i = 0; i < 10_000; i++) if (rng.Chance(Fix64.Parse("0.25"))) hits++;
        Assert.InRange(hits, 2300, 2700);
    }
}
