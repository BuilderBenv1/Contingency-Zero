using System;
using System.Collections.Generic;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;

namespace ContingencyZero.Game.Net;

/// <summary>
/// Test helper: plays a human seat badly but busily, issuing a few plausible commands at a time
/// (build extractors and structures near builders, queue units, move, fight, patrol, stop).
/// Used by the lockstep harness and the --dev-net-orders flag, never in normal play. Its
/// randomness is its own: only the resulting commands travel the network.
/// </summary>
public sealed class NetOrderBot
{
    private readonly Random _rng;

    public NetOrderBot(int seed) { _rng = new Random(seed); }

    /// <summary>Issues up to <paramref name="count"/> commands for <paramref name="player"/>.</summary>
    public void Act(World world, int player, Action<Command> issue, int count = 3)
    {
        if (player < 0 || player >= world.Players.Length || world.Players[player].Defeated) return;
        var mine = new List<int>();
        for (int id = 0; id < world.Units.Count; id++)
            if (world.Units.Alive[id] && world.Units.Owner[id] == player && world.Units.Complete[id]) mine.Add(id);
        if (mine.Count == 0) return;

        for (int k = 0; k < count; k++)
        {
            int unit = mine[_rng.Next(mine.Count)];
            UnitDef def = world.Catalog[world.Units.TypeIndex[unit]];
            if (def.IsFactory) Factory(world, player, unit, def, issue);
            else if (def.CanBuild && _rng.NextDouble() < 0.6) Build(world, player, unit, def, issue);
            else if (def.CanMove) Move(world, player, unit, issue);
        }
    }

    private void Factory(World world, int player, int unit, UnitDef def, Action<Command> issue)
    {
        double roll = _rng.NextDouble();
        if (roll < 0.7 && def.BuildOptions.Length > 0)
            issue(new Command(0, player, CommandType.FactoryQueue, unit, TypeIndex: def.BuildOptions[_rng.Next(def.BuildOptions.Length)], Arg: _rng.Next(1, 4)));
        else if (roll < 0.85)
            issue(new Command(0, player, CommandType.SetRally, unit, RandomSpot(world)));
        else
            issue(new Command(0, player, CommandType.FactoryRepeat, unit, Arg: _rng.Next(2)));
    }

    private void Build(World world, int player, int unit, UnitDef def, Action<Command> issue)
    {
        int type = def.BuildOptions[_rng.Next(def.BuildOptions.Length)];
        UnitDef target = world.Catalog[type];
        FixVec2 at = world.Units.Position[unit];
        if (target.NeedsMetalSpot)
        {
            // Nearest free metal spot.
            long best = long.MaxValue;
            foreach (MetalSpot s in world.Map.MetalSpots)
            {
                if (world.CanPlace(type, player, s.Position) != PlacementResult.Ok) continue;
                long dx = (s.Position.X.Raw - at.X.Raw) >> 16, dy = (s.Position.Y.Raw - at.Y.Raw) >> 16;
                long d = dx * dx + dy * dy;
                if (d < best) { best = d; at = s.Position; }
            }
            if (best == long.MaxValue) return;
        }
        else
        {
            bool found = false;
            for (int tries = 0; tries < 10 && !found; tries++)
            {
                var spot = new FixVec2(at.X + Fix64.FromInt(_rng.Next(-70, 71)), at.Y + Fix64.FromInt(_rng.Next(-70, 71)));
                if (world.CanPlace(type, player, spot) == PlacementResult.Ok) { at = spot; found = true; }
            }
            if (!found) return;
        }
        issue(new Command(0, player, CommandType.Build, unit, at, TypeIndex: type, Queue: _rng.NextDouble() < 0.3));
    }

    private void Move(World world, int player, int unit, Action<Command> issue)
    {
        double roll = _rng.NextDouble();
        CommandType type = roll < 0.45 ? CommandType.Move : roll < 0.8 ? CommandType.Fight : roll < 0.92 ? CommandType.Patrol : CommandType.Stop;
        issue(new Command(0, player, type, unit, RandomSpot(world), Queue: _rng.NextDouble() < 0.2));
    }

    private FixVec2 RandomSpot(World world) =>
        FixVec2.FromInts(_rng.Next(8, Math.Max(9, world.Map.Width - 8)), _rng.Next(8, Math.Max(9, world.Map.Height - 8)));
}
