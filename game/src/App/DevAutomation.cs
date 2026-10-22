using System;
using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Game.Match;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Match;
using Godot;

namespace ContingencyZero.Game.App;

/// <summary>
/// Command-line hooks for automated checks (CI smoke tests and screenshots). Ignored in normal play.
/// Pass after a double dash, for example:
///   --path game -- --dev-skirmish=proving_grounds --dev-shots=/tmp/shots --dev-seconds=40
///   --dev-menu --dev-shots=/tmp/shots
/// Screenshots are taken at a few fixed moments, then the game quits.
/// </summary>
public partial class DevAutomation : Node
{
    private readonly Dictionary<string, string> _args;
    private string _shots = "";
    private double _elapsed;
    private int _shot;
    private double[] _shotTimes = Array.Empty<double>();
    private double _quitAt;
    private MatchScene? _match;

    private DevAutomation(Dictionary<string, string> args) { _args = args; }

    public static DevAutomation? FromCommandLine(GameRoot root)
    {
        var args = new Dictionary<string, string>();
        foreach (string a in OS.GetCmdlineUserArgs())
        {
            if (!a.StartsWith("--dev")) continue;
            int eq = a.IndexOf('=');
            args[eq < 0 ? a : a[..eq]] = eq < 0 ? "" : a[(eq + 1)..];
        }
        return args.Count == 0 ? null : new DevAutomation(args);
    }

    private string Arg(string name, string fallback = "") => _args.TryGetValue(name, out var v) ? v : fallback;

    public override void _Ready()
    {
        _shots = Arg("--dev-shots");
        double seconds = double.Parse(Arg("--dev-seconds", "20"), System.Globalization.CultureInfo.InvariantCulture);
        _quitAt = seconds;
        _shotTimes = Arg("--dev-shot-times", "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        if (_shotTimes.Length == 0) _shotTimes = new[] { seconds * 0.3, seconds * 0.6, seconds * 0.95 };

        GameRoot root = GameRoot.Instance;
        if (_args.ContainsKey("--dev-menu"))
        {
            root.ShowMainMenu();
        }
        else if (_args.TryGetValue("--dev-screen", out var screen))
        {
            root.ShowMainMenu();
            switch (screen)
            {
                case "skirmish": root.ShowSkirmishSetup(); break;
                case "campaign": root.ShowCampaign(); break;
                case "replays": root.ShowReplays(); break;
                case "multiplayer": root.ShowMultiplayer(); break;
                case "editor": root.ShowEditor(); break;
            }
        }
        else if (_args.ContainsKey("--dev-replay"))
        {
            // Plays the newest saved replay, fast-forwarded by --dev-fastforward seconds.
            using var dir = DirAccess.Open(GameData.ReplaysDir);
            string? newest = dir?.GetFiles().Where(f => f.EndsWith(".czr")).OrderByDescending(f => f, StringComparer.Ordinal).FirstOrDefault();
            if (newest is not null)
            {
                byte[] bytes = Godot.FileAccess.GetFileAsBytes($"{GameData.ReplaysDir}/{newest}");
                using var ms = new System.IO.MemoryStream(bytes);
                var replay = ContingencyZero.Sim.Replay.MatchReplay.Read(ms);
                root.StartReplay(replay, replay.Title);
                _match = root.GetNodeOrNull<MatchScene>("Match");
                if (_match is not null) _match.Session.Speed = (double.Parse(Arg("--dev-replay-speed", "1"), System.Globalization.CultureInfo.InvariantCulture));
                GD.Print($"DEV: replaying {newest}, {replay.TickCount} ticks, final hash {replay.FinalHash:X16}");
            }
        }
        else if (_args.TryGetValue("--dev-mission", out var missionKey))
        {
            var mission = Missions.MissionInfo.LoadAll().First(m => m.Key == missionKey);
            root.StartMission(mission);
            _match = root.GetNodeOrNull<MatchScene>("Match");
            int ff = int.Parse(Arg("--dev-fastforward", "0"));
            if (_match is not null && ff > 0)
            {
                for (int i = 0; i < ff * 15; i++) _match.Session.World.Step();
                _match.Units.Snapshot();
                _match.Units.Snapshot();
            }
        }
        else if (_args.TryGetValue("--dev-skirmish", out var mapKey))
        {
            var entry = GameData.Maps().First(m => m.Key == mapKey);
            var map = entry.Load();
            var players = new List<PlayerSetup>
            {
                new("You", 0, Arg("--dev-faction", "lattice"), 0, Arg("--dev-self-ai") == "" ? AiDifficulty.None : AiDifficulty.Hard),
                new("Enemy", 1, "crucible", 1, AiDifficulty.Normal),
            };
            root.StartSkirmish(map, players, new MatchOptions(5000, 5000));
            _match = root.GetNodeOrNull<MatchScene>("Match");
            int ff = int.Parse(Arg("--dev-fastforward", "0"));
            if (_match is not null && ff > 0)
            {
                for (int i = 0; i < ff * 15; i++) _match.Session.World.Step();
                _match.Units.Snapshot();
                _match.Units.Snapshot();
            }
            if (_match is not null && Arg("--dev-zoom") is { Length: > 0 } z)
                _match.Rig.SetZoom(float.Parse(z, System.Globalization.CultureInfo.InvariantCulture));
            // --dev-look=x,z points the camera somewhere; --dev-look=fight finds a unit that's shooting.
            if (_match is not null && Arg("--dev-look") is { Length: > 0 } look)
            {
                var w = _match.Session.World;
                if (look == "fight")
                {
                    for (int id = 0; id < w.Units.Count; id++)
                        if (w.Units.Alive[id] && w.Units.Target0[id] >= 0 && !w.Catalog[w.Units.TypeIndex[id]].IsStructure)
                        {
                            _match.Rig.JumpTo(new Vector3(w.Units.Position[id].X.Raw / 65536f, 0, w.Units.Position[id].Y.Raw / 65536f));
                            break;
                        }
                }
                else
                {
                    var xz = look.Split(',');
                    _match.Rig.JumpTo(new Vector3(float.Parse(xz[0], System.Globalization.CultureInfo.InvariantCulture), 0,
                        float.Parse(xz[1], System.Globalization.CultureInfo.InvariantCulture)));
                }
            }
        }

        // ---- Multiplayer test hooks: --dev-host, --dev-join, --dev-net-* (see Net/NetDevDriver.cs) ----
        if (Net.NetDevDriver.Wants(_args)) AddChild(new Net.NetDevDriver(_args) { Name = "NetDev" });

        // ---- Map editor checks: --dev-editor-script=<name> (see MapEditor/EditorDevScript.cs; pass a long --dev-seconds) ----
        if (_args.TryGetValue("--dev-editor-script", out var editorScript)) AddChild(new MapEditor.EditorDevScript(editorScript, _shots) { Name = "EditorDev" });
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_shot < _shotTimes.Length && _elapsed >= _shotTimes[_shot])
        {
            if (_shots.Length > 0)
            {
                DirAccess.MakeDirRecursiveAbsolute(_shots);
                Image img = GetViewport().GetTexture().GetImage();
                string path = $"{_shots}/shot_{_shot}.png";
                img.SavePng(path);
                GD.Print($"DEV: saved {path}");
            }
            _shot++;
            if (_args.ContainsKey("--dev-select-all") && _match is not null)
            {
                _match.Selection.Set(_match.Input.OwnUnits(_ => true));
            }
            // --dev-orders: send our mobile units to the middle of the map, as a player would.
            if (_args.ContainsKey("--dev-orders") && _match is not null)
            {
                var w = _match.Session.World;
                var centre = FixVec2.FromInts(w.MapWidth / 2, w.MapHeight / 2);
                foreach (int id in _match.Input.OwnUnits(d => d.CanMove && !d.IsCommander))
                    _match.Session.Issue(new Command(0, 0, CommandType.Fight, id, centre));
            }
        }
        if (_elapsed >= _quitAt)
        {
            if (_args.ContainsKey("--dev-save-replay") && _match is not null)
            {
                string? saved = _match.SaveReplay();
                GD.Print($"DEV: replay {saved}, tick {_match.Session.World.Tick}, hash {_match.Session.World.ComputeHash():X16}");
            }
            GD.Print("DEV: done");
            GetTree().Quit();
        }
    }
}
