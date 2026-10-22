using System.Collections.Generic;
using ContingencyZero.Sim.Core;

namespace ContingencyZero.Sim.Replay;

/// <summary>Ready-made matches for tests, benchmarks and the M1 demo scene.</summary>
public static class Scenarios
{
    public const int LineBattleMapSize = 512;

    /// <summary>
    /// Two armies in blocks facing each other across a 512 x 512 map, each with a Commander at the
    /// back, all ordered to march on the enemy line at tick 0. Fights break out as the lines meet,
    /// about 35 seconds in. Unit ids: 0 and 1 are the Commanders, then the Lattice army, then the
    /// Crucible army.
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
            spawns.Add(new Spawn("lattice_strider", 0, At(136 + col * 10, 40 + row * 4)));
        }
        for (int i = 0; i < unitsPerSide; i++)
        {
            int col = i % perRow, row = i / perRow;
            spawns.Add(new Spawn("crucible_anvil", 1, At(136 + col * 10, 472 - row * 4)));
        }

        var scenario = new Scenario(seed, LineBattleMapSize, LineBattleMapSize, new[] { 0, 1 }, spawns);

        var commands = new List<Command>();
        for (int i = 0; i < unitsPerSide; i++)
        {
            int lattice = 2 + i;
            int crucible = 2 + unitsPerSide + i;
            int col = i % perRow;
            commands.Add(new Command(0, 0, CommandType.Move, lattice, At(136 + col * 10, 400)));
            commands.Add(new Command(0, 1, CommandType.Move, crucible, At(136 + col * 10, 112)));
        }

        return new MatchReplay(scenario, commands, ticks);
    }
}
