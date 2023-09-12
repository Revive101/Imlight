/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Common.Serializable.Caches;
using Imlight.Server.Shared.Networking;

namespace Imlight.Server.Game.Services;

public class ClientService : MessageService
{
    public ClientService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor)
    {
        return Akka.Actor.Props.Create(() => new ClientService(parentActor));
    }

    [MessageHandler(typeof(GAME.MSG_CLIENT_DISCONNECT))]
    private void ReceiveClientDisconnect(GAME.MSG_CLIENT_DISCONNECT message)
    {
        CloseSession();
    }
        
    [MessageHandler(typeof(GAME.MSG_QUERY_LOGOUT))]
    private void ReceiveQueryLogout(GAME.MSG_QUERY_LOGOUT message)
    {
        // Send the socket client disconnect, then wait about 1 second for the client to receive it before closing
        // the session.
        Task.Run(() =>
        {
            SendToSocket(new GAME.MSG_CLIENT_DISCONNECT());
            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
            CloseSession();
        });
    }
}