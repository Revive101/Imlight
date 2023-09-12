/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Net;
using Akka.Actor;
using Imlight.Common.IO;
using Imlight.Common.Serializable.ObjectProperty;
using WizUnraveler;
using WizUnraveler.Cache;
using Imlight.Common.Utilities;
using Imlight.Server.Login.Models;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;

namespace Imlight.Server.Login.Services;

internal class GameTransitionService : MessageService
{
    public GameTransitionService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor)
    {
        return Akka.Actor.Props.Create(() => new GameTransitionService(parentActor));
    }

    [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_SELECTCHARACTER))]
    private void ReceiveSelectCharacter(LOGIN_7_PROTOCOL.MSG_SELECTCHARACTER message)
    {
        // If the socket account cannot be found, send the client an error.
        var account = GetSocketAccount();
        if (account is null)
        {
            Log.Error("Service {Type} socket account could not be retrieved!", Log.Args(GetType()));
            SendErrorToSocket();
            return;
        }

        // If the given character does not exist on this account, send the client an error.
        var character = account.GetCharacter(message.CharID);
        if (character is null)
        {
            Log.Warning("Account {Id} attempted to get a character it didn't have.", Log.Args(account.AccountId));
            SendErrorToSocket();
            return;
        }

        // Enqueue the session actor onto the game server and create a session key.
        var gameServer = GetGameServer();
        var serverEnqueueResult = (LOGIN_7_PROTOCOL.MSG_CHARACTERSELECTED)SessionActor.EnqueueToServer(gameServer.ActorRef);
        var allocatedKey = CreateSessionKey(gameServer.ActorRef, account);

        // Craft a successful message. This will instead be cached if the server is full.
        var charSelectedMsg = new LOGIN_7_PROTOCOL.MSG_CHARACTERSELECTED()
        {
            // Set details about the game server.
            IP = gameServer.IP,
            TCPPort = gameServer.Port,
            UDPPort = gameServer.Port,
            Key = allocatedKey,                   // Login server -> game server session key.
            PrepPhase = 0,                        // (0|1): Player is in queue.
            Slot = 0,                             // The player's position in said queue.
            LoginServer = "Imlight.Login",      // TODO: This should be sourced from elsewhere.
                
            // Set details about the character.
            UserID = account.AccountId,
            CharID = character.CharId,
            ZoneID = new GID((ulong)gameServer.Port),
            ZoneName = character.Zone,
            Location = character.GetStringLocation(),
        };
            
        // Cache the message if the player is queued.
        if (serverEnqueueResult.PrepPhase > 0)
        {
            SessionActor.CachedDequeueMessage = charSelectedMsg;
            SendToSocket(serverEnqueueResult);
        }
        else
        {
            SendToSocket(charSelectedMsg);
        }
    }

    private SERVER_100_PROTOCOL.MSG_SERVERINFO GetGameServer()
    {
        var msg = new SERVER_100_PROTOCOL.MSG_QUERYGAMESERVERS();
            
#if DEBUG
        var localEndPoint = (IPEndPoint)SessionActor.Socket.LocalEndPoint;
        var isLocal = localEndPoint.Address.ToString().Contains("127.0.");
        msg = new SERVER_100_PROTOCOL.MSG_QUERYGAMESERVERS() { IsLocal = isLocal };
#else
            // Release builds should never be able to connect to their own local server.
            msg = new SERVER_100_PROTOCOL.MSG_QUERYGAMESERVERS() { IsLocal = false };
#endif
            
        return AskServer<SERVER_100_PROTOCOL.MSG_SERVERINFO>(msg);
    }

    private ByteString CreateSessionKey(ICanTell gameServerRef, Account account)
    {
        var msg = new SERVER_100_PROTOCOL.MSG_CREATEKEY()
        {
            Account = account
        };

        return gameServerRef.Ask<SERVER_100_PROTOCOL.MSG_CREATEKEYRSP>(msg)
            .Result
            .Key;
    }

    private void SendErrorToSocket(int errorCode = 1)
    {
        var msg = new LOGIN_7_PROTOCOL.MSG_CHARACTERSELECTED() { Error = errorCode };
        SendToSocket(msg);
    }
}