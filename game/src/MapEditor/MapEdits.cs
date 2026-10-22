using System;
using System.Collections.Generic;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Map;
using Godot;

namespace ContingencyZero.Game.MapEditor;

/// <summary>
/// Edits to a map's objects (metal spots, trees and rocks, start positions) and the symmetry
/// that mirrors them. Positions here are world units as floats; they're stored as Fix64.
/// </summary>
public static class MapEdits
{
    public const int MaxStarts = 8;
    public const int MinStarts = 2;

    /// <summary>Converts an editor float to a fixed-point value, rounded to 1/16 of a unit so it saves as short exact decimals.</summary>
    public static Fix64 Fix(float v) => Fix64.FromRaw((long)MathF.Round(v * 16f) * (Fix64.OneRaw / 16));

    public static float ToFloat(Fix64 v) => v.Raw / (float)Fix64.OneRaw;

    public static Vector2 ToVec(FixVec2 p) => new(ToFloat(p.X), ToFloat(p.Y));

    public static FixVec2 TileCentre(int tx, int ty) => FixVec2.FromInts(tx * MapData.TileSize + MapData.TileSize / 2, ty * MapData.TileSize + MapData.TileSize / 2);

    public static (int Tx, int Ty) TileAt(MapData map, Vector2 p) =>
        (Math.Clamp((int)MathF.Floor(p.X / MapData.TileSize), 0, map.TilesX - 1),
         Math.Clamp((int)MathF.Floor(p.Y / MapData.TileSize), 0, map.TilesY - 1));

    // ---- Symmetry -------------------------------------------------------------------------------

    /// <summary>The point and its mirror images (world units, x across and y down the map), without near-duplicates.</summary>
    public static List<Vector2> Mirrors(MapData map, Vector2 p, Symmetry s) => MirrorFrames(map, p, s).ConvertAll(m => m.P);

    /// <summary>Like <see cref="Mirrors"/>, also saying which axes each image is flipped in (so brushes can mirror their noise too).</summary>
    public static List<(Vector2 P, bool FlipX, bool FlipY)> MirrorFrames(MapData map, Vector2 p, Symmetry s)
    {
        float w = map.Width, h = map.Height;
        var list = new List<(Vector2 P, bool FlipX, bool FlipY)> { (p, false, false) };
        void Add(bool fx, bool fy)
        {
            var q = new Vector2(fx ? w - p.X : p.X, fy ? h - p.Y : p.Y);
            foreach (var e in list) if (e.P.DistanceSquaredTo(q) < 1f) return;
            list.Add((q, fx, fy));
        }
        switch (s)
        {
            case Symmetry.MirrorX: Add(true, false); break;
            case Symmetry.MirrorY: Add(false, true); break;
            case Symmetry.Rotate: Add(true, true); break;
            case Symmetry.FourWay: Add(true, false); Add(false, true); Add(true, true); break;
        }
        return list;
    }

    /// <summary>Tile mirror images (tile centres mirror onto tile centres).</summary>
    public static List<(int Tx, int Ty)> MirrorTiles(MapData map, int tx, int ty, Symmetry s)
    {
        var list = new List<(int, int)> { (tx, ty) };
        void Add(int x, int y) { if (!list.Contains((x, y))) list.Add((x, y)); }
        int mx = map.TilesX - 1 - tx, my = map.TilesY - 1 - ty;
        switch (s)
        {
            case Symmetry.MirrorX: Add(mx, ty); break;
            case Symmetry.MirrorY: Add(tx, my); break;
            case Symmetry.Rotate: Add(mx, my); break;
            case Symmetry.FourWay: Add(mx, ty); Add(tx, my); Add(mx, my); break;
        }
        return list;
    }

    // ---- Metal spots ----------------------------------------------------------------------------

    public static int MetalSpotOnTile(MapData map, int tx, int ty)
    {
        for (int i = 0; i < map.MetalSpots.Count; i++)
        {
            FixVec2 p = map.MetalSpots[i].Position;
            if (MapData.TileOf(p.X) == tx && MapData.TileOf(p.Y) == ty) return i;
        }
        return -1;
    }

    /// <summary>Nearest metal spot within <paramref name="radius"/> world units, or -1.</summary>
    public static int NearestMetalSpot(MapData map, Vector2 p, float radius)
    {
        int best = -1;
        float bestD = radius * radius;
        for (int i = 0; i < map.MetalSpots.Count; i++)
        {
            float d = ToVec(map.MetalSpots[i].Position).DistanceSquaredTo(p);
            if (d <= bestD) { bestD = d; best = i; }
        }
        return best;
    }

    /// <summary>Adds a metal spot on the tile (and its mirrors). Returns how many were added.</summary>
    public static int AddMetalSpot(MapData map, int tx, int ty, Fix64 yield, Symmetry s)
    {
        int added = 0;
        foreach (var (x, y) in MirrorTiles(map, tx, ty, s))
        {
            if (!map.InBounds(x, y) || MetalSpotOnTile(map, x, y) >= 0) continue;
            map.MetalSpots.Add(new MetalSpot(TileCentre(x, y), yield));
            added++;
        }
        return added;
    }

    /// <summary>Removes the metal spot nearest the point (and the spots on its mirror tiles).</summary>
    public static int RemoveMetalSpot(MapData map, Vector2 p, float radius, Symmetry s)
    {
        int i = NearestMetalSpot(map, p, radius);
        if (i < 0) return 0;
        FixVec2 pos = map.MetalSpots[i].Position;
        int removed = 0;
        foreach (var (x, y) in MirrorTiles(map, MapData.TileOf(pos.X), MapData.TileOf(pos.Y), s))
        {
            int j = MetalSpotOnTile(map, x, y);
            if (j < 0) continue;
            map.MetalSpots.RemoveAt(j);
            removed++;
        }
        return removed;
    }

    // ---- Trees and rocks ------------------------------------------------------------------------

    public static float Spacing(FeatureKind kind) => kind == FeatureKind.Tree ? 3.6f : 5.5f;

    /// <summary>Features per tile at 100% density.</summary>
    public static float MaxPerTile(FeatureKind kind) => kind == FeatureKind.Tree ? 1.1f : 0.3f;

    public static int CountInCircle(MapData map, FeatureKind kind, Vector2 c, float r)
    {
        int n = 0;
        float r2 = r * r;
        foreach (FeaturePlacement f in map.Features)
            if (f.Kind == kind && ToVec(f.Position).DistanceSquaredTo(c) <= r2) n++;
        return n;
    }

    /// <summary>Whether a tree or rock may stand here: dry, not a cliff, off metal spots and clear of starts.</summary>
    public static bool FeatureGroundOk(MapData map, FeatureKind kind, Vector2 p)
    {
        if (p.X < 1 || p.Y < 1 || p.X > map.Width - 1 || p.Y > map.Height - 1) return false;
        var (tx, ty) = TileAt(map, p);
        if (map.TileDepth(tx, ty) > (kind == FeatureKind.Tree ? -2 : 0)) return false;
        if (map.TileSlope(tx, ty) > (kind == FeatureKind.Tree ? 14 : Terrain.BotMaxSlope)) return false;
        if (MetalSpotOnTile(map, tx, ty) >= 0) return false;
        foreach (FixVec2 s in map.StartPositions)
            if (ToVec(s).DistanceSquaredTo(p) < 20f * 20f) return false;
        return true;
    }

    private static bool Crowded(MapData map, Vector2 p, float spacing, List<Vector2> justAdded)
    {
        float s2 = spacing * spacing;
        foreach (FeaturePlacement f in map.Features)
            if (ToVec(f.Position).DistanceSquaredTo(p) < s2) return true;
        foreach (Vector2 q in justAdded)
            if (q.DistanceSquaredTo(p) < s2) return true;
        return false;
    }

    /// <summary>
    /// Tops a circle up towards the target density of trees or rocks (never past it, so dragging
    /// back and forth doesn't pile them up). Mirrors each new one. Returns how many were added.
    /// </summary>
    public static int PaintFeatures(MapData map, FeatureKind kind, Vector2 centre, float radius, float density, Symmetry s, Random rng, int maxThisStep)
    {
        float tiles = MathF.PI * radius * radius / (MapData.TileSize * MapData.TileSize);
        int target = (int)MathF.Round(tiles * MaxPerTile(kind) * Math.Clamp(density, 0f, 1f));
        int have = CountInCircle(map, kind, centre, radius);
        int want = Math.Min(maxThisStep, target - have);
        if (want <= 0) return 0;

        int added = 0;
        var placed = new List<Vector2>();
        for (int attempt = 0; attempt < want * 12 && added < want; attempt++)
        {
            float a = (float)(rng.NextDouble() * Math.Tau);
            float r = radius * MathF.Sqrt((float)rng.NextDouble());
            var p = centre + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
            List<Vector2> images = Mirrors(map, p, s);
            bool ok = true;
            foreach (Vector2 q in images)
                if (!FeatureGroundOk(map, kind, q) || Crowded(map, q, Spacing(kind), placed)) { ok = false; break; }
            if (!ok) continue;
            // Same resources for every mirror image, so the map stays fair.
            Fix64 metal = kind == FeatureKind.Rock ? Fix64.FromInt(20 + rng.Next(0, 5) * 10) : Fix64.Zero;
            Fix64 energy = kind == FeatureKind.Tree ? Fix64.FromInt(30 + rng.Next(0, 4) * 10) : Fix64.Zero;
            foreach (Vector2 q in images)
            {
                map.Features.Add(new FeaturePlacement(kind, new FixVec2(Fix(q.X), Fix(q.Y)), metal, energy));
                placed.Add(q);
            }
            added++;
        }
        return added;
    }

    /// <summary>Removes features (of one kind, or every kind) inside the circle and its mirrors.</summary>
    public static int EraseFeatures(MapData map, FeatureKind? kind, Vector2 centre, float radius, Symmetry s)
    {
        float r2 = radius * radius;
        List<Vector2> centres = Mirrors(map, centre, s);
        return map.Features.RemoveAll(f =>
        {
            if (kind is { } k && f.Kind != k) return false;
            Vector2 p = ToVec(f.Position);
            foreach (Vector2 c in centres) if (c.DistanceSquaredTo(p) <= r2) return true;
            return false;
        });
    }

    // ---- Start positions ------------------------------------------------------------------------

    public static int NearestStart(MapData map, Vector2 p, float radius)
    {
        int best = -1;
        float bestD = radius * radius;
        for (int i = 0; i < map.StartPositions.Count; i++)
        {
            float d = ToVec(map.StartPositions[i]).DistanceSquaredTo(p);
            if (d <= bestD) { bestD = d; best = i; }
        }
        return best;
    }

    /// <summary>Adds a start on the tile (and, with symmetry, at its mirror images while there's room). Returns how many were added.</summary>
    public static int AddStart(MapData map, int tx, int ty, Symmetry s)
    {
        int added = 0;
        foreach (var (x, y) in MirrorTiles(map, tx, ty, s))
        {
            if (map.StartPositions.Count >= MaxStarts) break;
            FixVec2 c = TileCentre(x, y);
            if (NearestStart(map, ToVec(c), MapData.TileSize * 3) >= 0) continue;
            map.StartPositions.Add(c);
            added++;
        }
        return added;
    }

    public static bool MoveStart(MapData map, int index, int tx, int ty)
    {
        if (index < 0 || index >= map.StartPositions.Count) return false;
        FixVec2 c = TileCentre(tx, ty);
        if (map.StartPositions[index] == c) return false;
        map.StartPositions[index] = c;
        return true;
    }
}
