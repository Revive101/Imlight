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
 * Created by: phazejeff
 * Version: KALI 1.0
 * Last Updated: 11/01/2026
 */

namespace Imlight.PluginApi.Scheduling;

/// <summary>
/// A task waiting to run through an <see cref="ITaskScheduler"/>.
/// </summary>
public interface IScheduledTask {

    /// <summary>
    /// Stops the task from running again. Safe to call from any thread, more than once, and from inside the task.
    /// A run that has already started finishes.
    /// </summary>
    void Cancel();

}
