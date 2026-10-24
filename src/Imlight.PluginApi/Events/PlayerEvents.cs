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
 * Last Updated: 10/23/2026
 */

namespace Imlight.PluginApi.Events;

/// <summary>
/// Fired when a player says something in zone chat, before it is logged and shown to the zone.
/// Cancelling it drops the message. Group chat, quick chat and chat commands don't fire it, and whispers
/// fire <see cref="PlayerWhisperEvent"/> instead.
/// </summary>
public sealed class PlayerChatEvent(IPlayer player, string message) : ImlightEvent, ICancellable {

    /// <summary>
    /// The player who sent the message.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The message, with unsupported characters already removed.
    /// </summary>
    public string Message { get; } = message;

    /// <inheritdoc />
    public bool IsCancelled { get; set; }

}

/// <summary>
/// Fired when a player has been added to a zone: on first login to a wizard, and after every zone change.
/// </summary>
public sealed class PlayerZoneEnterEvent(IPlayer player, IZone zone) : ImlightEvent {

    /// <summary>
    /// The player who entered the zone.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The zone the player entered. <see cref="IZone.Players"/> already includes them.
    /// </summary>
    public IZone Zone { get; } = zone;

}

/// <summary>
/// Fired when a player's client attaches to the game server from login or after a dropped connection,
/// before they are placed in a zone. Zone changes don't fire it.
/// Cancelling it rejects the attach and closes the session.
/// </summary>
public sealed class PlayerLoginEvent(IPlayer player) : ImlightEvent, ICancellable {

    /// <summary>
    /// The player who is logging in. <see cref="IPlayer.Zone"/> is null, since they are not in a zone yet.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <inheritdoc />
    public bool IsCancelled { get; set; }

}

/// <summary>
/// Fired when a player's session ends (logging out, disconnecting or being kicked), before they are removed
/// from their zone. Zone changes don't fire it. A player who reconnects after a dropped connection may log in
/// again before the old session's quit fires.
/// </summary>
public sealed class PlayerQuitEvent(IPlayer player) : ImlightEvent {

    /// <summary>
    /// The player who is quitting.
    /// </summary>
    public IPlayer Player { get; } = player;

}

/// <summary>
/// Fired when a player is about to leave their zone for a different one, before the destination is loaded.
/// Teleports within the same zone don't fire it. Cancelling it keeps the player where they are.
/// </summary>
public sealed class PlayerZoneChangeEvent(IPlayer player, string destinationZone) : ImlightEvent, ICancellable {

    /// <summary>
    /// The player who is changing zones. <see cref="IPlayer.Zone"/> is the zone they are leaving.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The internal name of the zone the player is going to, e.g. <c>WizardCity/WC_Hub</c>.
    /// </summary>
    public string DestinationZone { get; } = destinationZone;

    /// <inheritdoc />
    public bool IsCancelled { get; set; }

}

/// <summary>
/// Fired when a player is about to go up one or more levels, from experience or from <see cref="IPlayer.SetLevel"/>.
/// Cancelling it keeps them at their current level; if experience caused it, they gain only as much
/// experience as fits within that level.
/// </summary>
public sealed class PlayerLevelUpEvent(IPlayer player, int newLevel) : ImlightEvent, ICancellable {

    /// <summary>
    /// The player who is levelling up. <see cref="IPlayer.Level"/> is still their old level.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The level the player is going to.
    /// </summary>
    public int NewLevel { get; } = newLevel;

    /// <inheritdoc />
    public bool IsCancelled { get; set; }

}

/// <summary>
/// Fired when a player whispers to another online player, before it is delivered.
/// Cancelling it drops the message. Quick chat whispers and group chat don't fire it.
/// </summary>
public sealed class PlayerWhisperEvent(IPlayer player, ulong targetCharacterId, string message)
    : ImlightEvent, ICancellable {

    /// <summary>
    /// The player who sent the whisper.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The <see cref="IPlayer.CharacterId"/> of the player being whispered to.
    /// </summary>
    public ulong TargetCharacterId { get; } = targetCharacterId;

    /// <summary>
    /// The message.
    /// </summary>
    public string Message { get; } = message;

    /// <inheritdoc />
    public bool IsCancelled { get; set; }

}
