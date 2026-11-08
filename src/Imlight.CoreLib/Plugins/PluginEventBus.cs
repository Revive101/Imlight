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
 * Last Updated: 11/08/2026
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using Imlight.Common;
using Imlight.PluginApi;
using Imlight.PluginApi.Events;

namespace Imlight.CoreLib.Plugins;

internal sealed class PluginEventBus {

    private sealed record Handler(ImlightPlugin Owner, Action<ImlightEvent> Action);

    private sealed class Scope(PluginEventBus bus, ImlightPlugin owner) : IEventBus {

        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : ImlightEvent
            => bus.Subscribe(owner, handler);

    }

    // Each list is immutable, so firing can read it from any actor without locking.
    private readonly ConcurrentDictionary<Type, ImmutableList<Handler>> _handlers = new();

    /// <summary>
    /// The event bus a plugin sees, which subscribes handlers under that plugin.
    /// </summary>
    public IEventBus For(ImlightPlugin plugin)
        => new Scope(this, plugin);

    /// <summary>
    /// Removes every handler the plugin subscribed.
    /// </summary>
    public void RemoveAll(ImlightPlugin plugin) {
        foreach (var type in _handlers.Keys) {
            _ = _handlers.AddOrUpdate(type, [], (_, list) => list.RemoveAll(x => x.Owner == plugin));
        }
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
                handler.Action(evt);
            }
            catch (Exception ex) {
                // A broken plugin must never take down the actor that fired the event.
                Logger.Error("Plugin {Name} threw handling {Event}: {Exception}",
                    Logger.Args(handler.Owner.Name, evt.GetType().Name, ex));
            }
        }

        return evt;
    }

    private void Subscribe<TEvent>(ImlightPlugin owner, Action<TEvent> handler) where TEvent : ImlightEvent {
        ArgumentNullException.ThrowIfNull(handler);

        var wrapped = new Handler(owner, e => handler((TEvent) e));
        _ = _handlers.AddOrUpdate(typeof(TEvent), [wrapped], (_, list) => list.Add(wrapped));
    }

}
