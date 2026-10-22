using System;
using System.Collections.Generic;
using ContingencyZero.Game.View;
using ContingencyZero.Sim.Map;
using Godot;

namespace ContingencyZero.Game.MapEditor;

/// <summary>
/// Draws the map being edited and keeps up with the brush. A <see cref="TerrainView"/> (the
/// game's own renderer) supplies the water, the border, the metal spot discs and the ground
/// material, so the editor looks like a match. The ground itself is drawn here in 16-tile chunks
/// that keep their vertex data, so a brush stroke only recomputes the vertices it touched and
/// re-uploads the few small chunks around them instead of the whole map.
/// </summary>
public partial class EditorTerrain : Node3D
{
    public const int Chunk = 16;
    private const float HeightScale = TerrainView.HeightScale;

    public MapData Map { get; private set; } = null!;
    /// <summary>Tints the ground by who can drive or build on it (the "where can units go" view).</summary>
    public bool ShowPassability { get; private set; }
    /// <summary>Milliseconds the last full rebuild and the last chunk update took (for the dev script's timings).</summary>
    public double LastBuildMs { get; private set; }
    public double LastUpdateMs { get; private set; }
    public int LastUpdateChunks { get; private set; }

    private TerrainView _view = null!;
    private Material? _groundMaterial;
    private Material? _fallbackMaterial;
    private readonly List<MeshInstance3D> _chunks = new();
    private int _chunksX, _chunksY;
    private int[] _fullIndices = Array.Empty<int>();

    /// <summary>One chunk's vertex arrays, kept so an update only recomputes the vertices that changed.</summary>
    private sealed class ChunkData
    {
        public required Vector3[] Verts;
        public required Vector3[] Normals;
        public required Color[] Colours;
        public required Vector2[] Uvs;
        public required int[] Indices;
        // Corner rectangle (map corners, inclusive) waiting to be recomputed; empty when x0 > x1.
        public int X0 = int.MaxValue, Y0 = int.MaxValue, X1 = int.MinValue, Y1 = int.MinValue;
    }
    private ChunkData?[] _data = Array.Empty<ChunkData?>();
    private readonly List<int> _pending = new();

    // The colour bands are scaled from the lowest ground (or the shoreline) to the highest.
    private float _baseline, _range, _water;
    private bool _hasWater;
    private TerrainPalette _pal = TerrainView.PaletteFor("temperate");

    public override void _Ready()
    {
        _view = new TerrainView { Name = "TerrainView" };
        AddChild(_view);
    }

    /// <summary>Rebuilds everything: the game's terrain view, then every ground chunk.</summary>
    public void Build(MapData map)
    {
        ulong t0 = Time.GetTicksUsec();
        Map = map;
        _pal = TerrainView.PaletteFor(map.Theme);
        _view.Build(map);

        // Borrow the game's ground material and hide its single big ground mesh: the chunks replace it.
        _groundMaterial = null;
        foreach (Node child in _view.GetChildren())
        {
            if (child.IsQueuedForDeletion() || child is not MeshInstance3D { Mesh: ArrayMesh } ground) continue;
            _groundMaterial = ground.MaterialOverride;
            ground.Visible = false;
            break;
        }
        _groundMaterial ??= _fallbackMaterial ??= new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.95f };

        ComputeScale(out _baseline, out _range, out _hasWater);
        _water = map.WaterLevel * HeightScale;

        _chunksX = (map.TilesX + Chunk - 1) / Chunk;
        _chunksY = (map.TilesY + Chunk - 1) / Chunk;
        int needed = _chunksX * _chunksY;
        while (_chunks.Count < needed)
        {
            var mi = new MeshInstance3D { CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(mi);
            _chunks.Add(mi);
        }
        while (_chunks.Count > needed)
        {
            _chunks[^1].QueueFree();
            _chunks.RemoveAt(_chunks.Count - 1);
        }
        _data = new ChunkData?[needed];
        RebuildAllChunks();
        LastBuildMs = (Time.GetTicksUsec() - t0) / 1000.0;
    }

    public void SetShowPassability(bool on)
    {
        if (ShowPassability == on) return;
        ShowPassability = on;
        if (Map is not null) RebuildAllChunks();
    }

    /// <summary>Updates the ground after the brush changed some corner rectangles (inclusive).</summary>
    public void UpdateRegions(List<(int X0, int Y0, int X1, int Y1)> rects)
    {
        ulong t0 = Time.GetTicksUsec();
        foreach (var (x0, y0, x1, y1) in rects)
            MarkDirty(x0 - 1, y0 - 1, x1 + 1, y1 + 1); // normals and the passability tint reach one corner further
        LastUpdateChunks = _pending.Count;
        foreach (int i in _pending) BuildChunk(i % _chunksX, i / _chunksX, partial: true);
        _pending.Clear();
        LastUpdateMs = (Time.GetTicksUsec() - t0) / 1000.0;
    }

    private void MarkDirty(int vx0, int vy0, int vx1, int vy1)
    {
        vx0 = Math.Max(0, vx0); vy0 = Math.Max(0, vy0);
        vx1 = Math.Min(Map.TilesX, vx1); vy1 = Math.Min(Map.TilesY, vy1);
        if (vx0 > vx1 || vy0 > vy1) return;
        // A corner on a chunk edge belongs to both chunks, so look one corner back.
        int cx0 = Math.Max(0, (vx0 - 1) / Chunk), cy0 = Math.Max(0, (vy0 - 1) / Chunk);
        int cx1 = Math.Min(_chunksX - 1, vx1 / Chunk), cy1 = Math.Min(_chunksY - 1, vy1 / Chunk);
        for (int cy = cy0; cy <= cy1; cy++)
        for (int cx = cx0; cx <= cx1; cx++)
        {
            int i = cy * _chunksX + cx;
            if (_data[i] is not { } d) continue;
            if (d.X0 > d.X1) _pending.Add(i);
            d.X0 = Math.Min(d.X0, vx0); d.Y0 = Math.Min(d.Y0, vy0);
            d.X1 = Math.Max(d.X1, vx1); d.Y1 = Math.Max(d.Y1, vy1);
        }
    }

    public void RebuildAllChunks()
    {
        for (int cy = 0; cy < _chunksY; cy++)
        for (int cx = 0; cx < _chunksX; cx++)
            BuildChunk(cx, cy, partial: false);
    }

    /// <summary>
    /// After a stroke: if the height range or the presence of water changed, the colour bands
    /// move (and a lake may need its water plane), so rebuild. Returns true if it did.
    /// </summary>
    public bool SettleAfterStroke()
    {
        ComputeScale(out float baseline, out float range, out bool hasWater);
        if (hasWater != _hasWater)
        {
            Build(Map); // the water plane only exists when something is under water
            return true;
        }
        if (MathF.Abs(baseline - _baseline) > 0.01f || MathF.Abs(range - _range) > 0.01f)
        {
            _baseline = baseline;
            _range = range;
            RebuildAllChunks();
        }
        RefreshMetalDiscs();
        return false;
    }

    /// <summary>Moves the game view's metal spot discs back onto the (re-sculpted) ground.</summary>
    /// <remarks>Also follows spots being added or removed, so placing one doesn't need a full rebuild. Returns false if the discs weren't found.</remarks>
    public bool RefreshMetalDiscs()
    {
        foreach (Node child in _view.GetChildren())
        {
            if (child.IsQueuedForDeletion() || child is not MultiMeshInstance3D { Multimesh: { } mm }) continue;
            if (mm.InstanceCount != Map.MetalSpots.Count) mm.InstanceCount = Map.MetalSpots.Count;
            for (int i = 0; i < mm.InstanceCount; i++)
            {
                Vector2 p = MapEdits.ToVec(Map.MetalSpots[i].Position);
                mm.SetInstanceTransform(i, new Transform3D(Basis.Identity, new Vector3(p.X, HeightAt(p.X, p.Y) + 0.08f, p.Y)));
            }
            return true;
        }
        return false;
    }

    private void ComputeScale(out float baseline, out float range, out bool hasWater)
    {
        int min = int.MaxValue, max = int.MinValue;
        foreach (int h in Map.Heights) { if (h < min) min = h; if (h > max) max = h; }
        float water = Map.WaterLevel * HeightScale;
        hasWater = min * HeightScale < water;
        baseline = hasWater ? water : min * HeightScale;
        range = MathF.Max(1, max * HeightScale - baseline);
    }

    // ---- Heights and picking ------------------------------------------------------------------

    /// <summary>Ground height in world units at a point, from the map as it is right now.</summary>
    public float HeightAt(float x, float z) => TerrainBrush.HeightStepsAt(Map, x, z) * HeightScale;

    public float WaterHeight => Map.WaterLevel * HeightScale;

    /// <summary>The ground or the water surface, whichever is higher.</summary>
    public float SurfaceAt(float x, float z) => MathF.Max(HeightAt(x, z), WaterHeight);

    /// <summary>Where a camera ray first meets the ground (not the water: the brush works on the ground under it).</summary>
    public Vector3? RayHit(Vector3 origin, Vector3 dir)
    {
        if (dir.Y >= -0.0001f) return null;
        float top = TerrainBrush.MaxHeight * HeightScale + 10f;
        float t = MathF.Max(0, (origin.Y - top) / -dir.Y);
        Vector3 p = origin + dir * t;
        const float step = 1.5f;
        float pad = 64f;
        for (int i = 0; i < 2000; i++)
        {
            bool inside = p.X >= -pad && p.Z >= -pad && p.X <= Map.Width + pad && p.Z <= Map.Height + pad;
            if (inside && p.Y <= HeightAt(p.X, p.Z))
            {
                Vector3 a = p - dir * step, b = p;
                for (int k = 0; k < 10; k++)
                {
                    Vector3 m = (a + b) / 2;
                    if (m.Y <= HeightAt(m.X, m.Z)) b = m; else a = m;
                }
                return new Vector3(b.X, HeightAt(b.X, b.Z), b.Z);
            }
            if (p.Y < -20) break;
            p += dir * step;
        }
        return null;
    }

    // ---- Chunk meshes -------------------------------------------------------------------------

    private static float Hash(int x, int y)
    {
        // Same noise as TerrainView, so the ground speckle matches a match.
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
    }

    private float H(int vx, int vy) => Map.CornerHeight(vx, vy) * HeightScale;

    /// <summary>Recomputes a chunk's vertices (all of them, or just its pending rectangle) and re-uploads it.</summary>
    private void BuildChunk(int cx, int cy, bool partial)
    {
        MapData map = Map;
        int x0 = cx * Chunk, y0 = cy * Chunk;
        int tw = Math.Min(Chunk, map.TilesX - x0), th = Math.Min(Chunk, map.TilesY - y0);
        int vw = tw + 1, vh = th + 1;
        int index = cy * _chunksX + cx;
        ChunkData? d = _data[index];
        if (d is null)
        {
            int count = vw * vh;
            int[] indices;
            if (tw == Chunk && th == Chunk)
            {
                if (_fullIndices.Length == 0) _fullIndices = Indices(Chunk, Chunk);
                indices = _fullIndices;
            }
            else indices = Indices(tw, th);
            d = _data[index] = new ChunkData
            {
                Verts = new Vector3[count], Normals = new Vector3[count], Colours = new Color[count], Uvs = new Vector2[count], Indices = indices,
            };
            partial = false;
        }

        // The part of the chunk to recompute, in chunk-local corners.
        int lx0 = 0, ly0 = 0, lx1 = vw - 1, ly1 = vh - 1;
        if (partial && d.X0 <= d.X1)
        {
            lx0 = Math.Max(0, d.X0 - x0); ly0 = Math.Max(0, d.Y0 - y0);
            lx1 = Math.Min(vw - 1, d.X1 - x0); ly1 = Math.Min(vh - 1, d.Y1 - y0);
        }
        d.X0 = d.Y0 = int.MaxValue;
        d.X1 = d.Y1 = int.MinValue;

        for (int y = ly0; y <= ly1; y++)
        for (int x = lx0; x <= lx1; x++)
        {
            int gx = x0 + x, gy = y0 + y;
            int i = y * vw + x;
            float h = H(gx, gy);
            d.Verts[i] = new Vector3(gx * MapData.TileSize, h, gy * MapData.TileSize);
            d.Uvs[i] = new Vector2((float)gx / map.TilesX, (float)gy / map.TilesY);
            var n = new Vector3(H(gx - 1, gy) - H(gx + 1, gy), 2 * MapData.TileSize, H(gx, gy - 1) - H(gx, gy + 1)).Normalized();
            d.Normals[i] = n;
            Color ground = GroundColour(gx, gy, h, n);
            d.Colours[i] = ShowPassability ? PassabilityColour(gx, gy, ground) : ground;
        }

        // Dispose the variants and the array at once: they hold native copies of the vertex data that
        // the GC can't see, and a stroke makes many of them (left to the finaliser, memory balloons).
        using Variant vVerts = Variant.CreateFrom(d.Verts), vNormals = Variant.CreateFrom(d.Normals),
            vColours = Variant.CreateFrom(d.Colours), vUvs = Variant.CreateFrom(d.Uvs), vIndices = Variant.CreateFrom(d.Indices);
        using var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vVerts;
        arrays[(int)Mesh.ArrayType.Normal] = vNormals;
        arrays[(int)Mesh.ArrayType.Color] = vColours;
        arrays[(int)Mesh.ArrayType.TexUV] = vUvs;
        arrays[(int)Mesh.ArrayType.Index] = vIndices;
        MeshInstance3D mi = _chunks[index];
        if (mi.Mesh is ArrayMesh existing)
        {
            existing.ClearSurfaces();
            existing.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        }
        else
        {
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mi.Mesh = mesh;
        }
        mi.MaterialOverride = _groundMaterial;
    }

    private static int[] Indices(int tw, int th)
    {
        int vw = tw + 1;
        var indices = new int[tw * th * 6];
        int k = 0;
        for (int y = 0; y < th; y++)
        for (int x = 0; x < tw; x++)
        {
            int a = y * vw + x, b = a + 1, c = a + vw, d = c + 1;
            indices[k++] = a; indices[k++] = b; indices[k++] = c;
            indices[k++] = b; indices[k++] = d; indices[k++] = c;
        }
        return indices;
    }

    /// <summary>The game's ground colouring (mirrors TerrainView.BuildGround), in linear light.</summary>
    private Color GroundColour(int x, int y, float h, Vector3 n)
    {
        TerrainPalette pal = _pal;
        float t = Mathf.Clamp((h - _baseline) / _range, 0, 1);
        Color c = _hasWater && t < 0.08f ? pal.Shore.Lerp(pal.Low, t / 0.08f)
            : !_hasWater && t < 0.08f ? pal.Low
            : t < 0.6f ? pal.Low.Lerp(pal.High, (t - 0.08f) / 0.52f)
            : pal.High.Lerp(pal.Peak, (t - 0.6f) / 0.4f);
        float steep = Mathf.Clamp((1 - n.Y) * 6f, 0, 1);
        c = c.Lerp(pal.Rock, steep);
        if (h < _water) c = c.Darkened(0.35f);
        float noise = Hash(x, y) * 0.12f - 0.06f + Hash(x / 3, y / 3) * 0.08f - 0.04f;
        return new Color(c.R + noise, c.G + noise, c.B + noise * 0.7f).SrgbToLinear();
    }

    // Colours for the passability view, by the worst tile touching the corner.
    public static readonly Color BuildColour = new(0.4f, 0.66f, 0.38f);
    public static readonly Color VehicleColour = new(0.8f, 0.74f, 0.34f);
    public static readonly Color BotColour = new(0.9f, 0.52f, 0.24f);
    public static readonly Color BlockedColour = new(0.8f, 0.2f, 0.18f);
    public static readonly Color ShallowColour = new(0.32f, 0.58f, 0.72f);
    public static readonly Color DeepColour = new(0.14f, 0.26f, 0.56f);

    private Color PassabilityColour(int vx, int vy, Color ground)
    {
        int worst = 0;
        for (int ty = vy - 1; ty <= vy; ty++)
        for (int tx = vx - 1; tx <= vx; tx++)
        {
            if (!Map.InBounds(tx, ty)) continue;
            worst = Math.Max(worst, Class(tx, ty));
        }
        Color c = worst switch
        {
            0 => BuildColour,
            1 => VehicleColour,
            2 => BotColour,
            3 => BlockedColour,
            4 => ShallowColour,
            _ => DeepColour,
        };
        // A strong tint over the real ground, so its shading and texture still read.
        return ground.Lerp(c.SrgbToLinear() * 0.62f, 0.7f);
    }

    /// <summary>0 buildable, 1 vehicles, 2 bots only, 3 nothing on land, 4 shallow water (wadeable), 5 deep water.</summary>
    public int Class(int tx, int ty)
    {
        int depth = Map.TileDepth(tx, ty);
        int slope = Map.TileSlope(tx, ty);
        if (depth > Terrain.MaxWadeDepth) return 5;
        if (depth > 0) return slope <= Terrain.VehicleMaxSlope ? 4 : 3;
        if (slope <= Terrain.StructureMaxSlope) return 0;
        if (slope <= Terrain.VehicleMaxSlope) return 1;
        if (slope <= Terrain.BotMaxSlope) return 2;
        return 3;
    }
}
