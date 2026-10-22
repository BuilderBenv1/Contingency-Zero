using System;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.State;

namespace ContingencyZero.Sim.Combat;

/// <summary>
/// Uniform grid of unit ids, rebuilt every tick, for range queries. Each cell is a singly linked list
/// threaded through an array, so there are no per-tick allocations.
/// </summary>
public sealed class SpatialGrid
{
    public const int CellSize = 16;

    private readonly int _cols;
    private readonly int _rows;
    private readonly int[] _head;
    private int[] _next = new int[256];

    public SpatialGrid(int mapWidth, int mapHeight)
    {
        _cols = mapWidth / CellSize + 1;
        _rows = mapHeight / CellSize + 1;
        _head = new int[_cols * _rows];
    }

    public void Rebuild(UnitStore units)
    {
        Array.Fill(_head, -1);
        if (_next.Length < units.Count) Array.Resize(ref _next, units.Count * 2);

        // Insert in descending id order so each cell list reads in ascending id order.
        for (int id = units.Count - 1; id >= 0; id--)
        {
            if (!units.Alive[id]) continue;
            int cell = CellOf(units.Position[id]);
            _next[id] = _head[cell];
            _head[cell] = id;
        }
    }

    /// <summary>
    /// Nearest living unit not on <paramref name="team"/> within <paramref name="range"/> of <paramref name="from"/>.
    /// Ties go to the lowest id. Returns -1 if none.
    /// </summary>
    public int FindNearestEnemy(UnitStore units, PlayerState[] players, FixVec2 from, int team, Fix64 range)
    {
        Fix64 bestDistSq = range * range;
        int best = -1;

        int reach = range.ToIntFloor() / CellSize + 1;
        int cx = Col(from.X), cy = Row(from.Y);
        for (int y = cy - reach; y <= cy + reach; y++)
        {
            if (y < 0 || y >= _rows) continue;
            for (int x = cx - reach; x <= cx + reach; x++)
            {
                if (x < 0 || x >= _cols) continue;
                for (int id = _head[y * _cols + x]; id != -1; id = _next[id])
                {
                    if (players[units.Owner[id]].Team == team) continue;
                    Fix64 d = FixVec2.DistanceSquared(from, units.Position[id]);
                    if (d < bestDistSq || (d == bestDistSq && (best == -1 || id < best)))
                    {
                        bestDistSq = d;
                        best = id;
                    }
                }
            }
        }
        return best;
    }

    private int Col(Fix64 x) => Clamp(x.ToIntFloor() / CellSize, _cols);
    private int Row(Fix64 y) => Clamp(y.ToIntFloor() / CellSize, _rows);
    private int CellOf(FixVec2 p) => Row(p.Y) * _cols + Col(p.X);
    private static int Clamp(int v, int count) => v < 0 ? 0 : v >= count ? count - 1 : v;
}
