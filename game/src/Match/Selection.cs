using System;
using System.Collections.Generic;
using System.Linq;
using ContingencyZero.Sim;

namespace ContingencyZero.Game.Match;

/// <summary>The player's current selection and their ten control groups.</summary>
public sealed class Selection
{
    private readonly List<int> _ids = new();
    private readonly List<int>[] _groups = Enumerable.Range(0, 10).Select(_ => new List<int>()).ToArray();

    public IReadOnlyList<int> Ids => _ids;
    public int Count => _ids.Count;
    public event Action? Changed;

    public bool Contains(int id) => _ids.Contains(id);

    public void Set(IEnumerable<int> ids)
    {
        _ids.Clear();
        foreach (int id in ids) if (!_ids.Contains(id)) _ids.Add(id);
        Changed?.Invoke();
    }

    public void Add(IEnumerable<int> ids)
    {
        foreach (int id in ids) if (!_ids.Contains(id)) _ids.Add(id);
        Changed?.Invoke();
    }

    public void Toggle(int id)
    {
        if (!_ids.Remove(id)) _ids.Add(id);
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (_ids.Count == 0) return;
        _ids.Clear();
        Changed?.Invoke();
    }

    /// <summary>Drops dead units. Call once per tick.</summary>
    public void Prune(World world)
    {
        int before = _ids.Count;
        _ids.RemoveAll(id => !world.Units.IsValid(id));
        foreach (var g in _groups) g.RemoveAll(id => !world.Units.IsValid(id));
        if (_ids.Count != before) Changed?.Invoke();
    }

    public void SaveGroup(int group)
    {
        _groups[group].Clear();
        _groups[group].AddRange(_ids);
    }

    public void AddToGroup(int group)
    {
        foreach (int id in _ids) if (!_groups[group].Contains(id)) _groups[group].Add(id);
    }

    public IReadOnlyList<int> Group(int group) => _groups[group];
}
