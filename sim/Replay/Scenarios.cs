using System.Collections.Generic;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;

namespace ContingencyZero.Sim.Replay;

/// <summary>Ready-made matches for tests, benchmarks and demos.</summary>
public static class Scenarios
{
    public const int LineBattleMapSize = 512;

    /// <summary>
    /// Two armies in blocks facing each other across a flat 512 x 512 map, each with a Commander at
    /// the back, all ordered to march on the enemy line at tick 0. Fights break out as the lines
    /// meet, about 35 seconds in. Unit ids: 0 and 1 are the Commanders, then the Lattice army, then
    /// the Crucible army.
    /// </summary>
    public static MatchReplay LineBattle(int unitsPerSide, ulong seed, int ticks)
    {
        static FixVec2 At(int x, int y) => FixVec2.FromInts(x, y);

        var spawns = new List<Spawn>
        {
            new("lattice_commander", 0, At(256, 20)),
            new("crucible_commander", 1, At(256, 492)),
        };

        const int perRow = 25;
        for (int i = 0; i < unitsPerSide; i++)
        {
            int col = i % perRow, row = i / perRow;
            spawns.Add(new Spawn("lattice_strider", 0, At(136 + col * 10, 40 + row * 5)));
        }
        for (int i = 0; i < unitsPerSide; i++)
        {
            int col = i % perRow, row = i / perRow;
            spawns.Add(new Spawn("crucible_anvil", 1, At(136 + col * 10, 472 - row * 5)));
        }

        var players = new[]
        {
            new PlayerSetup("Lattice", 0, "lattice", 0),
            new PlayerSetup("Crucible", 1, "crucible", 1),
        };
        var map = MapData.Flat("Line Battle", LineBattleMapSize / MapData.TileSize, LineBattleMapSize / MapData.TileSize);
        var scenario = new Scenario(map, seed, players, new MatchOptions(SpawnCommanders: false, RevealMap: true), spawns);

        var commands = new List<Command>();
        for (int i = 0; i < unitsPerSide; i++)
        {
            int lattice = 2 + i;
            int crucible = 2 + unitsPerSide + i;
            int col = i % perRow;
            commands.Add(new Command(0, 0, CommandType.Fight, lattice, At(136 + col * 10, 400)));
            commands.Add(new Command(0, 1, CommandType.Fight, crucible, At(136 + col * 10, 112)));
        }

        return new MatchReplay(scenario, commands, ticks);
    }

    /// <summary>A computer-versus-computer skirmish on the given map.</summary>
    public static Scenario AiSkirmish(MapData map, ulong seed, AiDifficulty a, AiDifficulty b,
        string factionA = "lattice", string factionB = "crucible")
    {
        var players = new[]
        {
            new PlayerSetup($"{factionA} ({a})", 0, factionA, 0, a),
            new PlayerSetup($"{factionB} ({b})", 1, factionB, 1, b),
        };
        return new Scenario(map, seed, players, new MatchOptions(), new List<Spawn>());
    }
}
