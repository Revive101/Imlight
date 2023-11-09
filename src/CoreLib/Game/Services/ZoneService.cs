/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Services;

public class ZoneService : MessageService {
    private IActorRef _zoneRef;
    private bool _isTransferQueued;

    public ZoneService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor) {
        return Akka.Actor.Props.Create(() => new ZoneService(parentActor));
    }

    protected override void OnPreDispose() {
        var globalId = GetActiveCoreObject().m_globalID;

        // If the zone reference is not null, we'll tell the zone to remove the player.
        _zoneRef?.Tell(new ZONE_102_PROTOCOL.MSG_REMOVEPLAYER() {
            Player = SessionActor.ActorRef,
            GlobalId = globalId
        });
        _zoneRef = null;

        base.OnPreDispose();
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONETRANSFER))]
    private void ReceiveZoneTransferRequest(ZONE_102_PROTOCOL.MSG_ZONETRANSFER message) {
        // This is an internal zone transfer message. It's meant to be the very first message sent to the
        // SessionActor in regards to a zone transfer. We're going to cache the destination zone and location,
        // then start the zone transfer handshake with the client.

        // Avoid duplicate transfer requests.
        if (_isTransferQueued) {
            return;
        }

        var character = GetActiveCharacter();

        // If the zone is ready and we're sending to client, begin the zone transfer handshake with the client.
        var zoneDetails = AskServer<ZONE_102_PROTOCOL.MSG_ZONETRANSFERRSP>(message);
        if (zoneDetails.ErrorCode == 0 && message.SendToClient) {
            _isTransferQueued = true;

            // Ask the client if it's okay with being transferred.
            var msg = new GAME_5_PROTOCOL.MSG_ZONETRANSFERREQUEST {
                ZoneName = message.DestinationZone,
                SendAck = 1
            };
            SendToSocket(msg);

            character.QueuedZoneName = message.DestinationZone;
            character.QueuedZoneLocation = message.DestinationLocation;
        }

        // If we're not sending this to client, this is an internal transfer, meaning we can immediately
        // setup the new details.
        if (!message.SendToClient) {
            _zoneRef = zoneDetails.ZoneActorRef;
        }

        Sender.Tell(zoneDetails);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_ZONETRANSFERACK))]
    private void ReceiveZoneTransferAck(GAME_5_PROTOCOL.MSG_ZONETRANSFERACK message) {
        DoZoneTransfer();
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_ZONETRANSFERNACK))]
    private void ReceiveZoneTransferNack(GAME_5_PROTOCOL.MSG_ZONETRANSFERNACK message) {
        Logger.Debug("Client was not OK with zone transfer! Possibly patching.");
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_RETRYTELEPORT))]
    private void ReceiveRetryTeleport(GAME_5_PROTOCOL.MSG_RETRYTELEPORT message) {
        DoZoneTransfer();
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
    private void ReceiveAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message) {
        if (_zoneRef is null) {
            throw new NullReferenceException(nameof(_zoneRef));
        }

        _zoneRef.Forward(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST))]
    private void ReceiveZoneBroadcast(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST message) {
        if (_zoneRef is null) {
            throw new Exception("Zone Reference was null.");
        }

        _zoneRef.Tell(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_FISHINTERACTION))]
    private void ReceiveZoneInteraction(ZONE_102_PROTOCOL.MSG_FISHINTERACTION message) {
        if (_zoneRef is null) {
            throw new Exception("Zone Reference was null.");
        }

        _zoneRef.Forward(message);
    }

    private void DoZoneTransfer() {
        var account = GetSocketAccount();
        var character = GetActiveCharacter();

        // Remove the player from their current zone. We're awaiting a reply so the zone can properly clean up
        // before we continue on potentially a different thread.
        var removePlayerMsg = new ZONE_102_PROTOCOL.MSG_REMOVEPLAYER() {
            Player = SessionActor.ActorRef,
            GlobalId = GetActiveCoreObject().m_globalID,
            IsPlayerStillConnected = true
        };
        _ = _zoneRef.Ask(removePlayerMsg).Result;

        // When we send this message, the client will disconnect from the current zone and reconnect to the next.
        // This means attach will happen again, so this is all we need to do here.
        var serverTransfer = new GAME_5_PROTOCOL.MSG_SERVERTRANSFER() {
            IP = character.GameServerIp,
            TCPPort = character.GameServerPort,
            UDPPort = character.GameServerPort,
            UserID = account.AccountId,
            CharID = character.CharId,
            ZoneName = character.QueuedZoneName,
            Location = character.QueuedZoneLocation,
            Slot = 0,
            SessionSlot = 0,
            SessionID = 0,
            TargetPlayerID = character.CharId,
            TransitionID = 1
        };
        SendToSocket(serverTransfer);
    }
}
