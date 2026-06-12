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
 * GAME TRANSITION SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages the process of transitioning a player from the login server 
 * to the appropriate game server, handling character selection and 
 * initial game server connection logic.
 * 
 * USAGE EXAMPLE:
 * 
 * NOTE:
 * 
 * TODO:
 * - Source LoginServer name from configuration instead of hardcoding
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using Akka.Actor;
using Imcodec.IO;
using Imcodec.Math;
using Imcodec.MessageLayer.Generated;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Login.Services;

internal class GameTransitionService(SessionActor sessionActor) : MessageService(sessionActor) {

    private const string FALLBACK_ZONE_NAME = "WizardCity/WC_Hub";

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

        var wizardName = character.PlayerNameBehavior.GetWizardName();
        Logger.Information("Sending wizard {name} to game server {IP}:{Port}.",
            Logger.Args(wizardName, gameServer.IP, gameServer.Port));

        var zoneName = DetermineZone(character);
        var location = DetermineLocation(character);

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
            ZoneName = zoneName,
            Location = location,
        };

        // Cache the message if the player is queued.
        if (serverEnqueueResult.PrepPhase > 0) {
            SessionActor.CachedDequeueMessage = charSelectedMsg;
            SendToSocket(serverEnqueueResult);
        }
        else {
            // There is no queue. Send the message to the game server.
            SendToSocket(charSelectedMsg);
            CloseSession();
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

        CloseSession();
    }

    private string DetermineZone(Wizard wizard) {
        // The player may have logged out in a zone we can't put them back into.

        // Minigames:
        if (wizard.Zone.Contains("Phantom")) {
            if (string.IsNullOrEmpty(wizard.PreviousZone) || wizard.PreviousZone.Contains("Phantom")) {
                return FALLBACK_ZONE_NAME;
            }
            
            return wizard.PreviousZone;
        }

        return wizard.Zone;
    }

    private string DetermineLocation(Wizard wizard) {
        if (wizard.Location == Vector3.Zero) {
            return "Start";
        }

        // The player may have logged out in a zone we can't put them back into.

        // Minigames:
        if (wizard.Zone.Contains("Phantom")) {
            return "Start";
        }

        return Util.GetCompactStringFromVector(wizard.Location, wizard.Orientation);
    }

}
