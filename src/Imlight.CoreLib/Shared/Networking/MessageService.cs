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
 * MESSAGE SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Provides a base abstract class for handling message-based communication 
 * and service interactions within a distributed actor-based networking system.
 * 
 * USAGE EXAMPLE:
 * // Sending a message to socket
 * SendToSocket(message);
 * 
 * // Asking another service for information
 * var response = AskOtherService<ResponseType>(requestMessage);
 * 
 * NOTE:
 * - SetWizBang() method
 * - SetState() method
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
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Shared.Networking;

internal abstract class MessageService(SessionActor sessionActor) : ReceiveProtocolDispatcher {

    protected SessionActor SessionActor { get; init; } = sessionActor;

    private Wizard _cachedWizard;
    private CoreObject _cachedWizardGameObject;

    /// <summary>
    /// Sends a message directly to the socket.
    /// </summary>
    /// <param name="message"></param>
    protected void SendToSocket(IMessage message) {
        if (SessionActor is null) {
            throw new ActorKilledException($"{GetType()} attempted to send message to undefined SessionActor.");
        }

        SessionActor.ActorRef.Tell(message);
    }

    /// <summary>
    /// Sends the SessionActor a server message. Used to send data to another service of the SessionActor.
    /// </summary>
    /// <param name="message"></param>
    protected void TellOtherServices(IServerMessage message) {
        if (message.ServiceID < 100) {
            throw new Exception($"You are sending a non-server message using {nameof(TellOtherServices)}! " +
                                $"Do not do this. Use {nameof(SendToSocket)} instead.");
        }

        SessionActor.ActorRef.Tell(message, Self);
    }

    /// <summary>
    /// Sends the SessionActor a server message. Used to send data to another service of the SessionActor.
    /// </summary>
    /// <param name="message"></param>
    protected void TellAllServices(IServerMessage message) {
        if (message.ServiceID < 100) {
            throw new Exception($"You are sending a non-server message using {nameof(TellOtherServices)}! " +
                                $"Do not do this. Use {nameof(SendToSocket)} instead.");
        }

        // The SessionActor has a check to not send a message to the sender.
        // We can just set the sender to the current actor, so it won't skip the service that sent it.
        SessionActor.ActorRef.Tell(message);
    }

    /// <summary>
    /// Broadcasts a message to the entire zone.
    /// </summary>
    /// <param name="originalMessage"></param>
    /// <param name="isSelfless"></param>
    /// <exception cref="ActorKilledException"></exception>
    protected void ZoneBroadcast(IMessage originalMessage, bool isSelfless = true) {
        if (SessionActor is null) {
            throw new ActorKilledException($"{GetType()} attempted to send message to undefined SessionActor.");
        }

        var message = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = originalMessage,
            Selfless = isSelfless,
            Sender = SessionActor.ActorRef
        };

        SessionActor.ActorRef.Tell(message, Self);
    }

    /// <summary>
    /// Broadcasts a message to the entire zone, excluding all players.
    /// </summary>
    /// <param name="originalMessage"></param>
    /// <exception cref="ActorKilledException"></exception>
    protected void ZoneBroadcastNoPlayers(IServerMessage originalMessage) {
        if (SessionActor is null) {
            throw new ActorKilledException($"{GetType()} attempted to send message to undefined SessionActor.");
        }

        var message = new ZONE_102_PROTOCOL.MSG_ZONESUPERVISORBROADCAST {
            Messages = [originalMessage],
            Sender = SessionActor.ActorRef
        };

        SessionActor.ActorRef.Tell(message, Self);
    }

    /// <summary>
    /// Asks the SessionActor for a return. Used to get data from another service of the SessionActor.
    /// </summary>
    /// <param name="message"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    protected T AskOtherService<T>(IServerMessage message)
        where T : IServerMessage {
        if (SessionActor is null) {
            throw new ActorKilledException($"{this.GetType()} attempted to send message to undefined SessionActor.");
        }

        if (message.ServiceID < 100) {
            throw new Exception($"You are sending a non-server message using {nameof(AskOtherService)}! " +
                                $"Do not do this. Use {nameof(SendToSocket)} instead.");
        }

        var task = SessionActor.HandleInternalAsk<T>(message);

        return task;
    }

    /// <summary>
    /// Asks the server the SessionActor is connected to.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    protected T AskServer<T>(IServerMessage message)
        where T : IServerMessage {
        if (SessionActor is null) {
            Logger.Error("{0} attempted to send message to undefined SessionActor.",
                Logger.Args(this.GetType()));

            return default(T);
        }
        if (message.ServiceID < 100) {
            throw new Exception($"You are sending a non-server message using {nameof(AskServer)}! " +
                                $"Do not do this. Use {nameof(SendToSocket)} instead.");
        }

        var task = SessionActor.AskServer<T>(message);

        return task;
    }

    /// <summary>
    /// Gets the connected account attached to the current SessionActor. An AccountService must be
    /// attached to the SessionActor prior.
    /// </summary>
    /// <returns></returns>
    protected Account GetSocketAccount() {
        // Get the account from the AccountService.
        var internalMessage = new ACCOUNT_104_PROTOCOL.MSG_QUERYACCOUNT();
        var account = AskOtherService<ACCOUNT_104_PROTOCOL.MSG_ACCOUNT>(internalMessage).Account;

        if (account is null) {
            throw new Exception($"{GetType()} could not get account from AccountService.");
        }

        return account;
    }

    /// <summary>
    /// Sends the SessionActor a close message.
    /// </summary>
    protected void CloseSession() {
        SessionActor.ActorRef.Tell("Close");

        // Remove the player from the online collection.
        OnlinePlayerCollection.RemoveOnlinePlayer(SessionActor.SessionID);
    }

    private void EnsureActiveWizardCached() {
        if (_cachedWizard is not null && _cachedWizardGameObject is not null) {
            return;
        }

        var msg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD();
        var response = AskOtherService<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(msg);
        _cachedWizard = response.Wizard;
        _cachedWizardGameObject = response.WizardGameObject;
    }

    protected CoreObject GetActiveGameObject() {
        EnsureActiveWizardCached();
        return _cachedWizardGameObject;
    }

    protected Wizard GetActiveWizard() {
        EnsureActiveWizardCached();
        return _cachedWizard;
    }

    protected Account GetActiveAccount() {
        return GetActiveWizard().Account;
    }

    /// <summary>
    /// Retrieves the WizardZoneObject associated with the specified global ID.
    /// </summary>
    /// <param name="globalId">The global ID of the WizardZoneObject.</param>
    /// <returns>The WizardZoneObject associated with the specified global ID. Null, if none was found.</returns>
    protected ZoneEntity GetZoneObject(ulong globalId) {
        var msg = new ZONE_102_PROTOCOL.MSG_QUERYZONEENTITY {
            GlobalID = globalId
        };
        var response = AskOtherService<ZONE_102_PROTOCOL.MSG_QUERYZONEENTITYRSP>(msg);
        if (response is null) {
            return null;
        }

        return response.ZoneObject;
    }

    /// <summary>
    /// Transfers the current zone to another zone. 
    /// </summary>
    /// <param name="destinationZone">The zone to transfer to.</param>
    /// <param name="doTeleportEffects">Specifies whether to show teleport effects. Default is true.</param>
    /// <param name="makePrivate">Specifies whether the zone should be private or not. Default is false.</param>
    /// <param name="ownerCharId">The character ID of the owner. Default is self.</param>
    /// <param name="destinationLocation">The location to transfer to. Default is "Start".</param>
    protected void Teleport(string destinationZone,
                            bool doTeleportEffects = true,
                            bool makePrivate = false,
                            ulong ownerCharId = 0,
                            string destinationLocation = "") {
        // Broadcast teleport effects to the zone, if applicable.
        if (doTeleportEffects) {
            var teleportEffectsMsg = new CHARACTER_103_PROTOCOL.MSG_DOTELEPORTEFFECTS();
            TellOtherServices(teleportEffectsMsg);

            // Wait 2 seconds for the effects to finish.
            Task.Delay(2000).Wait();
        }

        // If the destination location is nothing, default it to "Start."
        var zoneTransfer = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER {
            DestinationLocation = destinationLocation == "" ? "Start" : destinationLocation,
            DestinationZone = destinationZone,
            SendToClient = true,
            IsPrivate = makePrivate,
            OwnerCharId = ownerCharId == 0 ? GetActiveWizard().CharId : ownerCharId
        };
        TellOtherServices(zoneTransfer);
    }

    /// <summary>
    /// Checks if the player is online and retrieves the <see cref="OnlinePlayer"/> object.
    /// </summary>
    /// <param name="characterId">The character ID of the player.</param>
    /// <param name="onlinePlayer">The online player object.</param>
    /// <returns>True if the player is online, false otherwise.</returns>
    protected static bool TryGetOnlinePlayer(ulong characterId, out OnlinePlayer onlinePlayer) {
        onlinePlayer = default;
        var potentialPlayer = OnlinePlayerCollection.GetOnlinePlayer(characterId);

        if (potentialPlayer is null) {
            return false;
        }

        onlinePlayer = potentialPlayer;

        return true;
    }

    /// <summary>
    /// Informs the sender client with a message.
    /// </summary>
    /// <param name="reason">The reason for the message.</param>
    /// <param name="isImportant">Specifies whether the message is important or not. Default is false.</param>
    protected void InformGameClient(string reason, bool isImportant = false)
        => SessionActor.ActorRef.Tell(new EXTENDEDBASE_2_PROTOCOL.MSG_SERVERMESSAGE {
            Message = reason,
            Modal = (byte) (isImportant ? 1 : 0)
        });

    protected override void PreRestart(Exception reason, object message) {
        Logger.Error("MessageService {ServiceName} restarting due to {Reason}",
            Logger.Args(GetType().Name, reason.Message));

        base.PreRestart(reason, message);
    }

    /// <summary>
    /// Called when the service is about to be disposed. It is guaranteed that other services are still running,
    /// so this method is used to gracefully shutdown any service who's disposal may affect other services.
    /// Ensure that you call the base method when overriding this method to return a graceful shutdown response.
    /// </summary>
    protected virtual void OnPreDispose() {
        Sender.Tell(new SERVICE_101_PROTOCOL.MSG_PREDISPOSE());
    }

    /// <summary>
    /// Called when the service is disposed. This is where you should clean up any resources.
    /// </summary>
    protected virtual void OnDispose() {
        GC.SuppressFinalize(this);
    }

    #region Handlers

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_QUERYMESSAGESERVICEIDENTITY))]
    public void ReceiveMessageServiceIdentify(SERVICE_101_PROTOCOL.MSG_QUERYMESSAGESERVICEIDENTITY message) {
        var rsp = new SERVICE_101_PROTOCOL.MSG_MESSAGESERVICEIDENTITY() {
            Service = this
        };

        Sender.Tell(rsp);
    }

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_PREDISPOSE))]
    public void ReceivePreDispose(SERVICE_101_PROTOCOL.MSG_PREDISPOSE message) {
        OnPreDispose();
    }

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_DISPOSE))]
    public void ReceiveDispose(SERVICE_101_PROTOCOL.MSG_DISPOSE message) {
        OnDispose();
    }

    #endregion

}