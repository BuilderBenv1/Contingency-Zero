using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using ContingencyZero.Sim.Core;

namespace ContingencyZero.Sim.Map;

public enum FeatureKind : byte
{
    Wreck = 0,
    Rock = 1,
    Tree = 2,
    /// <summary>Broken slabs and beams left where a structure stood. Reclaimable, never decays.</summary>
    Rubble = 3,
}

public readonly record struct MetalSpot(FixVec2 Position, Fix64 Yield);

public readonly record struct FeaturePlacement(FeatureKind Kind, FixVec2 Position, Fix64 Metal, Fix64 Energy);

/// <summary>
/// A battlefield: a grid of terrain heights, the water level, metal spots, reclaimable features and
/// start positions. The map is split into square tiles <see cref="TileSize"/> world units across;
/// heights are stored at tile corners in quarter-unit steps.
/// </summary>
public sealed class MapData
{
    public const int TileSize = 8;
    /// <summary>One height step is a quarter of a world unit.</summary>
    public const int HeightStepsPerUnit = 4;

    public string Name { get; set; } = "Untitled";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public int TilesX { get; }
    public int TilesY { get; }
    public int Width => TilesX * TileSize;
    public int Height => TilesY * TileSize;

    /// <summary>Corner heights, (TilesX + 1) x (TilesY + 1), row-major.</summary>
    public int[] Heights { get; }
    public int WaterLevel { get; set; }
    public Fix64 WindMin { get; set; } = Fix64.FromInt(5);
    public Fix64 WindMax { get; set; } = Fix64.FromInt(20);
    /// <summary>Visual theme for the renderer ("temperate", "desert", "lava", "arctic").</summary>
    public string Theme { get; set; } = "temperate";

    public List<MetalSpot> MetalSpots { get; } = new();
    public List<FeaturePlacement> Features { get; } = new();
    public List<FixVec2> StartPositions { get; } = new();

    public MapData(int tilesX, int tilesY)
    {
        if (tilesX < 4 || tilesY < 4 || tilesX > 512 || tilesY > 512)
            throw new ArgumentOutOfRangeException(nameof(tilesX), "Maps are 4 to 512 tiles a side");
        TilesX = tilesX;
        TilesY = tilesY;
        Heights = new int[(tilesX + 1) * (tilesY + 1)];
    }

    /// <summary>A deep copy, so a match can reshape its ground without touching the map it was made from.</summary>
    public MapData Clone()
    {
        var m = new MapData(TilesX, TilesY)
        {
            Name = Name,
            Description = Description,
            Author = Author,
            WaterLevel = WaterLevel,
            WindMin = WindMin,
            WindMax = WindMax,
            Theme = Theme,
        };
        Array.Copy(Heights, m.Heights, Heights.Length);
        m.MetalSpots.AddRange(MetalSpots);
        m.Features.AddRange(Features);
        m.StartPositions.AddRange(StartPositions);
        return m;
    }

    public int CornerHeight(int vx, int vy)
    {
        vx = vx < 0 ? 0 : vx > TilesX ? TilesX : vx;
        vy = vy < 0 ? 0 : vy > TilesY ? TilesY : vy;
        return Heights[vy * (TilesX + 1) + vx];
    }

    public void SetCornerHeight(int vx, int vy, int h)
    {
        if (vx < 0 || vy < 0 || vx > TilesX || vy > TilesY) return;
        Heights[vy * (TilesX + 1) + vx] = h;
    }

    /// <summary>Average of the tile's four corners.</summary>
    public int TileHeight(int tx, int ty) =>
        (CornerHeight(tx, ty) + CornerHeight(tx + 1, ty) + CornerHeight(tx, ty + 1) + CornerHeight(tx + 1, ty + 1)) / 4;

    /// <summary>Difference between the tile's highest and lowest corner.</summary>
    public int TileSlope(int tx, int ty)
    {
        int a = CornerHeight(tx, ty), b = CornerHeight(tx + 1, ty), c = CornerHeight(tx, ty + 1), d = CornerHeight(tx + 1, ty + 1);
        int hi = a, lo = a;
        if (b > hi) hi = b; if (b < lo) lo = b;
        if (c > hi) hi = c; if (c < lo) lo = c;
        if (d > hi) hi = d; if (d < lo) lo = d;
        return hi - lo;
    }

    /// <summary>How far the tile sits below the water line, in height steps (negative means dry).</summary>
    public int TileDepth(int tx, int ty) => WaterLevel - TileHeight(tx, ty);

    public bool InBounds(int tx, int ty) => tx >= 0 && ty >= 0 && tx < TilesX && ty < TilesY;

    public static int TileOf(Fix64 coordinate) => coordinate.ToIntFloor() / TileSize;

    /// <summary>Ground height at a world position, bilinear between corners, in height steps (as Fix64).</summary>
    public Fix64 GroundHeightAt(FixVec2 p)
    {
        Fix64 fx = p.X / TileSize, fy = p.Y / TileSize;
        int tx = fx.ToIntFloor(), ty = fy.ToIntFloor();
        Fix64 ux = fx - Fix64.FromInt(tx), uy = fy - Fix64.FromInt(ty);
        Fix64 h00 = Fix64.FromInt(CornerHeight(tx, ty)), h10 = Fix64.FromInt(CornerHeight(tx + 1, ty));
        Fix64 h01 = Fix64.FromInt(CornerHeight(tx, ty + 1)), h11 = Fix64.FromInt(CornerHeight(tx + 1, ty + 1));
        Fix64 top = h00 + (h10 - h00) * ux;
        Fix64 bottom = h01 + (h11 - h01) * ux;
        return top + (bottom - top) * uy;
    }

    /// <summary>A flat dry map, for tests and as the editor's starting point.</summary>
    public static MapData Flat(string name, int tilesX, int tilesY, int height = 40, int startPositions = 2)
    {
        var map = new MapData(tilesX, tilesY) { Name = name, WaterLevel = 0 };
        Array.Fill(map.Heights, height);
        int w = map.Width, h = map.Height;
        if (startPositions >= 1) map.StartPositions.Add(FixVec2.FromInts(w / 2, 40));
        if (startPositions >= 2) map.StartPositions.Add(FixVec2.FromInts(w / 2, h - 40));
        if (startPositions >= 3) map.StartPositions.Add(FixVec2.FromInts(40, h / 2));
        if (startPositions >= 4) map.StartPositions.Add(FixVec2.FromInts(w - 40, h / 2));
        return map;
    }

    // ---- JSON ------------------------------------------------------------------------------------

    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            w.WriteStartObject();
            w.WriteNumber("formatVersion", 1);
            w.WriteString("name", Name);
            w.WriteString("description", Description);
            w.WriteString("author", Author);
            w.WriteString("theme", Theme);
            w.WriteNumber("tilesX", TilesX);
            w.WriteNumber("tilesY", TilesY);
            w.WriteNumber("waterLevel", WaterLevel);
            WriteFix(w, "windMin", WindMin);
            WriteFix(w, "windMax", WindMax);

            w.WriteStartArray("heights");
            foreach (int h in Heights) w.WriteNumberValue(h);
            w.WriteEndArray();

            w.WriteStartArray("metalSpots");
            foreach (MetalSpot s in MetalSpots)
            {
                w.WriteStartObject();
                WriteFix(w, "x", s.Position.X);
                WriteFix(w, "y", s.Position.Y);
                WriteFix(w, "yield", s.Yield);
                w.WriteEndObject();
            }
            w.WriteEndArray();

            w.WriteStartArray("features");
            foreach (FeaturePlacement f in Features)
            {
                w.WriteStartObject();
                w.WriteString("kind", f.Kind.ToString().ToLowerInvariant());
                WriteFix(w, "x", f.Position.X);
                WriteFix(w, "y", f.Position.Y);
                WriteFix(w, "metal", f.Metal);
                WriteFix(w, "energy", f.Energy);
                w.WriteEndObject();
            }
            w.WriteEndArray();

            w.WriteStartArray("startPositions");
            foreach (FixVec2 p in StartPositions)
            {
                w.WriteStartObject();
                WriteFix(w, "x", p.X);
                WriteFix(w, "y", p.Y);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Writes a fixed-point number as exact decimal text (via its raw value), never through floating point.</summary>
    private static void WriteFix(Utf8JsonWriter w, string name, Fix64 v)
    {
        w.WritePropertyName(name);
        w.WriteRawValue(FixToDecimalText(v));
    }

    internal static string FixToDecimalText(Fix64 v)
    {
        long raw = v.Raw;
        bool neg = raw < 0;
        ulong abs = neg ? (ulong)(-raw) : (ulong)raw;
        ulong whole = abs >> Fix64.FractionalBits;
        ulong frac = abs & (ulong)(Fix64.OneRaw - 1);
        if (frac == 0) return (neg ? "-" : "") + whole;
        // 16 fractional bits need at most 16 decimal digits to be exact.
        var sb = new StringBuilder();
        sb.Append(neg ? "-" : "").Append(whole).Append('.');
        for (int i = 0; i < 16 && frac != 0; i++)
        {
            frac *= 10;
            sb.Append((char)('0' + (int)(frac >> Fix64.FractionalBits)));
            frac &= (ulong)(Fix64.OneRaw - 1);
        }
        return sb.ToString();
    }

    public static MapData FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        JsonElement r = doc.RootElement;
        static Fix64 F(JsonElement o, string n, string fallback = "0") =>
            o.TryGetProperty(n, out JsonElement e) ? Fix64.Parse(e.GetRawText()) : Fix64.Parse(fallback);
        static string S(JsonElement o, string n, string fallback) =>
            o.TryGetProperty(n, out JsonElement e) && e.GetString() is { } s ? s : fallback;

        var map = new MapData(r.GetProperty("tilesX").GetInt32(), r.GetProperty("tilesY").GetInt32())
        {
            Name = S(r, "name", "Untitled"),
            Description = S(r, "description", ""),
            Author = S(r, "author", ""),
            Theme = S(r, "theme", "temperate"),
            WaterLevel = r.TryGetProperty("waterLevel", out JsonElement wl) ? wl.GetInt32() : 0,
            WindMin = F(r, "windMin", "5"),
            WindMax = F(r, "windMax", "20"),
        };

        int i = 0;
        foreach (JsonElement h in r.GetProperty("heights").EnumerateArray())
        {
            if (i >= map.Heights.Length) throw new InvalidDataException("Too many heights for the map size");
            map.Heights[i++] = h.GetInt32();
        }
        if (i != map.Heights.Length) throw new InvalidDataException("Too few heights for the map size");

        if (r.TryGetProperty("metalSpots", out JsonElement spots))
            foreach (JsonElement s in spots.EnumerateArray())
                map.MetalSpots.Add(new MetalSpot(new FixVec2(F(s, "x"), F(s, "y")), F(s, "yield", "2")));

        if (r.TryGetProperty("features", out JsonElement features))
            foreach (JsonElement f in features.EnumerateArray())
            {
                if (!Enum.TryParse(S(f, "kind", "rock"), true, out FeatureKind kind)) kind = FeatureKind.Rock;
                map.Features.Add(new FeaturePlacement(kind, new FixVec2(F(f, "x"), F(f, "y")), F(f, "metal"), F(f, "energy")));
            }

        if (r.TryGetProperty("startPositions", out JsonElement starts))
            foreach (JsonElement p in starts.EnumerateArray())
                map.StartPositions.Add(new FixVec2(F(p, "x"), F(p, "y")));

        return map;
    }

    /// <summary>Stable hash of the whole map, so replays and multiplayer can check everyone has the same one.</summary>
    public ulong Fingerprint()
    {
        var h = new StateHasher();
        h.Add(TilesX); h.Add(TilesY); h.Add(WaterLevel); h.Add(WindMin); h.Add(WindMax);
        foreach (int v in Heights) h.Add(v);
        foreach (MetalSpot s in MetalSpots) { h.Add(s.Position); h.Add(s.Yield); }
        foreach (FeaturePlacement f in Features) { h.Add((int)f.Kind); h.Add(f.Position); h.Add(f.Metal); h.Add(f.Energy); }
        foreach (FixVec2 p in StartPositions) h.Add(p);
        return h.Value;
    }
}
