using System;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;

namespace ContingencyZero.Sim.Map;

/// <summary>
/// Which tiles each kind of mover can use, plus which tiles structures currently occupy.
/// Passability comes from the map's heights and is refreshed where craters reshape the ground;
/// occupancy changes as structures go up and down.
/// </summary>
public sealed class Terrain
{
    // Slope limits are the most a tile's corners may differ, in height steps over one 8-unit tile.
    public const int BotMaxSlope = 26;
    public const int VehicleMaxSlope = 15;
    public const int HoverMaxSlope = 15;
    public const int AmphibiousMaxSlope = 20;
    public const int StructureMaxSlope = 10;
    /// <summary>Land units can wade this deep (one world unit).</summary>
    public const int MaxWadeDepth = 4;
    /// <summary>Ships need at least this much water (two world units).</summary>
    public const int MinShipDepth = 8;

    public MapData Map { get; }
    public int TilesX => Map.TilesX;
    public int TilesY => Map.TilesY;

    private readonly bool[][] _passable; // by MoveClass
    /// <summary>Unit id of the structure on each tile, or -1.</summary>
    public readonly int[] Occupant;
    /// <summary>The ground as the map was made: craters can't dig more than <see cref="MaxCraterDepth"/> below it.</summary>
    private readonly int[] _originalHeights;
    /// <summary>Deepest a crater can go below the original ground, in height steps (12 world units).</summary>
    public const int MaxCraterDepth = 48;
    /// <summary>Highest a rim can pile up above the original ground, in height steps.</summary>
    public const int MaxRimHeight = 10;

    public Terrain(MapData map)
    {
        Map = map;
        int n = map.TilesX * map.TilesY;
        Occupant = new int[n];
        Array.Fill(Occupant, -1);
        _originalHeights = (int[])map.Heights.Clone();

        int classes = Enum.GetValues<MoveClass>().Length;
        _passable = new bool[classes][];
        for (int c = 0; c < classes; c++) _passable[c] = new bool[n];

        RefreshPassability(0, 0, map.TilesX - 1, map.TilesY - 1);
        _zones = new int[classes][];
    }

    // ---- Connectivity zones ------------------------------------------------------------------
    // For each move class, every terrain-passable tile gets the id of the connected region it
    // belongs to (8-way, no corner cutting, same rules as the pathfinder; structures ignored).
    // Two tiles in different zones can never reach each other, so a search between them can be
    // skipped. Built lazily by a flood fill in tile order (deterministic) and dropped whenever
    // passability changes.
    private readonly int[]?[] _zones;

    /// <summary>Connected-region id of a tile for a mover, or -1 if the tile isn't passable for it.</summary>
    public int ZoneOf(MoveClass c, int tx, int ty)
    {
        if (!InBounds(tx, ty)) return -1;
        int[] z = _zones[(int)c] ??= BuildZones(c);
        return z[Index(tx, ty)];
    }

    private int[] BuildZones(MoveClass c)
    {
        int n = TilesX * TilesY;
        var zone = new int[n];
        Array.Fill(zone, -1);
        bool[] pass = _passable[(int)c];
        var stack = new int[n];
        int next = 0;
        for (int seed = 0; seed < n; seed++)
        {
            if (!pass[seed] || zone[seed] != -1) continue;
            int top = 0;
            stack[top++] = seed;
            zone[seed] = next;
            while (top > 0)
            {
                int i = stack[--top];
                int x = i % TilesX, y = i / TilesX;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (!InBounds(nx, ny)) continue;
                    int j = Index(nx, ny);
                    if (zone[j] != -1 || !pass[j]) continue;
                    if (dx != 0 && dy != 0 && (!pass[Index(x + dx, y)] || !pass[Index(x, y + dy)])) continue;
                    zone[j] = next;
                    stack[top++] = j;
                }
            }
            next++;
        }
        return zone;
    }

    /// <summary>Recomputes static passability for the tiles in a rectangle (inclusive). Returns true if any tile changed for a land mover.</summary>
    public bool RefreshPassability(int tx0, int ty0, int tx1, int ty1)
    {
        bool changed = false;
        MapData map = Map;
        if (tx0 < 0) tx0 = 0;
        if (ty0 < 0) ty0 = 0;
        if (tx1 >= map.TilesX) tx1 = map.TilesX - 1;
        if (ty1 >= map.TilesY) ty1 = map.TilesY - 1;
        for (int ty = ty0; ty <= ty1; ty++)
        for (int tx = tx0; tx <= tx1; tx++)
        {
            int i = ty * map.TilesX + tx;
            int slope = map.TileSlope(tx, ty);
            int depth = map.TileDepth(tx, ty);
            bool bot = slope <= BotMaxSlope && depth <= MaxWadeDepth;
            bool vehicle = slope <= VehicleMaxSlope && depth <= MaxWadeDepth;
            bool amphibious = slope <= AmphibiousMaxSlope;
            if (_passable[(int)MoveClass.Bot][i] != bot || _passable[(int)MoveClass.Vehicle][i] != vehicle
                || _passable[(int)MoveClass.Amphibious][i] != amphibious)
                changed = true;
            _passable[(int)MoveClass.Bot][i] = bot;
            _passable[(int)MoveClass.Vehicle][i] = vehicle;
            _passable[(int)MoveClass.Hover][i] = slope <= HoverMaxSlope;
            _passable[(int)MoveClass.Amphibious][i] = amphibious;
            _passable[(int)MoveClass.Ship][i] = depth >= MinShipDepth;
            _passable[(int)MoveClass.Air][i] = true;
        }
        // Craters can join or split regions: rebuild connectivity on next use.
        if (_zones is not null) Array.Clear(_zones);
        return changed;
    }

    /// <summary>Whether the last <see cref="Deform"/> changed where any land unit can go.</summary>
    public bool LastDeformChangedPassability { get; private set; }

    /// <summary>
    /// Blasts a crater: lowers the ground in a bowl of the given radius (world units) and depth
    /// (height steps) with a low raised rim, leaving corners under structures untouched, then
    /// refreshes passability. Returns the changed tile rectangle (inclusive), or null if nothing moved.
    /// </summary>
    public (int Tx0, int Ty0, int Tx1, int Ty1)? Deform(FixVec2 centre, Fix64 radius, int depth)
    {
        if (depth <= 0 || radius <= Fix64.Zero) return null;
        MapData map = Map;
        // Work in integer height-step space: corners sit on an 8-unit lattice.
        int cx = centre.X.ToIntFloor(), cy = centre.Y.ToIntFloor();
        int r = radius.ToIntFloor();
        if (r < 4) r = 4;
        int rimR = r + r / 2;                   // the rim ends 50% beyond the bowl
        int vx0 = (cx - rimR) / MapData.TileSize - 1, vx1 = (cx + rimR) / MapData.TileSize + 1;
        int vy0 = (cy - rimR) / MapData.TileSize - 1, vy1 = (cy + rimR) / MapData.TileSize + 1;
        if (vx0 < 0) vx0 = 0;
        if (vy0 < 0) vy0 = 0;
        if (vx1 > map.TilesX) vx1 = map.TilesX;
        if (vy1 > map.TilesY) vy1 = map.TilesY;
        long r2 = (long)r * r, rim2 = (long)rimR * rimR;
        // Deeper craters need to be wider, or their walls get too steep even for bots: the floor
        // for this blast is capped by its radius as well as by the overall limit.
        int cap = r * 14 / 10;
        if (cap > MaxCraterDepth) cap = MaxCraterDepth;
        bool any = false;
        for (int vy = vy0; vy <= vy1; vy++)
        for (int vx = vx0; vx <= vx1; vx++)
        {
            if (CornerTouchesStructure(vx, vy)) continue;
            long dx = (long)vx * MapData.TileSize - cx, dy = (long)vy * MapData.TileSize - cy;
            long d2 = dx * dx + dy * dy;
            int change;
            if (d2 < r2)
            {
                // A bowl with gentle edges: depth * (1 - (d/r)^2)^2, steepest a third of the way in.
                long k = r2 - d2;
                change = -(int)((depth * k * k) / (r2 * r2));
            }
            else if (d2 < rim2)
            {
                change = (int)((depth / 6) * (rim2 - d2) / (rim2 - r2)); // the rim
            }
            else continue;
            if (change == 0) continue;
            int original = _originalHeights[vy * (map.TilesX + 1) + vx];
            int h = map.CornerHeight(vx, vy) + change;
            if (h < original - cap) h = original - cap;
            if (h > original + MaxRimHeight) h = original + MaxRimHeight;
            if (h < 0) h = 0;
            if (h == map.CornerHeight(vx, vy)) continue;
            map.SetCornerHeight(vx, vy, h);
            any = true;
        }
        LastDeformChangedPassability = false;
        if (!any) return null;
        var rect = (Tx0: vx0 - 1 < 0 ? 0 : vx0 - 1, Ty0: vy0 - 1 < 0 ? 0 : vy0 - 1,
                    Tx1: vx1 >= map.TilesX ? map.TilesX - 1 : vx1, Ty1: vy1 >= map.TilesY ? map.TilesY - 1 : vy1);
        LastDeformChangedPassability = RefreshPassability(rect.Tx0, rect.Ty0, rect.Tx1, rect.Ty1);
        return rect;
    }

    /// <summary>True if any of the (up to four) tiles sharing this corner holds a structure.</summary>
    private bool CornerTouchesStructure(int vx, int vy)
    {
        for (int ty = vy - 1; ty <= vy; ty++)
        for (int tx = vx - 1; tx <= vx; tx++)
            if (InBounds(tx, ty) && Occupant[Index(tx, ty)] >= 0) return true;
        return false;
    }

    public int Index(int tx, int ty) => ty * TilesX + tx;
    public bool InBounds(int tx, int ty) => tx >= 0 && ty >= 0 && tx < TilesX && ty < TilesY;

    /// <summary>Terrain alone, ignoring structures.</summary>
    public bool TerrainPassable(MoveClass c, int tx, int ty) =>
        InBounds(tx, ty) && _passable[(int)c][Index(tx, ty)];

    /// <summary>Terrain and structures.</summary>
    public bool Passable(MoveClass c, int tx, int ty)
    {
        if (!InBounds(tx, ty)) return false;
        int i = Index(tx, ty);
        if (c == MoveClass.Air) return true;
        return _passable[(int)c][i] && Occupant[i] == -1;
    }

    public bool Passable(MoveClass c, FixVec2 p) => Passable(c, MapData.TileOf(p.X), MapData.TileOf(p.Y));

    /// <summary>Top-left tile of a structure with the given footprint centred as close as possible to <paramref name="p"/>.</summary>
    public static (int Tx, int Ty) FootprintOrigin(FixVec2 p, int footprint)
    {
        // Centre in tile units, minus half the footprint, rounded to the nearest tile.
        Fix64 cx = p.X / MapData.TileSize - Fix64.FromRatio(footprint, 2) + Fix64.Half;
        Fix64 cy = p.Y / MapData.TileSize - Fix64.FromRatio(footprint, 2) + Fix64.Half;
        return (cx.ToIntFloor(), cy.ToIntFloor());
    }

    /// <summary>World position of the centre of a footprint whose top-left tile is (tx, ty).</summary>
    public static FixVec2 FootprintCentre(int tx, int ty, int footprint) => new(
        Fix64.FromInt(tx * MapData.TileSize) + Fix64.FromRatio(footprint * MapData.TileSize, 2),
        Fix64.FromInt(ty * MapData.TileSize) + Fix64.FromRatio(footprint * MapData.TileSize, 2));

    /// <summary>Whether the ground under a footprint suits a structure (ignores other structures).</summary>
    public bool GroundSuits(int tx, int ty, int footprint, bool needsWater)
    {
        for (int y = ty; y < ty + footprint; y++)
        for (int x = tx; x < tx + footprint; x++)
        {
            if (!InBounds(x, y)) return false;
            int depth = Map.TileDepth(x, y);
            if (needsWater)
            {
                if (depth < MinShipDepth) return false;
            }
            else
            {
                if (depth > 0 || Map.TileSlope(x, y) > StructureMaxSlope) return false;
            }
        }
        return true;
    }

    public void SetOccupant(int tx, int ty, int footprint, int unitId)
    {
        for (int y = ty; y < ty + footprint; y++)
        for (int x = tx; x < tx + footprint; x++)
            if (InBounds(x, y)) Occupant[Index(x, y)] = unitId;
    }

    public void ClearOccupant(int unitId, int tx, int ty, int footprint)
    {
        for (int y = ty; y < ty + footprint; y++)
        for (int x = tx; x < tx + footprint; x++)
            if (InBounds(x, y) && Occupant[Index(x, y)] == unitId) Occupant[Index(x, y)] = -1;
    }
}
