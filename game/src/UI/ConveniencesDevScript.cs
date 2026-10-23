using System;
using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Match;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using Godot;

namespace ContingencyZero.Game.UI;

/// <summary>
/// Dev checks for build templates, economy readouts, replay bookmarks / timeline, spectating with
/// the casting panel and share codes: <c>--dev-conv-script=template|replay|spectate|share</c>
/// (with <c>--dev-shots=&lt;dir&gt;</c> and a long <c>--dev-seconds</c>). Each step runs at a fixed time
/// and may save a named screenshot. Used with the other dev flags, e.g. template wants
/// <c>--dev-skirmish=proving_grounds --dev-self-ai=1 --dev-fastforward=300</c> and replay wants <c>--dev-replay</c>.
/// </summary>
public partial class ConveniencesDevScript : Node
{
    private readonly string _script;
    private readonly string _shots;
    private readonly List<(double At, Action Act)> _steps = new();
    private double _t;
    private int _next;

    public ConveniencesDevScript(string script, string shots)
    {
        _script = script;
        _shots = shots;
    }

    private static MatchScene? Match => GameRoot.Instance.GetChildren().OfType<MatchScene>().LastOrDefault(m => !m.IsQueuedForDeletion());
    private Viewport Vp => GetViewport();

    private void At(double t, Action a) => _steps.Add((t, a));

    private void Shot(string name)
    {
        if (_shots.Length == 0) return;
        DirAccess.MakeDirRecursiveAbsolute(_shots);
        string path = $"{_shots}/{name}.png";
        Vp.GetTexture().GetImage().SavePng(path);
        GD.Print($"DEV-CONV: saved {path}");
    }

    private static void Log(string s) => GD.Print("DEV-CONV: " + s);

    /// <summary>Every descendant of a C# type (FindChildren's type filter only knows engine classes).</summary>
    private static IEnumerable<T> All<T>(Node root) where T : class
    {
        foreach (Node c in root.GetChildren(true))
        {
            if (c is T t) yield return t;
            foreach (T d in All<T>(c)) yield return d;
        }
    }

    public override void _Ready()
    {
        switch (_script)
        {
            case "template": Template(); break;
            case "replay": Replay(); break;
            case "spectate": Spectate(); break;
            case "share": Share(); break;
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

    // ---- Templates and economy ---------------------------------------------------------------

    private void Template()
    {
        Vector2 aim = Vector2.Zero;
        At(1.0, () =>
        {
            MatchScene m = Match!;
            var w = m.Session.World;
            int me = m.Session.LocalPlayer;
            int cmd = m.Input.OwnUnits(d => d.IsCommander).First();
            var c = w.Units.Position[cmd];
            // The structures around our start make the template.
            var structures = m.Input.OwnUnits(d => d.IsStructure).Where(id => w.Units.Complete[id]
                && ContingencyZero.Sim.Core.FixVec2.Distance(w.Units.Position[id], c).ToIntFloor() < 140).ToList();
            BuildTemplate? t = BuildTemplate.FromStructures(w, structures, me, "Dev base");
            if (t is null) { Log("no structures to template"); return; }
            TemplateStore.Add(t);
            Log($"template '{t.Name}': {t.Items.Count} items, roles {string.Join(",", t.Items.Select(i => i.Role))}");
            m.Selection.Set(m.Input.OwnUnits(d => d.CanBuild && d.CanMove));
            m.Input.BeginTemplatePlacement(t);
            aim = Vp.GetVisibleRect().Size * new Vector2(0.62f, 0.42f);
            SyntheticInput.MouseMove(Vp, aim, new Vector2(1, 0));
        });
        At(1.3, () => SyntheticInput.MouseMove(Vp, aim + new Vector2(2, 0), new Vector2(1, 0)));
        At(2.0, () => { Shot("template_ghost"); Log("hint: " + Match!.Input.HintText()); });
        At(2.3, () => { Match!.Input.RotateTemplate(); SyntheticInput.MouseMove(Vp, aim, new Vector2(-1, 0)); });
        At(3.0, () => Shot("template_rotated"));
        At(3.3, () =>
        {
            SyntheticInput.MouseButton(Vp, aim, MouseButton.Left, true);
            SyntheticInput.MouseButton(Vp, aim, MouseButton.Left, false);
        });
        At(4.5, () =>
        {
            MatchScene m = Match!;
            var w = m.Session.World;
            int b = m.Selection.Ids.FirstOrDefault(-1);
            if (b >= 0)
                Log($"builder {b} orders: {string.Join(", ", w.Units.Orders[b].Select(o => o.TypeIndex >= 0 ? w.Catalog[o.TypeIndex].Role : o.Type.ToString()))}");
            m.Input.SetMode(ClickMode.Select);
            Shot("template_stamped");
            // Economy readouts, with the metal meter's detail tooltip text logged.
            foreach (var meter in All<ResourceMeter>(m.Hud!))
                Log($"meter forecast '{meter.ForecastText}' tooltip:\n{meter.TooltipText}");
        });
    }

    // ---- Replay timeline and bookmarks -------------------------------------------------------

    private void Replay()
    {
        ReplayControls Controls() => All<ReplayControls>(Match!).First();
        At(1.0, () =>
        {
            MatchScene m = Match!;
            m.GetNodeOrNull<CastingOverlay>("Casting")!.Shown = true;
            ReplayTimeline? tl = Controls().Timeline;
            Log($"timeline markers: {tl?.GetType().Name}");
        });
        At(1.6, () =>
        {
            ReplayTimeline tl = Controls().Timeline!;
            // Hover the second-to-last marker so its highlight shows.
            int n = Count();
            if (n > 0) SyntheticInput.MouseMove(Vp, tl.MarkerPosition(Math.Max(0, n - 2)), new Vector2(1, 0));
        });
        At(2.2, () => Shot("replay_timeline"));
        At(2.5, () =>
        {
            ReplayTimeline tl = Controls().Timeline!;
            int n = Count();
            if (n == 0) { Log("replay has no bookmarks"); return; }
            Vector2 p = tl.MarkerPosition(n - 1);
            SyntheticInput.MouseButton(Vp, p, MouseButton.Left, true);
            SyntheticInput.MouseButton(Vp, p, MouseButton.Left, false);
        });
        At(2.6, () => Shot("replay_seeking"));
        At(7.0, () => { Shot("replay_after_jump"); Log($"tick now {Match!.Session.World.Tick}"); });
        At(7.5, () => Controls().JumpToPrevious()); // backwards: rebuilds the replay and seeks
        At(7.9, () => Shot("replay_seek_back"));
        At(14.0, () => { Shot("replay_after_back"); Log($"tick now {Match!.Session.World.Tick}, casting shown {Match!.GetNodeOrNull<CastingOverlay>("Casting")?.Shown}"); });
    }

    private int Count() =>
        All<ReplayControls>(Match!).First().Timeline is null ? 0 : ReplayMarks();

    private int ReplayMarks()
    {
        // The timeline doesn't expose its list; count via the replay channel's owner bookmarks in the browser format.
        var ctl = All<ReplayControls>(Match!).First();
        var field = typeof(ReplayControls).GetField("_replay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (field?.GetValue(ctl) as ContingencyZero.Sim.Replay.MatchReplay)?.Bookmarks.Count ?? 0;
    }

    // ---- Spectating ----------------------------------------------------------------------------

    private void Spectate()
    {
        At(0.5, () =>
        {
            MapData map = GameData.Maps().First(m => m.Key == "crater_basin").Load();
            var players = new List<PlayerSetup>();
            for (int i = 0; i < Math.Min(4, map.StartPositions.Count); i++)
                players.Add(new PlayerSetup($"{(i % 2 == 0 ? "Lattice" : "Crucible")} AI {i + 1}", i % 2, i % 2 == 0 ? "lattice" : "crucible", i, AiDifficulty.Hard));
            SkirmishSetup.StartSpectating(map, players, new MatchOptions(3000, 3000));
        });
        At(1.0, () =>
        {
            MatchScene m = Match!;
            var w = m.Session.World;
            for (int i = 0; i < 540 * 15; i++) { w.Step(); m.Bookmarks.OnTick(w); }
            m.Units.Snapshot();
            m.Units.Snapshot();
            m.GetNodeOrNull<CastingOverlay>("Casting")!.Shown = true;
            m.Rig.JumpTo(new Vector3(w.Map.Width / 2f, 0, w.Map.Height / 2f));
            Log($"spectating: local player {m.Session.LocalPlayer}, {m.Bookmarks.Items.Count} bookmarks so far: {string.Join(" | ", m.Bookmarks.Items.Select(b => b.Text))}");
        });
        At(4.0, () => Shot("spectate_casting"));
        At(4.3, () =>
        {
            MatchScene m = Match!;
            var vision = m.FindChildren("Vision", nameof(OptionButton), true, false).OfType<OptionButton>().First();
            vision.Select(2);
            vision.EmitSignal(OptionButton.SignalName.ItemSelected, 2L);
            m.Session.Speed = 4;
        });
        At(6.0, () => Shot("spectate_vision_p2"));
        At(6.5, () => Log($"saved replay {Match!.SaveReplay()} with {Match!.Bookmarks.Items.Count} bookmarks"));
    }

    // ---- Share codes -------------------------------------------------------------------------------

    private void Share()
    {
        string replayCode = "";
        At(1.0, () =>
        {
            var browser = All<ReplayBrowser>(GameRoot.Instance).FirstOrDefault();
            var button = browser?.FindChildren("CopyShareCode", nameof(Button), true, false).OfType<Button>().FirstOrDefault();
            button?.EmitSignal(BaseButton.SignalName.Pressed);
            var dlg = browser?.FindChildren("ShareCodeDialog", "", true, false).OfType<ShareCodeDialog>().FirstOrDefault();
            replayCode = dlg?.Code ?? "";
            Log($"replay code {replayCode.Length} chars, starts {(replayCode.Length > 20 ? replayCode[..20] : replayCode)}; clipboard matches: {DisplayServer.ClipboardGet() == replayCode}");
        });
        At(1.8, () => Shot("share_replay_code"));
        At(2.0, () =>
        {
            var browser = All<ReplayBrowser>(GameRoot.Instance).First();
            foreach (var d in browser.FindChildren("ShareCodeDialog", "", true, false)) d.QueueFree();
            browser.PasteCode();
        });
        At(2.6, () => Shot("share_replay_paste"));
        At(2.8, () =>
        {
            var browser = All<ReplayBrowser>(GameRoot.Instance).First();
            var dlg = All<ShareCodeDialog>(browser).Where(x => ((Node)(object)x).Name == "PasteCodeDialog").First();
            // A bad code first, then the real one.
            Log("bad code: " + dlg.DevImport("CZM1:abc"));
            Log("truncated code: " + dlg.DevImport(replayCode[..(replayCode.Length / 2)]));
            Log("good code: " + dlg.DevImport(replayCode));
        });
        At(3.0, () => Shot("share_replay_imported"));
        At(3.5, () =>
        {
            // A long code: just check the warning path.
            var big = new byte[ShareCodes.ComfortableLength];
            new Random(1).NextBytes(big);
            var d = ShareCodeDialog.ShowCode("SHARE REPLAY", "replay", ShareCodes.ReplayPrefix + ShareCodes.ToBase64Url(big));
            GameRoot.Instance.AddChild(d);
        });
        At(4.2, () => Shot("share_long_warning"));
        At(4.5, () =>
        {
            foreach (var d in GameRoot.Instance.FindChildren("ShareCodeDialog", "", true, false)) d.QueueFree();
            GameRoot.Instance.ShowSkirmishSetup();
        });
        At(5.5, () =>
        {
            MapData map = GameData.Maps().First(m => m.Key == "twin_rivers").Load();
            map.Name = "Twin Rivers (shared)";
            string code = ShareCodes.MapCode(map);
            Log($"map code {code.Length} chars; json {map.ToJson().Length} chars");
            // Round trip and damage check.
            Log($"round trip fingerprint ok: {ShareCodes.DecodeMap(code).Fingerprint() == map.Fingerprint()}");
            try { ShareCodes.DecodeMap(code[..^40] + "AAAA" + code[^36..]); Log("damaged map code accepted (bad)"); }
            catch (Exception e) { Log("damaged map code rejected: " + e.Message); }
            DisplayServer.ClipboardSet(code);
            var setup = All<SkirmishSetup>(GameRoot.Instance).First();
            setup.PasteMapCode();
        });
        At(6.2, () => Shot("share_map_paste"));
        At(6.4, () =>
        {
            var setup = All<SkirmishSetup>(GameRoot.Instance).First();
            var dlg = All<ShareCodeDialog>(setup).Where(x => ((Node)(object)x).Name == "PasteCodeDialog").First();
            Log("map import: " + dlg.DevImport(DisplayServer.ClipboardGet()));
        });
        At(8.0, () => Shot("share_map_imported"));
        At(8.5, () =>
        {
            var setup = All<SkirmishSetup>(GameRoot.Instance).First();
            var watch = setup.FindChildren("WatchOnly", nameof(CheckBox), true, false).OfType<CheckBox>().First();
            watch.ButtonPressed = true;
        });
        At(9.0, () => Shot("skirmish_watch_only"));
    }
}
