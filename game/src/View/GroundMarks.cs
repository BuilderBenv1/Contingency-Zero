using System;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.State;
using Godot;

namespace ContingencyZero.Game.View;

/// <summary>
/// The battle written onto the ground: tank tracks and blast craters, accumulated in a single
/// texture stretched over the whole map (R = darkening/tracks, G = churned-light ground) and
/// sampled by the ground shader in world UV. Purely visual; stamps come from sim positions every
/// tick via <see cref="Sync"/>, so replays and fast-forwards leave the same marks. Marks fade
/// slowly (minutes) and the texture bounds memory. Placement effects use coordinate hashes, never
/// randomness, so every viewer sees the same map.
/// </summary>
public partial class GroundMarks : Node3D
{
    /// <summary>The marks texture is this many texels across, whatever the map size.</summary>
    public const int Resolution = 2048;
    /// <summary>Uploads are limited to this many per second.</summary>
    private const float UploadInterval = 0.25f;
    /// <summary>The slow fade pass runs this often (seconds of wall time; purely cosmetic).</summary>
    private const float FadeInterval = 5f;
    /// <summary>Per fade pass: tracks lose this fraction (half-life ~8 minutes).</summary>
    private const float FadeAmount = 0.004f;

    private World _world = null!;
    private TerrainView _terrain = null!;
    private Image _image = null!;
    private ImageTexture _texture = null!;
    private Vector2[] _last = Array.Empty<Vector2>();
    private bool[] _stamped = Array.Empty<bool>();
    private bool _dirty;
    private float _uploadTimer;
    private float _fadeTimer;

    public void Init(World world, TerrainView terrain)
    {
        _world = world;
        _terrain = terrain;
        _image = Image.CreateEmpty(Resolution, Resolution, false, Image.Format.Rg8);
        _texture = ImageTexture.CreateFromImage(_image);
        if (terrain.GroundMaterial is ShaderMaterial mat)
        {
            mat.SetShaderParameter("marks_tex", _texture);
            mat.SetShaderParameter("marks", true);
        }
    }

    /// <summary>Stamps movement marks for every ground unit, then throttled upload/fade passes.</summary>
    public override void _Process(double delta)
    {
        if (_world is null) return;
        Sync();
        _uploadTimer -= (float)delta;
        if (_dirty && _uploadTimer <= 0f)
        {
            _texture.Update(_image);
            _dirty = false;
            _uploadTimer = UploadInterval;
        }
        _fadeTimer -= (float)delta;
        if (_fadeTimer <= 0f)
        {
            _fadeTimer = FadeInterval;
            Fade();
        }
    }

    /// <summary>
    /// Stamps tracks from each ground unit's last stamped position to its current sim position.
    /// Cheap to call every frame: idle units stamp nothing, and a missed stretch (fast-forward,
    /// replay seek) is stamped as one segment next time round.
    /// </summary>
    public void Sync()
    {
        UnitStore u = _world.Units;
        int n = u.Count;
        if (_last.Length < n)
        {
            Array.Resize(ref _last, n);
            Array.Resize(ref _stamped, n);
        }
        float water = _terrain.WaterHeight;
        for (int id = 0; id < n; id++)
        {
            if (!u.Alive[id] || !u.Complete[id]) { _stamped[id] = false; continue; }
            var def = _world.Catalog[u.TypeIndex[id]];
            if (def.MoveClass != MoveClass.Vehicle && def.MoveClass != MoveClass.Bot) { _stamped[id] = false; continue; }
            var p = new Vector2(u.Position[id].X.Raw / 65536f, u.Position[id].Y.Raw / 65536f);
            if (!_stamped[id] || _last[id].DistanceSquaredTo(p) > 36f)
            {
                // First sight of this unit, or it jumped (teleport, replay seek): start fresh.
                _last[id] = p;
                _stamped[id] = true;
                continue;
            }
            if (_last[id] == p) continue;
            if (_terrain.HeightAt(p.X, p.Y) < water) { _last[id] = p; continue; }
            float width = def.MoveClass == MoveClass.Bot ? 0.9f : Mathf.Max(1.4f, def.Radius.Raw / 65536f * 1.4f);
            StampTracks(_last[id], p, width, def.MoveClass == MoveClass.Bot ? 0.05f : 0.10f);
            _last[id] = p;
        }
    }

    /// <summary>A crater decal: dark rim ring, lighter blasted bowl, radial ejecta streaks.</summary>
    public void StampCrater(Vector2 centre, float radius)
    {
        float texPerUnit = Resolution / (float)_terrain.Map.Width;
        float r = Mathf.Max(1.5f, radius) * texPerUnit;
        Stamp(centre, r * 1.7f, (x, y, d) =>
        {
            float rn = d / r;
            float ang = Mathf.Atan2(y, x);
            // Ejecta: a few hashed radial streaks thrown outward past the rim.
            float streak = Hash(ang * 3.4f, (int)(radius * 10)) > 0.55 && rn is > 0.9f and < 1.7f
                ? (1f - Mathf.Abs(rn - 1.25f) / 0.45f) * 0.5f : 0f;
            float rim = Mathf.Exp(-Mathf.Pow((rn - 0.92f) / 0.16f, 2f)) * 0.55f;
            float bowlDark = (1f - Mathf.SmoothStep(0.25f, 0.9f, rn)) * 0.35f;
            float bowlLight = (1f - Mathf.SmoothStep(0.1f, 0.55f, rn)) * 0.6f;
            return (Mathf.Clamp(rim + bowlDark + streak, 0f, 0.85f), bowlLight);
        });
    }

    /// <summary>Two parallel tread strips behind a vehicle, with crossbars for the track pattern.</summary>
    private void StampTracks(Vector2 a, Vector2 b, float width, float strength)
    {
        float texPerUnit = Resolution / (float)_terrain.Map.Width;
        Vector2 mid = (a + b) * 0.5f;
        float reach = a.DistanceTo(b) * 0.5f + width;
        float tread = width * 0.26f;
        float gap = width * 0.24f;
        Stamp(mid, reach * texPerUnit, (x, y, d) =>
        {
            // x/y are texel offsets from the stamp centre; rotate into the track's frame.
            Vector2 along = (b - a).Normalized();
            float u = x * along.X + y * along.Y;
            float v = -x * along.Y + y * along.X;
            if (Mathf.Abs(u) > reach * texPerUnit) return (0f, 0f);
            float side = Mathf.Abs(Mathf.Abs(v) - gap - tread * 0.5f) - tread * 0.5f;
            if (side > 0f) return (0f, 0f);
            // Crossbars every few texels make the tread read at close zoom.
            float bar = 0.6f + 0.4f * (Mathf.PosMod(u * 0.9f, 1f) < 0.45f ? 1f : 0f);
            float edge = 1f - Mathf.SmoothStep(0f, Mathf.Max(0.001f, tread * 0.4f), side + tread * 0.2f);
            float centrePress = 1f - Mathf.Clamp(Mathf.Abs(v) / (gap + tread), 0f, 1f);
            return (strength * bar * edge, centrePress * 0.08f);
        });
    }

    /// <summary>
    /// Adds a shaped stamp into the texture: <paramref name="shape"/> maps a texel offset
    /// (in texels, x = east, y = south) to a (darken, lighten) addition. Clamped accumulation.
    /// </summary>
    private void Stamp(Vector2 centreWorld, float radiusTexels, Func<float, float, float, (float Dark, float Light)> shape)
    {
        float w = _terrain.Map.Width, h = _terrain.Map.Height;
        float cx = centreWorld.X / w * Resolution, cy = centreWorld.Y / h * Resolution;
        int x0 = Math.Max(0, (int)(cx - radiusTexels)), x1 = Math.Min(Resolution - 1, (int)(cx + radiusTexels) + 1);
        int y0 = Math.Max(0, (int)(cy - radiusTexels)), y1 = Math.Min(Resolution - 1, (int)(cy + radiusTexels) + 1);
        if (x1 < x0 || y1 < y0) return;
        byte[] data = _image.GetData();
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float dx = x - cx, dy = y - cy;
            if (dx * dx + dy * dy > radiusTexels * radiusTexels) continue;
            var (dark, light) = shape(dx, dy, Mathf.Sqrt(dx * dx + dy * dy));
            if (dark <= 0f && light <= 0f) continue;
            int i = (y * Resolution + x) * 2;
            data[i] = (byte)Mathf.Min(255, data[i] + (int)(dark * 255f));
            data[i + 1] = (byte)Mathf.Min(255, data[i + 1] + (int)(light * 255f));
        }
        _image.SetData(Resolution, Resolution, false, Image.Format.Rg8, data);
        _dirty = true;
    }

    /// <summary>The slow fade: every mark loses a little of its strength, so old tracks melt away.</summary>
    private void Fade()
    {
        byte[] data = _image.GetData();
        int keep = (int)((1f - FadeAmount) * 256f);
        bool any = false;
        for (int i = 0; i < data.Length; i += 2)
        {
            if (data[i] == 0 && data[i + 1] == 0) continue;
            data[i] = (byte)(data[i] * keep / 256);
            data[i + 1] = (byte)(data[i + 1] * keep / 256);
            any = true;
        }
        if (any)
        {
            _image.SetData(Resolution, Resolution, false, Image.Format.Rg8, data);
            _dirty = true;
        }
    }

    /// <summary>Stable per-position hash in 0..1 (visual detail only; must match on every machine).</summary>
    private static float Hash(float a, int b)
    {
        unchecked
        {
            uint h = (uint)(a * 374761393f) ^ (uint)b * 668265263u;
            h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }
    }
}
