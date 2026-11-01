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
 * TASK SCHEDULER
 * ========================================================================
 *
 * PURPOSE:
 * Lets a plugin run code later or on a repeat without its own timers.
 *
 * USAGE EXAMPLE:
 * public override void OnEnable()
 *     => Scheduler.RunEvery(TimeSpan.FromMinutes(10), () => Server.Broadcast("Remember to stretch!"));
 *
 * Created by: phazejeff
 * Version: KALI 1.0
 * Last Updated: 11/01/2026
 */

using System;

namespace Imlight.PluginApi.Scheduling;

/// <summary>
/// Runs a plugin's code after a delay or on a repeat.
/// </summary>
/// <remarks>
/// Each plugin's tasks run one at a time on that plugin's own actor, so they never overlap each other, and a slow
/// task only delays that plugin's other tasks. They can still run at the same time as the plugin's event and command
/// handlers. A task that throws is logged, and a repeating task keeps repeating.
/// Every task is cancelled just before <see cref="ImlightPlugin.OnDisable"/>, and tasks scheduled after that never run.
/// </remarks>
public interface ITaskScheduler {

    /// <summary>
    /// Runs <paramref name="task"/> once, after <paramref name="delay"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delay"/> is negative.</exception>
    IScheduledTask RunAfter(TimeSpan delay, Action task);

    /// <summary>
    /// Runs <paramref name="task"/> repeatedly until it is cancelled, first after <paramref name="interval"/>.
    /// Each run starts <paramref name="interval"/> after the last one finished, so runs never pile up.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> is not positive.</exception>
    IScheduledTask RunEvery(TimeSpan interval, Action task);

}
