using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ContingencyZero.Sim.Tests;

/// <summary>Enforces the hard rules in CLAUDE.md by scanning the sim source.</summary>
public class SimRulesTests
{
    private static readonly (string Pattern, string Why)[] Forbidden =
    {
        (@"\bfloat\b", "floating point differs between machines; use Fix64"),
        (@"\bdouble\b", "floating point differs between machines; use Fix64"),
        (@"\bdecimal\b", "use Fix64"),
        (@"\bMathF?\.", "System.Math works on floats; use Fix64 helpers"),
        (@"\bSystem\.Random\b|\bnew Random\(", "use the world's DeterministicRng"),
        (@"\bDateTime\b|\bStopwatch\b|\bEnvironment\.TickCount", "the sim must not read the clock"),
        (@"\bGuid\.NewGuid\b", "ids must be deterministic"),
        (@"\bHashSet<", "iteration order is undefined; use arrays or sorted lists"),
        (@"\bGodot\b", "the sim must not depend on Godot"),
        (@"\bstatic\s+(readonly\s+)?(List|Dictionary|Queue|Stack)<", "shared mutable state breaks parallel worlds; keep it on World"),
    };

    [Fact]
    public void Sim_source_follows_the_determinism_rules()
    {
        string simDir = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(TestData.UnitsDirectory()))!, "sim");
        var problems = new List<string>();

        foreach (string file in Directory.GetFiles(simDir, "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                              && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string code = lines[i].Trim();
                if (code.StartsWith("//")) continue;
                foreach (var (pattern, why) in Forbidden)
                {
                    if (Regex.IsMatch(code, pattern))
                        problems.Add($"{Path.GetFileName(file)}:{i + 1}: '{code}' ({why})");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
