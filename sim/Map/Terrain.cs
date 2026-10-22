using System;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;

namespace ContingencyZero.Sim.Map;

/// <summary>
/// Which tiles each kind of mover can use, plus which tiles structures currently occupy.
/// Static passability is computed once from the map; occupancy changes as structures go up and down.
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

    public Terrain(MapData map)
    {
        Map = map;
        int n = map.TilesX * map.TilesY;
        Occupant = new int[n];
        Array.Fill(Occupant, -1);

        int classes = Enum.GetValues<MoveClass>().Length;
        _passable = new bool[classes][];
        for (int c = 0; c < classes; c++) _passable[c] = new bool[n];

        for (int ty = 0; ty < map.TilesY; ty++)
        for (int tx = 0; tx < map.TilesX; tx++)
        {
            int i = ty * map.TilesX + tx;
            int slope = map.TileSlope(tx, ty);
            int depth = map.TileDepth(tx, ty);
            _passable[(int)MoveClass.Bot][i] = slope <= BotMaxSlope && depth <= MaxWadeDepth;
            _passable[(int)MoveClass.Vehicle][i] = slope <= VehicleMaxSlope && depth <= MaxWadeDepth;
            _passable[(int)MoveClass.Hover][i] = slope <= HoverMaxSlope;
            _passable[(int)MoveClass.Amphibious][i] = slope <= AmphibiousMaxSlope;
            _passable[(int)MoveClass.Ship][i] = depth >= MinShipDepth;
            _passable[(int)MoveClass.Air][i] = true;
        }
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
