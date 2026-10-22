using System.Collections.Generic;
using ContingencyZero.Game.View;
using ContingencyZero.Game.View.Meshes;
using ContingencyZero.Sim.Data;
using Godot;

namespace ContingencyZero.Game.UI;

/// <summary>
/// Build-menu portraits: renders each unit's model in a little off-screen studio, one per frame,
/// and hands back a texture that fills in once rendered (a placeholder until then).
/// </summary>
public partial class IconFactory : Node
{
    public const int Size = 96;

    private static IconFactory? _instance;
    private readonly Dictionary<string, ImageTexture> _icons = new();
    private readonly Queue<(UnitDef Def, Color Team, ImageTexture Target)> _queue = new();
    private SubViewport _viewport = null!;
    private Node3D _stage = null!;
    private MeshInstance3D _body = null!;
    private MeshInstance3D _turret = null!;
    private MeshInstance3D _rotor = null!;
    private Camera3D _camera = null!;
    private ShaderMaterial _material = null!;
    private (UnitDef Def, Color Team, ImageTexture Target)? _rendering;
    private int _waitFrames;

    public static IconFactory? Instance => _instance;

    public override void _Ready()
    {
        _instance = this;
        _viewport = new SubViewport
        {
            Size = new Vector2I(Size, Size),
            TransparentBg = true,
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            Msaa3D = Viewport.Msaa.Msaa4X,
        };
        AddChild(_viewport);
        _stage = new Node3D();
        _viewport.AddChild(_stage);
        // The unit shader reads the team colour from per-instance data; for a lone mesh in the
        // studio, read it from a uniform instead.
        _material = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = UnitView.UnitShader
                    .Replace("varying vec4 inst;", "varying vec4 inst;\nuniform vec3 team_colour;")
                    .Replace("inst = INSTANCE_CUSTOM;", "inst = vec4(team_colour, 0.0);"),
            },
        };
        _body = new MeshInstance3D { MaterialOverride = _material };
        _turret = new MeshInstance3D { MaterialOverride = _material };
        _rotor = new MeshInstance3D { MaterialOverride = _material };
        _stage.AddChild(_body);
        _stage.AddChild(_turret);
        _stage.AddChild(_rotor);
        _stage.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, 35, 0), LightEnergy = 1.2f });
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.ClearColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.55f, 0.58f, 0.65f),
            AmbientLightEnergy = 1.0f,
        };
        _stage.AddChild(new WorldEnvironment { Environment = env });
        _camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Current = true, Far = 500 };
        _stage.AddChild(_camera);
    }

    /// <summary>The icon for a unit (blank until it has been rendered, a frame or two later).</summary>
    public static Texture2D For(UnitDef def, Color team)
    {
        if (_instance is null) return new PlaceholderTexture2D { Size = new Vector2(Size, Size) };
        string key = def.Id + team.ToHtml();
        if (_instance._icons.TryGetValue(key, out var tex)) return tex;
        var img = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        tex = ImageTexture.CreateFromImage(img);
        _instance._icons[key] = tex;
        _instance._queue.Enqueue((def, team, tex));
        return tex;
    }

    public override void _Process(double delta)
    {
        if (_rendering is { } job)
        {
            if (_waitFrames-- > 0) return;
            Image img = _viewport.GetTexture().GetImage();
            if (img is not null && !img.IsEmpty())
            {
                img.Convert(Image.Format.Rgba8);
                job.Target.Update(img);
            }
            _rendering = null;
            _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        }

        if (_queue.Count == 0) return;
        var next = _queue.Dequeue();
        UnitModel model = UnitModels.For(next.Def);
        _body.Mesh = model.Body;
        _turret.Mesh = model.Turret;
        _turret.Position = model.TurretPivot;
        _rotor.Mesh = model.Rotor;
        _rotor.Position = model.RotorPivot;
        // One icon renders at a time, so the shared material just takes this icon's colour.
        _material.SetShaderParameter("team_colour", new Vector3(next.Team.R, next.Team.G, next.Team.B));
        if (model.RotorAxis != Vector3.Zero) _rotor.Basis = Basis.Identity;

        float extent = Mathf.Max(model.Radius * 2.2f, model.Height * 1.3f);
        _camera.Size = Mathf.Max(4, extent * 1.15f);
        float pitch = Mathf.DegToRad(30), yaw = Mathf.DegToRad(35);
        var focus = new Vector3(0, model.Height * 0.4f, 0);
        _camera.Position = focus + new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch)) * 200;
        _camera.LookAt(focus, Vector3.Up);
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        _rendering = next;
        _waitFrames = 1;
    }
}
