/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Threading.Tasks;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imlight.CoreLib.Shared.Networking;

namespace Imlight.CoreLib.Game.Services;

public class ClientService(SessionActor sessionActor) : MessageService(sessionActor) {
    
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
