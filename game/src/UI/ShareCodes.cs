using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using ContingencyZero.Game.App;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Replay;
using Godot;
using FileAccess = Godot.FileAccess;

namespace ContingencyZero.Game.UI;

/// <summary>
/// Text codes for sharing replays and maps through chat: a short prefix naming what it is and the
/// format version, then base64url. A replay code carries the replay file's own bytes (already
/// deflated, and self-checking through <see cref="MatchReplay.Read"/>). A map code carries the
/// map's fingerprint and its deflated JSON, so a damaged paste is caught before it is saved.
/// </summary>
public static class ShareCodes
{
    public const string ReplayPrefix = "CZR1:";
    public const string MapPrefix = "CZM1:";
    /// <summary>Codes longer than this are awkward to paste into most chat apps (still allowed).</summary>
    public const int ComfortableLength = 200 * 1024;
    /// <summary>Refuse to inflate a map past this, so a hostile code can't eat all the memory.</summary>
    private const int MaxMapJsonBytes = 32 * 1024 * 1024;

    // ---- Replays ---------------------------------------------------------------------------------

    public static string ReplayCode(byte[] replayFile) => ReplayPrefix + ToBase64Url(replayFile);

    public static string ReplayCode(MatchReplay replay)
    {
        using var ms = new MemoryStream();
        replay.Write(ms);
        return ReplayCode(ms.ToArray());
    }

    /// <summary>Reads a replay code. Throws <see cref="InvalidDataException"/> with a readable message if it isn't one.</summary>
    public static MatchReplay DecodeReplay(string code)
    {
        byte[] bytes = Payload(code, ReplayPrefix, "replay");
        try
        {
            using var ms = new MemoryStream(bytes);
            return MatchReplay.Read(ms);
        }
        catch (Exception e) when (e is not InvalidDataException)
        {
            throw new InvalidDataException("The code is incomplete or damaged (was all of it copied?)");
        }
    }

    // ---- Maps ------------------------------------------------------------------------------------

    public static string MapCode(MapData map)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
            w.Write(map.Fingerprint());
        using (var z = new DeflateStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            byte[] json = Encoding.UTF8.GetBytes(map.ToJson());
            z.Write(json, 0, json.Length);
        }
        return MapPrefix + ToBase64Url(ms.ToArray());
    }

    /// <summary>Reads and checks a map code (parses, fingerprint matches, at least two starts).</summary>
    public static MapData DecodeMap(string code)
    {
        byte[] bytes = Payload(code, MapPrefix, "map");
        if (bytes.Length < 9) throw new InvalidDataException("The code is too short to be a map");
        ulong expected = BitConverter.ToUInt64(bytes, 0);
        string json;
        try
        {
            using var z = new DeflateStream(new MemoryStream(bytes, 8, bytes.Length - 8), CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            var buffer = new byte[64 * 1024];
            int n;
            while ((n = z.Read(buffer, 0, buffer.Length)) > 0)
            {
                outMs.Write(buffer, 0, n);
                if (outMs.Length > MaxMapJsonBytes) throw new InvalidDataException("That map is far too big");
            }
            json = Encoding.UTF8.GetString(outMs.ToArray());
        }
        catch (InvalidDataException) { throw; }
        catch (Exception)
        {
            throw new InvalidDataException("The code is incomplete or damaged (was all of it copied?)");
        }
        MapData map;
        try { map = MapData.FromJson(json); }
        catch (Exception e)
        {
            GD.Print($"Map code didn't parse: {e.Message}");
            throw new InvalidDataException("The code is damaged: the map inside it doesn't read");
        }
        if (map.Fingerprint() != expected) throw new InvalidDataException("The map doesn't match its checksum: the code was damaged");
        if (map.StartPositions.Count < 2) throw new InvalidDataException("That map has fewer than two start positions");
        return map;
    }

    /// <summary>
    /// Saves a shared map into user://maps. If one of your maps is already identical it is reused;
    /// a different map with the same name gets a numbered file name. Returns the file name.
    /// </summary>
    public static string SaveMap(MapData map)
    {
        DirAccess.MakeDirRecursiveAbsolute(GameData.UserMapsDir);
        string baseName = GameData.SafeFileName(map.Name).Replace(".json", "");
        string name = baseName + ".json";
        ulong fp = map.Fingerprint();
        for (int i = 2; FileAccess.FileExists($"{GameData.UserMapsDir}/{name}"); i++)
        {
            try
            {
                if (MapData.FromJson(FileAccess.GetFileAsString($"{GameData.UserMapsDir}/{name}")).Fingerprint() == fp) return name;
            }
            catch (Exception) { /* a broken file: pick another name */ }
            name = $"{baseName}_{i}.json";
        }
        GameData.SaveUserMap(map, name);
        return name;
    }

    // ---- Encoding --------------------------------------------------------------------------------

    public static string ToBase64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Decodes base64url (or plain base64), ignoring whitespace and missing padding.</summary>
    public static byte[] FromBase64Url(string text)
    {
        var sb = new StringBuilder(text.Length + 3);
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c) || c == '=') continue;
            sb.Append(c switch { '-' => '+', '_' => '/', _ => c });
        }
        while (sb.Length % 4 != 0) sb.Append('=');
        return Convert.FromBase64String(sb.ToString());
    }

    private static byte[] Payload(string code, string prefix, string what)
    {
        string t = (code ?? "").Trim();
        if (t.Length == 0) throw new InvalidDataException("Paste a code first");
        int at = t.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (at < 0)
        {
            if (t.StartsWith(prefix == ReplayPrefix ? MapPrefix : ReplayPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(prefix == ReplayPrefix ? "That's a map code, not a replay code" : "That's a replay code, not a map code");
            if (t.StartsWith("CZR", StringComparison.OrdinalIgnoreCase) || t.StartsWith("CZM", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("That code is from a newer version of the game");
            throw new InvalidDataException($"That isn't a {what} code (they start with {prefix})");
        }
        try { return FromBase64Url(t[(at + prefix.Length)..]); }
        catch (FormatException) { throw new InvalidDataException("The code is incomplete or damaged (was all of it copied?)"); }
    }

    public static string Size(int chars) => chars < 1024 ? $"{chars} characters" : chars < 1024 * 1024 ? $"{chars / 1024f:0.#} KB" : $"{chars / 1048576f:0.##} MB";
}

/// <summary>
/// Shows a share code that has just been put on the clipboard (with its size, and a warning when
/// it's long), or takes a pasted one. Esc closes.
/// </summary>
public partial class ShareCodeDialog : ModalLayer
{
    /// <summary>Longer codes are shown cut short in the box (the clipboard has the whole code).</summary>
    private const int ShownChars = 6000;

    public string Code { get; private set; } = "";
    private Label _status = null!;
    private TextEdit _text = null!;
    private Func<string, string>? _import;
    private Action? _onDone;

    /// <summary>Copies <paramref name="code"/> to the clipboard and shows it.</summary>
    public static ShareCodeDialog ShowCode(string title, string what, string code)
    {
        var d = new ShareCodeDialog { Name = "ShareCodeDialog", Code = code };
        d.Build(640);
        DisplayServer.ClipboardSet(code);
        d.Body.AddChild(UiTheme.Title(title, 26));
        var intro = UiTheme.Label($"The {what} code is on your clipboard. Paste it anywhere: whoever gets it uses \"Paste code\" to add it to their game.", 14, UiTheme.Dim);
        intro.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        d.Body.AddChild(intro);
        d._text = new TextEdit
        {
            Text = code.Length > ShownChars ? code[..ShownChars] + " ..." : code,
            Editable = false,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            CustomMinimumSize = new Vector2(0, 150),
            Name = "Code",
        };
        d._text.AddThemeFontSizeOverride("font_size", 12);
        d.Body.AddChild(d._text);
        bool long_ = code.Length > ShareCodes.ComfortableLength;
        d._status = UiTheme.Label(long_
            ? $"{ShareCodes.Size(code.Length)}: that's long. Some chat apps cut long messages short; if it doesn't arrive whole, share the file instead."
            : $"{ShareCodes.Size(code.Length)}. Copied.", 14, long_ ? UiTheme.Amber : UiTheme.Good);
        d._status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        d.Body.AddChild(d._status);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 12);
        buttons.AddChild(UiTheme.Button("Copy again", () => { DisplayServer.ClipboardSet(code); d._status.Text = $"{ShareCodes.Size(code.Length)}. Copied again."; }, 150));
        buttons.AddChild(UiTheme.Button("Close", d.QueueFree, 150));
        d.Body.AddChild(buttons);
        return d;
    }

    /// <summary>
    /// Asks for a code. <paramref name="import"/> gets the text and returns a message to show on
    /// success; it throws (with a readable message) if the code is bad, and the dialog stays open.
    /// </summary>
    public static ShareCodeDialog Paste(string title, string what, string prefix, Func<string, string> import, Action? onDone = null)
    {
        var d = new ShareCodeDialog { Name = "PasteCodeDialog", _import = import, _onDone = onDone };
        d.Build(640);
        d.Body.AddChild(UiTheme.Title(title, 26));
        var intro = UiTheme.Label($"Paste a {what} code (it starts with {prefix}).", 14, UiTheme.Dim);
        intro.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        d.Body.AddChild(intro);
        d._text = new TextEdit { WrapMode = TextEdit.LineWrappingMode.Boundary, CustomMinimumSize = new Vector2(0, 150), PlaceholderText = prefix + "...", Name = "Code" };
        d._text.AddThemeFontSizeOverride("font_size", 12);
        string clip = DisplayServer.ClipboardGet() ?? "";
        if (clip.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) d._text.Text = clip.Trim();
        d.Body.AddChild(d._text);
        d._status = UiTheme.Label(d._text.Text.Length > 0 ? "Found a code on your clipboard." : "", 14, UiTheme.Dim);
        d._status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        d.Body.AddChild(d._status);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 12);
        buttons.AddChild(UiTheme.Button("Paste from clipboard", () => { d._text.Text = (DisplayServer.ClipboardGet() ?? "").Trim(); d._status.Text = ""; }, 200));
        Button go = UiTheme.Button("Import", () => d.TryImport(import, onDone), 150);
        go.AddThemeColorOverride("font_color", UiTheme.Amber);
        buttons.AddChild(go);
        buttons.AddChild(UiTheme.Button("Cancel", d.QueueFree, 120));
        d.Body.AddChild(buttons);
        return d;
    }

    /// <summary>For dev automation: sets the box text and presses Import. Returns the status line.</summary>
    public string DevImport(string code)
    {
        _text.Text = code;
        if (_import is not null) TryImport(_import, _onDone);
        return _status.Text;
    }

    private void TryImport(Func<string, string> import, Action? onDone)
    {
        try
        {
            string message = import(_text.Text);
            _status.Text = message;
            _status.AddThemeColorOverride("font_color", UiTheme.Good);
            onDone?.Invoke();
            GetTree().CreateTimer(1.2).Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
        }
        catch (Exception e)
        {
            _status.Text = $"Couldn't import: {e.Message}";
            _status.AddThemeColorOverride("font_color", UiTheme.Bad);
            Audio.Sfx.Play(Audio.SfxKind.Error, -6);
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            QueueFree();
            GetViewport().SetInputAsHandled();
        }
    }
}
