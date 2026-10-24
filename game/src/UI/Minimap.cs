using System;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Match;
using ContingencyZero.Game.View;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.State;
using Godot;

namespace ContingencyZero.Game.UI;

/// <summary>
/// The minimap: terrain, fog, metal spots, units as team-coloured dots (radar blips for unseen
/// enemies) and the camera's view. Left click or drag moves the camera; right click sends the
/// selection there.
/// </summary>
public partial class Minimap : Control
{
    public const int PixelSize = 228;

    private SimSession _session = null!;
    private CameraRig _rig = null!;
    private TerrainView _terrain = null!;
    private InputController _input = null!;
    private Image _base = null!;
    private Image _image = null!;
    private ImageTexture _texture = null!;
    private int _w, _h;
    private float _scale;
    private bool _dragging;
    private int _frame;

    private World World => _session.World;

    public void Init(SimSession session, CameraRig rig, TerrainView terrain, InputController input)
    {
        _session = session;
        _rig = rig;
        _terrain = terrain;
        _input = input;
        MapData map = World.Map;
        float aspect = (float)map.Width / map.Height;
        _w = aspect >= 1 ? PixelSize : (int)(PixelSize * aspect);
        _h = aspect >= 1 ? (int)(PixelSize / aspect) : PixelSize;
        _scale = (float)_w / map.Width;
        CustomMinimumSize = new Vector2(PixelSize, PixelSize);
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Stop;

        _base = RenderBase(map, _w, _h);
        _image = (Image)_base.Duplicate();
        _texture = ImageTexture.CreateFromImage(_image);
        Redraw();
    }

    /// <summary>A small picture of the map's terrain, for the minimap and the skirmish screen.</summary>
    public static Image RenderBase(MapData map, int w, int h)
    {
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        TerrainPalette pal = TerrainView.PaletteFor(map.Theme);
        int min = int.MaxValue, max = int.MinValue;
        foreach (int v in map.Heights) { min = Math.Min(min, v); max = Math.Max(max, v); }
        int baseline = Math.Max(min, map.WaterLevel);
        // Gentle maps shouldn't stretch the whole palette over a few metres of relief.
        float range = Math.Max(80, max - baseline);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int tx = x * map.TilesX / w, ty = y * map.TilesY / h;
            int hh = map.TileHeight(tx, ty);
            Color c;
            if (hh < map.WaterLevel)
            {
                float depth = Math.Clamp((map.WaterLevel - hh) / 40f, 0, 1);
                c = pal.Water.Lerp(pal.WaterDeep, depth);
                c.A = 1;
            }
            else
            {
                float t = Math.Clamp((hh - baseline) / range, 0, 1);
                c = t < 0.5f ? pal.Low.Lerp(pal.High, t * 2) : pal.High.Lerp(pal.Peak, (t - 0.5f) * 2);
                // Hill shading from the east-west slope.
                int e = map.TileHeight(Math.Min(tx + 1, map.TilesX - 1), ty);
                float shade = Math.Clamp((hh - e) / 12f, -0.25f, 0.25f);
                c = c.Lightened(Math.Max(0, shade)).Darkened(Math.Max(0, -shade));
                if (map.TileSlope(tx, ty) > Terrain.VehicleMaxSlope) c = c.Lerp(pal.Rock, 0.6f);
            }
            img.SetPixel(x, y, c);
        }
        foreach (MetalSpot s in map.MetalSpots)
        {
            int x = (int)(s.Position.X.Raw / 65536f / map.Width * w), y = (int)(s.Position.Y.Raw / 65536f / map.Height * h);
            // A small dark smudge, like the patch on the ground, not a bright marker.
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (Math.Abs(dx) + Math.Abs(dy) < 2 && x + dx >= 0 && y + dy >= 0 && x + dx < w && y + dy < h)
                    img.SetPixel(x + dx, y + dy, img.GetPixel(x + dx, y + dy).Darkened(0.45f));
        }
        return img;
    }

    public void OnTick()
    {
        // Redraw a few times a second; it's cheap but not free.
        if (++_frame % 4 == 0) Redraw();
    }

    private void Redraw()
    {
        _image.CopyFrom(_base);
        int me = _session.LocalPlayer;
        int team = me >= 0 ? World.Players[me].Team : -1;

        if (team >= 0 && !World.Vision.RevealAll)
        {
            bool[] vis = World.Vision.VisibleGrid(team);
            bool[] exp = World.Vision.ExploredGrid(team);
            int tx = World.Map.TilesX;
            for (int y = 0; y < _h; y++)
            for (int x = 0; x < _w; x++)
            {
                int i = (y * World.Map.TilesY / _h) * tx + x * tx / _w;
                if (vis[i]) continue;
                Color c = _image.GetPixel(x, y);
                _image.SetPixel(x, y, exp[i] ? c.Darkened(0.45f) : c.Darkened(0.82f));
            }
        }

        UnitStore u = World.Units;
        for (int id = 0; id < u.Count; id++)
        {
            if (!u.Alive[id]) continue;
            bool visible = team < 0 || World.CanSee(team, id);
            bool blip = !visible && World.CanSee(team, id, radarCounts: true);
            if (!visible && !blip) continue;
            UnitDef def = World.Catalog[u.TypeIndex[id]];
            Color c = blip ? new Color(1f, 0.3f, 0.25f) : UiTheme.TeamColour(u.Owner[id]);
            int px = (int)(u.Position[id].X.Raw / 65536f * _scale), py = (int)(u.Position[id].Y.Raw / 65536f * _scale);
            int r = def.IsStructure ? Math.Max(1, def.Footprint) : def.IsCommander ? 2 : 1;
            for (int dy = -r + 1; dy < r; dy++)
            for (int dx = -r + 1; dx < r; dx++)
                if (px + dx >= 0 && py + dy >= 0 && px + dx < _w && py + dy < _h)
                    _image.SetPixel(px + dx, py + dy, def.IsCommander && !blip ? Colors.White : c);
        }
        _texture.Update(_image);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var offset = new Vector2((PixelSize - _w) / 2f, (PixelSize - _h) / 2f);
        DrawTextureRect(_texture, new Rect2(offset, new Vector2(_w, _h)), false);

        // The camera's view on the ground.
        Camera3D cam = _rig.Camera;
        Vector2 vs = GetViewport().GetVisibleRect().Size;
        Vector2[] corners = { Vector2.Zero, new(vs.X, 0), vs, new(0, vs.Y) };
        var pts = new Vector2[5];
        for (int i = 0; i < 4; i++)
        {
            Vector3 o = cam.ProjectRayOrigin(corners[i]), d = cam.ProjectRayNormal(corners[i]);
            float t = d.Y < -0.001f ? (o.Y - _terrain.WaterHeight) / -d.Y : 0;
            Vector3 g = o + d * t;
            pts[i] = offset + new Vector2(g.X * _scale, g.Z * _scale);
        }
        pts[4] = pts[0];
        DrawPolyline(pts, new Color(1, 1, 1, 0.8f), 1.2f);
        DrawRect(new Rect2(offset, new Vector2(_w, _h)), new Color(0.3f, 0.45f, 0.55f), false, 1);
    }

    private Vector3 ToWorld(Vector2 local)
    {
        var offset = new Vector2((PixelSize - _w) / 2f, (PixelSize - _h) / 2f);
        Vector2 p = (local - offset) / _scale;
        return new Vector3(Math.Clamp(p.X, 0, World.Map.Width), 0, Math.Clamp(p.Y, 0, World.Map.Height));
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                _dragging = mb.Pressed;
                if (mb.Pressed) _rig.JumpTo(ToWorld(mb.Position));
                AcceptEvent();
            }
            else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
            {
                Vector3 w = ToWorld(mb.Position);
                var target = new FixVec2(Fix64.FromRaw((long)(w.X * 65536)), Fix64.FromRaw((long)(w.Z * 65536)));
                foreach (int id in SelectionIds())
                {
                    UnitDef def = World.Catalog[World.Units.TypeIndex[id]];
                    if (def.IsFactory) _input.Issue(new Command(0, 0, CommandType.SetRally, id, target));
                    else if (def.CanMove) _input.Issue(new Command(0, 0, mb.CtrlPressed ? CommandType.Fight : CommandType.Move, id, target, Queue: mb.ShiftPressed));
                }
                Audio.Sfx.Play(Audio.SfxKind.Acknowledge, -8);
                AcceptEvent();
            }
        }
        else if (e is InputEventMouseMotion mm && _dragging)
        {
            _rig.JumpTo(ToWorld(mm.Position));
            AcceptEvent();
        }
    }

    public Func<System.Collections.Generic.IEnumerable<int>> SelectionIds = () => Array.Empty<int>();

    public override void _Process(double delta) => QueueRedraw();
}
