using System;
using System.IO;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using ContingencyZero.Sim.Replay;

namespace ContingencyZero.Sim.Tests;

internal static class TestData
{
    private static readonly Lazy<UnitCatalog> LazyCatalog = new(() => UnitCatalog.LoadDirectory(UnitsDirectory()));

    /// <summary>The real unit data from data/units, so tests break if the data files do.</summary>
    public static UnitCatalog Catalog => LazyCatalog.Value;

    public static string DataDirectory()
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            string? dir = start;
            while (dir is not null)
            {
                string candidate = Path.Combine(dir, "data");
                if (Directory.Exists(Path.Combine(candidate, "units"))) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
        }
        throw new DirectoryNotFoundException("Couldn't find data/units above the test output or working folder");
    }

    public static string UnitsDirectory() => Path.Combine(DataDirectory(), "units");

    public static MapData LoadMap(string name) =>
        MapData.FromJson(File.ReadAllText(Path.Combine(DataDirectory(), "maps", name + ".json")));

    public static FixVec2 At(int x, int y) => FixVec2.FromInts(x, y);

    /// <summary>A flat 256 x 256 world with two players and no Commanders, for focused tests.</summary>
    public static World Flat(int startMetal = 1000, int startEnergy = 1000, bool sameTeam = false, ulong seed = 1)
    {
        var map = MapData.Flat("Test", 32, 32);
        var players = new[]
        {
            new PlayerSetup("A", 0, "lattice", 0),
            new PlayerSetup("B", sameTeam ? 0 : 1, "crucible", 1),
        };
        return new World(Catalog, map, seed, players,
            new MatchOptions(startMetal, startEnergy, SpawnCommanders: false, RevealMap: true));
    }

    public static int Type(string id) => Catalog.IndexOf(id);

    public static MatchReplay Battle(int unitsPerSide, ulong seed, int ticks) =>
        Scenarios.LineBattle(unitsPerSide, seed, ticks);

    /// <summary>Steps until <paramref name="done"/> is true or the tick limit runs out. Returns ticks used.</summary>
    public static int RunUntil(World world, Func<bool> done, int maxTicks)
    {
        for (int i = 0; i < maxTicks; i++)
        {
            if (done()) return i;
            world.Step();
        }
        return maxTicks;
    }
}
