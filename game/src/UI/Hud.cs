using System;
using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Audio;
using ContingencyZero.Game.Match;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Missions;
using ContingencyZero.Sim.State;
using Godot;

namespace ContingencyZero.Game.UI;

/// <summary>
/// The in-game interface: resource bar along the top, minimap bottom-left, selection details
/// bottom-centre, orders and build menu bottom-right, a message feed, mission objectives, and
/// the box-select rectangle.
/// </summary>
public partial class Hud : CanvasLayer
{
    private SimSession _session = null!;
    private InputController _input = null!;
    private Selection _selection = null!;
    private CameraRig _rig = null!;

    private ResourceMeter _metal = null!;
    private ResourceMeter _energy = null!;
    private Label _clock = null!;
    private Label _speed = null!;
    private Label _wind = null!;
    private Minimap _minimap = null!;
    private SelectionPanel _selectionPanel = null!;
    private CommandCard _commandCard = null!;
    private VBoxContainer _feed = null!;
    private VBoxContainer _objectives = null!;
    private PanelContainer _objectivesPanel = null!;
    private Label _modeHint = null!;
    private Label _waiting = null!;
    private BoxOverlay _box = null!;
    private Control _root = null!;

    public event Action? MenuRequested;

    private World World => _session.World;

    public void Init(SimSession session, InputController input, Selection selection, CameraRig rig, ContingencyZero.Game.View.TerrainView terrainInfo)
    {
        _session = session;
        _input = input;
        _selection = selection;
        _rig = rig;

        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiTheme.Theme };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        _box = new BoxOverlay { Input = input, MouseFilter = Control.MouseFilterEnum.Ignore };
        _box.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_box);

        BuildTopBar();

        _minimap = new Minimap();
        _minimap.Init(session, rig, terrainInfo, input);
        _minimap.SelectionIds = () => selection.Ids.Where(id => World.Units.IsValid(id) && World.Units.Owner[id] == session.LocalPlayer).ToList();
        var mmPanel = UiTheme.Panel(_minimap, 6);
        mmPanel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        mmPanel.Position = new Vector2(8, -8);
        mmPanel.GrowVertical = Control.GrowDirection.Begin;
        mmPanel.AnchorTop = 1; mmPanel.AnchorBottom = 1;
        mmPanel.OffsetTop = -240; mmPanel.OffsetBottom = -8; mmPanel.OffsetLeft = 8; mmPanel.OffsetRight = 248;
        _root.AddChild(mmPanel);

        _selectionPanel = new SelectionPanel();
        _selectionPanel.Init(session, selection, input);
        var selPanel = UiTheme.Panel(_selectionPanel, 8);
        selPanel.AnchorLeft = 0; selPanel.AnchorRight = 1; selPanel.AnchorTop = 1; selPanel.AnchorBottom = 1;
        selPanel.OffsetLeft = 256; selPanel.OffsetRight = -476; selPanel.OffsetTop = -170; selPanel.OffsetBottom = -8;
        _root.AddChild(selPanel);

        _commandCard = new CommandCard();
        _commandCard.Init(session, selection, input);
        var cardPanel = UiTheme.Panel(_commandCard, 8);
        cardPanel.AnchorLeft = 1; cardPanel.AnchorRight = 1; cardPanel.AnchorTop = 1; cardPanel.AnchorBottom = 1;
        cardPanel.OffsetLeft = -468; cardPanel.OffsetRight = -8; cardPanel.OffsetTop = -330; cardPanel.OffsetBottom = -8;
        _root.AddChild(cardPanel);
        _commandCard.Refresh();

        _feed = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _feed.AnchorLeft = 0; _feed.AnchorTop = 1; _feed.AnchorBottom = 1;
        _feed.OffsetLeft = 12; _feed.OffsetRight = 520; _feed.OffsetTop = -440; _feed.OffsetBottom = -250;
        _feed.Alignment = BoxContainer.AlignmentMode.End;
        _root.AddChild(_feed);

        _modeHint = UiTheme.Label("", 18, UiTheme.Amber, HorizontalAlignment.Center);
        _modeHint.AnchorLeft = 0.5f; _modeHint.AnchorRight = 0.5f; _modeHint.OffsetLeft = -300; _modeHint.OffsetRight = 300;
        _modeHint.OffsetTop = 64; _modeHint.OffsetBottom = 90;
        _modeHint.AddThemeConstantOverride("outline_size", 6);
        _modeHint.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        _root.AddChild(_modeHint);

        _waiting = UiTheme.Label("Waiting for other players...", 22, UiTheme.Amber, HorizontalAlignment.Center);
        _waiting.AnchorLeft = 0; _waiting.AnchorRight = 1; _waiting.AnchorTop = 0.5f; _waiting.AnchorBottom = 0.5f;
        _waiting.OffsetTop = -16; _waiting.OffsetBottom = 16;
        _waiting.Visible = false;
        _root.AddChild(_waiting);

        _objectives = new VBoxContainer();
        _objectivesPanel = UiTheme.Panel(_objectives, 10);
        _objectivesPanel.Position = new Vector2(12, 60);
        _objectivesPanel.CustomMinimumSize = new Vector2(360, 0);
        _objectivesPanel.Visible = World.Mission is not null;
        _root.AddChild(_objectivesPanel);

        input.ModeChanged += UpdateModeHint;
        input.Notice += msg => Post(msg, UiTheme.Amber);
        selection.Changed += () => { _selectionPanel.Refresh(); _commandCard.Refresh(); };
    }

    private void BuildTopBar()
    {
        var bar = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        bar.AddThemeConstantOverride("separation", 14);
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.04f, 0.05f, 0.07f, 0.88f), UiTheme.PanelEdge, 1, 0, 6));
        panel.AnchorLeft = 0; panel.AnchorRight = 1; panel.OffsetBottom = 50;
        panel.AddChild(bar);
        _root.AddChild(panel);

        _metal = new ResourceMeter("METAL", UiTheme.Metal);
        _energy = new ResourceMeter("ENERGY", UiTheme.Energy);
        bar.AddChild(_metal);
        bar.AddChild(_energy);

        _wind = UiTheme.Label("", 14, UiTheme.Dim);
        _wind.VerticalAlignment = VerticalAlignment.Center;
        bar.AddChild(_wind);

        bar.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });

        _speed = UiTheme.Label("", 15, UiTheme.Dim);
        _speed.VerticalAlignment = VerticalAlignment.Center;
        bar.AddChild(_speed);
        _clock = UiTheme.Label("0:00", 20, UiTheme.Ink);
        _clock.VerticalAlignment = VerticalAlignment.Center;
        _clock.CustomMinimumSize = new Vector2(70, 0);
        bar.AddChild(_clock);
        var menu = UiTheme.Button("Menu", () => MenuRequested?.Invoke(), 90, 15);
        menu.TooltipText = "Pause menu (Esc / F10)";
        bar.AddChild(menu);
    }

    private void UpdateModeHint()
    {
        _modeHint.Text = _input.Mode switch
        {
            ClickMode.Move => "MOVE: click the ground",
            ClickMode.Fight => "ATTACK: click an enemy, or the ground to attack-move (Ctrl: fire at the ground)",
            ClickMode.Patrol => "PATROL: click the far end of the route",
            ClickMode.Reclaim => "RECLAIM: click a wreck, rock or tree",
            ClickMode.Assist => "GUARD / ASSIST: click a friendly unit",
            ClickMode.DGun => "BREACH CANNON: click a target (500 energy)",
            ClickMode.Place => $"BUILD {World.Catalog[_input.PlaceType].Name.ToUpperInvariant()}: click to place, drag for a row, shift to keep placing, right-click to cancel",
            _ => "",
        };
    }

    // ---- Messages ----------------------------------------------------------------------------

    private readonly List<(Label Label, double Expires)> _feedItems = new();

    public void Post(string text, Color colour, float seconds = 6)
    {
        var l = UiTheme.Label(text, 17, colour);
        l.AddThemeConstantOverride("outline_size", 5);
        l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        l.MouseFilter = Control.MouseFilterEnum.Ignore;
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _feed.AddChild(l);
        _feedItems.Add((l, Time.GetTicksMsec() / 1000.0 + seconds));
        while (_feedItems.Count > 7)
        {
            _feedItems[0].Label.QueueFree();
            _feedItems.RemoveAt(0);
        }
    }

    // ---- Per tick / frame ---------------------------------------------------------------------

    private double _lastAttackAlert = -100;

    /// <summary>Reacts to this tick's events: alerts, mission messages.</summary>
    public void OnTick()
    {
        int me = _session.LocalPlayer;
        foreach (GameEvent e in World.Events)
        {
            switch (e.Type)
            {
                case GameEventType.UnitDamaged when e.Unit >= 0 && World.Units.Owner[e.Unit] == me:
                    break;
                case GameEventType.UnitDestroyed when e.Other == me:
                {
                    UnitDef def = World.Catalog[e.Value];
                    double now = Time.GetTicksMsec() / 1000.0;
                    if (def.IsStructure && now - _lastAttackAlert > 20)
                    {
                        _lastAttackAlert = now;
                        Post("Base under attack!", UiTheme.Bad);
                        Sfx.Play(SfxKind.Alert, -2);
                    }
                    if (def.IsCommander) Post("Your Commander has been destroyed!", UiTheme.Bad, 10);
                    break;
                }
                case GameEventType.BuildComplete when e.Other == me:
                {
                    UnitDef def = World.Catalog[World.Units.TypeIndex[e.Unit]];
                    if (def.IsStructure) Post($"{def.Name} complete", UiTheme.Good, 4);
                    break;
                }
                case GameEventType.PlayerDefeated:
                    if (e.Other != me) Post($"{World.Players[e.Other].Name} has been destroyed", UiTheme.Amber, 8);
                    break;
                case GameEventType.OrderFailed when e.Other == me:
                    Post(e.Value == 1 ? "Not enough energy for the Breach Cannon" : "Couldn't build there: the site is blocked", UiTheme.Amber, 4);
                    Sfx.Play(SfxKind.Error, -8);
                    break;
                case GameEventType.MissionMessage when World.Mission is { } m:
                    Post(m.Messages[Math.Clamp(e.Value, 0, m.Messages.Count - 1)], UiTheme.Accent, 14);
                    Sfx.Play(SfxKind.MissionMessage, -4);
                    break;
            }
        }
        _minimap.OnTick();
        _selectionPanel.OnTick();
        _commandCard.OnTick();
        RefreshObjectives();

        PlayerState p = World.Players[Math.Max(0, me)];
        _metal.Sample(p.MetalIncome, p.MetalSpent, p.MetalWasted, p.BuildRatio, Fix64.Zero);
        _energy.Sample(p.EnergyIncome, p.EnergySpent, p.EnergyWasted, p.BuildRatio, p.ConverterEnergy);
    }

    private string _objectivesKey = "";

    private void RefreshObjectives()
    {
        if (World.Mission is not { } m) return;
        string key = string.Join(",", m.Objectives.Select(o => (int)o.State));
        if (key == _objectivesKey) return;
        _objectivesKey = key;
        foreach (Node c in _objectives.GetChildren()) c.QueueFree();
        _objectives.AddChild(UiTheme.Label("OBJECTIVES", 14, UiTheme.Dim));
        foreach (Objective o in m.Objectives)
        {
            if (o.State == ObjectiveState.Hidden) continue;
            string mark = o.State switch { ObjectiveState.Complete => "[done]", ObjectiveState.Failed => "[failed]", _ => "[ ]" };
            Color c = o.State switch { ObjectiveState.Complete => UiTheme.Good, ObjectiveState.Failed => UiTheme.Bad, _ => UiTheme.Ink };
            var l = UiTheme.Label($"{mark} {o.Text}{(o.Optional ? "  (optional)" : "")}", 15, c);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            l.CustomMinimumSize = new Vector2(340, 0);
            _objectives.AddChild(l);
        }
    }

    public override void _Process(double delta)
    {
        if (_session is null) return;
        int me = _session.LocalPlayer;
        PlayerState p = World.Players[Math.Max(0, me)];
        _metal.Show(p.Metal, p.MetalStorage, p.MetalIncome, p.MetalSpent, p.BuildRatio < Fix64.Parse("0.98") && p.Metal < p.MetalStorage / 20);
        _energy.Show(p.Energy, p.EnergyStorage, p.EnergyIncome, p.EnergySpent, p.BuildRatio < Fix64.Parse("0.98") && p.Energy < p.EnergyStorage / 20);
        _wind.Text = $"Wind {World.Wind.Raw / 65536f:0}";

        int secs = World.Tick / World.TicksPerSecond;
        _clock.Text = $"{secs / 60}:{secs % 60:D2}";
        _speed.Text = _session.Paused ? "PAUSED" : Math.Abs(_session.Speed - 1) > 0.01 ? $"x{_session.Speed:0.##}" : _session.IsReplay ? "REPLAY" : "";
        _waiting.Visible = _session.Waiting && !_session.IsMultiplayer; // multiplayer names who it's waiting for (Net/NetMatchOverlay)

        double now = Time.GetTicksMsec() / 1000.0;
        for (int i = _feedItems.Count - 1; i >= 0; i--)
        {
            var (label, expires) = _feedItems[i];
            float left = (float)(expires - now);
            if (left <= 0) { label.QueueFree(); _feedItems.RemoveAt(i); continue; }
            label.Modulate = new Color(1, 1, 1, Mathf.Clamp(left, 0, 1));
        }
        _box.QueueRedraw();
    }
}

/// <summary>Draws the drag-select rectangle.</summary>
public partial class BoxOverlay : Control
{
    public InputController? Input;
    public override void _Draw()
    {
        if (Input?.Box is { } r)
        {
            DrawRect(r, new Color(0.3f, 1f, 0.45f, 0.12f));
            DrawRect(r, new Color(0.3f, 1f, 0.45f, 0.9f), false, 1.5f);
        }
    }
}

/// <summary>
/// One resource readout: name, stored / storage with a fill bar, income and spending, and under
/// the flow a one-line forecast of what those numbers mean ("Stall in 40s", "Full in 25s",
/// "Stalling: builds at 60%"). The forecast averages the last two seconds of ticks so it reads
/// steadily instead of flickering with every shot fired.
/// </summary>
public partial class ResourceMeter : HBoxContainer
{
    private const int SmoothTicks = 2 * World.TicksPerSecond;

    private readonly Label _value;
    private readonly Label _flow;
    private readonly Label _forecast;
    private readonly ProgressBar _bar;
    private readonly Label _stall;
    private readonly Color _colour;

    // Per-tick samples (per second units), averaged for the forecast.
    private readonly float[] _income = new float[SmoothTicks];
    private readonly float[] _spent = new float[SmoothTicks];
    private readonly float[] _wasted = new float[SmoothTicks];
    private readonly float[] _ratio = new float[SmoothTicks];
    private readonly float[] _converter = new float[SmoothTicks];
    private int _samples, _cursor;

    /// <summary>The last forecast shown (for tests and dev checks).</summary>
    public string ForecastText => _forecast.Text;

    public ResourceMeter(string name, Color colour)
    {
        _colour = colour;
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 8);
        var title = UiTheme.Label(name, 13, colour);
        title.VerticalAlignment = VerticalAlignment.Center;
        AddChild(title);

        var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 2);
        _value = UiTheme.Label("0 / 0", 15, UiTheme.Ink);
        _bar = new ProgressBar { CustomMinimumSize = new Vector2(190, 8), ShowPercentage = false, MaxValue = 1, MouseFilter = MouseFilterEnum.Ignore };
        _bar.AddThemeStyleboxOverride("fill", UiTheme.Box(colour, colour, 0, 2, 0));
        col.AddChild(_value);
        col.AddChild(_bar);
        AddChild(col);

        // Flow on top, forecast underneath; together they stay inside the bar's height.
        var flowCol = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        flowCol.AddThemeConstantOverride("separation", 0);
        _flow = UiTheme.Label("", 14, UiTheme.Dim);
        _flow.CustomMinimumSize = new Vector2(150, 0);
        flowCol.AddChild(_flow);
        _forecast = UiTheme.Label("", 12, UiTheme.Dim);
        _forecast.CustomMinimumSize = new Vector2(150, 0);
        flowCol.AddChild(_forecast);
        AddChild(flowCol);

        _stall = UiTheme.Label("STALL", 13, UiTheme.Bad);
        _stall.VerticalAlignment = VerticalAlignment.Center;
        _stall.Visible = false;
        AddChild(_stall);
    }

    /// <summary>Records one tick's flow (per-tick figures from the sim). Call once per sim tick.</summary>
    public void Sample(Fix64 incomePerTick, Fix64 spentPerTick, Fix64 wastedPerTick, Fix64 buildRatio, Fix64 converterPerTick)
    {
        _income[_cursor] = PerSecond(incomePerTick);
        _spent[_cursor] = PerSecond(spentPerTick);
        _wasted[_cursor] = PerSecond(wastedPerTick);
        _ratio[_cursor] = buildRatio.Raw / 65536f;
        _converter[_cursor] = PerSecond(converterPerTick);
        _cursor = (_cursor + 1) % SmoothTicks;
        if (_samples < SmoothTicks) _samples++;
    }

    private static float PerSecond(Fix64 perTick) => perTick.Raw / 65536f * World.TicksPerSecond;

    private float Average(float[] samples)
    {
        if (_samples == 0) return 0;
        float sum = 0;
        for (int i = 0; i < _samples; i++) sum += samples[i];
        return sum / _samples;
    }

    public void Show(Fix64 stored, Fix64 storage, Fix64 incomePerTick, Fix64 spentPerTick, bool stalling)
    {
        float s = stored.Raw / 65536f, cap = Math.Max(1, storage.Raw / 65536f);
        float inc = PerSecond(incomePerTick), spend = PerSecond(spentPerTick);
        _value.Text = $"{s:0} / {cap:0}";
        _bar.Value = s / cap;
        _flow.Text = $"+{inc:0.#}  -{spend:0.#}";
        _flow.AddThemeColorOverride("font_color", inc >= spend ? UiTheme.Good : UiTheme.Bad);
        _stall.Visible = stalling && (Time.GetTicksMsec() / 400) % 2 == 0;

        var (text, colour) = Forecast(s, cap, Average(_income), Average(_spent), Average(_wasted), Average(_ratio), Average(_converter), stalling);
        if (_forecast.Text != text) _forecast.Text = text;
        _forecast.AddThemeColorOverride("font_color", colour);
    }

    /// <summary>
    /// What the (smoothed, per second) numbers add up to. Red while stalling, amber when a stall is
    /// under 30 seconds away or the store is overflowing, dim otherwise.
    /// </summary>
    public static (string Text, Color Colour) Forecast(float stored, float storage, float income, float spent, float wasted,
        float buildRatio, float converter, bool stalling)
    {
        float net = income - spent;
        if (stalling || (buildRatio < 0.98f && stored < storage * 0.05f))
            return ($"Stalling: builds at {Mathf.Clamp(buildRatio * 100, 0, 99):0}%", UiTheme.Bad);
        if (net < -0.05f)
        {
            float t = stored / -net;
            if (t < 30) return ($"Stall in {Duration(t)}", UiTheme.Amber);
            if (t < 600) return ($"Stall in {Duration(t)}", UiTheme.Dim);
        }
        if (wasted > 0.05f && stored >= storage - 1)
            return ($"Full: wasting {wasted:0.#}/s", UiTheme.Amber);
        if (net > 0.05f && stored < storage - 1)
        {
            float t = (storage - stored) / net;
            if (t < 120) return ($"Full in {Duration(t)}", UiTheme.Dim);
        }
        if (converter > 0.05f && spent > 0.05f && converter / spent >= 0.2f)
            return ($"{Mathf.Clamp(converter / spent * 100, 0, 100):0}% to converters", UiTheme.Dim);
        return ("Steady", UiTheme.Dim);
    }

    private static string Duration(float seconds)
    {
        int t = Math.Max(1, (int)Math.Round(seconds));
        return t < 60 ? $"{t}s" : $"{t / 60}m{t % 60:00}s";
    }
}
