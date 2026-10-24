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
 * Last Updated: 10/24/2026
 */

namespace Imlight.PluginApi.Events;

/// <summary>
/// What sent a player to another zone, or to another spot in the same one.
/// </summary>
public enum ZoneChangeCause {

    /// <summary>
    /// Anything not listed below, such as teleporting to a friend or group member, a minigame or the tutorial.
    /// </summary>
    Other,

    /// <summary>
    /// A door, portal or zone trigger.
    /// </summary>
    Door,

    /// <summary>
    /// The Go Home button, which sends the player to their world's hub.
    /// </summary>
    Recall,

    /// <summary>
    /// The Go To Dorm button.
    /// </summary>
    Dorm,

    /// <summary>
    /// The Spiral Door's world list.
    /// </summary>
    WorldTeleport,

    /// <summary>
    /// A dungeon sigil whose countdown finished.
    /// </summary>
    Dungeon,

    /// <summary>
    /// Losing or fleeing a duel, which sends the player to their world's hub.
    /// </summary>
    Defeat,

    /// <summary>
    /// A teleport cantrip.
    /// </summary>
    Cantrip,

    /// <summary>
    /// An admin command.
    /// </summary>
    Command,

}

/// <summary>
/// Why a zone change the player was about to make didn't happen.
/// </summary>
public enum ZoneChangeFailReason {

    /// <summary>
    /// The server couldn't load the destination zone.
    /// </summary>
    ServerError,

    /// <summary>
    /// The client declined the transfer.
    /// </summary>
    ClientDeclined,

    /// <summary>
    /// The client has to patch files for the destination before it can go.
    /// </summary>
    PatchingBlocked,

}

/// <summary>
/// Where a zone event came from.
/// </summary>
public enum ZoneEventSource {

    /// <summary>
    /// The client's own scripting, such as the end of a cutscene. The client controls these entirely.
    /// </summary>
    Client,

    /// <summary>
    /// The player reaching a dialog entry that posts an event.
    /// </summary>
    Dialog,

    /// <summary>
    /// The player finishing loading into the zone.
    /// </summary>
    EnterZone,

}
