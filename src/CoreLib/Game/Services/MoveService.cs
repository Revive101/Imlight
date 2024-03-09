/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using SharpDX;

namespace Imlight.CoreLib.Game.Services;

internal class MoveService : MessageService {

    private const byte MarkManaCostPercent = 20;

    public MoveService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor) {
        return Akka.Actor.Props.Create(() => new MoveService(parentActor));
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVE))]
    private void ReceiveClientMove(GAME_5_PROTOCOL.MSG_CLIENTMOVE message) {
        // MoveService saves the location of the player's game object.
        // CharacterService saves the location of the player's character persistently.

        // Restore actual location information, as it is compressed by a factor of 4 and unsigned.
        // Yaw is represented in radians in the client, but transmitted to the server as degrees.
        var x = unchecked((short) message.LocationX) * 4.0f;
        var y = unchecked((short) message.LocationY) * 4.0f;
        var z = unchecked((short) message.LocationZ) * 4.0f;
        var direction = (float) (message.Direction * Math.PI * 2 / 250);

        var activeCharacterObject = GetActiveGameObject();
        activeCharacterObject.m_location = new Vector3(x, y, z);
        activeCharacterObject.m_orientation = new Vector3(0, 0, direction);

        // Broadcast the move to all other players in the zone.
        BroadcastClientMove(message);
        SendZoneInteractionFishRequest();
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVESTATE))]
    private void ReceiveClientMoveState(GAME_5_PROTOCOL.MSG_CLIENTMOVESTATE message) {
        var globalId = GetActiveGameObject().m_globalID;

        var stateMsg = new GAME_5_PROTOCOL.MSG_MOVESTATE {
            NewState = message.NewState,
            GlobalID = globalId
        };
        ZoneBroadcast(stateMsg);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_JUMP))]
    private void ReceiveClientJump(GAME_5_PROTOCOL.MSG_JUMP message) {
        var excludeOriginator = message.ExcludeOriginator == 1;
        ZoneBroadcast(message, excludeOriginator);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_MARK_LOCATION))]
    private void ReceiveMarkLocation(GAME_5_PROTOCOL.MSG_MARK_LOCATION message) {
        var wizard = GetActiveWizard();

        // If the character doesn't have enough mana, return.
        if (wizard.GameStats.m_currentMana < wizard.GameStats.m_baseMana / MarkManaCostPercent) {
            var failedRsp = new GAME_5_PROTOCOL.MSG_MARK_LOCATION_RESPONSE {
                Result = 0,
                MarkType = "1"
            };
            SendToSocket(failedRsp);
            return;
        }

        wizard.SetMarkedLocation(wizard.Location, wizard.Orientation, wizard.Zone);

        var oldMana = wizard.GameStats.m_currentMana;
        var newMana = oldMana - (wizard.GameStats.m_baseMana * ((float) MarkManaCostPercent / 100));
        wizard.GameStats.m_currentMana = (int) newMana;

        SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEMANA() {
            Mana = wizard.GameStats.m_currentMana,
            MaxMana = wizard.GameStats.m_baseMana,
            DisplayDiff = (byte) (oldMana - newMana)
        });

        var rsp = new GAME_5_PROTOCOL.MSG_MARK_LOCATION_RESPONSE {
            Result = 1,
            ZoneName = wizard.Zone,
            ZoneType = 0,
            ZoneDisplayNameId = "Zone_00000026", // Should be Wizard City, maybe .lang was updated
            LocationX = wizard.MarkedLocation.X,
            LocationY = wizard.MarkedLocation.Y,
            LocationZ = wizard.MarkedLocation.Z,
            Direction = wizard.Orientation.Z,
            MarkType = "1",
            InstanceId = new GID(1),
            CommonsZoneId = "0",
        };
        SendToSocket(rsp);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_RECALL_LOCATION))]
    private void ReceiveRecallLocation(GAME_5_PROTOCOL.MSG_RECALL_LOCATION message) {
        var wizard = GetActiveWizard();

        // If we are in the same zone as the marked location, teleport to it.
        if (wizard.MarkedZone == wizard.Zone) {
            var serverTeleportRsp = new GAME_5_PROTOCOL.MSG_SERVERTELEPORT {
                // Compress the location by a factor of 4 and convert to unsigned.
                Direction = (byte) (wizard.MarkedOrientation.Z / Math.PI / 2 * 250),
                LocationX = (ushort) (wizard.MarkedLocation.X / 4),
                LocationY = (ushort) (wizard.MarkedLocation.Y / 4),
                LocationZ = (ushort) (wizard.MarkedLocation.Z / 4),
                MobileID = wizard.GameObject.m_nMobileID,
            };
            var recallRsp = new GAME_5_PROTOCOL.MSG_MARK_LOCATION_RESPONSE {
                Result = 1,
                MarkType = "1"
            };

            // Broadcast the server teleport.
            var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST() {
                Sender = SessionActor.ActorRef,
                Message = serverTeleportRsp,
                Selfless = false,
            };
            TellOtherServices(broadcastMsg);

            // Send the recall response to the client.
            SendToSocket(recallRsp);
        }
        // If we're not in the same zone, send a zone transfer prior to the server teleport.
        else {
            var zoneTransfer = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER {
                DestinationLocation =
                    Util.GetCompactStringFromVector(wizard.MarkedLocation, wizard.MarkedOrientation),
                DestinationZone = wizard.MarkedZone,
                SendToClient = true,
            };
            TellOtherServices(zoneTransfer);

            var recallRsp = new GAME_5_PROTOCOL.MSG_MARK_LOCATION_RESPONSE {
                Result = 1,
                MarkType = "1"
            };
            SendToSocket(recallRsp);
        }
    }

    private void BroadcastClientMove(GAME_5_PROTOCOL.MSG_CLIENTMOVE message) {
        // Query the mobile ID from the CharacterService
        var mobileId = GetActiveGameObject().m_nMobileID;

        var serverMoveMsg = new GAME_5_PROTOCOL.MSG_SERVERMOVE {
            LocationX = message.LocationX,
            LocationY = message.LocationY,
            LocationZ = message.LocationZ,
            Direction = message.Direction,
            MobileID = mobileId,
        };
        ZoneBroadcast(serverMoveMsg);
    }

    private void SendZoneInteractionFishRequest() {
        var characterObj = GetActiveGameObject();
        var msg = new ZONE_102_PROTOCOL.MSG_FISHINTERACTION() {
            CoreObject = characterObj,
            Suspect = SessionActor.ActorRef
        };
        TellOtherServices(msg);
    }
}
