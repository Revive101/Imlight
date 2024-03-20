/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Services;

public class ZoneService : MessageService {
    public IActorRef ZoneActor;
    private bool _isTransferQueued;

    public ZoneService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor) {
        return Akka.Actor.Props.Create(() => new ZoneService(parentActor));
    }

    protected override void OnPreDispose() {
        var globalId = GetActiveGameObject().m_globalID;

        // If the zone reference is not null, we'll tell the zone to remove the player.
        ZoneActor?.Tell(new ZONE_102_PROTOCOL.MSG_REMOVEPLAYER() {
            Player = SessionActor.ActorRef,
            GlobalId = globalId
        });
        ZoneActor = null;

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

        var character = GetActiveWizard();

        // If the zone is ready and we're sending to client, begin the zone transfer handshake with the client.
        var zoneDetails = AskServer<ZONE_102_PROTOCOL.MSG_ZONETRANSFERRSP>(message);
        if (zoneDetails.ErrorCode == 0 && message.SendToClient) {
            // Check if the destination zone is the same as the current zone. If so, we move the player to the
            // destination coordinates using a SERVERTELEPORT.
            if (message.DestinationZone == character.Zone) {
                // Split coordinate string by commas.
                var coords = message.DestinationLocation.Split(',').ToArray();
                // Put split string coordinates into Vector3.
                var destinationCoords = new SharpDX.Vector3(
                    float.Parse(coords[0]) / 4,
                    float.Parse(coords[1]) / 4,
                    float.Parse(coords[2]) / 4);

                var serverTele = new GAME_5_PROTOCOL.MSG_SERVERTELEPORT() {
                    LocationX = (ushort) destinationCoords.X,
                    LocationY = (ushort) destinationCoords.Y,
                    LocationZ = (ushort) destinationCoords.Z,
                    Direction = 0,
                    MobileID = GetActiveGameObject().m_nMobileID,
                };
                SendToSocket(serverTele);
                return;
            }

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
            ZoneActor = zoneDetails.ZoneActorRef;
        }

        Sender.Tell(zoneDetails);
    }

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_ZONEHOP))]
    private void ReceiveZoneHop(WIZARD2_53_PROTOCOL.MSG_ZONEHOP message) {
        var character = GetActiveWizard();

        _isTransferQueued = true;
        var zoneTransferRequestMessage = new GAME_5_PROTOCOL.MSG_ZONETRANSFERREQUEST {
            ZoneName = character.Zone,
            SendAck = 0
        };
        SendToSocket(zoneTransferRequestMessage);

        character.QueuedZoneName = character.Zone;
        character.QueuedZoneLocation = Util.GetCompactStringFromVector(character.Location, character.Orientation);
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_WORLDTELEPORTREQUEST))]
    private void ReceiveWorldTeleportRequest(WIZARD_12_PROTOCOL.MSG_WORLDTELEPORTREQUEST message) {
        var zoneName = message.World;
        if (zoneName.Length == 0) { // user clicked "exit", do nothing
            return;
        }

        // WizardCity goes to that tutorial place
        if (zoneName == "WizardCity") {
            zoneName = "WizardCity/WC_Ravenwood_Teleporter";
        } else {
            zoneName = AccessPassManager.GetContainedZoneName(zoneName);
        }
    
        var msg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER() {
            DestinationZone = zoneName,
            DestinationLocation = "Start",
            SendToClient = true
        };
        TellOtherServices(msg);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_ZONETRANSFERACK))]
    private void ReceiveZoneTransferAck(GAME_5_PROTOCOL.MSG_ZONETRANSFERACK message) {
        // The client has accepted the zone transfer. We can now send the server transfer message.
        DoZoneTransfer();
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_ZONETRANSFERNACK))]
    private void ReceiveZoneTransferNack(GAME_5_PROTOCOL.MSG_ZONETRANSFERNACK message) {
        // The client has denied the zone transfer. We can now send the server transfer message.
        Logger.Debug("Client was not OK with zone transfer! Possibly patching.");
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_RETRYTELEPORT))]
    private void ReceiveRetryTeleport(GAME_5_PROTOCOL.MSG_RETRYTELEPORT message) {
        DoZoneTransfer();
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
    private void ReceiveAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message) {
        // This is an internal message from MSG_ATTACH to add the player to the zone.
        if (ZoneActor is null) {
            throw new NullReferenceException(nameof(ZoneActor));
        }

        ZoneActor.Forward(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST))]
    private void ReceiveZoneBroadcast(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST message) {
        if (ZoneActor is null) {
            throw new Exception("Zone Reference was null.");
        }

        ZoneActor.Tell(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_FISHINTERACTION))]
    private void ReceiveZoneInteraction(ZONE_102_PROTOCOL.MSG_FISHINTERACTION message) {
        if (ZoneActor is null) {
            throw new Exception("Zone Reference was null.");
        }

        ZoneActor.Forward(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECT))]
    private void ReceiveQueryZoneObject(ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECT message) {
        if (ZoneActor is null) {
            throw new Exception("Zone Reference was null.");
        }

        ZoneActor.Forward(message);
    }

    private void DoZoneTransfer() {
        var account = GetSocketAccount();
        var character = GetActiveWizard();

        // Remove the player from their current zone. We're awaiting a reply so the zone can properly clean up
        // before we continue on potentially a different thread.
        var removePlayerMsg = new ZONE_102_PROTOCOL.MSG_REMOVEPLAYER() {
            Player = SessionActor.ActorRef,
            GlobalId = GetActiveGameObject().m_globalID,
            IsPlayerStillConnected = true
        };
        _ = ZoneActor.Ask(removePlayerMsg).Result;

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
