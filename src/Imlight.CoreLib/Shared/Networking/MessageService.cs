/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Akka.Actor;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Shared.Networking;

public abstract class MessageService(SessionActor sessionActor) : ReceiveProtocolDispatcher {

    protected SessionActor SessionActor { get; init; } = sessionActor;

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

    /// <summary>
    /// Gets the active <see cref="TypeCache.CoreObject"/> of this session. Requires an active
    /// <see cref="WizardService"/> as a running service.
    /// </summary>
    /// <returns></returns>
    protected CoreObject GetActiveGameObject() {
        var msg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD();
        var response = AskOtherService<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(msg);

        return response.WizardGameObject;
    }

    /// <summary>
    /// Gets the active <see cref="Wizard"/> of this session. Requires an active
    /// <see cref="WizardService"/> as a running service.
    /// </summary>
    /// <returns></returns>
    protected Wizard GetActiveWizard() {
        var msg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD();
        var response = AskOtherService<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(msg);

        return response.Wizard;
    }

    /// <summary>
    /// Gets the active <see cref="Account"/> of this session. Requires an active
    /// <see cref="WizardService"/> as a running service.
    /// </summary>
    /// <returns></returns>
    protected Account GetActiveAccount() {
        var character = GetActiveWizard();

        return AccountCollection.GetAccount(character.AccountId);
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

    protected void DoZoneTransfer(string destinationZone, bool makePrivate = false, string destinationLocation = "") {
        // If the destination location is nothing, default it to "Start."
        var zoneTransfer = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER {
            DestinationLocation = destinationLocation == "" ? "Start" : destinationLocation,
            DestinationZone = destinationZone,
            SendToClient = true,
            IsPrivate = makePrivate,
            OwnerCharId = GetActiveWizard().CharId
        };
        TellOtherServices(zoneTransfer);
    }

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
