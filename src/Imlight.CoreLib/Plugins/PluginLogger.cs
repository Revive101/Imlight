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
 * Forwards one plugin's log messages to the server's Logger, labelled with
 * the plugin's name.
 *
 * USAGE EXAMPLE:
 * plugin.Logger = new PluginLogger(plugin.Name);
 *
 * NOTE:
 * Messages go in as arguments rather than as the template, because a plugin
 * will usually pass an interpolated string that may hold braces.
 *
 * Created by: phazejeff
 * Version: KALI 1.0
 * Last Updated: 11/01/2026
 */

using System;
using Imlight.Common;
using Imlight.PluginApi;

namespace Imlight.CoreLib.Plugins;

/// <summary>
/// Logs for one plugin, as an <see cref="IPluginLogger"/>.
/// </summary>
internal sealed class PluginLogger(string pluginName) : IPluginLogger {

    private const string Template = "[{Plugin:l}] {Text:l}";

    public void Debug(string message) => Logger.Debug(Template, Logger.Args(pluginName, message));

    public void Information(string message) => Logger.Information(Template, Logger.Args(pluginName, message));

    public void Warning(string message) => Logger.Warning(Template, Logger.Args(pluginName, message));

    public void Error(string message) => Logger.Error(Template, Logger.Args(pluginName, message));

    public void Error(string message, Exception exception)
        => Logger.Error($"{Template}: {{Exception}}", Logger.Args(pluginName, message, exception));

}
