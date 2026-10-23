using System;
using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Match;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.State;
using Godot;

namespace ContingencyZero.Game.View;

/// <summary>
/// Dev checks for craters, rubble, salvage, area orders and rally rotations:
/// <c>--dev-sim-script=orders|salvage|ruins</c> on top of <c>--dev-showcase</c> (with <c>--dev-shots=&lt;dir&gt;</c>
/// and a long <c>--dev-seconds</c>). Orders go through the session like a player's; the salvage
/// script runs the sim ahead first so the reclaiming is done by the time it looks.
/// </summary>
public partial class SimFeaturesDevScript : Node
{
    private readonly string _script;
    private readonly string _shots;
    private readonly List<(double At, Action Act)> _steps = new();
    private double _t;
    private int _next;

    public SimFeaturesDevScript(string script, string shots)
    {
        _script = script;
        _shots = shots;
    }

    private static MatchScene? Match => GameRoot.Instance.GetChildren().OfType<MatchScene>().LastOrDefault(m => !m.IsQueuedForDeletion());

    private void At(double t, Action a) => _steps.Add((t, a));

    private void Shot(string name)
    {
        if (_shots.Length == 0) return;
        DirAccess.MakeDirRecursiveAbsolute(_shots);
        string path = $"{_shots}/{name}.png";
        GetViewport().GetTexture().GetImage().SavePng(path);
        Log($"saved {path}");
    }

    private static void Log(string s) => GD.Print("DEV-SIM: " + s);

    public override void _Ready()
    {
        switch (_script)
        {
            case "orders": Orders(); break;
            case "salvage": Salvage(); break;
            case "ruins": Ruins(); break;
            default: Log($"unknown script {_script}"); break;
        }
    }

    public override void _Process(double delta)
    {
        _t += delta;
        while (_next < _steps.Count && _t >= _steps[_next].At)
        {
            try { _steps[_next].Act(); }
            catch (Exception e) { Log($"step {_next} failed: {e}"); }
            _next++;
        }
    }

    private static List<int> Own(World w, Func<int, bool> pick)
    {
        var list = new List<int>();
        for (int id = 0; id < w.Units.Count; id++)
            if (w.Units.IsValid(id) && w.Units.Owner[id] == 0 && pick(id)) list.Add(id);
        return list;
    }

    private static Vector3 V(World w, FixVec2 p, TerrainView t)
    {
        float x = p.X.Raw / 65536f, z = p.Y.Raw / 65536f;
        return new Vector3(x, t.SurfaceAt(x, z), z);
    }

    /// <summary>A factory with four rally stops, tanks with queued area orders, and an area drag in progress.</summary>
    private void Orders()
    {
        At(1.0, () =>
        {
            MatchScene m = Match!;
            World w = m.Session.World;
            int plant = Own(w, id => w.Catalog[w.Units.TypeIndex[id]].Role == "factory_vehicle" || w.Catalog[w.Units.TypeIndex[id]].Id.EndsWith("vehicle_plant")).FirstOrDefault(-1);
            if (plant < 0) { Log("no vehicle plant"); return; }
            FixVec2 p = w.Units.Position[plant];
            var stops = new[] { (0, 40), (-60, 30), (60, 34), (0, 80) };
            for (int i = 0; i < stops.Length; i++)
                m.Session.Issue(new Command(0, 0, CommandType.SetRally, plant, p + FixVec2.FromInts(stops[i].Item1, stops[i].Item2), Queue: i > 0));
            var tanks = Own(w, id => w.Catalog[w.Units.TypeIndex[id]].Id == "lattice_strider").Take(5).ToList();
            foreach (int t in tanks)
            {
                m.Session.Issue(new Command(0, 0, CommandType.Stop, t));
                m.Session.Issue(new Command(0, 0, CommandType.AttackArea, t, p + FixVec2.FromInts(-40, 120), Arg: 26, Queue: true));
                m.Session.Issue(new Command(0, 0, CommandType.AttackArea, t, p + FixVec2.FromInts(50, 140), Arg: 20, Queue: true));
            }
            m.Selection.Set(tanks.Append(plant));
            m.Rig.JumpTo(V(w, p + FixVec2.FromInts(0, 70), m.Terrain));
            // A player dragging out an area-reclaim circle with R.
            m.Overlay.DragCircle = (V(w, p + FixVec2.FromInts(80, 60), m.Terrain), 22f, new Color(0.8f, 0.5f, 1f, 0.8f));
        });
        At(2.5, () => Shot("orders"));
        At(2.6, () =>
        {
            // The Commander fires the Breach Cannon across open ground: a glassed streak and scar.
            MatchScene m = Match!;
            World w = m.Session.World;
            int cmdr = Own(w, id => w.Catalog[w.Units.TypeIndex[id]].IsCommander).FirstOrDefault(-1);
            if (cmdr < 0) return;
            m.Session.Issue(new Command(0, 0, CommandType.DGun, cmdr, w.Units.Position[cmdr] + FixVec2.FromInts(30, 18)));
            m.Selection.Set(new[] { cmdr });
            m.Overlay.DragCircle = null;
            m.Rig.JumpTo(V(w, w.Units.Position[cmdr] + FixVec2.FromInts(15, 10), m.Terrain));
            m.Rig.SetZoom(55);
        });
        At(7.5, () => Shot("dgun"));
    }

    /// <summary>Our army storms the Crucible base (which holds its fire): craters, foundations, rubble and burns.</summary>
    private void Ruins()
    {
        At(0.8, () =>
        {
            MatchScene m = Match!;
            World w = m.Session.World;
            var orders = new List<Command>();
            FixVec2 baseCentre = FixVec2.FromInts(256, 512 - 105);
            for (int id = 0; id < w.Units.Count; id++)
            {
                if (!w.Units.IsValid(id)) continue;
                var d = w.Catalog[w.Units.TypeIndex[id]];
                if (w.Units.Owner[id] == 1)
                {
                    orders.Add(new Command(w.Tick, 1, CommandType.SetFireState, id, Arg: 0));
                    if (d.CanMove) orders.Add(new Command(w.Tick, 1, CommandType.Move, id, FixVec2.FromInts(40 + id % 40 * 10, 500)));
                }
                else if (d.IsArmed && d.CanMove && !d.IsCommander)
                {
                    orders.Add(new Command(w.Tick, 0, CommandType.AttackArea, id, baseCentre, Arg: 120));
                }
            }
            w.Step(orders);
            for (int i = 0; i < 15 * 110; i++) w.Step();
            m.Units.Snapshot();
            int rubble = w.Features.Count(f => !f.Gone && f.Kind == FeatureKind.Rubble);
            Log($"tick {w.Tick}, terrain revision {w.TerrainRevision}, rubble {rubble}");
            m.Rig.JumpTo(V(w, FixVec2.FromInts(250, 430), m.Terrain));
            m.Rig.SetZoom(120);
        });
        At(3.0, () => Shot("ruins"));
        At(3.2, () =>
        {
            MatchScene m = Match!;
            m.Rig.JumpTo(V(m.Session.World, FixVec2.FromInts(256, 470), m.Terrain));
            m.Rig.SetZoom(60);
        });
        At(5.0, () => Shot("ruins_close"));
    }

    /// <summary>After the battle: the Commander clears the field, salvaging enemy tanks; then the plant's menu and a wreck's label.</summary>
    private void Salvage()
    {
        At(0.8, () =>
        {
            MatchScene m = Match!;
            World w = m.Session.World;
            // Let the armies clash, then the Crucible pulls back and holds fire while we sweep the field.
            for (int i = 0; i < 15 * 40; i++) w.Step();
            var orders = new List<Command>();
            for (int id = 0; id < w.Units.Count; id++)
            {
                if (!w.Units.IsValid(id) || w.Units.Owner[id] != 1) continue;
                orders.Add(new Command(w.Tick, 1, CommandType.SetFireState, id, Arg: 0));
                if (w.Catalog[w.Units.TypeIndex[id]].CanMove) orders.Add(new Command(w.Tick, 1, CommandType.Move, id, FixVec2.FromInts(40 + id % 40 * 10, 500)));
            }
            int cmdr = Own(w, id => w.Catalog[w.Units.TypeIndex[id]].IsCommander).FirstOrDefault(-1);
            var builders = Own(w, id => w.Catalog[w.Units.TypeIndex[id]].CanReclaim && w.Catalog[w.Units.TypeIndex[id]].CanMove);
            // Run ahead with the builders sweeping the field (same path a player's orders take: commands into the step).
            orders.AddRange(builders.Select(b => new Command(w.Tick, 0, CommandType.ReclaimArea, b, FixVec2.FromInts(256, 256), Arg: 150)));
            w.Step(orders);
            int anvil = w.Catalog.IndexOf("crucible_anvil");
            for (int i = 0; i < 15 * 240 && !w.HasSalvaged(0, anvil); i++) w.Step();
            Log($"anvil salvage {w.Players[0].SalvageProgress[anvil]}/{w.SalvageNeeded(anvil)}, cmdr {cmdr}, builders {builders.Count}");
            m.Units.Snapshot();
            int plant = Own(w, id => w.Catalog[w.Units.TypeIndex[id]].Id.EndsWith("vehicle_plant")).FirstOrDefault(-1);
            if (plant >= 0)
            {
                m.Selection.Set(new[] { plant });
                m.Rig.JumpTo(V(w, w.Units.Position[plant], m.Terrain));
            }
            // Point at an enemy wreck that's still lying there.
            for (int f = w.Features.Count - 1; f >= 0; f--)
            {
                Feature ft = w.Features[f];
                if (ft.Gone || ft.Kind != FeatureKind.Wreck || ft.SourceTypeIndex < 0) continue;
                if (!w.IsSalvageable(0, w.Catalog[ft.SourceTypeIndex])) continue;
                m.Overlay.HoverFeature = f;
                if (plant < 0) m.Rig.JumpTo(V(w, ft.Position, m.Terrain));
                Log($"hover wreck {f} {w.Catalog[ft.SourceTypeIndex].Id}");
                break;
            }
        });
        At(2.5, () => Shot("salvage"));
        At(2.7, () =>
        {
            MatchScene m = Match!;
            World w = m.Session.World;
            int hover = m.Overlay.HoverFeature;
            if (hover >= 0) m.Rig.JumpTo(V(w, w.Features[hover].Position, m.Terrain));
        });
        At(4.0, () => Shot("salvage_wreck"));
    }
}
