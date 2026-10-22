using System;
using System.Collections.Generic;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;

namespace ContingencyZero.Sim.Pathing;

/// <summary>
/// Deterministic A* over map tiles with integer costs (10 straight, 14 diagonal). Ties break on tile
/// index, so the same request always gives the same path on every machine. The result is smoothed
/// by line-of-sight so units don't zig-zag along tile centres.
/// </summary>
public sealed class Pathfinder
{
    private const int Straight = 10;
    private const int Diagonal = 14;
    /// <summary>Upper bound on tiles expanded per search, so one hopeless request can't stall a tick.</summary>
    private const int MaxExpansions = 12000;

    private readonly Terrain _terrain;
    private readonly int[] _g;
    private readonly int[] _parent;
    private readonly int[] _openStamp;
    private readonly int[] _closedStamp;
    private int _stamp;

    // Binary min-heap of (f, tile).
    private long[] _heap = new long[1024];
    private int _heapCount;

    private static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
    private static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

    public Pathfinder(Terrain terrain)
    {
        _terrain = terrain;
        int n = terrain.TilesX * terrain.TilesY;
        _g = new int[n];
        _parent = new int[n];
        _openStamp = new int[n];
        _closedStamp = new int[n];
    }

    /// <summary>
    /// Path from <paramref name="from"/> to <paramref name="to"/> for the given mover. If the goal
    /// can't be reached, goes to the reachable tile closest to it. Returns waypoints in world space,
    /// ending at the goal (or the closest reachable point); empty if already there.
    /// </summary>
    public List<FixVec2> Find(MoveClass moveClass, FixVec2 from, FixVec2 to)
    {
        var result = new List<FixVec2>();
        int sx = Clamp(MapData.TileOf(from.X), _terrain.TilesX), sy = Clamp(MapData.TileOf(from.Y), _terrain.TilesY);
        int gx = Clamp(MapData.TileOf(to.X), _terrain.TilesX), gy = Clamp(MapData.TileOf(to.Y), _terrain.TilesY);
        int start = _terrain.Index(sx, sy);
        int goal = _terrain.Index(gx, gy);

        if (start == goal)
        {
            result.Add(to);
            return result;
        }

        _stamp++;
        if (_stamp == int.MaxValue)
        {
            Array.Clear(_openStamp);
            Array.Clear(_closedStamp);
            _stamp = 1;
        }
        _heapCount = 0;

        _g[start] = 0;
        _parent[start] = -1;
        _openStamp[start] = _stamp;
        Push(Heuristic(sx, sy, gx, gy), start);

        int best = start;
        int bestH = Heuristic(sx, sy, gx, gy);
        int expansions = 0;
        bool found = false;

        while (_heapCount > 0 && expansions < MaxExpansions)
        {
            int current = Pop();
            if (_closedStamp[current] == _stamp) continue;
            _closedStamp[current] = _stamp;
            expansions++;

            if (current == goal) { found = true; break; }

            int cx = current % _terrain.TilesX, cy = current / _terrain.TilesX;
            int h = Heuristic(cx, cy, gx, gy);
            if (h < bestH || (h == bestH && current < best)) { bestH = h; best = current; }

            for (int d = 0; d < 8; d++)
            {
                int nx = cx + Dx[d], ny = cy + Dy[d];
                if (!_terrain.InBounds(nx, ny)) continue;
                int n = _terrain.Index(nx, ny);
                if (_closedStamp[n] == _stamp) continue;

                if (!_terrain.Passable(moveClass, nx, ny)) continue;
                if (d >= 4 && (!_terrain.Passable(moveClass, cx + Dx[d], cy) || !_terrain.Passable(moveClass, cx, cy + Dy[d])))
                    continue; // no cutting corners

                int g = _g[current] + (d < 4 ? Straight : Diagonal);
                if (_openStamp[n] == _stamp && g >= _g[n]) continue;
                _g[n] = g;
                _parent[n] = current;
                _openStamp[n] = _stamp;
                Push(g + Heuristic(nx, ny, gx, gy), n);
            }
        }

        int end = found ? goal : best;
        if (end == start)
        {
            if (found) result.Add(to);
            return result;
        }

        var tiles = new List<int>();
        for (int t = end; t != -1; t = _parent[t]) tiles.Add(t);
        tiles.Reverse();

        // String-pull: keep only the tiles needed to stay on walkable ground.
        int anchor = 0;
        for (int i = 2; i < tiles.Count; i++)
        {
            if (!LineWalkable(moveClass, tiles[anchor], tiles[i]))
            {
                anchor = i - 1;
                result.Add(TileCentre(tiles[anchor]));
            }
        }
        result.Add(found ? to : TileCentre(end));
        return result;
    }

    /// <summary>Closest tile to <paramref name="p"/> the mover can stand on, searching outwards in rings.</summary>
    public FixVec2 NearestPassable(MoveClass moveClass, FixVec2 p, int maxRadius = 12)
    {
        int px = Clamp(MapData.TileOf(p.X), _terrain.TilesX), py = Clamp(MapData.TileOf(p.Y), _terrain.TilesY);
        if (_terrain.Passable(moveClass, px, py)) return p;
        for (int r = 1; r <= maxRadius; r++)
        {
            int bestIndex = -1, bestDist = int.MaxValue;
            for (int y = py - r; y <= py + r; y++)
            for (int x = px - r; x <= px + r; x++)
            {
                if (Math2.Abs(x - px) != r && Math2.Abs(y - py) != r) continue;
                if (!_terrain.Passable(moveClass, x, y)) continue;
                int dist = (x - px) * (x - px) + (y - py) * (y - py);
                int idx = _terrain.Index(x, y);
                if (dist < bestDist || (dist == bestDist && idx < bestIndex)) { bestDist = dist; bestIndex = idx; }
            }
            if (bestIndex >= 0) return TileCentre(bestIndex);
        }
        return p;
    }

    /// <summary>Whether a straight walk from <paramref name="a"/> to <paramref name="b"/> stays on usable tiles.</summary>
    public bool StraightLineClear(MoveClass moveClass, FixVec2 a, FixVec2 b)
    {
        int ax = Clamp(MapData.TileOf(a.X), _terrain.TilesX), ay = Clamp(MapData.TileOf(a.Y), _terrain.TilesY);
        int bx = Clamp(MapData.TileOf(b.X), _terrain.TilesX), by = Clamp(MapData.TileOf(b.Y), _terrain.TilesY);
        if (!_terrain.Passable(moveClass, bx, by)) return false;
        return LineWalkable(moveClass, _terrain.Index(ax, ay), _terrain.Index(bx, by), skipStart: true);
    }

    private bool LineWalkable(MoveClass moveClass, int a, int b, bool skipStart = false)
    {
        int x0 = a % _terrain.TilesX, y0 = a / _terrain.TilesX;
        int x1 = b % _terrain.TilesX, y1 = b / _terrain.TilesX;
        int dx = Math2.Abs(x1 - x0), dy = Math2.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        int x = x0, y = y0;
        bool first = true;
        while (true)
        {
            if (!(first && skipStart) && !_terrain.Passable(moveClass, x, y)) return false;
            first = false;
            if (x == x1 && y == y1) return true;
            int e2 = 2 * err;
            if (e2 > -dy && e2 < dx)
            {
                // Diagonal step: both neighbours must be clear too.
                if (!_terrain.Passable(moveClass, x + sx, y) || !_terrain.Passable(moveClass, x, y + sy)) return false;
            }
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
        }
    }

    private FixVec2 TileCentre(int index) => new(
        Fix64.FromInt((index % _terrain.TilesX) * MapData.TileSize + MapData.TileSize / 2),
        Fix64.FromInt((index / _terrain.TilesX) * MapData.TileSize + MapData.TileSize / 2));

    private static int Heuristic(int x0, int y0, int x1, int y1)
    {
        int dx = Math2.Abs(x1 - x0), dy = Math2.Abs(y1 - y0);
        int lo = dx < dy ? dx : dy, hi = dx < dy ? dy : dx;
        return Diagonal * lo + Straight * (hi - lo);
    }

    private static int Clamp(int v, int count) => v < 0 ? 0 : v >= count ? count - 1 : v;

    private void Push(int f, int tile)
    {
        if (_heapCount == _heap.Length) Array.Resize(ref _heap, _heap.Length * 2);
        long key = ((long)f << 32) | (uint)tile;
        int i = _heapCount++;
        _heap[i] = key;
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (_heap[parent] <= _heap[i]) break;
            (_heap[parent], _heap[i]) = (_heap[i], _heap[parent]);
            i = parent;
        }
    }

    private int Pop()
    {
        long top = _heap[0];
        _heap[0] = _heap[--_heapCount];
        int i = 0;
        while (true)
        {
            int l = 2 * i + 1, r = l + 1, m = i;
            if (l < _heapCount && _heap[l] < _heap[m]) m = l;
            if (r < _heapCount && _heap[r] < _heap[m]) m = r;
            if (m == i) break;
            (_heap[m], _heap[i]) = (_heap[i], _heap[m]);
            i = m;
        }
        return (int)(top & 0xFFFFFFFF);
    }
}

/// <summary>Integer helpers, so sim code never reaches for System.Math.</summary>
public static class Math2
{
    public static int Abs(int v) => v < 0 ? -v : v;
    public static int Min(int a, int b) => a < b ? a : b;
    public static int Max(int a, int b) => a > b ? a : b;
    public static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
}
