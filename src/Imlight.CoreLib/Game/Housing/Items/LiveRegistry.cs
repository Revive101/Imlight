/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * HOUSING SYSTEM
 * ========================================================================
 *
 * PURPOSE:
 * Finds, creates and shares the live state of everything in use, one state per key, for the house and the attic registries.
 *
 * USAGE EXAMPLE:
 * var registry = new LiveRegistry<ulong, HouseState>(state => state.LotGid, state => state.Viewers);
 * if (registry.TryEnter(lotGid, () => Load(lotGid), out var state)) { ... registry.Release(state); }
 *
 * NOTE:
 * - A state leaves the registry when its last viewer leaves; it is already persisted by then.
 * - The load runs under a lock of its key, after any earlier state has stored its last change.
 * - Get publishes a state without counting a viewer, so a caller that does not go on to TryEnter leaves it live
 *   until a viewer comes and goes.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/21/2026
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Imlight.CoreLib.Shared.Utilities;

namespace Imlight.CoreLib.Game.Housing.Items;

internal sealed class LiveRegistry<TKey, TState>(Func<TState, TKey> keyOf, Func<TState, ViewerCount> viewersOf)
    where TKey : notnull
    where TState : class {

    private readonly ConcurrentDictionary<TKey, TState> _states = new();
    private readonly StripedLocks _creationLocks = new();

    /// <summary>
    /// Finds the live state of a key, loading it when it is not live.
    /// </summary>
    /// <param name="key">The key to find.</param>
    /// <param name="load">Loads the state; <c>null</c> when it cannot be loaded.</param>
    /// <returns>The live state, or <c>null</c> when it cannot be loaded.</returns>
    internal TState Get(TKey key, Func<TState> load) {
        while (true) {
            if (TryGetLive(key, out var live)) {
                return live;
            }

            var published = Publish(key, load, out var raced);
            if (!raced) {
                return published;
            }
        }
    }

    /// <summary>
    /// Finds the live state of a key and counts a viewer in.
    /// </summary>
    /// <param name="key">The key to find.</param>
    /// <param name="load">Loads the state; <c>null</c> when it cannot be loaded.</param>
    /// <param name="state">The live state when this returns <c>true</c>; call <see cref="Release"/> when the viewer is gone.</param>
    /// <returns><c>false</c> when the state cannot be loaded.</returns>
    internal bool TryEnter(TKey key, Func<TState> load, out TState state) {
        while ((state = Get(key, load)) is not null) {
            if (viewersOf(state).Enter()) {
                return true;
            }

            Evict(state);
        }

        return false;
    }

    /// <summary>
    /// Counts a viewer out of a state and drops the state from the registry when nobody is left.
    /// </summary>
    /// <param name="state">The state the viewer entered with <see cref="TryEnter"/>.</param>
    internal void Release(TState state) {
        if (viewersOf(state).Leave()) {
            Evict(state);
        }
    }

    private bool TryGetLive(TKey key, out TState state) {
        if (!_states.TryGetValue(key, out state)) {
            return false;
        }

        if (!viewersOf(state).IsRetired) {
            return true;
        }

        Evict(state);
        state = null;

        return false;
    }

    private TState Publish(TKey key, Func<TState> load, out bool raced) {
        lock (_creationLocks.For((ulong) key.GetHashCode())) {
            raced = _states.ContainsKey(key);
            if (raced) {
                return null;
            }

            var state = load();
            if (state is not null) {
                _states[key] = state;
            }

            return state;
        }
    }

    private void Evict(TState state)
        => _states.TryRemove(new KeyValuePair<TKey, TState>(keyOf(state), state));

}
