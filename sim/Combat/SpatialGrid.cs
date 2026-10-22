using System;
using System.Collections.Generic;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.State;

namespace ContingencyZero.Sim.Combat;

/// <summary>
/// Uniform grid of living unit ids for range queries. Each cell is a singly linked list threaded
/// through an array, so rebuilding allocates nothing. Cells list ids in ascending order.
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
        Array.Fill(_head, -1);
    }

    public void Rebuild(UnitStore units)
    {
        Array.Fill(_head, -1);
        if (_next.Length < units.Count) Array.Resize(ref _next, units.Count * 2);

        for (int id = units.Count - 1; id >= 0; id--)
        {
            if (!units.Alive[id]) continue;
            int cell = CellOf(units.Position[id]);
            _next[id] = _head[cell];
            _head[cell] = id;
        }
    }

    /// <summary>Appends every living unit within <paramref name="radius"/> (plus <paramref name="pad"/>) of <paramref name="centre"/>.</summary>
    public void Query(UnitStore units, FixVec2 centre, Fix64 radius, List<int> results, Fix64 pad = default)
    {
        Fix64 reachDist = radius + pad;
        Fix64 limitSq = reachDist * reachDist;
        int reach = reachDist.ToIntFloor() / CellSize + 1;
        int cx = Col(centre.X), cy = Row(centre.Y);
        for (int y = cy - reach; y <= cy + reach; y++)
        {
            if (y < 0 || y >= _rows) continue;
            for (int x = cx - reach; x <= cx + reach; x++)
            {
                if (x < 0 || x >= _cols) continue;
                for (int id = _head[y * _cols + x]; id != -1; id = _next[id])
                {
                    if (FixVec2.DistanceSquared(centre, units.Position[id]) <= limitSq)
                        results.Add(id);
                }
            }
        }
    }

    /// <summary>
    /// Nearest unit within <paramref name="range"/> that passes <paramref name="accept"/>.
    /// Ties go to the lowest id. Returns -1 if none.
    /// </summary>
    public int FindNearest(UnitStore units, FixVec2 from, Fix64 range, Func<int, bool> accept, Fix64 minRange = default)
    {
        Fix64 bestDistSq = range * range;
        Fix64 minSq = minRange * minRange;
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
                    Fix64 d = FixVec2.DistanceSquared(from, units.Position[id]);
                    if (d > bestDistSq || d < minSq) continue;
                    if (d == bestDistSq && best != -1 && id > best) continue;
                    if (!accept(id)) continue;
                    bestDistSq = d;
                    best = id;
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
