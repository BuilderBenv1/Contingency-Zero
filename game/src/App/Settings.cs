using System.Text.Json;
using Godot;
using FileAccess = Godot.FileAccess;

namespace ContingencyZero.Game.App;

/// <summary>Player settings, saved to user://settings.json.</summary>
public sealed class Settings
{
    private const string FilePath = "user://settings.json";

    public float MasterVolume { get; set; } = 0.8f;
    public float SfxVolume { get; set; } = 0.8f;
    public float MusicVolume { get; set; } = 0.5f;
    public bool Fullscreen { get; set; } = false;
    public bool EdgeScroll { get; set; } = true;
    public float ScrollSpeed { get; set; } = 1.0f;
    public bool AlwaysShowHealthBars { get; set; } = false;
    /// <summary>Size of every menu and HUD element, 80..150 (100 = the 1600x900 design size). 115 suits a Steam Deck.</summary>
    public int UiScalePercent { get; set; } = 100;
    /// <summary>Always show the controller button hints in a match (otherwise only while a pad is connected).</summary>
    public bool GamepadHints { get; set; } = false;
    /// <summary>The old-school look: chunky pixels, a fixed palette and pixel-textured units and ground.</summary>
    public bool RetroLook { get; set; } = false;
    public string PlayerName { get; set; } = "Commander";
    public string LastMap { get; set; } = "proving_grounds";
    public string LastFaction { get; set; } = "lattice";
    public string LastHost { get; set; } = "127.0.0.1";

    public static Settings Current { get; private set; } = Load();

    private static Settings Load()
    {
        try
        {
            if (FileAccess.FileExists(FilePath))
            {
                string text = FileAccess.GetFileAsString(FilePath);
                return JsonSerializer.Deserialize<Settings>(text) ?? new Settings();
            }
        }
        catch (JsonException)
        {
            // Corrupt settings: start fresh.
        }
        return new Settings();
    }

    public void Save()
    {
        using var f = FileAccess.Open(FilePath, FileAccess.ModeFlags.Write);
        f?.StoreString(JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Applies window and audio settings.</summary>
    public void Apply()
    {
        var mode = DisplayServer.WindowGetMode();
        if (Fullscreen && mode != DisplayServer.WindowMode.Fullscreen)
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
        else if (!Fullscreen && mode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen)
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Maximized);
        SetBus("Master", MasterVolume);
        SetBus("SFX", SfxVolume);
        SetBus("Music", MusicVolume);
        ApplyUiScale();
    }

    public const int MinUiScale = 80, MaxUiScale = 150;

    /// <summary>Scales every canvas item (menus, HUD) about the 1600x900 design size; the battlefield itself is unaffected.</summary>
    public void ApplyUiScale()
    {
        UiScalePercent = Mathf.Clamp(UiScalePercent, MinUiScale, MaxUiScale);
        if (Engine.GetMainLoop() is SceneTree tree && tree.Root is { } root)
            root.ContentScaleFactor = UiScalePercent / 100f;
    }

    /// <summary>The Steam Deck preset: a larger UI for the 7-inch 1280x800 screen, controller hints on.</summary>
    public void ApplySteamDeckPreset()
    {
        UiScalePercent = 115;
        GamepadHints = true;
        Apply();
    }

    private static void SetBus(string name, float volume)
    {
        int bus = AudioServer.GetBusIndex(name);
        if (bus < 0) return;
        AudioServer.SetBusVolumeDb(bus, volume <= 0.001f ? -80f : Mathf.LinearToDb(volume));
    }
}
