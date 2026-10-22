using Godot;

namespace ContingencyZero.Game.MapEditor;

/// <summary>The editor's tools, in toolbar order.</summary>
public enum EditorTool
{
    Raise,
    Lower,
    Smooth,
    Flatten,
    Plateau,
    Roughen,
    Metal,
    Trees,
    Rocks,
    Starts,
}

/// <summary>How edits are mirrored so a map stays fair.</summary>
public enum Symmetry
{
    None,
    /// <summary>Left half mirrors the right half.</summary>
    MirrorX,
    /// <summary>Top half mirrors the bottom half.</summary>
    MirrorY,
    /// <summary>Half-turn about the centre (the usual 1v1 layout).</summary>
    Rotate,
    /// <summary>Mirrored both ways (the usual four-player layout).</summary>
    FourWay,
}

/// <summary>Names, hotkeys, colours and help text for each tool.</summary>
public static class ToolInfo
{
    public static readonly EditorTool[] All =
    {
        EditorTool.Raise, EditorTool.Lower, EditorTool.Smooth, EditorTool.Flatten, EditorTool.Plateau,
        EditorTool.Roughen, EditorTool.Metal, EditorTool.Trees, EditorTool.Rocks, EditorTool.Starts,
    };

    public static bool IsTerrain(EditorTool t) => t <= EditorTool.Roughen;
    public static bool IsFeature(EditorTool t) => t is EditorTool.Trees or EditorTool.Rocks;
    public static bool UsesBrush(EditorTool t) => IsTerrain(t) || IsFeature(t);

    public static string Name(EditorTool t) => t switch
    {
        EditorTool.Raise => "Raise",
        EditorTool.Lower => "Lower",
        EditorTool.Smooth => "Smooth",
        EditorTool.Flatten => "Flatten",
        EditorTool.Plateau => "Plateau",
        EditorTool.Roughen => "Roughen",
        EditorTool.Metal => "Metal spots",
        EditorTool.Trees => "Trees",
        EditorTool.Rocks => "Rocks",
        _ => "Start positions",
    };

    /// <summary>The key that picks the tool: 1 to 9, then 0.</summary>
    public static Key Hotkey(EditorTool t) => t switch
    {
        EditorTool.Raise => Key.Key1,
        EditorTool.Lower => Key.Key2,
        EditorTool.Smooth => Key.Key3,
        EditorTool.Flatten => Key.Key4,
        EditorTool.Plateau => Key.Key5,
        EditorTool.Roughen => Key.Key6,
        EditorTool.Metal => Key.Key7,
        EditorTool.Trees => Key.Key8,
        EditorTool.Rocks => Key.Key9,
        _ => Key.Key0,
    };

    public static string HotkeyText(EditorTool t) => t == EditorTool.Starts ? "0" : ((int)t + 1).ToString();

    /// <summary>One line for the tooltip and the top of the options panel.</summary>
    public static string Summary(EditorTool t) => t switch
    {
        EditorTool.Raise => "Builds up the ground under the brush. Hold the mouse to keep raising.",
        EditorTool.Lower => "Digs the ground down. Dig below the water level to make lakes and rivers.",
        EditorTool.Smooth => "Softens bumps and cliffs. Use it to make slopes that vehicles can climb.",
        EditorTool.Flatten => "Levels ground to the height where you first click. Bases need flat ground.",
        EditorTool.Plateau => "Sets ground to an exact height with a sharp edge: mesas, cliffs and ramps.",
        EditorTool.Roughen => "Adds random bumps so open ground looks natural.",
        EditorTool.Metal => "Places metal spots, where players build metal extractors. Snaps to tiles.",
        EditorTool.Trees => "Paints forests. Builders can reclaim trees for energy.",
        EditorTool.Rocks => "Scatters rocks. Builders can reclaim rocks for metal.",
        _ => "Where each player's Commander starts. A map needs 2 to 8.",
    };

    /// <summary>Mouse instructions for the help line at the bottom of the screen.</summary>
    public static string MouseHelp(EditorTool t) => t switch
    {
        EditorTool.Raise => "Left drag: raise  ·  Right drag: lower",
        EditorTool.Lower => "Left drag: lower  ·  Right drag: raise",
        EditorTool.Smooth => "Left drag: smooth",
        EditorTool.Flatten => "Left drag: flatten to the height where you start  ·  Right drag: smooth",
        EditorTool.Plateau => "Left drag: set ground to the plateau height  ·  Ctrl+click: pick the height under the cursor",
        EditorTool.Roughen => "Left drag: roughen  ·  Right drag: smooth",
        EditorTool.Metal => "Left click: place a metal spot  ·  Right click: remove one",
        EditorTool.Trees => "Left drag: plant trees  ·  Right drag: clear trees  ·  Shift+right drag: clear everything",
        EditorTool.Rocks => "Left drag: scatter rocks  ·  Right drag: clear rocks  ·  Shift+right drag: clear everything",
        _ => "Left click: add a start  ·  Drag a marker: move it  ·  Right click a marker: remove it",
    };

    /// <summary>Accent colour for the tool's button icon and brush ring.</summary>
    public static Color Colour(EditorTool t) => t switch
    {
        EditorTool.Raise => new Color(0.45f, 0.95f, 0.55f),
        EditorTool.Lower => new Color(1.0f, 0.55f, 0.3f),
        EditorTool.Smooth => new Color(0.45f, 0.8f, 1.0f),
        EditorTool.Flatten => new Color(0.92f, 0.94f, 1.0f),
        EditorTool.Plateau => new Color(1.0f, 0.75f, 0.3f),
        EditorTool.Roughen => new Color(0.8f, 0.6f, 1.0f),
        EditorTool.Metal => new Color(0.55f, 0.85f, 1.0f),
        EditorTool.Trees => new Color(0.45f, 0.85f, 0.4f),
        EditorTool.Rocks => new Color(0.75f, 0.72f, 0.68f),
        _ => new Color(1.0f, 0.68f, 0.22f),
    };

    public static string SymmetryName(Symmetry s) => s switch
    {
        Symmetry.MirrorX => "Mirror left-right",
        Symmetry.MirrorY => "Mirror top-bottom",
        Symmetry.Rotate => "Rotate half-turn",
        Symmetry.FourWay => "Four-way mirror",
        _ => "No symmetry",
    };
}
