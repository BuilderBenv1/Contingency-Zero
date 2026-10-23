using ContingencyZero.Game.App;
using Godot;

namespace ContingencyZero.Game.Controls;

/// <summary>Steam Deck detection and its first-run defaults.</summary>
public static class SteamDeck
{
    /// <summary>Steam sets SteamDeck=1 on the Deck; a 1280x800 screen on Linux is a good second guess.</summary>
    public static bool Detected
    {
        get
        {
            if (OS.GetEnvironment("SteamDeck") == "1") return true;
            if (OS.GetName() != "Linux") return false;
            Vector2I s = DisplayServer.ScreenGetSize();
            return s == new Vector2I(1280, 800) || s == new Vector2I(800, 1280);
        }
    }

    /// <summary>On the very first run on a Deck (no settings saved yet) use the bigger UI and controller hints.</summary>
    public static void ApplyFirstRunPreset()
    {
        if (FileAccess.FileExists("user://settings.json") || !Detected) return;
        Settings.Current.Fullscreen = true;
        Settings.Current.ApplySteamDeckPreset();
        Settings.Current.Save();
        GD.Print("Steam Deck detected: applied the Steam Deck preset (UI 115%, controller hints, fullscreen).");
    }
}
