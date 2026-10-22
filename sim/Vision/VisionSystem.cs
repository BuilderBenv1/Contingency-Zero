using System;
using System.Collections.Generic;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.State;

namespace ContingencyZero.Sim.Vision;

/// <summary>
/// Fog of war. Every few ticks each team's sight and radar are stamped onto a tile grid.
/// "Visible" means a unit of the team sees the tile now; "explored" means it has ever been seen;
/// "radar" means radar covers it, which shows blips and lets artillery fire there.
/// </summary>
public sealed class VisionSystem
{
    public const int MaxTeams = 8;
    public const int UpdateInterval = 3;

    private readonly int _tilesX, _tilesY;
    private readonly bool[][] _visible = new bool[MaxTeams][];
    private readonly bool[][] _explored = new bool[MaxTeams][];
    private readonly bool[][] _radar = new bool[MaxTeams][];
    private readonly Dictionary<int, (int, int)[]> _circleCache = new();

    public bool RevealAll { get; set; }

    public VisionSystem(int tilesX, int tilesY)
    {
        _tilesX = tilesX;
        _tilesY = tilesY;
        for (int t = 0; t < MaxTeams; t++)
        {
            _visible[t] = new bool[tilesX * tilesY];
            _explored[t] = new bool[tilesX * tilesY];
            _radar[t] = new bool[tilesX * tilesY];
        }
    }

    public int TilesX => _tilesX;
    public int TilesY => _tilesY;

    public bool IsVisible(int team, int tx, int ty) =>
        RevealAll || (InBounds(tx, ty) && _visible[team][ty * _tilesX + tx]);

    public bool IsExplored(int team, int tx, int ty) =>
        RevealAll || (InBounds(tx, ty) && _explored[team][ty * _tilesX + tx]);

    public bool IsOnRadar(int team, int tx, int ty) =>
        RevealAll || (InBounds(tx, ty) && _radar[team][ty * _tilesX + tx]);

    /// <summary>Raw grids, for the renderer's fog texture and the minimap.</summary>
    public bool[] VisibleGrid(int team) => _visible[team];
    public bool[] ExploredGrid(int team) => _explored[team];
    public bool[] RadarGrid(int team) => _radar[team];

    private bool InBounds(int tx, int ty) => tx >= 0 && ty >= 0 && tx < _tilesX && ty < _tilesY;

    public void Update(World world)
    {
        for (int t = 0; t < MaxTeams; t++)
        {
            Array.Clear(_visible[t]);
            Array.Clear(_radar[t]);
        }

        UnitStore units = world.Units;
        for (int id = 0; id < units.Count; id++)
        {
            if (!units.Alive[id]) continue;
            int team = world.Players[units.Owner[id]].Team;
            UnitDef def = world.Catalog[units.TypeIndex[id]];
            int tx = MapData.TileOf(units.Position[id].X), ty = MapData.TileOf(units.Position[id].Y);

            int sight = units.Complete[id] ? def.SightRange.ToIntFloor() : 8;
            Stamp(_visible[team], _explored[team], tx, ty, sight);
            if (units.Complete[id] && def.RadarRange.ToIntFloor() > 0)
                Stamp(_radar[team], null, tx, ty, def.RadarRange.ToIntFloor());
        }
    }

    private void Stamp(bool[] grid, bool[]? also, int cx, int cy, int rangeUnits)
    {
        foreach (var (dx, dy) in Circle(rangeUnits))
        {
            int x = cx + dx, y = cy + dy;
            if (!InBounds(x, y)) continue;
            int i = y * _tilesX + x;
            grid[i] = true;
            if (also is not null) also[i] = true;
        }
    }

    /// <summary>Tile offsets within a radius given in world units. Cached per radius.</summary>
    private (int, int)[] Circle(int rangeUnits)
    {
        int r = (rangeUnits + MapData.TileSize - 1) / MapData.TileSize;
        if (_circleCache.TryGetValue(r, out var cached)) return cached;
        var list = new List<(int, int)>();
        int r2 = r * r + r; // a touch generous so small sights still cover a full ring
        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
            if (dx * dx + dy * dy <= r2) list.Add((dx, dy));
        var arr = list.ToArray();
        _circleCache[r] = arr;
        return arr;
    }
}
