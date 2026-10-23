using System;
using System.Linq;
using System.Threading.Tasks;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Match;
using Godot;

namespace ContingencyZero.Game.Controls;

/// <summary>
/// Dev check for controller play with no controller attached: feeds synthetic joypad events
/// through <see cref="Input.ParseInputEvent"/> (exactly what a pad driver does) and saves
/// screenshots as pad_*.png in --dev-shots. Scripts:
///   match  (with --dev-skirmish; --dev-self-ai=1 --dev-fastforward=180 gives an army to box)
///          cursor, box select, move order, radial menu + attack-move, build menu + placement,
///          pause menu navigation, minimap steering
///   menu   (with --dev-menu) title screen, skirmish setup, settings and controls with the d-pad
/// Pass a --dev-seconds long enough for the script (about 40 s); "DEV-PAD:" lines report state.
/// </summary>
public partial class GamepadDevScript : Node
{
    private readonly string _script;
    private readonly string _shots;

    public GamepadDevScript(string script, string shots)
    {
        _script = script;
        _shots = shots;
    }

    public override void _Ready()
    {
        // --dev-ui-scale=115 tries the Steam Deck interface size without saving it.
        foreach (string a in OS.GetCmdlineUserArgs())
            if (a.StartsWith("--dev-ui-scale=") && int.TryParse(a["--dev-ui-scale=".Length..], out int pct))
            {
                Settings.Current.UiScalePercent = pct;
                Settings.Current.ApplyUiScale();
            }
        _ = Run();
    }

    private async Task Run()
    {
        try
        {
            await Wait(2.5);
            if (_script == "menu") await Menu();
            else await MatchScript();
            Log("script finished");
        }
        catch (Exception e)
        {
            GD.PushError($"DEV-PAD: script failed: {e}");
        }
    }

    // ---- Helpers ------------------------------------------------------------------------------

    private async Task Wait(double s) => await ToSignal(GetTree().CreateTimer(s, processAlways: true), Timer.SignalName.Timeout);
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    private static void Log(string s) => GD.Print($"DEV-PAD: {s}");

    private static void Btn(JoyButton b, bool down) => SyntheticInput.JoyButton(b, down);

    private async Task Tap(JoyButton b, double after = 0.25)
    {
        Btn(b, true);
        await Wait(0.07);
        Btn(b, false);
        await Wait(after);
    }

    private static void Stick(Vector2 v)
    {
        SyntheticInput.JoyAxis(JoyAxis.LeftX, v.X);
        SyntheticInput.JoyAxis(JoyAxis.LeftY, v.Y);
    }

    /// <summary>Steers the left stick until the virtual cursor reaches a point, like a player would.</summary>
    private async Task StickTo(Vector2 target, float tolerance = 5)
    {
        GamepadController pad = GamepadController.Current ?? throw new InvalidOperationException("no gamepad controller");
        double start = Time.GetTicksMsec() / 1000.0;
        while (Time.GetTicksMsec() / 1000.0 - start < 6)
        {
            Vector2 d = target - pad.Cursor;
            if (d.Length() <= tolerance) break;
            // Full tilt when far, easing off near the target (with a floor so it never stalls in the deadzone).
            float tilt = Mathf.Clamp(d.Length() / 120f, 0.62f, 1f);
            Stick(d.Normalized() * tilt);
            await Frame();
        }
        Stick(Vector2.Zero);
        await Frame();
        await Frame();
    }

    private async Task Shot(string name)
    {
        await Frame();
        await Frame();
        if (_shots.Length == 0) return;
        DirAccess.MakeDirRecursiveAbsolute(_shots);
        string path = $"{_shots}/pad_{name}.png";
        GetViewport().GetTexture().GetImage().SavePng(path);
        Log($"shot {path}");
    }

    private string FocusName()
    {
        Control? f = GetViewport().GuiGetFocusOwner();
        return f switch
        {
            null => "(none)",
            Button b => $"button '{b.Text}'",
            _ => $"{f.GetClass()} {f.Name}",
        };
    }

    // ---- In a match -------------------------------------------------------------------------

    private async Task MatchScript()
    {
        MatchScene match = GameRoot.Instance.GetNode<MatchScene>("Match");
        var world = match.Session.World;
        int me = match.Session.LocalPlayer;
        Vector2 size = GetViewport().GetVisibleRect().Size;
        Log($"viewport {size} (window {DisplayServer.WindowGetSize()})");

        // 1. Wake the pad and move the cursor.
        SyntheticInput.JoyAxis(JoyAxis.LeftX, 0.6f);
        await Wait(0.05);
        await StickTo(size / 2 + new Vector2(-120, -60));
        await Shot("01_cursor");
        GamepadController pad = GamepadController.Current!;
        Log($"cursor active={pad.Active} at {pad.Cursor}");

        // 2. Box select own mobile units in the open part of the screen.
        Camera3D cam = match.Rig.Camera;
        var mine = match.Units.VisibleUnits
            .Where(v => world.Units.Owner[v.Id] == me && world.Catalog[world.Units.TypeIndex[v.Id]].CanMove)
            .Select(v => (v.Id, S: cam.UnprojectPosition(v.Pos)))
            .Where(v => v.S.X > 280 && v.S.X < size.X - 500 && v.S.Y > 110 && v.S.Y < size.Y - 380)
            .ToList();
        Log($"{mine.Count} own mobile units in the open screen area");
        Vector2 lo, hi;
        if (mine.Count > 0)
        {
            lo = new Vector2(mine.Min(v => v.S.X), mine.Min(v => v.S.Y)) - new Vector2(28, 28);
            hi = new Vector2(mine.Max(v => v.S.X), mine.Max(v => v.S.Y)) + new Vector2(28, 28);
        }
        else
        {
            lo = size / 2 - new Vector2(160, 120);
            hi = size / 2 + new Vector2(160, 60);
        }
        await StickTo(lo);
        Btn(JoyButton.A, true);
        await Wait(0.1);
        await StickTo(hi);
        await Shot("02_boxing");
        Btn(JoyButton.A, false);
        await Wait(0.3);
        await Shot("03_selected");
        Log($"box select: {match.Selection.Count} selected");

        // 3. Context order (X) on open ground.
        Vector2 ground = new(Mathf.Clamp((lo.X + hi.X) / 2 + 220, 300, size.X - 520), Mathf.Clamp((lo.Y + hi.Y) / 2 - 40, 130, size.Y - 400));
        await StickTo(ground);
        await Tap(JoyButton.X, 0.15);
        await Shot("04_move_order");
        int first = match.Selection.Ids.FirstOrDefault(world.Units.IsValid, -1);
        if (first >= 0)
            Log($"after X: unit {first} has {world.Units.Orders[first].Count} orders" + (world.Units.Orders[first].Count > 0 ? $", first {world.Units.Orders[first][0].Type}" : ""));

        // 4. Radial menu: tap Y, push the stick at Attack-move (item 1 of 10), A confirms, A at the ground executes.
        await Tap(JoyButton.Y, 0.1);
        float a = Mathf.Tau / 10 * 1;
        Stick(new Vector2(Mathf.Sin(a), -Mathf.Cos(a)));
        await Wait(0.3);
        await Shot("05_radial");
        Log($"radial open={pad.RadialOpen} hover={pad.RadialHover} ({(pad.RadialHover >= 0 ? pad.RadialItems[pad.RadialHover].Label : "-")})");
        await Tap(JoyButton.A, 0.1);
        Stick(Vector2.Zero);
        await Wait(0.1);
        Log($"after radial: mode {match.Input.Mode}");
        await StickTo(ground + new Vector2(-60, 90));
        await Shot("06_attack_mode");
        await Tap(JoyButton.A, 0.2);
        if (first >= 0 && world.Units.IsValid(first))
            Log($"attack-move: mode now {match.Input.Mode}, unit {first} orders {world.Units.Orders[first].Count}" + (world.Units.Orders[first].Count > 0 ? $", first {world.Units.Orders[first][0].Type}" : ""));

        // 5. Build: View tap selects the Commander, d-pad opens the build grid, A picks, A places.
        await Tap(JoyButton.Back, 0.6);
        Log($"View tap: {match.Selection.Count} selected, commander={match.Selection.Ids.Any(id => world.Catalog[world.Units.TypeIndex[id]].IsCommander)}");
        await Tap(JoyButton.DpadDown, 0.15);
        await Tap(JoyButton.DpadRight, 0.15);
        await Shot("07_build_menu");
        Log($"build menu: navigating={pad.NavigatingCard} button={pad.Card?.NavButton?.Name}");
        await Tap(JoyButton.A, 0.2);
        Log($"after A: mode {match.Input.Mode}, type {(match.Input.PlaceType >= 0 ? world.Catalog[match.Input.PlaceType].Id : "-")}");
        int commander = match.Selection.Ids.FirstOrDefault(id => world.Units.IsValid(id) && world.Catalog[world.Units.TypeIndex[id]].IsCommander, -1);
        int before = commander >= 0 ? world.Units.Orders[commander].Count : 0;
        await StickTo(size / 2 + new Vector2(-200, 150));
        await Shot("08_placing");
        await Tap(JoyButton.A, 0.5);
        if (commander >= 0)
            Log($"placed: commander orders {before} -> {world.Units.Orders[commander].Count}" + (world.Units.Orders[commander].Count > 0 ? $", first {world.Units.Orders[commander][0].Type}" : "") + $", mode {match.Input.Mode}");
        await Shot("09_placed");

        // 6. RB selects the army.
        await Tap(JoyButton.RightShoulder, 0.3);
        Log($"RB: {match.Selection.Count} selected");

        // 7. Pause menu with the d-pad.
        await Tap(JoyButton.Start, 0.5);
        Log($"pause open={MenuNavigator.MenuOpen}, focus {FocusName()}");
        await Shot("10_pause");
        await Tap(JoyButton.DpadDown, 0.2);
        await Tap(JoyButton.DpadDown, 0.2);
        Log($"pause after 2x down: focus {FocusName()}");
        await Shot("11_pause_nav");
        await Tap(JoyButton.B, 0.4);
        Log($"after B: menu open={MenuNavigator.MenuOpen}, paused={match.Session.Paused}");

        // 8. Hold View to steer on the minimap, A jumps the camera there.
        Vector3 focusBefore = match.Rig.Focus;
        Btn(JoyButton.Back, true);
        await Wait(0.6);
        Btn(JoyButton.Back, false);
        await Wait(0.1);
        Log($"minimap mode={pad.MinimapMode} rect={pad.MinimapRect}");
        await StickTo(pad.MinimapRect.Position + pad.MinimapRect.Size * new Vector2(0.75f, 0.25f), 3);
        await Shot("12_minimap");
        await Tap(JoyButton.A, 0.3);
        Log($"camera focus {focusBefore} -> {match.Rig.Focus}");
        await Tap(JoyButton.B, 0.3);
        Log($"minimap mode after B={pad.MinimapMode}");
        await Shot("13_done");
    }

    // ---- Menus ------------------------------------------------------------------------------

    private async Task Menu()
    {
        await Tap(JoyButton.DpadDown, 0.3); // first press lands on the first button
        Log($"title: focus {FocusName()}");
        await Shot("m01_title");
        await Tap(JoyButton.DpadDown, 0.2);
        Log($"title after down: focus {FocusName()}");
        await Tap(JoyButton.DpadUp, 0.2);
        await Tap(JoyButton.A, 1.0);
        Log($"skirmish setup: top layer {MenuNavigator.TopMenu?.GetType().Name}, focus {FocusName()}");
        await Shot("m02_skirmish");
        for (int i = 0; i < 4; i++) await Tap(JoyButton.DpadDown, 0.15);
        Log($"skirmish after 4x down: focus {FocusName()}");
        await Shot("m03_skirmish_nav");
        await Tap(JoyButton.B, 1.0);
        Log($"after B: top layer {MenuNavigator.TopMenu?.GetType().Name}, focus {FocusName()}");
        await Shot("m04_back");

        // Settings is the sixth entry.
        for (int i = 0; i < 8 && !FocusName().Contains("Settings"); i++) await Tap(JoyButton.DpadDown, 0.15);
        await Tap(JoyButton.A, 0.6);
        Log($"settings: focus {FocusName()}");
        await Shot("m05_settings");
        await Tap(JoyButton.B, 0.5);
        await Tap(JoyButton.DpadDown, 0.2);
        Log($"title: focus {FocusName()}");
        await Tap(JoyButton.A, 0.6);
        Log($"controls: focus {FocusName()}");
        await Tap(JoyButton.DpadRight, 0.2);
        await Tap(JoyButton.A, 0.4);
        Log($"controls page: focus {FocusName()}");
        await Shot("m06_controls_pad");
        await Tap(JoyButton.B, 0.5);

        // Campaign and replays, there and back.
        for (int i = 0; i < 8 && !FocusName().Contains("Campaign"); i++) await Tap(JoyButton.DpadUp, 0.15);
        await Tap(JoyButton.A, 1.0);
        Log($"campaign: focus {FocusName()}");
        await Shot("m07_campaign");
        await Tap(JoyButton.B, 1.0);
        for (int i = 0; i < 8 && !FocusName().Contains("Replays"); i++) await Tap(JoyButton.DpadDown, 0.15);
        await Tap(JoyButton.A, 1.0);
        Log($"replays: focus {FocusName()}");
        await Shot("m08_replays");
        await Tap(JoyButton.B, 1.0);
        Log($"back on title: focus {FocusName()}");
    }
}
