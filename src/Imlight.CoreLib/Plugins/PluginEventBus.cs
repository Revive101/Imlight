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
 * PLUGIN EVENT BUS
 * ========================================================================
 *
 * PURPOSE:
 * Delivers ImlightEvents to plugin handlers, synchronously, on the thread
 * of whichever actor fired the event.
 *
 * USAGE EXAMPLE:
 * var evt = PluginManager.Events.Fire(new OnPlayerChat(player, message));
 * if (evt.IsCancelled) { return; }
 *
 * NOTE:
 * Handlers are matched on the event's exact type.
 *
 * Created by: phazejeff
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using Imlight.Common;
using Imlight.PluginApi.Events;

namespace Imlight.CoreLib.Plugins;

internal sealed class PluginEventBus : IEventBus {

    // Each list is immutable, so firing can read it from any actor without locking.
    private readonly ConcurrentDictionary<Type, ImmutableList<Action<ImlightEvent>>> _handlers = new();

    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : ImlightEvent {
        ArgumentNullException.ThrowIfNull(handler);

        Action<ImlightEvent> wrapped = e => handler((TEvent) e);
        _ = _handlers.AddOrUpdate(typeof(TEvent), [wrapped], (_, list) => list.Add(wrapped));
    }

    /// <summary>
    /// Delivers the event to its handlers and returns it, so the caller can read the outcome.
    /// </summary>
    public TEvent Fire<TEvent>(TEvent evt) where TEvent : ImlightEvent {
        if (!_handlers.TryGetValue(evt.GetType(), out var handlers)) {
            return evt;
        }

        foreach (var handler in handlers) {
            try {
                handler(evt);
            }
            catch (Exception ex) {
                // A broken plugin must never take down the actor that fired the event.
                Logger.Error("A plugin threw handling {Event}: {Exception}", Logger.Args(evt.GetType().Name, ex));
            }
        }

        return evt;
    }

}
