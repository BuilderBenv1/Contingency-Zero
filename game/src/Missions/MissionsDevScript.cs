using System;
using System.Globalization;
using System.Linq;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Match;
using ContingencyZero.Game.UI;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Match;
using Godot;

namespace ContingencyZero.Game.Missions;

/// <summary>
/// Dev checks for co-op and survival (--dev-missions-script=NAME, screenshots from DevAutomation's
/// --dev-shots / --dev-shot-times / --dev-seconds). Ignored in normal play.
///   survival        single-player survival: --dev-survival-map=key (crater_basin), --dev-survival-difficulty=normal,
///                   --dev-survival-allies=N computer co-defenders, --dev-fastforward=S runs the sim ahead,
///                   --dev-survival-kill-at=S destroys your
///                   Commanders S seconds in (to reach the game-over screen quickly)
///   campaign-coop   the campaign screen on its Co-op list
///   skirmish        the skirmish setup with Survival picked
///   coop-solo       --dev-coop=coop_01: a co-op mission alone with a computer ally, fast-forwarded by --dev-fastforward
/// </summary>
public partial class MissionsDevScript : Node
{
    private readonly string _script;
    private readonly System.Collections.Generic.Dictionary<string, string> _args;
    private double _elapsed;
    private bool _killed;
    private MatchScene? _match;

    public MissionsDevScript(string script, System.Collections.Generic.Dictionary<string, string> args)
    {
        _script = script;
        _args = args;
    }

    private string Arg(string name, string fallback = "") => _args.TryGetValue(name, out var v) && v.Length > 0 ? v : fallback;
    private double Num(string name, double fallback) =>
        double.TryParse(Arg(name), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : fallback;

    public override void _Ready()
    {
        GameRoot root = GameRoot.Instance;
        switch (_script)
        {
            case "campaign-coop":
                CampaignScreen.ShowCoop = true;
                root.ShowCampaign();
                break;

            case "skirmish":
                root.ShowSkirmishSetup();
                Callable.From(() =>
                {
                    if (root.FindChild("GameType", true, false) is OptionButton type)
                    {
                        type.Select(1);
                        type.EmitSignal(OptionButton.SignalName.ItemSelected, 1);
                    }
                }).CallDeferred();
                break;

            case "coop-solo":
            {
                MissionInfo mission = MissionInfo.LoadAll().First(m => m.Key == Arg("--dev-coop", "coop_01"));
                root.StartMission(mission);
                _match = root.GetNodeOrNull<MatchScene>("Match");
                FastForward();
                Print($"co-op solo {mission.Key}: players {string.Join(", ", _match!.Session.Scenario.Players.Select(p => $"{p.Name}/{p.Ai}"))}");
                break;
            }

            case "survival":
            {
                var entry = GameData.Maps().First(m => m.Key == Arg("--dev-survival-map", "crater_basin"));
                AiDifficulty difficulty = Enum.TryParse(Arg("--dev-survival-difficulty", "Normal"), true, out AiDifficulty d) ? d : AiDifficulty.Normal;
                SurvivalGame.Start(entry.Load(), difficulty, 1000, Settings.Current.LastFaction, (int)Num("--dev-survival-allies", 0));
                _match = root.GetNodeOrNull<MatchScene>("Match");
                if (_match is null) break;
                FastForward();
                World w = _match.Session.World;
                Print($"survival on {w.Map.Name}, {difficulty}: tick {w.Tick}, wave {w.Mission!.Wave}, hash {w.ComputeHash():X16}");
                // Look at the base (--dev-survival-look=wave: the newest wave instead).
                bool wave = Arg("--dev-survival-look") == "wave";
                for (int id = w.Units.Count - 1; id >= 0; id--)
                    if (w.Units.Alive[id] && (wave ? w.Units.Owner[id] == w.Players.Length - 1
                            : w.Units.Owner[id] == _match.Session.LocalPlayer && w.Catalog[w.Units.TypeIndex[id]].IsCommander))
                    {
                        _match.Rig.JumpTo(new Vector3(w.Units.Position[id].X.Raw / 65536f, 0, w.Units.Position[id].Y.Raw / 65536f));
                        break;
                    }
                break;
            }
        }
    }

    private void FastForward()
    {
        int ff = (int)Num("--dev-fastforward", 0);
        if (_match is null || ff <= 0) return;
        for (int i = 0; i < ff * World.TicksPerSecond && !_match.Session.World.IsOver; i++) _match.Session.World.Step();
        _match.Units.Snapshot();
        _match.Units.Snapshot();
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_match is null || !IsInstanceValid(_match)) return;
        World w = _match.Session.World;
        double killAt = Num("--dev-survival-kill-at", -1);
        if (!_killed && killAt > 0 && _elapsed >= killAt)
        {
            _killed = true;
            // Dev only: end the match the quick way so the game-over screen can be checked.
            for (int id = 0; id < w.Units.Count; id++)
                if (w.Units.Alive[id] && w.Units.Owner[id] == _match.Session.LocalPlayer && w.Catalog[w.Units.TypeIndex[id]].IsCommander)
                    w.Kill(id);
            Print($"killed the local Commander at tick {w.Tick}");
        }
        if (w.IsOver && SurvivalScores.Record(w) is { } r && !_reported)
        {
            _reported = true;
            Print($"survival over: {r.Score.Waves} waves, {r.Score.Seconds} s, {r.Score.Points} points, new best {r.NewBest}");
        }
    }

    private bool _reported;

    private static void Print(string text) => GD.Print($"MISSIONSDEV {text}");
}
