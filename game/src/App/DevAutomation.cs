using System;
using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Game.Match;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using ContingencyZero.Sim.Replay;
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
        if (Arg("--dev-retro") is { Length: > 0 } retro) View.RetroLook.Forced = retro != "0";
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
        else if (_args.ContainsKey("--dev-showcase"))
        {
            // A set-piece for promo screenshots: two bases on Proving Grounds and two armies ordered
            // to attack-move into each other. Everything after the orders is the normal sim.
            MapData map = GameData.Maps().First(m => m.Key == "proving_grounds").Load();
            var spawns = new List<Spawn>();
            void Side(string f, int owner, int dir)
            {
                int Y(int y) => dir > 0 ? y : 512 - y;
                (string Type, int X, int Y)[] basePieces =
                {
                    ("vehicle_plant", 190, 105), ("bot_lab", 330, 105), ("solar_collector", 130, 60), ("solar_collector", 390, 60),
                    ("wind_turbine", 100, 120), ("wind_turbine", 420, 120), ("heavy_turret", 260, 150), ("light_turret", 180, 165),
                    ("light_turret", 340, 165), ("fusion_reactor", 260, 30), ("radar_tower", 455, 75), ("aa_turret", 75, 75),
                };
                foreach (var (t, x, y) in basePieces)
                    spawns.Add(new Spawn(t == "bot_lab" || t == "vehicle_plant" ? $"{f}_{t}" : $"{f}_{t}", owner, FixVec2.FromInts(x, Y(y))));
                string tank = f == "lattice" ? "lattice_strider" : "crucible_anvil";
                int k = owner * 1000;
                // Loose, uneven groups rather than parade lines.
                int J(int i, int amp) { uint h = (uint)((k + i) * 2654435761u); return (int)(h >> 16) % (2 * amp + 1) - amp; }
                void Add(string type, int x, int y) { k++; spawns.Add(new Spawn(type, owner, FixVec2.FromInts(x + J(k, 7), Y(y + J(k + 77, 6))))); }
                for (int i = 0; i < 12; i++) Add(tank, 150 + i * 19, 212);
                for (int i = 0; i < 6; i++) Add($"{f}_heavy_assault", 175 + i * 30, 200);
                for (int i = 0; i < 14; i++) Add($"{f}_assault_bot", 140 + i * 17, 224);
                for (int i = 0; i < 6; i++) Add($"{f}_artillery_vehicle", 185 + i * 28, 185);
                for (int i = 0; i < 3; i++) Add($"{f}_flak_vehicle", 215 + i * 40, 192);
                for (int i = 0; i < 5; i++) Add($"{f}_gunship", 190 + i * 32, 190);
            }
            Side("lattice", 0, +1);
            Side("crucible", 1, -1);
            var players = new List<PlayerSetup> { new("You", 0, "lattice", 0), new("Crucible", 1, "crucible", 1) };
            var scenario = new Scenario(map, 2026, players, new MatchOptions(5000, 5000, RevealMap: true), spawns);
            var session = new SimSession(scenario, GameData.Catalog, 0, new LocalChannel());
            MatchScene.SuppressHints = true;
            _match = root.StartMatch(session, MatchKind.Skirmish, "Showcase", null);
            var w = session.World;
            var orders = new List<Command>();
            for (int id = 0; id < w.Units.Count; id++)
            {
                var d = w.Catalog[w.Units.TypeIndex[id]];
                if (!d.CanMove || d.CanBuild || !d.IsArmed) continue;
                int owner = w.Units.Owner[id];
                orders.Add(new Command(0, owner, CommandType.Fight, id, FixVec2.FromInts(256, owner == 0 ? 430 : 82)));
            }
            int ff = int.Parse(Arg("--dev-fastforward", "0"));
            w.Step(orders);
            for (int i = 1; i < ff * 15; i++) w.Step();
            _match.Units.Snapshot();
            _match.Units.Snapshot();
            var look = Arg("--dev-look", "256,256").Split(',');
            _match.Rig.JumpTo(new Vector3(float.Parse(look[0], System.Globalization.CultureInfo.InvariantCulture), 0,
                float.Parse(look[1], System.Globalization.CultureInfo.InvariantCulture)));
            if (Arg("--dev-zoom") is { Length: > 0 } sz)
                _match.Rig.SetZoom(float.Parse(sz, System.Globalization.CultureInfo.InvariantCulture));
        }
        else if (_args.ContainsKey("--dev-parade"))
        {
            // Every unit and building of both factions lined up on Proving Grounds, idle and on
            // the same team, for checking models in the game. Lattice rows north, Crucible south.
            MapData map = GameData.Maps().First(m => m.Key == "proving_grounds").Load();
            var spawns = new List<Spawn>();
            var catalog = GameData.Catalog;
            foreach (var (faction, owner) in new[] { ("lattice", 0), ("crucible", 1) })
            {
                var defs = catalog.All.Where(d => d.Faction == faction).OrderBy(d => d.Footprint).ThenBy(d => d.Id, StringComparer.Ordinal).ToList();
                int x = 40;
                foreach (var d in defs.Where(d => !d.IsStructure))
                {
                    spawns.Add(new Spawn(d.Id, owner, FixVec2.FromInts(x, owner == 0 ? 150 : 200)));
                    x += 14;
                }
                x = 30;
                foreach (var d in defs.Where(d => d.IsStructure))
                {
                    int half = d.Footprint * 4;
                    spawns.Add(new Spawn(d.Id, owner, FixVec2.FromInts(x + half, owner == 0 ? 290 : 370)));
                    x += half * 2 + 4;
                }
            }
            var players = new List<PlayerSetup> { new("You", 0, "lattice", 0), new("Crucible", 1, "crucible", 0) };
            var scenario = new Scenario(map, 2026, players, new MatchOptions(5000, 5000, RevealMap: true), spawns);
            var session = new SimSession(scenario, GameData.Catalog, 0, new LocalChannel());
            MatchScene.SuppressHints = true;
            _match = root.StartMatch(session, MatchKind.Skirmish, "Parade", null);
            for (int i = 0; i < 15; i++) session.World.Step();
            _match.Units.Snapshot();
            _match.Units.Snapshot();
            var look = Arg("--dev-look", "200,175").Split(',');
            _match.Rig.JumpTo(new Vector3(float.Parse(look[0], System.Globalization.CultureInfo.InvariantCulture), 0,
                float.Parse(look[1], System.Globalization.CultureInfo.InvariantCulture)));
            if (Arg("--dev-zoom") is { Length: > 0 } sz)
                _match.Rig.SetZoom(float.Parse(sz, System.Globalization.CultureInfo.InvariantCulture));
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
                new("Enemy", 1, "crucible", 1, Arg("--dev-enemy") == "hard" ? AiDifficulty.Hard : AiDifficulty.Normal),
            };
            // --dev-ffa: fill every start position with a Hard computer player, each on its own team.
            if (_args.ContainsKey("--dev-ffa"))
                for (int i = 2; i < map.StartPositions.Count; i++)
                    players.Add(new($"AI {i + 1}", i, i % 2 == 0 ? "lattice" : "crucible", i, AiDifficulty.Hard));
            root.StartSkirmish(map, players, new MatchOptions(5000, 5000, RevealMap: _args.ContainsKey("--dev-reveal")));
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
                if (look.StartsWith("battle"))
                {
                    // Keep playing until some 64-unit square has at least N units shooting (battle:N),
                    // then point the camera at the busiest one.
                    int want = look.Contains(':') ? int.Parse(look.Split(':')[1]) : 1;
                    (int, int) best = default;
                    int bestCount = 0;
                    for (int extra = 0; extra < 600 * 15; extra += 15)
                    {
                        // Score a 96-unit square by its mobile units, but only where two sides meet and shots are flying.
                        var units = new Dictionary<(int, int), int>();
                        var firing = new Dictionary<(int, int), int>();
                        var owners = new Dictionary<(int, int), int>();
                        for (int id = 0; id < w.Units.Count; id++)
                        {
                            if (!w.Units.Alive[id] || w.Catalog[w.Units.TypeIndex[id]].IsStructure) continue;
                            var cell = (w.Units.Position[id].X.ToIntFloor() / 96, w.Units.Position[id].Y.ToIntFloor() / 96);
                            owners[cell] = owners.GetValueOrDefault(cell) | (1 << w.Units.Owner[id]);
                            units[cell] = units.GetValueOrDefault(cell) + 1;
                            if (w.Units.Target0[id] >= 0 || w.Units.Target1[id] >= 0) firing[cell] = firing.GetValueOrDefault(cell) + 1;
                        }
                        var counts = new Dictionary<(int, int), int>();
                        foreach (var (cell, n) in units)
                            if (System.Numerics.BitOperations.PopCount((uint)owners[cell]) >= 2 && firing.GetValueOrDefault(cell) >= 3)
                                counts[cell] = n;
                        if (counts.Count > 0)
                        {
                            var top = counts.OrderByDescending(kv => kv.Value).First();
                            best = top.Key; bestCount = top.Value;
                        }
                        if (bestCount >= want) break;
                        for (int i = 0; i < 15; i++) w.Step();
                    }
                    _match.Units.Snapshot();
                    _match.Units.Snapshot();
                    // Centre on the units shooting in that square.
                    float sx = 0, sz = 0; int shooters = 0;
                    for (int id = 0; id < w.Units.Count; id++)
                    {
                        if (!w.Units.Alive[id] || w.Units.Target0[id] < 0 || w.Catalog[w.Units.TypeIndex[id]].IsStructure) continue;
                        if (w.Units.Position[id].X.ToIntFloor() / 96 != best.Item1 || w.Units.Position[id].Y.ToIntFloor() / 96 != best.Item2) continue;
                        sx += w.Units.Position[id].X.Raw / 65536f; sz += w.Units.Position[id].Y.Raw / 65536f; shooters++;
                    }
                    _match.Rig.JumpTo(shooters > 0 ? new Vector3(sx / shooters, 0, sz / shooters) : new Vector3(best.Item1 * 96 + 48, 0, best.Item2 * 96 + 48));
                    GD.Print($"DEV: battle at {best} with {bestCount} units, {w.Tick / 15 / 60}:{w.Tick / 15 % 60:D2}");
                }
                else if (look == "fight")
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

        // ---- Templates, economy readouts, replay timeline, spectating, share codes: --dev-conv-script=template|replay|spectate|share (see UI/ConveniencesDevScript.cs) ----
        if (_args.TryGetValue("--dev-conv-script", out var convScript)) AddChild(new UI.ConveniencesDevScript(convScript, _shots) { Name = "ConvDev" });

        // ---- Controller checks: --dev-gamepad-script=match|menu (see Input/GamepadDevScript.cs; pass a long --dev-seconds) ----
        if (_args.TryGetValue("--dev-gamepad-script", out var padScript)) AddChild(new Controls.GamepadDevScript(padScript, _shots) { Name = "GamepadDev" });

        // ---- Craters, rubble, salvage, area orders, rally stops: --dev-sim-script=orders|salvage with --dev-showcase (see View/SimFeaturesDevScript.cs) ----
        if (_args.TryGetValue("--dev-sim-script", out var simScript)) AddChild(new View.SimFeaturesDevScript(simScript, _shots) { Name = "SimFeaturesDev" });

        // ---- Co-op and survival: --dev-missions-script=survival|campaign-coop|skirmish|coop-solo (see Missions/MissionsDevScript.cs) ----
        if (_args.TryGetValue("--dev-missions-script", out var missionsScript)) AddChild(new Missions.MissionsDevScript(missionsScript, _args) { Name = "MissionsDev" });
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
            // --dev-select-army: select our combat units in view, as a player would before attacking.
            if (_args.ContainsKey("--dev-select-army") && _match is not null)
            {
                var w = _match.Session.World;
                Vector3 f = _match.Rig.Focus;
                _match.Selection.Set(_match.Input.OwnUnits(d => d.CanMove && d.IsArmed && !d.CanBuild).Where(id =>
                    new Vector2(w.Units.Position[id].X.Raw / 65536f - f.X, w.Units.Position[id].Y.Raw / 65536f - f.Z).Length() < 90));
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
