using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.Game.Zone.Core;

/// <summary>
/// The current state of every named object in one zone instance (a lever's "On", a door's "Idle_Open").
/// Trigger requirements read it and object interactions and trigger results write it; it lives and dies
/// with the zone, so every instance keeps its own puzzle progress.
/// </summary>
public sealed class ZoneObjectStates {

    private readonly ConcurrentDictionary<string, string> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _changed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Records the state an object enters. Returns false when it was already in that state.</summary>
    public bool Set(string objectName, string state) {
        if (string.IsNullOrEmpty(objectName)) {
            return false;
        }

        var changed = false;
        _changed[objectName] = 0;
        _states.AddOrUpdate(
            objectName,
            _ => { changed = true; return state ?? string.Empty; },
            (_, old) => { changed = !string.Equals(old, state, StringComparison.Ordinal); return state ?? string.Empty; });

        return changed;
    }

    /// <summary>Records the state an object starts in, unless it has already been changed.</summary>
    public void SeedDefault(string objectName, string state) {
        if (!string.IsNullOrEmpty(objectName) && !string.IsNullOrEmpty(state)) {
            _states.TryAdd(objectName, state);
        }
    }

    public string Get(string objectName)
        => !string.IsNullOrEmpty(objectName) && _states.TryGetValue(objectName, out var state) ? state : null;

    public bool IsIn(string objectName, string state)
        => string.Equals(Get(objectName), state, StringComparison.OrdinalIgnoreCase);

    /// <summary>The state of an object something changed since the zone loaded, else null.</summary>
    public string GetIfChanged(string objectName)
        => !string.IsNullOrEmpty(objectName) && _changed.ContainsKey(objectName) ? Get(objectName) : null;

    /// <summary>Every object something changed since the zone loaded, with its state.</summary>
    public KeyValuePair<string, string>[] SnapshotChanged()
        => [.. _states.Where(kv => _changed.ContainsKey(kv.Key))];

}
