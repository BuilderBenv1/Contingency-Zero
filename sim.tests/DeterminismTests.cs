using System.Collections.Generic;
using System.IO;
using ContingencyZero.Sim.Replay;
using Xunit;

namespace ContingencyZero.Sim.Tests;

/// <summary>
/// The M1 gate: a 1,000-unit battle must replay to the same state hash every time, on every OS.
/// CI runs this on Linux and Windows; the golden hash below is the cross-platform check.
/// </summary>
public class DeterminismTests
{
    private const int UnitsPerSide = 500;
    private const int Ticks = 60 * World.TicksPerSecond; // one minute: the lines meet and fight
    private const ulong Seed = 20261022;

    /// <summary>
    /// Final hash of <see cref="TestData.Battle"/> with the settings above. If a deliberate sim change
    /// moves it, update it in the same commit and say why. If it changes by accident, something in
    /// the sim has stopped being deterministic or has changed behaviour.
    /// </summary>
    private const ulong GoldenHash = 0xF0012FE2728DA4B1UL;

    private static List<ulong> HashTrace(MatchReplay replay)
    {
        var trace = new List<ulong>();
        replay.Play(TestData.Catalog, w =>
        {
            if (w.Tick % World.TicksPerSecond == 0) trace.Add(w.ComputeHash());
        });
        return trace;
    }

    [Fact]
    public void Same_battle_twice_gives_identical_state_every_second()
    {
        MatchReplay replay = TestData.Battle(UnitsPerSide, Seed, Ticks);
        Assert.Equal(HashTrace(replay), HashTrace(replay));
    }

    [Fact]
    public void Battle_actually_fights()
    {
        World world = TestData.Battle(UnitsPerSide, Seed, Ticks).Play(TestData.Catalog);
        Assert.Equal(2 + 2 * UnitsPerSide, world.Units.Count);
        Assert.NotEmpty(world.Wrecks);
    }

    [Fact]
    public void Replay_survives_a_save_and_load()
    {
        MatchReplay original = TestData.Battle(UnitsPerSide, Seed, Ticks);
        using var stream = new MemoryStream();
        original.Write(stream);
        stream.Position = 0;
        MatchReplay loaded = MatchReplay.Read(stream);

        Assert.Equal(
            original.Play(TestData.Catalog).ComputeHash(),
            loaded.Play(TestData.Catalog).ComputeHash());
        // 1,000 orders and 1,002 starting units come to about 70 KB; a minute of full state would be megabytes.
        Assert.True(stream.Length < 128 * 1024, $"Replay was {stream.Length} bytes");
    }

    [Fact]
    public void Different_seed_changes_the_outcome()
    {
        ulong a = TestData.Battle(UnitsPerSide, Seed, Ticks).Play(TestData.Catalog).ComputeHash();
        ulong b = TestData.Battle(UnitsPerSide, Seed + 1, Ticks).Play(TestData.Catalog).ComputeHash();
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Battle_matches_the_golden_hash()
    {
        ulong hash = TestData.Battle(UnitsPerSide, Seed, Ticks).Play(TestData.Catalog).ComputeHash();
        Assert.True(hash == GoldenHash, $"State hash is 0x{hash:X16}, golden is 0x{GoldenHash:X16}");
    }
}
