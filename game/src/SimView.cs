using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Replay;
using Godot;

namespace ContingencyZero.Game;

/// <summary>
/// M1 demo scene. Runs the line battle in the sim at 15 ticks a second and draws every unit
/// through one MultiMesh per team, seen through a fixed isometric orthographic camera.
///
/// Controls: WASD or arrows to pan, mouse wheel to zoom, right-click to send your (blue) army
/// somewhere, Space to pause.
///
/// The view only reads sim state and sends Commands; it never writes to the World.
/// </summary>
public partial class SimView : Node3D
{
    private const int UnitsPerSide = 200;
    private const int HumanPlayer = 0;

    private static readonly Color LatticeColour = new(0.35f, 0.62f, 1.0f);
    private static readonly Color CrucibleColour = new(1.0f, 0.52f, 0.16f);
    private static readonly Color WreckColour = new(0.22f, 0.22f, 0.24f);

    private World _world = null!;
    private ILookup<int, Command> _scripted = null!;
    private readonly List<Command> _pending = new();
    private bool _paused;

    private Camera3D _camera = null!;
    private Vector3 _focus = new(256, 0, 256);
    private MultiMesh[] _teamMeshes = null!;
    private MultiMesh _wreckMesh = null!;
    private Label _hud = null!;

    public override void _Ready()
    {
        string unitsDir = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "data", "units"));
        UnitCatalog catalog = UnitCatalog.LoadDirectory(unitsDir);

        MatchReplay battle = Scenarios.LineBattle(UnitsPerSide, seed: 1997, ticks: int.MaxValue);
        _world = battle.Scenario.CreateWorld(catalog);
        _scripted = battle.Commands.ToLookup(c => c.Tick);

        BuildScene();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_paused || _world.WinningTeam() is not null) return;

        var commands = _scripted[_world.Tick].ToList();
        foreach (Command c in _pending)
            commands.Add(c with { Tick = _world.Tick });
        _pending.Clear();

        _world.Step(commands);
    }

    public override void _Process(double delta)
    {
        PanCamera((float)delta);
        DrawUnits();
        DrawHud();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true } mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp) _camera.Size = Mathf.Max(60f, _camera.Size * 0.9f);
            else if (mb.ButtonIndex == MouseButton.WheelDown) _camera.Size = Mathf.Min(900f, _camera.Size * 1.1f);
            else if (mb.ButtonIndex == MouseButton.Right) OrderArmyTo(mb.Position);
        }
        else if (@event is InputEventKey { Pressed: true, Keycode: Key.Space })
        {
            _paused = !_paused;
        }
    }

    // ---- Scene setup -------------------------------------------------------------------------

    private void BuildScene()
    {
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.08f, 0.09f, 0.11f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.55f, 0.55f, 0.6f),
        };
        AddChild(new WorldEnvironment { Environment = env });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, 30, 0), ShadowEnabled = true });

        float size = _world.MapWidth;
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(size, _world.MapHeight) },
            Position = new Vector3(size / 2, 0, _world.MapHeight / 2f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.23f, 0.25f, 0.21f) },
        });

        _teamMeshes = new[]
        {
            AddMultiMesh(new BoxMesh { Size = new Vector3(3, 2, 3) }),
            AddMultiMesh(new BoxMesh { Size = new Vector3(3, 2, 3) }),
        };
        _wreckMesh = AddMultiMesh(new BoxMesh { Size = new Vector3(2.6f, 0.6f, 2.6f) });

        _camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = 320,
            Far = 4000,
        };
        AddChild(_camera);
        PlaceCamera();

        var layer = new CanvasLayer();
        _hud = new Label { Position = new Vector2(16, 12) };
        _hud.AddThemeFontSizeOverride("font_size", 18);
        layer.AddChild(_hud);
        AddChild(layer);
    }

    private MultiMesh AddMultiMesh(Mesh mesh)
    {
        var multi = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = mesh,
        };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = multi,
            MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true },
        });
        return multi;
    }

    // ---- Camera ------------------------------------------------------------------------------

    /// <summary>Fixed isometric view: 30 degrees down, 45 degrees round.</summary>
    private static readonly Vector3 CameraOffset = new(245, 200, 245);

    private void PlaceCamera()
    {
        _camera.Position = _focus + CameraOffset;
        _camera.LookAt(_focus, Vector3.Up);
    }

    private void PanCamera(float delta)
    {
        var forward = new Vector3(-1, 0, -1).Normalized();
        var right = new Vector3(1, 0, -1).Normalized();
        Vector3 move = Vector3.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) move += forward;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) move -= forward;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) move += right;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) move -= right;
        if (move == Vector3.Zero) return;

        _focus += move.Normalized() * _camera.Size * 0.8f * delta;
        _focus = new Vector3(
            Mathf.Clamp(_focus.X, 0, _world.MapWidth), 0, Mathf.Clamp(_focus.Z, 0, _world.MapHeight));
        PlaceCamera();
    }

    // ---- Orders ------------------------------------------------------------------------------

    private void OrderArmyTo(Vector2 screenPos)
    {
        Vector3 origin = _camera.ProjectRayOrigin(screenPos);
        Vector3 direction = _camera.ProjectRayNormal(screenPos);
        Vector3? hit = new Plane(Vector3.Up, 0).IntersectsRay(origin, direction);
        if (hit is null) return;

        // Send every combat unit of the human player there in a loose block, Commander stays put.
        var units = _world.Units;
        int slot = 0;
        for (int id = 0; id < units.Count; id++)
        {
            if (!units.Alive[id] || units.Owner[id] != HumanPlayer) continue;
            if (_world.Catalog[units.TypeIndex[id]].IsCommander) continue;
            int col = slot % 20, row = slot / 20;
            slot++;
            var target = new FixVec2(
                ToFix(hit.Value.X + (col - 10) * 4f),
                ToFix(hit.Value.Z + row * 4f));
            _pending.Add(new Command(0, HumanPlayer, CommandType.Move, id, target));
        }
    }

    // ---- Drawing -----------------------------------------------------------------------------

    private void DrawUnits()
    {
        var units = _world.Units;
        var counts = new int[_teamMeshes.Length];

        foreach (MultiMesh mm in _teamMeshes)
            if (mm.InstanceCount < units.Count) mm.InstanceCount = units.Count;

        for (int id = 0; id < units.Count; id++)
        {
            if (!units.Alive[id]) continue;
            int team = _world.Players[units.Owner[id]].Team;
            if (team < 0 || team >= _teamMeshes.Length) continue;

            var def = _world.Catalog[units.TypeIndex[id]];
            float scale = def.IsCommander ? 2.2f : 1f;
            float healthShade = 0.45f + 0.55f * ToFloat(units.Health[id] / def.MaxHealth);
            Color baseColour = def.Faction == "lattice" ? LatticeColour : CrucibleColour;

            var pos = units.Position[id];
            var xform = new Transform3D(Basis.Identity.Scaled(new Vector3(scale, scale, scale)),
                new Vector3(ToFloat(pos.X), scale, ToFloat(pos.Y)));

            MultiMesh mm = _teamMeshes[team];
            int i = counts[team]++;
            mm.SetInstanceTransform(i, xform);
            mm.SetInstanceColor(i, baseColour * healthShade);
        }

        for (int t = 0; t < _teamMeshes.Length; t++)
            _teamMeshes[t].VisibleInstanceCount = counts[t];

        var wrecks = _world.Wrecks;
        if (_wreckMesh.InstanceCount < wrecks.Count) _wreckMesh.InstanceCount = wrecks.Count * 2;
        int w = 0;
        foreach (var wreck in wrecks)
        {
            if (wreck.Gone) continue;
            _wreckMesh.SetInstanceTransform(w, new Transform3D(Basis.Identity,
                new Vector3(ToFloat(wreck.Position.X), 0.3f, ToFloat(wreck.Position.Y))));
            _wreckMesh.SetInstanceColor(w, WreckColour);
            w++;
        }
        _wreckMesh.VisibleInstanceCount = w;
    }

    private void DrawHud()
    {
        var p = _world.Players[HumanPlayer];
        var alive = new int[2];
        for (int id = 0; id < _world.Units.Count; id++)
            if (_world.Units.Alive[id]) alive[_world.Units.Owner[id]]++;

        int seconds = _world.Tick / World.TicksPerSecond;
        string status = _world.WinningTeam() switch
        {
            0 => "  VICTORY: the Crucible Commander is down",
            1 => "  DEFEAT: your Commander is down",
            _ => _paused ? "  PAUSED" : "",
        };

        _hud.Text =
            $"{seconds / 60}:{seconds % 60:D2}{status}\n" +
            $"Metal {ToFloat(p.Metal):0} / {ToFloat(p.MetalStorage):0}   " +
            $"Energy {ToFloat(p.Energy):0} / {ToFloat(p.EnergyStorage):0}\n" +
            $"Lattice {alive[0]}   Crucible {alive[1]}   Wrecks {_world.Wrecks.Count(x => !x.Gone)}\n" +
            "WASD pan · wheel zoom · right-click move · Space pause";
    }

    // Floats are fine out here in the view; they never flow back into the sim.
    private static float ToFloat(Fix64 v) => v.Raw / (float)Fix64.OneRaw;
    private static Fix64 ToFix(float v) => Fix64.FromRaw((long)(v * Fix64.OneRaw));
}
