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
 * HANDSHAKE SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Marks a message service that must be created first when a session's
 * services are loaded, because it drives the connection handshake.
 * 
 * USAGE EXAMPLE:
 * internal class ControlService : MessageService, IHandshakeService { ... }
 * 
 * NOTE:
 * ControlService is the only implementer. Its constructor sends the
 * SessionOffer, the first thing the client waits for on connect, so it
 * must not queue behind the other services' setup Asks. The service list
 * comes from a HashSet (ServiceFactory.ServiceTypes), so its order cannot
 * be relied upon; this marker makes the ordering explicit.
 * See SessionActor.SetServices.
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 08/12/2026
 */

namespace Imlight.CoreLib.Shared.Networking;

/// <summary>
/// Marks a message service that must be created first when a session's services are loaded, because it drives
/// the connection handshake. ControlService is the one; its constructor sends the SessionOffer, the first
/// thing the client waits for on connect. SessionActor.SetServices creates services implementing this
/// interface before all others, so the offer goes out immediately instead of queuing behind the other
/// services' blocking identity Asks; a late offer outlives the client's patience at "enter world".
/// </summary>
internal interface IHandshakeService { }
