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
 * COMPONENT FRAMEWORK
 * ========================================================================
 * 
 * PURPOSE:
 * The Timers of a component that implements IWithTimers. Its timers run on the entity's
 * scheduler, keyed per component, and fire into that component alone.
 * 
 * USAGE EXAMPLE:
 * Timers.StartSingleTimer("FinalKill", new COMBAT_106_PROTOCOL.MSG_COMBATDEATH(), delay);
 * 
 * NOTE:
 * Keys only collide within one component, and CancelAll leaves the other components' timers
 * running.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;

namespace Imlight.CoreLib.Game.Zone.Core;

internal sealed class ComponentTimerScheduler(ITimerScheduler entityTimers, ZoneEntityComponent component) : ITimerScheduler {

    private sealed record Key(ZoneEntityComponent Component, object Inner);

    public IReadOnlyCollection<object> ActiveTimers => [.. entityTimers.ActiveTimers
        .OfType<Key>()
        .Where(key => ReferenceEquals(key.Component, component))
        .Select(key => key.Inner)];

    public void StartPeriodicTimer(object key, object msg, TimeSpan interval)
        => entityTimers.StartPeriodicTimer(Wrap(key), Envelope(msg), interval);

    public void StartPeriodicTimer(object key, object msg, TimeSpan interval, IActorRef sender)
        => entityTimers.StartPeriodicTimer(Wrap(key), Envelope(msg), interval, sender);

    public void StartPeriodicTimer(object key, object msg, TimeSpan initialDelay, TimeSpan interval)
        => entityTimers.StartPeriodicTimer(Wrap(key), Envelope(msg), initialDelay, interval);

    public void StartPeriodicTimer(object key, object msg, TimeSpan initialDelay, TimeSpan interval, IActorRef sender)
        => entityTimers.StartPeriodicTimer(Wrap(key), Envelope(msg), initialDelay, interval, sender);

    public void StartSingleTimer(object key, object msg, TimeSpan timeout)
        => entityTimers.StartSingleTimer(Wrap(key), Envelope(msg), timeout);

    public void StartSingleTimer(object key, object msg, TimeSpan timeout, IActorRef sender)
        => entityTimers.StartSingleTimer(Wrap(key), Envelope(msg), timeout, sender);

    public bool IsTimerActive(object key) => entityTimers.IsTimerActive(Wrap(key));

    public void Cancel(object key) => entityTimers.Cancel(Wrap(key));

    public void CancelAll() {
        foreach (var key in ActiveTimers) {
            Cancel(key);
        }
    }

    private Key Wrap(object key) => new(component, key);

    private ComponentMessage Envelope(object msg) => new(component, msg);

}
