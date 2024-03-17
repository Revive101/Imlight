/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.IO;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;
using SharpDX;

namespace Imlight.CoreLib.Login.Services;

internal class GameTransitionService : MessageService {
    public GameTransitionService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new GameTransitionService(parentActor));

    [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_SELECTCHARACTER))]
    private void ReceiveSelectCharacter(LOGIN_7_PROTOCOL.MSG_SELECTCHARACTER message) {
        // If the socket account cannot be found, send the client an error.
        var account = GetSocketAccount();
        if (account is null) {
            Logger.Error("Service {Type} socket account could not be retrieved!", Logger.Args(GetType()));
            SendErrorToSocket();
            return;
        }

        // If the given character does not exist on this account, send the client an error.
        var character = account.GetCharacter(message.CharID);
        if (character is null) {
            Logger.Warning("Account {Id} attempted to get a character it didn't have.", Logger.Args(account.AccountId));
            SendErrorToSocket();
            return;
        }

        // Enqueue the session actor onto the game server and create a session key.
        var gameServer = GetGameServer();
        var serverEnqueueResult = (LOGIN_7_PROTOCOL.MSG_CHARACTERSELECTED) SessionActor.EnqueueToServer(gameServer.ActorRef);
        var allocatedKey = CreateSessionKey(gameServer.ActorRef, account);

        var wizardNAme = character.PlayerNameBehavior.GetWizardName();
        Logger.Information("Sending wizard {name} to game server {IP}:{Port}.",
            Logger.Args(wizardNAme, gameServer.IP, gameServer.Port));

        // If this character was just made, their default location is Vector3.Zero.
        // In such a case, we'll default them to a location called "Start," which the game client
        // usually has a location for.
        var stringLocation = character.Location == Vector3.Zero
            ? "Start"
            : Util.GetCompactStringFromVector(character.Location, character.Orientation);

        var charSelectedMsg = new LOGIN_7_PROTOCOL.MSG_CHARACTERSELECTED() {
            // Set details about the game server.
            IP = gameServer.IP,
            TCPPort = gameServer.Port,
            UDPPort = gameServer.Port,
            Key = allocatedKey,                   // Login server -> game server session key.
            PrepPhase = 0,                        // (0|1): Player is in queue.
            Slot = 0,                             // The player's position in said queue.
            LoginServer = "Imlight.Login",        // TODO: This should be sourced from elsewhere.

            // Set details about the character.
            UserID = account.AccountId,
            CharID = character.CharId,
            ZoneID = new GID((ulong) gameServer.Port),
            ZoneName = character.Zone,
            Location = stringLocation,
        };

        // Cache the message if the player is queued.
        if (serverEnqueueResult.PrepPhase > 0) {
            SessionActor.CachedDequeueMessage = charSelectedMsg;
            SendToSocket(serverEnqueueResult);
        }
        else {
            SendToSocket(charSelectedMsg);
        }
    }

    private SERVER_100_PROTOCOL.MSG_SERVERINFO GetGameServer() {
        var msg = new SERVER_100_PROTOCOL.MSG_GETBESTSERVER();
        return AskServer<SERVER_100_PROTOCOL.MSG_SERVERINFO>(msg);
    }

    private ByteString CreateSessionKey(ICanTell gameServerRef, Account account) {
        var msg = new SERVER_100_PROTOCOL.MSG_CREATEKEY() {
            Account = account
        };

        return gameServerRef.Ask<SERVER_100_PROTOCOL.MSG_CREATEKEYRSP>(msg)
            .Result
            .Key;
    }

    private void SendErrorToSocket(int errorCode = 1) {
        var msg = new LOGIN_7_PROTOCOL.MSG_CHARACTERSELECTED() { Error = errorCode };
        SendToSocket(msg);
    }
}
