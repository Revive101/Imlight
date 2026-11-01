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
 * PLUGIN SCHEDULER
 * ========================================================================
 *
 * PURPOSE:
 * Runs one plugin's scheduled tasks on an actor of its own, so they never
 * overlap each other and a slow task can't stall a session or the command
 * dispatcher.
 *
 * USAGE EXAMPLE:
 * plugin.Scheduler = new PluginScheduler(system, plugin.Name);
 * ((PluginScheduler) plugin.Scheduler).CancelAll();
 *
 * NOTE:
 * Cancelling only sets a flag that the actor checks when a task comes due,
 * so nothing has to track the tasks waiting on the Akka scheduler. A repeating
 * task books its next run after each run finishes, so a slow one can't flood
 * the mailbox.
 *
 * Created by: phazejeff
 * Version: KALI 1.0
 * Last Updated: 11/01/2026
 */

using System;
using Akka.Actor;
using Imlight.Common;
using Imlight.PluginApi.Scheduling;

namespace Imlight.CoreLib.Plugins;

/// <summary>
/// Schedules one plugin's tasks for it, as an <see cref="ITaskScheduler"/>.
/// </summary>
internal sealed class PluginScheduler(ActorSystem system, string pluginName) : ITaskScheduler {

    private sealed class ScheduledTask(PluginScheduler owner, Action action, TimeSpan? interval) : IScheduledTask {

        private volatile bool _cancelled;

        public Action Action => action;

        public TimeSpan? Interval => interval;

        public bool IsCancelled => _cancelled || owner._cancelled;

        public void Cancel() => _cancelled = true;

    }

    private sealed class TaskActor : ReceiveActor, IWithTimers {

        public ITimerScheduler Timers { get; set; }

        public TaskActor(string pluginName)
            => Receive<ScheduledTask>(task => Run(task, pluginName));

        private void Run(ScheduledTask task, string pluginName) {
            if (task.IsCancelled) {
                return;
            }

            try {
                task.Action();
            }
            catch (Exception ex) {
                // A broken task must never restart the actor running the plugin's other tasks.
                Logger.Error("Plugin {Name} threw in a scheduled task: {Exception}", Logger.Args(pluginName, ex));
            }

            if (task.Interval is { } interval && !task.IsCancelled) {
                Timers.StartSingleTimer(task, task, interval);
            }
        }

    }

    private readonly IActorRef _actor = system.ActorOf(Props.Create(() => new TaskActor(pluginName)));

    private volatile bool _cancelled;

    public IScheduledTask RunAfter(TimeSpan delay, Action task) {
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(task);

        return Schedule(delay, new ScheduledTask(this, task, null));
    }

    public IScheduledTask RunEvery(TimeSpan interval, Action task) {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(task);

        return Schedule(interval, new ScheduledTask(this, task, interval));
    }

    /// <summary>
    /// Cancels every task, including any scheduled from here on.
    /// </summary>
    public void CancelAll() => _cancelled = true;

    private ScheduledTask Schedule(TimeSpan delay, ScheduledTask task) {
        system.Scheduler.ScheduleTellOnce(delay, _actor, task, ActorRefs.NoSender);

        return task;
    }

}
