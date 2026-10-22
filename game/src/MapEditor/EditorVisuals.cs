using System;
using System.Collections.Generic;
using ContingencyZero.Game.App;
using ContingencyZero.Game.View;
using ContingencyZero.Game.View.Meshes;
using ContingencyZero.Sim.Map;
using Godot;

namespace ContingencyZero.Game.MapEditor;

/// <summary>Trees, rocks and wrecks placed on the map being edited (drawn like the game's features).</summary>
public partial class EditorFeatures : Node3D
{
    private readonly Dictionary<string, MultiMesh> _batches = new();
    private ShaderMaterial? _material;
    private string _theme = "";

    private static float Hash(int a, int b)
    {
        unchecked
        {
            uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777216f;
        }
    }

    public void Rebuild(MapData map, EditorTerrain terrain)
    {
        _material ??= new ShaderMaterial { Shader = new Shader { Code = UnitView.UnitShader } };
        if (_theme != map.Theme)
        {
            // Tree and rock meshes depend on the theme: start the batches afresh.
            foreach (Node c in GetChildren()) c.QueueFree();
            _batches.Clear();
            _theme = map.Theme;
        }

        var pending = new Dictionary<string, (Func<Mesh> Make, List<Transform3D> Xf)>();
        string theme = map.Theme;
        foreach (FeaturePlacement f in map.Features)
        {
            float x = MapEdits.ToFloat(f.Position.X), z = MapEdits.ToFloat(f.Position.Y);
            int hx = (int)(f.Position.X.Raw >> 12), hz = (int)(f.Position.Y.Raw >> 12);
            int variant = (int)(Hash(hx, hz) * 3) % 3;
            string key;
            Func<Mesh> make;
            float scale;
            switch (f.Kind)
            {
                case FeatureKind.Tree:
                    key = $"tree{variant}";
                    make = () => UnitModels.Tree(theme, variant);
                    scale = 0.85f + Hash(hx + 17, hz) * 0.4f;
                    break;
                case FeatureKind.Rock:
                    key = $"rock{variant}";
                    make = () => UnitModels.Rock(theme, variant);
                    scale = 0.7f + Math.Min(1.2f, MapEdits.ToFloat(f.Metal) / 60f);
                    break;
                default:
                {
                    const float size = 6f; // the sim gives map wrecks a radius of 3, which the game draws 6 across
                    key = $"wreck{size}";
                    make = () => UnitModels.Wreck(size);
                    scale = 1f;
                    break;
                }
            }
            float yaw = Hash(hx + 5, hz + 9) * Mathf.Tau;
            var basis = new Basis(Vector3.Up, yaw).Scaled(new Vector3(scale, scale, scale));
            if (!pending.TryGetValue(key, out var entry)) pending[key] = entry = (make, new List<Transform3D>());
            entry.Xf.Add(new Transform3D(basis, new Vector3(x, terrain.HeightAt(x, z), z)));
        }

        var neutral = new Color(0.6f, 0.6f, 0.6f, 0);
        foreach (var (key, entry) in pending)
        {
            if (!_batches.TryGetValue(key, out MultiMesh? mm))
            {
                mm = MultiMeshUtil.Create(entry.Make(), 64);
                AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = _material });
                _batches[key] = mm;
            }
            MultiMeshUtil.Ensure(mm, entry.Xf.Count);
            for (int i = 0; i < entry.Xf.Count; i++)
            {
                mm.SetInstanceTransform(i, entry.Xf[i]);
                mm.SetInstanceCustomData(i, neutral);
            }
        }
        foreach (var kv in _batches)
            kv.Value.VisibleInstanceCount = pending.TryGetValue(kv.Key, out var e) ? e.Xf.Count : 0;
    }
}

/// <summary>Big numbered beacons over each start position, in the players' colours, plus the base area ring.</summary>
public partial class StartMarkers : Node3D
{
    private readonly List<MeshInstance3D> _beams = new();
    private MeshInstance3D _rings = null!;
    private ImmediateMesh _ringMesh = null!;
    private StandardMaterial3D _ringMaterial = null!;
    public int Highlight { get; set; } = -1;

    public override void _Ready()
    {
        _ringMesh = new ImmediateMesh();
        _ringMaterial = OverlayMaterial(noDepth: false);
        _rings = new MeshInstance3D { Mesh = _ringMesh, MaterialOverride = _ringMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_rings);
    }

    public static StandardMaterial3D OverlayMaterial(bool noDepth) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        VertexColorUseAsAlbedo = true,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        NoDepthTest = noDepth,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    public void Refresh(MapData map, EditorTerrain terrain)
    {
        int n = map.StartPositions.Count;
        while (_beams.Count < n)
        {
            var mat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            };
            var beam = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.6f, BottomRadius = 1.6f, Height = 26f, RadialSegments = 12, Rings = 1 },
                MaterialOverride = mat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(beam);
            _beams.Add(beam);
        }
        for (int i = 0; i < _beams.Count; i++)
        {
            MeshInstance3D beam = _beams[i];
            beam.Visible = i < n;
            if (i >= n) continue;
            Vector2 p = MapEdits.ToVec(map.StartPositions[i]);
            float y = terrain.SurfaceAt(p.X, p.Y);
            beam.Position = new Vector3(p.X, y + 13f, p.Y);
            Color c = UiTheme.TeamColour(i);
            ((StandardMaterial3D)beam.MaterialOverride).AlbedoColor = new Color(c, i == Highlight ? 0.85f : 0.55f);
        }

        // Rings on the ground: the commander's spot and the area the base will need.
        _ringMesh.ClearSurfaces();
        if (n == 0) return;
        _ringMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < n; i++)
        {
            Vector2 p = MapEdits.ToVec(map.StartPositions[i]);
            Color c = UiTheme.TeamColour(i);
            Ribbon(_ringMesh, terrain, p, 4.5f, 1.4f, new Color(c, 0.9f), 32);
            Ribbon(_ringMesh, terrain, p, MapValidator.BaseRadiusTiles * MapData.TileSize, 0.8f, new Color(c, i == Highlight ? 0.75f : 0.4f), 72);
        }
        _ringMesh.SurfaceEnd();
    }

    /// <summary>Adds a ring of triangles lying on the ground (or water) to an immediate mesh between SurfaceBegin/End.</summary>
    public static void Ribbon(ImmediateMesh m, EditorTerrain terrain, Vector2 centre, float radius, float width, Color colour, int segments, float lift = 0.35f)
    {
        float inner = MathF.Max(0, radius - width / 2), outer = radius + width / 2;
        for (int s = 0; s < segments; s++)
        {
            float a0 = s * Mathf.Tau / segments, a1 = (s + 1) * Mathf.Tau / segments;
            Vector3 P(float a, float r)
            {
                float x = centre.X + MathF.Cos(a) * r, z = centre.Y + MathF.Sin(a) * r;
                return new Vector3(x, terrain.SurfaceAt(x, z) + lift, z);
            }
            Vector3 i0 = P(a0, inner), o0 = P(a0, outer), i1 = P(a1, inner), o1 = P(a1, outer);
            m.SurfaceSetColor(colour); m.SurfaceAddVertex(i0);
            m.SurfaceSetColor(colour); m.SurfaceAddVertex(o0);
            m.SurfaceSetColor(colour); m.SurfaceAddVertex(o1);
            m.SurfaceSetColor(colour); m.SurfaceAddVertex(i0);
            m.SurfaceSetColor(colour); m.SurfaceAddVertex(o1);
            m.SurfaceSetColor(colour); m.SurfaceAddVertex(i1);
        }
    }
}

/// <summary>
/// The brush outline that follows the mouse over the ground (with fainter copies where symmetry
/// mirrors it), plus the plateau's target level and placement previews for spots and starts.
/// </summary>
public partial class BrushCursor : Node3D
{
    private ImmediateMesh _lines = null!;
    private ImmediateMesh _fills = null!;

    public override void _Ready()
    {
        _lines = new ImmediateMesh();
        _fills = new ImmediateMesh();
        AddChild(new MeshInstance3D { Mesh = _fills, MaterialOverride = StartMarkers.OverlayMaterial(noDepth: false), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        AddChild(new MeshInstance3D { Mesh = _lines, MaterialOverride = StartMarkers.OverlayMaterial(noDepth: true), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    public void Clear()
    {
        _lines.ClearSurfaces();
        _fills.ClearSurfaces();
    }

    /// <summary>Draws brush rings at each centre; the first is the one under the mouse.</summary>
    public void DrawBrush(EditorTerrain terrain, List<Vector2> centres, float radius, Color colour, float pixel, float? plateauLevel)
    {
        Clear();
        _lines.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < centres.Count; i++)
        {
            float a = i == 0 ? 0.95f : 0.45f;
            StartMarkers.Ribbon(_lines, terrain, centres[i], radius, pixel * 2.6f, new Color(colour, a), 72, 0.25f);
            StartMarkers.Ribbon(_lines, terrain, centres[i], radius * 0.5f, pixel * 1.2f, new Color(colour, a * 0.45f), 48, 0.25f);
            StartMarkers.Ribbon(_lines, terrain, centres[i], pixel * 2.2f, pixel * 2.4f, new Color(colour, a), 12, 0.25f);
        }
        _lines.SurfaceEnd();

        if (plateauLevel is { } level)
        {
            // A see-through lid at the plateau height, so you can see where it will cut the hills.
            _fills.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            foreach (Vector2 c in centres)
            {
                const int seg = 48;
                var col = new Color(colour, 0.28f);
                for (int s = 0; s < seg; s++)
                {
                    float a0 = s * Mathf.Tau / seg, a1 = (s + 1) * Mathf.Tau / seg;
                    _fills.SurfaceSetColor(col); _fills.SurfaceAddVertex(new Vector3(c.X, level, c.Y));
                    _fills.SurfaceSetColor(col); _fills.SurfaceAddVertex(new Vector3(c.X + MathF.Cos(a0) * radius, level, c.Y + MathF.Sin(a0) * radius));
                    _fills.SurfaceSetColor(col); _fills.SurfaceAddVertex(new Vector3(c.X + MathF.Cos(a1) * radius, level, c.Y + MathF.Sin(a1) * radius));
                }
            }
            _fills.SurfaceEnd();
        }
    }

    /// <summary>A marker for a single placement (metal spot, start) or a removal target.</summary>
    public void DrawMarker(EditorTerrain terrain, List<Vector2> centres, float radius, Color colour, float pixel, bool square)
    {
        Clear();
        _lines.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < centres.Count; i++)
        {
            float a = i == 0 ? 0.95f : 0.45f;
            Vector2 c = centres[i];
            if (square)
            {
                // The tile outline, so it's clear the spot snaps to the grid.
                float h = MapData.TileSize / 2f;
                Edge(terrain, c + new Vector2(-h, -h), c + new Vector2(h, -h), pixel * 2f, new Color(colour, a * 0.7f));
                Edge(terrain, c + new Vector2(h, -h), c + new Vector2(h, h), pixel * 2f, new Color(colour, a * 0.7f));
                Edge(terrain, c + new Vector2(h, h), c + new Vector2(-h, h), pixel * 2f, new Color(colour, a * 0.7f));
                Edge(terrain, c + new Vector2(-h, h), c + new Vector2(-h, -h), pixel * 2f, new Color(colour, a * 0.7f));
            }
            StartMarkers.Ribbon(_lines, terrain, c, radius, pixel * 2.6f, new Color(colour, a), 40, 0.3f);
        }
        _lines.SurfaceEnd();
    }

    private void Edge(EditorTerrain terrain, Vector2 a, Vector2 b, float width, Color colour)
    {
        Vector2 d = (b - a).Normalized();
        var side = new Vector2(-d.Y, d.X) * width / 2;
        const int steps = 4;
        for (int s = 0; s < steps; s++)
        {
            Vector2 p0 = a.Lerp(b, s / (float)steps), p1 = a.Lerp(b, (s + 1) / (float)steps);
            Vector3 V(Vector2 p) => new(p.X, terrain.SurfaceAt(p.X, p.Y) + 0.3f, p.Y);
            Vector3 a0 = V(p0 - side), a1 = V(p0 + side), b0 = V(p1 - side), b1 = V(p1 + side);
            _lines.SurfaceSetColor(colour); _lines.SurfaceAddVertex(a0);
            _lines.SurfaceSetColor(colour); _lines.SurfaceAddVertex(a1);
            _lines.SurfaceSetColor(colour); _lines.SurfaceAddVertex(b1);
            _lines.SurfaceSetColor(colour); _lines.SurfaceAddVertex(a0);
            _lines.SurfaceSetColor(colour); _lines.SurfaceAddVertex(b1);
            _lines.SurfaceSetColor(colour); _lines.SurfaceAddVertex(b0);
        }
    }
}

/// <summary>Screen-space labels over the 3D view: the big numbered badges on start positions.</summary>
public partial class EditorOverlay : Control
{
    private MapEditorScene _ed = null!;

    public void Init(MapEditorScene ed)
    {
        _ed = ed;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        if (_ed.Document is not { } doc || _ed.ModalOpen) return;
        MapData map = doc.Map;
        Camera3D cam = _ed.DevCamera;
        Font font = ThemeDB.FallbackFont;
        for (int i = 0; i < map.StartPositions.Count; i++)
        {
            Vector2 p = MapEdits.ToVec(map.StartPositions[i]);
            var top = new Vector3(p.X, _ed.TerrainNode.SurfaceAt(p.X, p.Y) + 27f, p.Y);
            if (cam.IsPositionBehind(top)) continue;
            Vector2 s = cam.UnprojectPosition(top);
            Color c = UiTheme.TeamColour(i);
            const float r = 17;
            DrawCircle(s + new Vector2(0, 2), r + 3, new Color(0, 0, 0, 0.45f));
            DrawCircle(s, r + 2, new Color(0.03f, 0.04f, 0.06f));
            DrawCircle(s, r, c);
            string n = (i + 1).ToString();
            const int size = 22;
            Vector2 ts = font.GetStringSize(n, HorizontalAlignment.Left, -1, size);
            DrawString(font, s + new Vector2(-ts.X / 2, size * 0.36f), n, HorizontalAlignment.Left, -1, size, new Color(0.03f, 0.04f, 0.06f));
        }
    }
}
