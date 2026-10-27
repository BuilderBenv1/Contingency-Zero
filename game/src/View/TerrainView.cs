using System;
using System.Collections.Generic;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Vision;
using Godot;

namespace ContingencyZero.Game.View;

/// <summary>Colours for a map theme.</summary>
public sealed record TerrainPalette(Color Shore, Color Low, Color High, Color Peak, Color Rock, Color Water, Color WaterDeep, bool LavaWater, Color Sky);

/// <summary>
/// Draws the battlefield: a lit, vertex-coloured heightmap mesh with fog of war in its shader,
/// a water (or lava) plane, metal spot markers and a dark border beyond the map edge.
/// </summary>
public partial class TerrainView : Node3D
{
    public const float HeightScale = 1f / MapData.HeightStepsPerUnit;

    public MapData Map { get; private set; } = null!;
    private float[] _heights = Array.Empty<float>();
    private ShaderMaterial _material = null!;
    private Image _fogImage = null!;
    private ImageTexture _fogTexture = null!;
    private int _team = -1;

    public static TerrainPalette PaletteFor(string theme) => theme switch
    {
        "desert" => new TerrainPalette(new Color(0.64f, 0.56f, 0.44f), new Color(0.56f, 0.47f, 0.37f), new Color(0.5f, 0.41f, 0.33f),
            new Color(0.6f, 0.53f, 0.44f), new Color(0.58f, 0.47f, 0.37f), new Color(0.2f, 0.42f, 0.5f, 0.75f), new Color(0.08f, 0.2f, 0.3f), false,
            new Color(0.62f, 0.5f, 0.4f)),
        "lava" => new TerrainPalette(new Color(0.3f, 0.27f, 0.25f), new Color(0.27f, 0.25f, 0.24f), new Color(0.33f, 0.3f, 0.28f),
            new Color(0.39f, 0.35f, 0.32f), new Color(0.24f, 0.22f, 0.21f), new Color(1.0f, 0.38f, 0.05f, 0.95f), new Color(0.9f, 0.15f, 0.02f), true,
            new Color(0.28f, 0.22f, 0.2f)),
        "arctic" => new TerrainPalette(new Color(0.58f, 0.59f, 0.59f), new Color(0.65f, 0.66f, 0.66f), new Color(0.69f, 0.7f, 0.7f),
            new Color(0.75f, 0.76f, 0.76f), new Color(0.42f, 0.43f, 0.44f), new Color(0.35f, 0.55f, 0.7f, 0.8f), new Color(0.12f, 0.25f, 0.4f), false,
            new Color(0.6f, 0.68f, 0.76f)),
        _ => new TerrainPalette(new Color(0.462f, 0.495f, 0.385f), new Color(0.363f, 0.462f, 0.308f), new Color(0.407f, 0.473f, 0.341f),
            new Color(0.484f, 0.528f, 0.407f), new Color(0.48f, 0.47f, 0.43f), new Color(0.2f, 0.32f, 0.36f, 0.82f), new Color(0.06f, 0.12f, 0.15f), false,
            new Color(0.56f, 0.59f, 0.58f)),
    };

    /// <summary>How heights map onto the palette's bands for one map.</summary>
    public readonly record struct ColourBands(float Baseline, float Range, float Water, bool HasWater)
    {
        public static ColourBands For(float minHeight, float maxHeight, float water)
        {
            bool hasWater = minHeight < water;
            // Bands run from the shoreline when there is water, otherwise from the lowest ground.
            float baseline = hasWater ? water : minHeight;
            // Gentle maps shouldn't stretch the whole palette over a few metres of relief.
            return new ColourBands(baseline, Mathf.Max(30, maxHeight - baseline), water, hasWater);
        }
    }

    /// <summary>
    /// The ground colour at a vertex: a narrow beach above the waterline, then low/high/peak bands
    /// by height, rock on steep slopes, darker underwater, plus a little per-vertex noise.
    /// Shared with the map editor so a map looks the same in both.
    /// </summary>
    public static Color GroundColour(TerrainPalette pal, ColourBands b, float h, Vector3 normal, int x, int y)
    {
        float t = Mathf.Clamp((h - b.Baseline) / b.Range, 0, 1);
        float above = h - b.Water;
        Color c = b.HasWater && above < 2.5f ? pal.Shore.Lerp(pal.Low, Mathf.Clamp(above / 2.5f, 0, 1))
            : t < 0.3f ? pal.Low
            : t < 0.8f ? pal.Low.Lerp(pal.High, (t - 0.3f) / 0.5f)
            : pal.High.Lerp(pal.Peak, (t - 0.8f) / 0.2f);
        float steep = Mathf.Clamp((0.92f - normal.Y) * 10f, 0, 1);
        c = c.Lerp(pal.Rock, steep);
        if (h < b.Water) c = c.Darkened(0.35f);
        float noise = Hash(x, y) * 0.12f - 0.06f + Hash(x / 3, y / 3) * 0.08f - 0.04f;
        return new Color(c.R + noise, c.G + noise, c.B + noise * 0.7f);
    }

    /// <summary>
    /// How much of each ground texture (shore, low ground, high ground, cliff rock) shows at a
    /// corner, summing to 1. They follow the same bands as <see cref="GroundColour"/>.
    /// </summary>
    public static (Vector2 A, Vector2 B) GroundWeights(ColourBands b, float h, Vector3 normal)
    {
        float t = Mathf.Clamp((h - b.Baseline) / b.Range, 0, 1);
        float shore = b.HasWater ? Mathf.Clamp(1 - (h - b.Water) / 2.5f, 0, 1) : 0;
        float high = Mathf.SmoothStep(0.3f, 0.8f, t);
        float low = (1 - high) * (1 - shore);
        high *= 1 - shore;
        float rock = Mathf.Clamp((0.92f - normal.Y) * 10f, 0, 1);
        float keep = 1 - rock;
        return (new Vector2(shore * keep, low * keep), new Vector2(high * keep, rock));
    }

    /// <summary>
    /// The battlefield's light for a theme, shared by matches and the map editor. The
    /// Compatibility renderer lights in display space, so a linear tonemap keeps colours as authored.
    /// </summary>
    public static void ConfigureLighting(Godot.Environment env, DirectionalLight3D sun, string theme)
    {
        TerrainPalette palette = PaletteFor(theme);
        env.BackgroundMode = Godot.Environment.BGMode.Color;
        env.BackgroundColor = palette.Sky.Darkened(0.6f);
        env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
        // Hard sun and a low, grey ambient: strong shadows, a harsher battlefield.
        env.AmbientLightColor = palette.Sky.Lerp(new Color(0.6f, 0.6f, 0.6f), 0.6f);
        env.AmbientLightEnergy = 0.4f;
        env.TonemapMode = Godot.Environment.ToneMapper.Linear;
        if (theme == "desert")
        {
            // Warm late-afternoon grade; the haze itself is a screen-space gradient in MatchScene
            // (Environment fog is unreliable in the Compatibility renderer). Under the retro
            // palette the warmth mottles into few colour steps, so keep the sun near-neutral.
            // The extra ambient keeps shade-side cliff faces readable.
            sun.LightColor = RetroLook.Enabled ? new Color(0.98f, 0.94f, 0.86f) : new Color(1f, 0.93f, 0.82f);
            sun.LightEnergy = 1.05f;
            env.AmbientLightEnergy = 0.5f;
        }
        else if (theme == "temperate")
        {
            // Soft, near-neutral daylight: green ground stays calm and shaded rock keeps its strata.
            sun.LightColor = new Color(0.98f, 0.98f, 0.94f);
            sun.LightEnergy = 0.95f;
            env.AmbientLightEnergy = 0.52f;
        }
        else if (theme == "arctic")
        {
            // Neutral sun keeps open snow white; the cooler ambient supplies blue only in shade.
            sun.LightColor = new Color(1f, 0.99f, 0.97f);
            sun.LightEnergy = 0.9f;
            env.AmbientLightEnergy = 0.58f;
        }
        else
        {
            sun.LightEnergy = theme == "lava" ? 0.85f : 1.0f;
            sun.LightColor = theme == "lava" ? new Color(1f, 0.8f, 0.66f) : new Color(1f, 0.95f, 0.86f);
            if (theme == "lava") env.AmbientLightEnergy = 0.52f;
        }
    }

    public void Build(MapData map)
    {
        Map = map;
        foreach (Node c in GetChildren()) c.QueueFree();

        int vx = map.TilesX + 1, vy = map.TilesY + 1;
        _heights = new float[vx * vy];
        _simRevision = 0;
        for (int i = 0; i < _heights.Length; i++) _heights[i] = map.Heights[i] * HeightScale;
        _original = (float[])_heights.Clone();

        TerrainPalette pal = PaletteFor(map.Theme);
        _pal = pal;
        _scorch = new float[vx * vy];
        AddChild(BuildGround(map, pal));
        AddChild(BuildWater(map, pal));
        AddChild(BuildBorder(map, pal));
        AddChild(BuildMetalSpots(map));
    }

    /// <summary>Ground height in world units at a point, bilinear between corners.</summary>
    public float HeightAt(float x, float z)
    {
        if (_heights.Length == 0) return 0;
        float fx = Mathf.Clamp(x / MapData.TileSize, 0, Map.TilesX - 0.001f);
        float fz = Mathf.Clamp(z / MapData.TileSize, 0, Map.TilesY - 0.001f);
        int tx = (int)fx, tz = (int)fz;
        float ux = fx - tx, uz = fz - tz;
        int w = Map.TilesX + 1;
        float h00 = _heights[tz * w + tx], h10 = _heights[tz * w + tx + 1];
        float h01 = _heights[(tz + 1) * w + tx], h11 = _heights[(tz + 1) * w + tx + 1];
        return Mathf.Lerp(Mathf.Lerp(h00, h10, ux), Mathf.Lerp(h01, h11, ux), uz);
    }

    /// <summary>Height to stand things on: the ground, or the water surface where it's flooded.</summary>
    public float SurfaceAt(float x, float z) => Mathf.Max(HeightAt(x, z), WaterHeight);

    public float WaterHeight => Map.WaterLevel * HeightScale;

    public Vector3 NormalAt(float x, float z)
    {
        const float e = 2f;
        float hl = HeightAt(x - e, z), hr = HeightAt(x + e, z);
        float hd = HeightAt(x, z - e), hu = HeightAt(x, z + e);
        return new Vector3(hl - hr, 2 * e, hd - hu).Normalized();
    }

    /// <summary>Where a screen ray first hits the ground (marching along the ray against the heightmap).</summary>
    public Vector3? RayHit(Vector3 origin, Vector3 dir)
    {
        if (dir.Y >= -0.0001f) return null;
        // Start where the ray crosses a ceiling above the highest terrain.
        float top = 120f;
        float t = (origin.Y - top) / -dir.Y;
        if (t < 0) t = 0;
        Vector3 p = origin + dir * t;
        float step = 2f;
        for (int i = 0; i < 600; i++)
        {
            float ground = SurfaceAt(p.X, p.Z);
            if (p.Y <= ground)
            {
                // Refine between the last two samples.
                Vector3 a = p - dir * step, b = p;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 m = (a + b) / 2;
                    if (m.Y <= SurfaceAt(m.X, m.Z)) b = m; else a = m;
                }
                return new Vector3(b.X, SurfaceAt(b.X, b.Z), b.Z);
            }
            p += dir * step;
        }
        // Missed: fall back to the water/ground plane.
        float tp = (origin.Y - WaterHeight) / -dir.Y;
        return origin + dir * tp;
    }

    private static float Hash(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
    }

    public const int Chunk = 16;
    private Node3D _ground = null!;
    private readonly List<MeshInstance3D> _chunks = new();
    private int _chunksX, _chunksY;
    private Vector3[] _normals = Array.Empty<Vector3>();
    private Color[] _colours = Array.Empty<Color>();
    private Vector2[] _weightsA = Array.Empty<Vector2>(), _weightsB = Array.Empty<Vector2>();
    private float[] _scorch = Array.Empty<float>();
    /// <summary>The ground as the match began, so craters can be coloured by how deep they were dug.</summary>
    private float[] _original = Array.Empty<float>();
    private ColourBands _bands;
    private TerrainPalette _pal = PaletteFor("temperate");
    private readonly HashSet<int> _dirtyChunks = new();
    private readonly List<int> _rebuildNow = new();
    /// <summary>Most ground chunks rebuilt in one frame; a big bombing run spreads over a few frames.</summary>
    public const int MaxChunkRebuildsPerFrame = 6;

    /// <summary>Goes up every time the ground's shape changes, so things standing on it know to re-seat.</summary>
    public int Revision { get; private set; }

    /// <summary>The ground material (shared by the map editor's own chunks).</summary>
    public Material? GroundMaterial => _material;

    /// <summary>Shows or hides the ground (the map editor draws its own while painting).</summary>
    public bool GroundVisible
    {
        get => _ground?.Visible ?? false;
        set { if (_ground is not null) _ground.Visible = value; }
    }

    /// <summary>
    /// The ground, drawn in 16-tile chunks so a crater or a scorch mark only rebuilds the few
    /// chunks it touches. Vertex arrays are kept so a refresh recomputes just the changed corners.
    /// </summary>
    private Node3D BuildGround(MapData map, TerrainPalette pal)
    {
        int vx = map.TilesX + 1, vy = map.TilesY + 1;
        float water = map.WaterLevel * HeightScale;
        float min = float.MaxValue, max = float.MinValue;
        foreach (float h in _heights) { min = Mathf.Min(min, h); max = Mathf.Max(max, h); }
        _bands = ColourBands.For(min, max, water);
        _normals = new Vector3[vx * vy];
        _colours = new Color[vx * vy];
        _weightsA = new Vector2[vx * vy];
        _weightsB = new Vector2[vx * vy];
        RecomputeCorners(0, 0, map.TilesX, map.TilesY);

        _fogImage = Image.CreateEmpty(map.TilesX, map.TilesY, false, Image.Format.Rg8);
        _fogImage.Fill(new Color(1, 1, 0));
        _fogTexture = ImageTexture.CreateFromImage(_fogImage);

        _material = new ShaderMaterial { Shader = new Shader { Code = GroundShader } };
        _material.SetShaderParameter("fog_tex", _fogTexture);
        _material.SetShaderParameter("map_size", new Vector2(map.Width, map.Height));
        _material.SetShaderParameter("retro", RetroLook.Enabled);
        _material.SetShaderParameter("water_level", water);
        _material.SetShaderParameter("lava_distance_tex", LavaDistanceTexture(map));
        ApplyGroundTextures(_material, map.Theme);

        _ground = new Node3D { Name = "Ground" };
        _chunks.Clear();
        _chunksX = (map.TilesX + Chunk - 1) / Chunk;
        _chunksY = (map.TilesY + Chunk - 1) / Chunk;
        for (int cy = 0; cy < _chunksY; cy++)
        for (int cx = 0; cx < _chunksX; cx++)
        {
            var mi = new MeshInstance3D { MaterialOverride = _material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _ground.AddChild(mi);
            _chunks.Add(mi);
            BuildChunk(cx, cy);
        }
        return _ground;
    }

    /// <summary>Horizontal distance to the lava, including on sheer banks where height is useless.</summary>
    private static ImageTexture LavaDistanceTexture(MapData map)
    {
        int vx = map.TilesX + 1, vy = map.TilesY + 1;
        var distance = new float[vx * vy];
        const float far = 1e6f;
        for (int i = 0; i < distance.Length; i++)
            distance[i] = map.Theme == "lava" && map.Heights[i] < map.WaterLevel ? 0f : far;
        if (map.Theme == "lava")
        {
            const float diagonal = 1.41421356f;
            for (int y = 0; y < vy; y++)
            for (int x = 0; x < vx; x++)
            {
                int i = y * vx + x;
                float d = distance[i];
                if (x > 0) d = Mathf.Min(d, distance[i - 1] + 1f);
                if (y > 0) d = Mathf.Min(d, distance[i - vx] + 1f);
                if (x > 0 && y > 0) d = Mathf.Min(d, distance[i - vx - 1] + diagonal);
                if (x + 1 < vx && y > 0) d = Mathf.Min(d, distance[i - vx + 1] + diagonal);
                distance[i] = d;
            }
            for (int y = vy - 1; y >= 0; y--)
            for (int x = vx - 1; x >= 0; x--)
            {
                int i = y * vx + x;
                float d = distance[i];
                if (x + 1 < vx) d = Mathf.Min(d, distance[i + 1] + 1f);
                if (y + 1 < vy) d = Mathf.Min(d, distance[i + vx] + 1f);
                if (x + 1 < vx && y + 1 < vy) d = Mathf.Min(d, distance[i + vx + 1] + diagonal);
                if (x > 0 && y + 1 < vy) d = Mathf.Min(d, distance[i + vx - 1] + diagonal);
                distance[i] = d;
            }
        }
        var bytes = new byte[distance.Length];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)Mathf.RoundToInt(Mathf.Clamp(distance[i] * MapData.TileSize / 12f, 0f, 1f) * 255f);
        var image = Image.CreateEmpty(vx, vy, false, Image.Format.R8);
        image.SetData(vx, vy, false, Image.Format.R8, bytes);
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>Ground texture weights at a corner: (shore, low ground), (high ground, cliff rock).</summary>
    public (Vector2 A, Vector2 B) WeightsAt(int cx, int cy)
    {
        int vx = Map.TilesX + 1;
        int i = Math.Clamp(cy, 0, Map.TilesY) * vx + Math.Clamp(cx, 0, Map.TilesX);
        return i < _weightsA.Length ? (_weightsA[i], _weightsB[i]) : (Vector2.Zero, Vector2.Zero);
    }

    /// <summary>The fog-of-war texture (R = in sight, G = explored), for other ground-level shaders.</summary>
    public Texture2D FogTexture => _fogTexture;

    /// <summary>Recomputes normals and colours for corners in an inclusive corner rectangle.</summary>
    private void RecomputeCorners(int x0, int y0, int x1, int y1)
    {
        int vx = Map.TilesX + 1, vy = Map.TilesY + 1;
        x0 = Math.Max(0, x0); y0 = Math.Max(0, y0); x1 = Math.Min(vx - 1, x1); y1 = Math.Min(vy - 1, y1);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            int i = y * vx + x;
            float h = _heights[i];
            float hl = _heights[y * vx + Math.Max(x - 1, 0)], hr = _heights[y * vx + Math.Min(x + 1, vx - 1)];
            float hd = _heights[Math.Max(y - 1, 0) * vx + x], hu = _heights[Math.Min(y + 1, vy - 1) * vx + x];
            var n = new Vector3(hl - hr, 2 * MapData.TileSize, hd - hu).Normalized();
            _normals[i] = n;
            Color c = GroundColour(_pal, _bands, h, n, x, y);
            float baseLum = Math.Max(0.02f, (c.R + c.G + c.B) / 3f);
            float dug = _original.Length > i ? _original[i] - h : 0;
            if (dug > 0.2f)
            {
                // Blasted earth: churned, dark brown soil in the bowl, deeper is darker.
                c = c.Lerp(new Color(0.2f, 0.15f, 0.11f), Mathf.Clamp(dug / 5f, 0.15f, 0.75f));
            }
            else if (dug < -0.2f)
            {
                // Thrown-up rim: loose, lighter dirt.
                c = c.Lerp(new Color(0.45f, 0.38f, 0.29f), Mathf.Clamp(-dug / 2f, 0.1f, 0.45f));
            }
            float sc = _scorch.Length > i ? _scorch[i] : 0;
            if (sc > 0)
            {
                // Burnt ground: darker, a little browner, and duller.
                c = c.Lerp(new Color(0.09f, 0.08f, 0.07f), Mathf.Min(0.8f, sc * 0.85f));
            }
            // Alpha: how much of its brightness damage left (the textured ground darkens by it).
            c.A = Mathf.Clamp((c.R + c.G + c.B) / 3f / baseLum, 0.05f, 1f);
            _colours[i] = c;
            (_weightsA[i], _weightsB[i]) = GroundWeights(_bands, h, n);
        }
    }

    private void BuildChunk(int cx, int cy)
    {
        int vx = Map.TilesX + 1;
        int tx0 = cx * Chunk, ty0 = cy * Chunk;
        int tw = Math.Min(Chunk, Map.TilesX - tx0), th = Math.Min(Chunk, Map.TilesY - ty0);
        int cw = tw + 1, ch = th + 1;
        var verts = new Vector3[cw * ch];
        var normals = new Vector3[cw * ch];
        var colours = new Color[cw * ch];
        var uvs = new Vector2[cw * ch];
        var uv2s = new Vector2[cw * ch];
        for (int y = 0; y < ch; y++)
        for (int x = 0; x < cw; x++)
        {
            int gx = tx0 + x, gy = ty0 + y;
            int gi = gy * vx + gx, li = y * cw + x;
            verts[li] = new Vector3(gx * MapData.TileSize, _heights[gi], gy * MapData.TileSize);
            normals[li] = _normals[gi];
            colours[li] = _colours[gi];
            uvs[li] = _weightsA[gi];
            uv2s[li] = _weightsB[gi];
        }
        var indices = new int[tw * th * 6];
        int k = 0;
        for (int y = 0; y < th; y++)
        for (int x = 0; x < tw; x++)
        {
            int a = y * cw + x, b = a + 1, c = a + cw, d = c + 1;
            indices[k++] = a; indices[k++] = b; indices[k++] = c;
            indices[k++] = b; indices[k++] = d; indices[k++] = c;
        }
        // Dispose the native copies at once: a stroke or a bombing run makes many of them.
        using Variant vVerts = Variant.CreateFrom(verts), vNormals = Variant.CreateFrom(normals),
            vColours = Variant.CreateFrom(colours), vUvs = Variant.CreateFrom(uvs), vUv2s = Variant.CreateFrom(uv2s), vIndices = Variant.CreateFrom(indices);
        using var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vVerts;
        arrays[(int)Mesh.ArrayType.Normal] = vNormals;
        arrays[(int)Mesh.ArrayType.Color] = vColours;
        arrays[(int)Mesh.ArrayType.TexUV] = vUvs;
        arrays[(int)Mesh.ArrayType.TexUV2] = vUv2s;
        arrays[(int)Mesh.ArrayType.Index] = vIndices;
        MeshInstance3D mi = _chunks[cy * _chunksX + cx];
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
    }

    /// <summary>
    /// The ground changed in a tile rectangle (inclusive): re-read heights from the map, recompute
    /// the corners around it and mark the chunks it touches for a rebuild on the next frame.
    /// </summary>
    public void RefreshRegion(int tx0, int ty0, int tx1, int ty1)
    {
        if (_heights.Length == 0) return;
        int vx = Map.TilesX + 1;
        int x0 = Math.Max(0, tx0 - 1), y0 = Math.Max(0, ty0 - 1);
        int x1 = Math.Min(Map.TilesX, tx1 + 2), y1 = Math.Min(Map.TilesY, ty1 + 2);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            _heights[y * vx + x] = Map.Heights[y * vx + x] * HeightScale;
        RecomputeCorners(x0, y0, x1, y1);
        MarkChunks(x0, y0, x1, y1);
        Revision++;
    }

    private int _simRevision;
    private readonly List<(int Tx0, int Ty0, int Tx1, int Ty1)> _changes = new();

    /// <summary>
    /// Catches up with craters the sim has made since the last call (it keeps a log, so ticks that
    /// were never drawn, such as a replay seek or a fast-forward, still reach the mesh). Returns the
    /// changed tile rectangles, or null if nothing changed. Cheap when nothing did.
    /// </summary>
    public IReadOnlyList<(int Tx0, int Ty0, int Tx1, int Ty1)>? SyncWith(ContingencyZero.Sim.World world)
    {
        if (_heights.Length == 0 || world.TerrainRevision == _simRevision) return null;
        if (!world.TerrainChangesSince(_simRevision, _changes))
        {
            _changes.Clear();
            _changes.Add((0, 0, Map.TilesX - 1, Map.TilesY - 1));
        }
        _simRevision = world.TerrainRevision;
        foreach (var (tx0, ty0, tx1, ty1) in _changes) RefreshRegion(tx0, ty0, tx1, ty1);
        return _changes;
    }

    /// <summary>Burns the ground: darkens corners within <paramref name="radius"/> of a point, for good.</summary>
    public void Scorch(Vector3 centre, float radius, float strength = 1f)
    {
        if (_scorch.Length == 0) return;
        int vx = Map.TilesX + 1;
        int x0 = Math.Max(0, (int)((centre.X - radius) / MapData.TileSize)), x1 = Math.Min(Map.TilesX, (int)((centre.X + radius) / MapData.TileSize) + 1);
        int y0 = Math.Max(0, (int)((centre.Z - radius) / MapData.TileSize)), y1 = Math.Min(Map.TilesY, (int)((centre.Z + radius) / MapData.TileSize) + 1);
        bool any = false;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float dx = x * MapData.TileSize - centre.X, dz = y * MapData.TileSize - centre.Z;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d >= radius) continue;
            float add = strength * (1 - d / radius);
            int i = y * vx + x;
            float before = _scorch[i];
            _scorch[i] = Mathf.Min(1.5f, before + add);
            if (_scorch[i] != before) any = true;
        }
        if (!any) return;
        RecomputeCorners(x0, y0, x1, y1);
        MarkChunks(x0, y0, x1, y1);
    }

    private void MarkChunks(int cx0, int cy0, int cx1, int cy1)
    {
        // Corner rectangle to chunk rectangle (a corner on a chunk edge belongs to both chunks).
        int c0 = Math.Max(0, (cx0 - 1) / Chunk), c1 = Math.Min(_chunksX - 1, cx1 / Chunk);
        int r0 = Math.Max(0, (cy0 - 1) / Chunk), r1 = Math.Min(_chunksY - 1, cy1 / Chunk);
        for (int r = r0; r <= r1; r++)
        for (int c = c0; c <= c1; c++)
            _dirtyChunks.Add(r * _chunksX + c);
    }

    public override void _Process(double delta)
    {
        if (_dirtyChunks.Count == 0) return;
        // Many hits in one frame share a rebuild per chunk; past the cap the rest wait a frame.
        _rebuildNow.Clear();
        foreach (int i in _dirtyChunks)
        {
            _rebuildNow.Add(i);
            if (_rebuildNow.Count >= MaxChunkRebuildsPerFrame) break;
        }
        foreach (int i in _rebuildNow)
        {
            _dirtyChunks.Remove(i);
            BuildChunk(i % _chunksX, i / _chunksX);
        }
    }

    /// <summary>Copies a team's fog grids into the ground shader. Pass -1 to clear the fog (spectating).</summary>
    public void UpdateFog(VisionSystem vision, int team)
    {
        if (_fogImage is null) return;
        if (team < 0)
        {
            if (_team != -2) { _fogImage.Fill(new Color(1, 1, 0)); _fogTexture.Update(_fogImage); _team = -2; }
            return;
        }
        _team = team;
        bool[] vis = vision.VisibleGrid(team);
        bool[] exp = vision.ExploredGrid(team);
        int w = vision.TilesX, h = vision.TilesY;
        byte[] data = new byte[w * h * 2];
        for (int i = 0; i < w * h; i++)
        {
            data[i * 2] = vis[i] || vision.RevealAll ? (byte)255 : (byte)0;
            data[i * 2 + 1] = exp[i] || vision.RevealAll ? (byte)255 : (byte)0;
        }
        _fogImage.SetData(w, h, false, Image.Format.Rg8, data);
        _fogTexture.Update(_fogImage);
    }

    private const string GroundShader = @"
shader_type spatial;
render_mode diffuse_lambert, specular_schlick_ggx;
uniform sampler2D fog_tex : filter_linear;
uniform vec2 map_size;
uniform bool retro = false;
uniform bool textured = false;
uniform sampler2D tex_shore : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D tex_low : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D tex_patch : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D tex_high : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D tex_rock : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float patch_amount = 1.0;
uniform sampler2D nrm_shore : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D nrm_low : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D nrm_patch : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D nrm_high : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D nrm_rock : hint_normal, filter_linear_mipmap, repeat_enable;
uniform vec3 avg_shore = vec3(0.5);
uniform vec3 avg_low = vec3(0.5);
uniform vec3 avg_patch = vec3(0.5);
uniform vec3 avg_high = vec3(0.5);
uniform vec3 avg_rock = vec3(0.5);
uniform vec3 cliff_band = vec3(0.0);
uniform float cliff_grey = 0.0;
uniform float cliff_snow = 0.0;
uniform float lava_heat = 0.0;
uniform float water_level = 0.0;
uniform sampler2D lava_distance_tex : filter_linear, repeat_disable;
uniform float tile = 9.0;
uniform float tint = 0.45;
uniform float bump = 1.0;
uniform sampler2D marks_tex : filter_linear;
uniform bool marks = false;
varying vec3 world_pos;
varying vec4 wts;
varying vec3 wnorm;
void vertex() {
    world_pos = VERTEX;
    wts = vec4(UV, UV2);
    wnorm = NORMAL;
    // Ground textures are laid flat along world X and Z, so that's the tangent frame.
    TANGENT = vec3(1.0, 0.0, 0.0);
    BINORMAL = vec3(0.0, 0.0, 1.0);
}
float h2(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float n2(vec2 x) {
    vec2 i = floor(x); vec2 f = fract(x); f = f * f * (3.0 - 2.0 * f);
    return mix(mix(h2(i), h2(i + vec2(1.0, 0.0)), f.x), mix(h2(i + vec2(0.0, 1.0)), h2(i + vec2(1.0, 1.0)), f.x), f.y);
}
// A thin, wandering level-set rather than a painted orange vein network.
float crack(vec2 p) {
    float v = n2(p) * 0.68 + n2(p * 2.07 + 13.0) * 0.32;
    return 1.0 - smoothstep(0.003, 0.01, abs(v - 0.5));
}
// A texture sampled at two unrelated scales and rotations, blended by slow noise, so the
// repeat never lines up into a visible grid.
const mat2 ROT = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8));
vec3 col2(sampler2D t, vec2 p, float k) { return mix(texture(t, p).rgb, texture(t, ROT * p * 0.61 + vec2(0.37, 0.71)).rgb, k); }
vec3 nrm2(sampler2D t, vec2 p, float k) {
    vec3 a = texture(t, p).rgb, b = texture(t, ROT * p * 0.61 + vec2(0.37, 0.71)).rgb;
    // Rotate the second sample's normal back by the same rotation it was sampled with.
    vec2 bxy = transpose(ROT) * (b.xy * 2.0 - 1.0) * 0.5 + 0.5;
    return mix(a, vec3(bxy, b.z), k);
}
vec3 tri_weights(vec3 n) {
    vec3 b = pow(abs(n), vec3(5.0));
    return b / max(dot(b, vec3(1.0)), 0.0001);
}
float tri_noise(vec3 p, vec3 b, float scale) {
    return n2(p.zy / scale) * b.x + n2(p.xz / scale) * b.y + n2(p.xy / scale) * b.z;
}
// Every layer gets a real side projection on steep faces. A one-tile wall must not interpolate
// flat-ground corner weights and stretch a single row of sand down its face.
vec3 tri_col(sampler2D t, vec3 p, vec3 b, float scale, float k) {
    return col2(t, p.zy / scale, k) * b.x + col2(t, p.xz / scale, k) * b.y + col2(t, p.xy / scale, k) * b.z;
}
vec3 tri_nrm(sampler2D t, vec3 p, vec3 b, float scale, float k) {
    vec3 x = nrm2(t, p.zy / scale, k) * 2.0 - 1.0;
    vec3 y = nrm2(t, p.xz / scale, k) * 2.0 - 1.0;
    vec3 z = nrm2(t, p.xy / scale, k) * 2.0 - 1.0;
    return normalize(vec3(x.z, x.y, x.x) * b.x + vec3(y.x, y.z, y.y) * b.y + z * b.z);
}
void fragment() {
    vec2 uv = world_pos.xz / map_size;
    // Five taps soften the tile-shaped edge of what's in sight.
    vec2 px = vec2(6.0) / map_size;
    vec2 fog = texture(fog_tex, uv).rg * 0.4
        + (texture(fog_tex, uv + vec2(px.x, 0.0)).rg + texture(fog_tex, uv - vec2(px.x, 0.0)).rg
         + texture(fog_tex, uv + vec2(0.0, px.y)).rg + texture(fog_tex, uv - vec2(0.0, px.y)).rg) * 0.15;
    float light = mix(mix(0.16, 0.5, fog.g), 1.0, fog.r);
    vec2 w = world_pos.xz;
    vec3 c;
    float cliff = 0.0;
    vec3 lava_emit = vec3(0.0);
    vec3 geometric = normalize(cross(dFdx(world_pos), dFdy(world_pos)));
    geometric *= sign(geometric.y + 0.00001);
    if (textured) {
        // Corner weights supply height bands only; geometric steepness is decided per fragment.
        float mixk = smoothstep(0.35, 0.65, n2(w * 0.021 + 3.0));
        vec2 p = w / tile;
        vec3 anrm = abs(geometric);
        // Projection axes must vary continuously across shared triangle edges. The face normal
        // still decides where rock is exposed, but must not select a different texture projection
        // for each half of a heightmap quad.
        vec3 tw = tri_weights(normalize(wnorm));
        // Use the same world-space projection everywhere. On level triangles the Y weight is
        // one, so this is identical to flat X/Z sampling; on slopes it avoids a per-triangle
        // projection switch and its rectangular lighting and texture seams.
        float side = 1.0;
        cliff = 1.0 - smoothstep(0.76, 0.94, anrm.y);
        // Arctic material changes must be continuous across triangle edges. Its shared corner
        // normals still turn steep on the wall, but do not leave a rectangular face-normal cap.
        float arctic_cliff = 1.0 - smoothstep(0.58, 0.94, normalize(wnorm).y);
        cliff = mix(cliff, arctic_cliff, cliff_snow);
        float rock_tile = tile * 1.3;
        vec3 cs = mix(col2(tex_shore, p, mixk), tri_col(tex_shore, world_pos, tw, tile, mixk), side);
        vec3 cl = mix(col2(tex_low, p, mixk), tri_col(tex_low, world_pos, tw, tile, mixk), side);
        vec3 cp = mix(col2(tex_patch, p, mixk), tri_col(tex_patch, world_pos, tw, tile, mixk), side);
        vec3 ch = mix(col2(tex_high, p, mixk), tri_col(tex_high, world_pos, tw, tile, mixk), side);
        vec3 cr = mix(col2(tex_rock, p, mixk), tri_col(tex_rock, world_pos, tw, rock_tile, mixk), side);
        float rock_luma = dot(cr, vec3(0.299, 0.587, 0.114));
        cr = mix(cr, vec3(rock_luma), cliff_grey * 0.92);
        float patchy = smoothstep(0.52, 0.78, n2(w * 0.07 + 17.0) * 0.55 + n2(w * 0.23 + 5.0) * 0.3 + n2(w * 0.9 + 2.0) * 0.15) * patch_amount;
        vec4 k = wts / max(dot(wts, vec4(1.0)), 0.001);
        k.xyz *= 1.0 - cliff;
        k.w = cliff;
        float wl = k.y * (1.0 - patchy), wp = k.y * patchy;
        float hs = dot(cs, vec3(0.333)) / dot(avg_shore, vec3(0.333)), hl = dot(cl, vec3(0.333)) / dot(avg_low, vec3(0.333));
        float hp = dot(cp, vec3(0.333)) / dot(avg_patch, vec3(0.333)), hh = dot(ch, vec3(0.333)) / dot(avg_high, vec3(0.333));
        float hr = dot(cr, vec3(0.333)) / dot(avg_rock, vec3(0.333));
        vec4 a1 = vec4(k.x, wl, wp, k.z) * 1.6 + vec4(hs, hl, hp, hh) * 0.4;
        float a2 = k.w * 1.6 + hr * 0.4;
        float top = max(max(max(a1.x, a1.y), max(a1.z, a1.w)), a2) - 0.35;
        vec4 b1 = max(a1 - top, 0.0) * step(0.001, vec4(k.x, wl, wp, k.z));
        float b2 = max(a2 - top, 0.0) * step(0.001, k.w);
        float sum = max(dot(b1, vec4(1.0)) + b2, 0.0001);
        b1 /= sum; b2 /= sum;
        vec3 tex = cs * b1.x + cl * b1.y + cp * b1.z + ch * b1.w + cr * b2;
        vec3 av = avg_shore * b1.x + avg_low * b1.y + avg_patch * b1.z + avg_high * b1.w + avg_rock * b2;
        vec3 ns = mix(nrm2(nrm_shore, p, mixk), tri_nrm(nrm_shore, world_pos, tw, tile, mixk) * 0.5 + 0.5, side);
        vec3 nl = mix(nrm2(nrm_low, p, mixk), tri_nrm(nrm_low, world_pos, tw, tile, mixk) * 0.5 + 0.5, side);
        vec3 np = mix(nrm2(nrm_patch, p, mixk), tri_nrm(nrm_patch, world_pos, tw, tile, mixk) * 0.5 + 0.5, side);
        vec3 nh = mix(nrm2(nrm_high, p, mixk), tri_nrm(nrm_high, world_pos, tw, tile, mixk) * 0.5 + 0.5, side);
        vec3 nr = mix(nrm2(nrm_rock, p, mixk), tri_nrm(nrm_rock, world_pos, tw, rock_tile, mixk) * 0.5 + 0.5, side);
        vec3 nt = ns * b1.x + nl * b1.y + np * b1.z + nh * b1.w + nr * b2;
        // Convert the mixed world normal back to the declared X/Z tangent frame.
        vec3 nw = nt * 2.0 - 1.0;
        vec3 n = normalize(vec3(nw.x, nw.z, nw.y)) * 0.5 + 0.5;
        // Cliffs ignore interpolated ground tint. Temperate supplies a neutral fieldstone
        // band; desert keeps its lighter sandstone source average.
        vec3 band = COLOR.rgb / max(COLOR.a, 0.05);
        vec3 rock_band = cliff_band.r > 0.001 ? cliff_band : avg_rock * 1.18;
        band = mix(band, rock_band, cliff);
        c = tex * mix(vec3(1.0), band / max(av, vec3(0.05)), tint);
        c *= COLOR.a;
        c *= 0.9 + 0.2 * n2(w * 0.03 + 11.0);
        float strata = sin(world_pos.y * 2.1 + n2(w * 0.055) * 2.4) * 0.5 + 0.5;
        c *= 1.0 + cliff * (0.16 + strata * 0.16);
        // Open arctic snow is white-to-pale-grey, not a tinted rock texture. Preserve broad
        // height-band variation but remove source chroma and most high-frequency gravel grain.
        float open_snow = cliff_snow * (1.0 - cliff);
        float snow_luma = dot(c, vec3(0.299, 0.587, 0.114));
        vec3 calm_snow = vec3(snow_luma) * (0.96 + 0.07 * n2(w * 0.035 + 8.0));
        c = mix(c, calm_snow, open_snow * 0.8);
        // Temperate moss is a restrained detail in dark seams and on upward-facing ledges,
        // never a green tint over the whole rock face.
        float moss_pockets = smoothstep(0.58, 0.82, n2(w * 0.11 + world_pos.y * 0.07));
        float moss_sites = max((1.0 - strata) * 0.55, smoothstep(0.12, 0.5, geometric.y));
        float moss = cliff_grey * cliff * moss_pockets * moss_sites * 0.11;
        c = mix(c, c * vec3(0.82, 0.94, 0.76), moss);
        // Smooth corner normals turn up at a crest while the face normal stays steep.
        // Arctic uses actual snow geometry below rather than an additive white crest glow.
        float crest = smoothstep(0.5, 0.95, normalize(wnorm).y) * cliff * (1.0 - cliff_snow);
        vec3 crest_colour = mix(vec3(0.12, 0.095, 0.06), vec3(0.065, 0.064, 0.058), cliff_grey);
        c += crest_colour * crest;
        // Crisp snow sits only on upward-facing ledges, never on sheer rock or in a broad halo.
        float snow_ledge = smoothstep(0.55, 0.85, normalize(wnorm).y) * cliff * cliff_snow;
        vec3 snow_cap = vec3(0.64, 0.65, 0.65) * (0.97 + 0.05 * n2(w * 0.18 + 4.0));
        c = mix(c, snow_cap, snow_ledge * 0.82);
        if (lava_heat > 0.5) {
            // Volcanic ash is darker than desert soil; exposed rock is charcoal basalt with a
            // faint red-brown cast, not the pale tan source average used by ordinary cliffs.
            c *= mix(0.85, 1.0, cliff);
            float ash_luma = dot(c, vec3(0.299, 0.587, 0.114));
            c = mix(c, vec3(ash_luma) * vec3(0.9, 0.87, 0.85), (1.0 - cliff) * 0.32);
            float basalt_luma = dot(c, vec3(0.299, 0.587, 0.114));
            c = mix(c, vec3(basalt_luma) * vec3(0.34, 0.33, 0.32), cliff * 0.9);
            // Long vertical joints and shorter bedding fractures give the face a columnar rhythm.
            float joints = crack(vec2((w.x + w.y * 0.63) * 0.18, world_pos.y * 0.025));
            float bedding = crack(vec2((w.x - w.y) * 0.035, world_pos.y * 0.42));
            c *= 1.0 - cliff * (joints * 0.5 + bedding * 0.24);
            float above = max(world_pos.y - water_level, 0.0);
            float bank_distance = texture(lava_distance_tex, uv).r * 12.0;
            bank_distance += (tri_noise(world_pos + vec3(19.0, 0.0, 19.0), tw, 3.5) - 0.5) * 1.2;
            float bank_shape = 1.0 - smoothstep(2.0, 16.0, bank_distance);
            float walkable = 1.0 - cliff;
            float bank = bank_shape * walkable;
            // River heat belongs to walkable cooled crust. On sheer rock it would smear into
            // vertical curtains, so faces receive only the narrow lava-line rim and thin cracks.
            float crust_noise = 0.82 + 0.18 * tri_noise(world_pos, tw, 3.0);
            c = mix(c, c * vec3(0.82, 0.84, 0.86) * crust_noise, bank * 0.22);
            float glow_noise = 0.45 + 0.55 * tri_noise(world_pos + vec3(9.0), tw, 4.5);
            lava_emit += vec3(1.0, 0.17, 0.012) * bank * sqrt(bank) * glow_noise * 0.65;
            // Sparse open-ground fissures, gated by broad noise so they never cover the ash.
            float fissure_sites = smoothstep(0.67, 0.83, n2(w * 0.035 + 31.0))
                * smoothstep(0.35, 0.7, n2(w * 0.15 + 7.0));
            float ground_fissure = crack(w * 0.12) * fissure_sites * walkable * (1.0 - bank);
            c *= 1.0 - ground_fissure * 0.5;
            lava_emit += vec3(1.0, 0.22, 0.018) * ground_fissure * 0.75;
            // Only a few low cliff cracks remain molten; the upper basalt stays cold.
            float low_face = 1.0 - smoothstep(1.5, 13.0, above);
            float cliff_fissure = crack(vec2((w.x + w.y * 0.63) * 0.18, world_pos.y * 0.12))
                * low_face * cliff * smoothstep(0.6, 0.78, n2(w * 0.06 + 4.0));
            lava_emit += vec3(1.0, 0.12, 0.006) * cliff_fissure;
            float lava_rim = 1.0 - smoothstep(0.1, 2.0, above);
            lava_emit += vec3(1.0, 0.16, 0.01) * lava_rim * 0.3;
            float face_ember = (1.0 - smoothstep(2.0, 10.0, above)) * cliff;
            lava_emit += vec3(0.22, 0.07, 0.015) * face_ember;
        }
        NORMAL_MAP = n;
        NORMAL_MAP_DEPTH = bump * (0.6 + 0.6 * b2) * mix(1.0, 0.3, open_snow);
        ROUGHNESS = 0.85 - 0.25 * (b1.x + b2 * 0.3);
        SPECULAR = mix(0.25, 0.12, cliff_grey * cliff);
    } else {
        float d = n2(w * 0.06) * 0.42 + n2(w * 0.23) * 0.28 + n2(w * 0.9) * 0.18 + n2(w * 3.1) * 0.12;
        float stones = smoothstep(0.84, 0.92, n2(w * 1.1 + 13.0)) * smoothstep(0.5, 0.8, n2(w * 0.08 + 5.0));
        float dry = smoothstep(0.55, 0.78, n2(w * 0.035 + 7.0));
        c = COLOR.rgb * (0.7 + 0.55 * d);
        c = mix(c, mix(COLOR.rgb * 0.65, vec3(0.3, 0.26, 0.21), 0.5) * (0.85 + 0.3 * d), dry * 0.45);
        c = mix(c, c * 0.62, stones * 0.6);
        ROUGHNESS = 0.95;
        SPECULAR = 0.0;
    }
    if (marks) {
        // The battle so far: tank tracks and craters darken the ground; blasted bowls read lighter.
        vec2 mk = texture(marks_tex, uv).rg;
        c *= 1.0 - mk.r * 0.55;
        c = mix(c, c * 1.12 + vec3(0.03, 0.025, 0.02), mk.g * 0.45);
    }
    ALBEDO = c * light;
    // Local sky bounce keeps shade-side rock readable without a luminous arctic snow outline.
    EMISSION = c * light * cliff * mix(mix(0.28, 0.42, lava_heat), 0.08, cliff_snow) + lava_emit * light;
}";

    /// <summary>Per-theme ground shader tuning (tile size, band tint, normal depth); unset themes use the shader defaults.</summary>
    private static readonly Dictionary<string, Vector3> GroundTune = new()
    {
        // Desert reads calmest with large, softly tinted tiles and few dark patches:
        // the battle, not the ground, is busy.
        ["desert"] = new Vector3(18f, 0.6f, 0.7f),
        ["temperate"] = new Vector3(17f, 0.82f, 0.86f),
        ["arctic"] = new Vector3(20f, 0.65f, 0.55f),
        ["lava"] = new Vector3(16f, 0.75f, 0.8f),
    };

    /// <summary>Per-theme strength of the patch layer's ragged blotches (shader default 1).</summary>
    private static readonly Dictionary<string, float> PatchAmount = new()
    {
        ["desert"] = 0.3f,
        ["temperate"] = 0.2f,
        ["arctic"] = 0f,
        ["lava"] = 0.25f,
    };

    /// <summary>Gives the ground shader the theme's texture set: colour and normal maps for shore, ground, patches, high ground and cliffs.</summary>
    public static void ApplyGroundTextures(ShaderMaterial mat, string theme)
    {
        if (!TextureSet.GroundAverages.ContainsKey(theme)) theme = "temperate";
        Color[] avg = TextureSet.GroundAverages[theme];
        string[] slots = { "shore", "low", "patch", "high", "rock" };
        for (int i = 0; i < slots.Length; i++)
        {
            if (ResourceLoader.Load<Texture2D>($"res://textures/{theme}_{slots[i]}.png") is not { } tex) return;
            mat.SetShaderParameter($"tex_{slots[i]}", tex);
            mat.SetShaderParameter($"nrm_{slots[i]}", ResourceLoader.Load<Texture2D>($"res://textures/{theme}_{slots[i]}_n.png"));
            mat.SetShaderParameter($"avg_{slots[i]}", new Vector3(avg[i].R, avg[i].G, avg[i].B));
        }
        if (theme == "temperate")
        {
            Color rock = PaletteFor(theme).Rock;
            var greyRock = new Vector3(rock.R * 0.70f, rock.G * 0.65f, rock.B * 0.58f);
            mat.SetShaderParameter("cliff_band", greyRock);
            mat.SetShaderParameter("cliff_grey", 1f);
        }
        if (theme == "arctic")
        {
            Color rock = PaletteFor(theme).Rock;
            // Exposed rock stays distinctly darker than the surrounding snow, even in full sun.
            mat.SetShaderParameter("cliff_band", new Vector3(rock.R * 0.58f, rock.G * 0.58f, rock.B * 0.58f));
            mat.SetShaderParameter("cliff_snow", 1f);
        }
        if (theme == "lava")
        {
            // Basalt stays dark, but its strata and shade-side faces must not collapse to black.
            mat.SetShaderParameter("cliff_band", new Vector3(0.09f, 0.088f, 0.085f));
            mat.SetShaderParameter("lava_heat", 1f);
        }
        if (GroundTune.TryGetValue(theme, out Vector3 tune))
        {
            mat.SetShaderParameter("tile", tune.X);
            mat.SetShaderParameter("tint", tune.Y);
            mat.SetShaderParameter("bump", tune.Z);
        }
        if (PatchAmount.TryGetValue(theme, out float patch)) mat.SetShaderParameter("patch_amount", patch);
        mat.SetShaderParameter("textured", true);
    }

    private Node3D BuildWater(MapData map, TerrainPalette pal)
    {
        var node = new Node3D { Name = "Water" };
        float level = map.WaterLevel * HeightScale;
        bool anyWater = false;
        foreach (int h in map.Heights) if (h < map.WaterLevel) { anyWater = true; break; }
        if (!anyWater) return node;

        var plane = new PlaneMesh { Size = new Vector2(map.Width, map.Height), SubdivideWidth = 1, SubdivideDepth = 1 };
        var mat = new ShaderMaterial { Shader = new Shader { Code = pal.LavaWater ? LavaShader : WaterShader } };
        mat.SetShaderParameter("shallow", pal.Water);
        mat.SetShaderParameter("deep", pal.WaterDeep);
        var mi = new MeshInstance3D
        {
            Mesh = plane,
            MaterialOverride = mat,
            Position = new Vector3(map.Width / 2f, level + 0.05f, map.Height / 2f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        node.AddChild(mi);
        return node;
    }

    private const string WaterShader = @"
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, specular_schlick_ggx;
uniform vec4 shallow : source_color;
uniform vec4 deep : source_color;
varying vec3 wp;
void vertex() { wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment() {
    float w = sin(wp.x * 0.15 + TIME * 0.8) * sin(wp.z * 0.13 - TIME * 0.6);
    float w2 = sin((wp.x + wp.z) * 0.31 + TIME * 1.3) * 0.5;
    ALBEDO = mix(deep.rgb, shallow.rgb, 0.55 + 0.15 * w);
    ALPHA = shallow.a;
    ROUGHNESS = 0.15;
    METALLIC = 0.1;
    NORMAL = normalize(NORMAL + vec3(w * 0.08, 0.0, w2 * 0.08));
}";

    private const string LavaShader = @"
shader_type spatial;
render_mode unshaded, cull_disabled;
uniform vec4 shallow : source_color;
uniform vec4 deep : source_color;
varying vec3 wp;
void vertex() { wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment() {
    float n = sin(wp.x * 0.11 + TIME * 0.4) * sin(wp.z * 0.09 - TIME * 0.3) + 0.5 * sin((wp.x - wp.z) * 0.23 + TIME);
    vec3 c = mix(deep.rgb, shallow.rgb, 0.5 + 0.35 * n);
    ALBEDO = c * 1.4;
}";

    private Node3D BuildBorder(MapData map, TerrainPalette pal)
    {
        // A dark skirt around the map so the edge doesn't float in the void.
        float w = map.Width, h = map.Height;
        var mat = new StandardMaterial3D { AlbedoColor = pal.Sky.Darkened(0.75f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        var root = new Node3D { Name = "Border" };
        float pad = 600;
        void Add(Vector3 centre, Vector2 size) =>
            root.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = size }, MaterialOverride = mat, Position = centre });
        float y = -2;
        Add(new Vector3(w / 2, y, -pad / 2), new Vector2(w + pad * 2, pad));
        Add(new Vector3(w / 2, y, h + pad / 2), new Vector2(w + pad * 2, pad));
        Add(new Vector3(-pad / 2, y, h / 2), new Vector2(pad, h));
        Add(new Vector3(w + pad / 2, y, h / 2), new Vector2(pad, h));
        return root;
    }

    /// <summary>Metal deposits as dark, ragged patches of ore grit sitting on the ground, like
    /// TA's: no rings or glow, you spot them by looking. Each patch is a small grid draped over
    /// the terrain so it hugs slopes; the shader breaks its outline up per spot.</summary>
    private Node3D BuildMetalSpots(MapData map)
    {
        var mat = new ShaderMaterial { Shader = new Shader { Code = SpotShader } };
        return new MeshInstance3D { Mesh = MetalSpotMesh(map, HeightAt), MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Name = "MetalSpots" };
    }

    /// <summary>The ore patch mesh for every metal spot, draped over the ground given by heightAt.
    /// The map editor rebuilds it as spots move or the ground is sculpted.</summary>
    public static ArrayMesh MetalSpotMesh(MapData map, Func<float, float, float> heightAt)
    {
        if (map.MetalSpots.Count == 0) return new ArrayMesh();
        const float R = 5.2f;
        const int N = 8;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < map.MetalSpots.Count; i++)
        {
            var s = map.MetalSpots[i];
            float cx = s.Position.X.Raw / 65536f, cz = s.Position.Y.Raw / 65536f;
            var seed = new Vector2((i * 0.6180339f) % 1f * 37f, (i * 0.4142135f) % 1f * 53f);
            Vector3 P(int gx, int gz)
            {
                float u = gx / (float)N * 2 - 1, v = gz / (float)N * 2 - 1;
                float x = cx + u * R, z = cz + v * R;
                return new Vector3(x, heightAt(x, z) + 0.07f, z);
            }
            void V(int gx, int gz)
            {
                st.SetUV(new Vector2(gx / (float)N * 2 - 1, gz / (float)N * 2 - 1));
                st.SetUV2(seed);
                st.SetNormal(Vector3.Up);
                st.AddVertex(P(gx, gz));
            }
            for (int gz = 0; gz < N; gz++)
            for (int gx = 0; gx < N; gx++)
            {
                V(gx, gz); V(gx + 1, gz); V(gx, gz + 1);
                V(gx + 1, gz); V(gx + 1, gz + 1); V(gx, gz + 1);
            }
        }
        return st.Commit();
    }

    private const string SpotShader = @"
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled;
varying vec2 seed;
void vertex() { seed = UV2; }
float h(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float n(vec2 p) {
    vec2 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(h(i), h(i + vec2(1, 0)), f.x), mix(h(i + vec2(0, 1)), h(i + vec2(1, 1)), f.x), f.y);
}
void fragment() {
    vec2 uv = UV;
    float wob = n(uv * 2.2 + seed) * 0.6 + n(uv * 5.0 + seed * 1.7) * 0.3;
    float d = length(uv) + (wob - 0.45) * 0.55;
    float blob = 1.0 - smoothstep(0.55, 0.95, d);
    // Clumps of ore: a cluster of darker stones with a few cold metallic flecks.
    vec2 cell = floor(uv * 9.0 + seed);
    float stone = step(0.45, h(cell)) * (1.0 - smoothstep(0.3, 0.5, length(fract(uv * 9.0 + seed) - 0.5)));
    float fleck = step(0.93, h(floor(uv * 26.0 + seed * 3.0)));
    float grit = n(uv * 14.0 + seed);
    vec3 dirt = mix(vec3(0.11, 0.10, 0.09), vec3(0.20, 0.19, 0.17), grit);
    vec3 col = mix(dirt, vec3(0.27, 0.27, 0.26), stone * 0.8);
    col = mix(col, vec3(0.52, 0.53, 0.52), fleck * blob);
    ALBEDO = col;
    METALLIC = 0.35 + 0.4 * max(stone, fleck);
    ROUGHNESS = 0.75 - 0.35 * fleck;
    ALPHA = blob * (0.7 + 0.25 * stone);
}";
}
