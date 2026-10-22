using System;
using System.IO;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Replay;

namespace ContingencyZero.Sim.Tests;

internal static class TestData
{
    private static readonly Lazy<UnitCatalog> LazyCatalog = new(() => UnitCatalog.LoadDirectory(UnitsDirectory()));

    /// <summary>The real unit data from data/units, so tests break if the data files do.</summary>
    public static UnitCatalog Catalog => LazyCatalog.Value;

    public static string UnitsDirectory()
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            string? dir = start;
            while (dir is not null)
            {
                string candidate = Path.Combine(dir, "data", "units");
                if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
        }
        throw new DirectoryNotFoundException("Couldn't find data/units above the test output or working folder");
    }

    public static FixVec2 At(int x, int y) => FixVec2.FromInts(x, y);

    public static MatchReplay Battle(int unitsPerSide, ulong seed, int ticks) =>
        Scenarios.LineBattle(unitsPerSide, seed, ticks);
}
