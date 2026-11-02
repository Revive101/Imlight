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
 * PLUGIN LOGGER
 * ========================================================================
 *
 * PURPOSE:
 * Lets a plugin write to the server log, labelled with the plugin's name.
 *
 * USAGE EXAMPLE:
 * public override void OnEnable() => Logger.Information($"Loaded from {DataFolder}");
 *
 * Created by: phazejeff
 * Version: KALI 1.0
 * Last Updated: 11/01/2026
 */

using System;

namespace Imlight.PluginApi;

/// <summary>
/// Writes to the server log, with each line labelled with the plugin's name.
/// </summary>
/// <remarks>
/// Messages are written as plain text, so braces in them are safe. The server's configured log level decides which
/// messages are kept.
/// </remarks>
public interface IPluginLogger {

    /// <summary>
    /// Logs detail that is only useful when debugging the plugin.
    /// </summary>
    void Debug(string message);

    /// <summary>
    /// Logs something worth knowing during normal running.
    /// </summary>
    void Information(string message);

    /// <summary>
    /// Logs something unexpected that the plugin can carry on from.
    /// </summary>
    void Warning(string message);

    /// <summary>
    /// Logs a failure.
    /// </summary>
    void Error(string message);

    /// <summary>
    /// Logs a failure and the exception that caused it.
    /// </summary>
    void Error(string message, Exception exception);

}
