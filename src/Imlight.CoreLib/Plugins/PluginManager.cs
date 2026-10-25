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
 * PLUGIN MANAGER
 * ========================================================================
 *
 * PURPOSE:
 * Loads plugin DLLs from a directory, enables them, and owns the event bus
 * the rest of the server fires into.
 *
 * USAGE EXAMPLE:
 * PluginManager.LoadAll("./plugins");  // at boot, before servers accept connections
 * PluginManager.DisableAll();          // on shutdown
 *
 * NOTE:
 * Plugins are loaded into the default load context, so they share the
 * server's copy of Imlight.PluginApi. A plugin's own dependencies are not
 * resolved; for now a plugin must be a single DLL.
 *
 * Created by: phazejeff
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using Imlight.Common;
using Imlight.PluginApi;

namespace Imlight.CoreLib.Plugins;

/// <summary>
/// Loads plugins and connects them to the server.
/// </summary>
public static class PluginManager {

    private static readonly List<ImlightPlugin> s_plugins = [];

    internal static PluginEventBus Events { get; } = new();

    internal static PluginCommandRegistry Commands { get; } = new();

    internal static PluginServer Server { get; } = new();

    /// <summary>
    /// Loads every plugin DLL in the directory, creating it if needed, and enables each plugin.
    /// A plugin that fails is logged and skipped.
    /// </summary>
    public static void LoadAll(string directory) {
        _ = Directory.CreateDirectory(directory);

        foreach (var path in Directory.GetFiles(directory, "*.dll")) {
            try {
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(path));
                var pluginTypes = assembly.GetTypes().Where(x => x.IsSubclassOf(typeof(ImlightPlugin)) && !x.IsAbstract);

                foreach (var type in pluginTypes) {
                    Enable((ImlightPlugin) Activator.CreateInstance(type)!);
                }
            }
            catch (Exception ex) {
                Logger.Error("Could not load plugin {Path}: {Exception}", Logger.Args(path, ex));
            }
        }
    }

    /// <summary>
    /// Disables every plugin, in the reverse of the order they were enabled.
    /// </summary>
    public static void DisableAll() {
        for (var i = s_plugins.Count - 1; i >= 0; i--) {
            try {
                s_plugins[i].OnDisable();
            }
            catch (Exception ex) {
                Logger.Error("Plugin {Name} failed to disable: {Exception}", Logger.Args(s_plugins[i].Name, ex));
            }
        }

        s_plugins.Clear();
    }

    private static void Enable(ImlightPlugin plugin) {
        try {
            plugin.Events = Events;
            plugin.Commands = Commands.For(plugin);
            plugin.Server = Server;
            plugin.OnEnable();
            s_plugins.Add(plugin);

            Logger.Information("Enabled plugin {Name} {Version}.", Logger.Args(plugin.Name, plugin.Version));
        }
        catch (Exception ex) {
            // Don't leave a half-enabled plugin's commands reachable.
            Commands.RemoveAll(plugin);
            Logger.Error("Plugin {Type} failed to enable: {Exception}", Logger.Args(plugin.GetType().Name, ex));
        }
    }

}
