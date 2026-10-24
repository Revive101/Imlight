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
 * IMLIGHT PLUGIN
 * ========================================================================
 *
 * PURPOSE:
 * The base class every plugin derives from. The server creates every
 * non-abstract subclass it finds in the plugins directory and calls
 * OnEnable at boot and OnDisable at shutdown.
 *
 * USAGE EXAMPLE:
 * public sealed class GreeterPlugin : ImlightPlugin {
 *     public override string Name => "Greeter";
 *     public override string Version => "1.0.0";
 *
 *     public override void OnEnable()
 *         => Events.Subscribe<OnPlayerZoneEnter>(e => e.Player.SendMessage($"Welcome to {e.Zone}!"));
 * }
 *
 * Created by: phazejeff
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using Imlight.PluginApi.Commands;
using Imlight.PluginApi.Events;

namespace Imlight.PluginApi;

/// <summary>
/// The base class for an Imlight plugin. Subclasses need a public parameterless constructor.
/// </summary>
public abstract class ImlightPlugin {

    /// <summary>
    /// The plugin's name, shown in the server log.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// The plugin's version, shown in the server log.
    /// </summary>
    public abstract string Version { get; }

    /// <summary>
    /// The server's event bus. Subscribe to events here in <see cref="OnEnable"/>.
    /// </summary>
    public IEventBus Events { get; internal set; } = null!;

    /// <summary>
    /// Registers this plugin's chat commands. Register them here in <see cref="OnEnable"/>.
    /// </summary>
    public ICommandRegistry Commands { get; internal set; } = null!;

    /// <summary>
    /// Called at boot, before the servers accept connections.
    /// </summary>
    public virtual void OnEnable() { }

    /// <summary>
    /// Called when the server process exits.
    /// </summary>
    public virtual void OnDisable() { }

}
