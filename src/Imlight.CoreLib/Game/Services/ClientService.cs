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
 * CLIENT SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages client connection lifecycle, handling disconnection and logout 
 * processes for game server sessions.
 * 
 * USAGE EXAMPLE:
 * Internal service for managing client connection state and graceful 
 * disconnection mechanisms.
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Threading.Tasks;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imlight.CoreLib.Shared.Networking;

namespace Imlight.CoreLib.Game.Services;

internal class ClientService(SessionActor sessionActor) : MessageService(sessionActor) {
    
    protected static Props Props(SessionActor parentActor) 
        => Akka.Actor.Props.Create(() => new ClientService(parentActor));

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT))]
    private void ReceiveClientDisconnect() 
        => CloseSession();

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_QUERY_LOGOUT))]
    private void ReceiveQueryLogout(GAME_5_PROTOCOL.MSG_QUERY_LOGOUT message) =>
        // Send the socket client disconnect, then wait about 1 second for the client to receive it before closing
        // the session.
        Task.Run(() => {
            SendToSocket(new GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT());
            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
            CloseSession();
        });

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQASKSERVER))]
    private void ReceiveReqServer(GAME_5_PROTOCOL.MSG_REQASKSERVER message) {
        // TODO: Implement this message handler. This is here just so we don't get
        // a ton of unhandled message exceptions in the logs.
    }

}
