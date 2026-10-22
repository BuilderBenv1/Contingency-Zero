using System;
using System.Collections.Generic;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Map;

namespace ContingencyZero.Game.MapEditor;

/// <summary>Everything about a map that undo can restore (not its name, size or file).</summary>
public sealed class MapSnapshot
{
    public required int[] Heights { get; init; }
    public required int WaterLevel { get; init; }
    public required Fix64 WindMin { get; init; }
    public required Fix64 WindMax { get; init; }
    public required string Theme { get; init; }
    public required MetalSpot[] MetalSpots { get; init; }
    public required FeaturePlacement[] Features { get; init; }
    public required FixVec2[] StartPositions { get; init; }

    public static MapSnapshot Of(MapData m) => new()
    {
        Heights = (int[])m.Heights.Clone(),
        WaterLevel = m.WaterLevel,
        WindMin = m.WindMin,
        WindMax = m.WindMax,
        Theme = m.Theme,
        MetalSpots = m.MetalSpots.ToArray(),
        Features = m.Features.ToArray(),
        StartPositions = m.StartPositions.ToArray(),
    };

    public void ApplyTo(MapData m)
    {
        Array.Copy(Heights, m.Heights, Heights.Length);
        m.WaterLevel = WaterLevel;
        m.WindMin = WindMin;
        m.WindMax = WindMax;
        m.Theme = Theme;
        m.MetalSpots.Clear(); m.MetalSpots.AddRange(MetalSpots);
        m.Features.Clear(); m.Features.AddRange(Features);
        m.StartPositions.Clear(); m.StartPositions.AddRange(StartPositions);
    }

    /// <summary>Whether the map still matches this snapshot (so an empty stroke leaves no undo step).</summary>
    public bool SameAs(MapData m)
    {
        if (m.WaterLevel != WaterLevel || m.WindMin != WindMin || m.WindMax != WindMax || m.Theme != Theme) return false;
        if (m.MetalSpots.Count != MetalSpots.Length || m.Features.Count != Features.Length || m.StartPositions.Count != StartPositions.Length) return false;
        if (!m.Heights.AsSpan().SequenceEqual(Heights)) return false;
        for (int i = 0; i < MetalSpots.Length; i++) if (m.MetalSpots[i] != MetalSpots[i]) return false;
        for (int i = 0; i < Features.Length; i++) if (m.Features[i] != Features[i]) return false;
        for (int i = 0; i < StartPositions.Length; i++) if (m.StartPositions[i] != StartPositions[i]) return false;
        return true;
    }
}

/// <summary>
/// The map being edited plus where it came from, whether it has unsaved changes, and the undo
/// history. Every edit is wrapped in <see cref="Begin"/> / <see cref="Commit"/> so one stroke or
/// one click is one undo step.
/// </summary>
public sealed class EditorDocument
{
    private const int MaxUndo = 80;

    public MapData Map { get; }
    /// <summary>File name in user://maps once saved there (or opened from there); null for new maps and built-in templates.</summary>
    public string? FileName { get; set; }
    /// <summary>Where the map came from, for the title bar ("New map", "Template: Proving Grounds", ...).</summary>
    public string Origin { get; set; } = "New map";
    public bool Dirty { get; set; }

    private readonly List<MapSnapshot> _undo = new();
    private readonly List<MapSnapshot> _redo = new();
    private MapSnapshot? _pending;

    public EditorDocument(MapData map) { Map = map; }

    public bool CanUndo => _undo.Count > 0 && _pending is null;
    public bool CanRedo => _redo.Count > 0 && _pending is null;
    public bool InChange => _pending is not null;

    /// <summary>Remembers the map as it is now, before an edit.</summary>
    public void Begin()
    {
        _pending ??= MapSnapshot.Of(Map);
    }

    /// <summary>Ends the edit started by <see cref="Begin"/>. Returns false (and records nothing) if nothing changed.</summary>
    public bool Commit()
    {
        if (_pending is null) return false;
        MapSnapshot before = _pending;
        _pending = null;
        if (before.SameAs(Map)) return false;
        _undo.Add(before);
        if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
        _redo.Clear();
        Dirty = true;
        return true;
    }

    /// <summary>Throws away an edit in progress, putting the map back how it was.</summary>
    public void Cancel()
    {
        if (_pending is null) return;
        _pending.ApplyTo(Map);
        _pending = null;
    }

    public bool Undo()
    {
        if (!CanUndo) return false;
        _redo.Add(MapSnapshot.Of(Map));
        MapSnapshot s = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        s.ApplyTo(Map);
        Dirty = true;
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo) return false;
        _undo.Add(MapSnapshot.Of(Map));
        MapSnapshot s = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        s.ApplyTo(Map);
        Dirty = true;
        return true;
    }

    /// <summary>A deep copy of the map (for test play, so the match can't touch the document).</summary>
    public MapData CloneMap() => MapData.FromJson(Map.ToJson());
}
