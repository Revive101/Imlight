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
    private const byte MARK_MANA_COST_PERCENT = 20;

    private TypeCache.CoreObject _activeCoreObject;

    public MoveService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new MoveService(parentActor));

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVE))]
    private void ReceiveClientMove(GAME_5_PROTOCOL.MSG_CLIENTMOVE message) {
        // MoveService saves the location of the player's game object.
        // CharacterService saves the location of the player's character persistently.

        this._activeCoreObject ??= GetActiveGameObject();

        // Restore actual location information, as it is compressed by a factor of 4 and unsigned.
        // Yaw is represented in radians in the client, but transmitted to the server as degrees.
        var deflatedPos = new Vector3(message.LocationX, message.LocationY, message.LocationZ);
        var deflatedDir = message.Direction;
        var inflatedPos = DecompressLocation(deflatedPos);
        var inflatedDir = DecompressDirection(deflatedDir);

        _activeCoreObject.m_location = inflatedPos;
        _activeCoreObject.m_orientation = new Vector3(0, 0, inflatedDir);

        // Broadcast the move to all other players in the zone.
        BroadcastClientMove(message);
        SendZoneInteractionFishRequest();
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVESTATE))]
    private void ReceiveClientMoveState(GAME_5_PROTOCOL.MSG_CLIENTMOVESTATE message)
        => BroadcastClientMoveState(message);

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_JUMP))]
    private void ReceiveClientJump(GAME_5_PROTOCOL.MSG_JUMP message) {
        var excludeOriginator = message.ExcludeOriginator == 1;
        ZoneBroadcast(message, excludeOriginator);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_MARK_LOCATION))]
    private void ReceiveMarkLocation(GAME_5_PROTOCOL.MSG_MARK_LOCATION message) {
        var wizard = GetActiveWizard();

        // If the character doesn't have enough mana, return.
        if (wizard.GameStats.m_currentMana < wizard.GameStats.m_baseMana / MARK_MANA_COST_PERCENT) {
            var failedRsp = new GAME_5_PROTOCOL.MSG_MARK_LOCATION_RESPONSE {
                Result = 0,
                MarkType = "1"
            };
            SendToSocket(failedRsp);
            return;
        }

        wizard.SetMarkedLocation(wizard.Location, wizard.Orientation, wizard.Zone);

        var oldMana = wizard.GameStats.m_currentMana;
        var newMana = oldMana - (wizard.GameStats.m_baseMana * ((float) MARK_MANA_COST_PERCENT / 100));
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
            var deflatedPos = CompressLocation(wizard.MarkedLocation);
            var deflatedDir = CompressDirection(wizard.MarkedOrientation.Z);

            var serverTeleportRsp = new GAME_5_PROTOCOL.MSG_SERVERTELEPORT {
                // Compress the location by a factor of 4 and convert to unsigned.
                Direction = deflatedDir,
                LocationX = (ushort) deflatedPos.X,
                LocationY = (ushort) deflatedPos.Y,
                LocationZ = (ushort) deflatedPos.Z,
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
            DoZoneTransfer(wizard.MarkedZone);

            var recallRsp = new GAME_5_PROTOCOL.MSG_MARK_LOCATION_RESPONSE {
                Result = 1,
                MarkType = "1"
            };
            SendToSocket(recallRsp);
        }
    }

    private void BroadcastClientMove(GAME_5_PROTOCOL.MSG_CLIENTMOVE message) {
        this._activeCoreObject ??= GetActiveGameObject();

        var serverMoveMsg = new GAME_5_PROTOCOL.MSG_SERVERMOVE {
            LocationX = message.LocationX,
            LocationY = message.LocationY,
            LocationZ = message.LocationZ,
            Direction = message.Direction,
            MobileID = _activeCoreObject.m_nMobileID,
        };
        ZoneBroadcast(serverMoveMsg);
    }

    private void BroadcastClientMoveState(GAME_5_PROTOCOL.MSG_CLIENTMOVESTATE message) {
        this._activeCoreObject ??= GetActiveGameObject();

        var stateMsg = new GAME_5_PROTOCOL.MSG_MOVESTATE {
            NewState = message.NewState,
            GlobalID = _activeCoreObject.m_globalID
        };
        ZoneBroadcast(stateMsg);
    }

    private void SendZoneInteractionFishRequest() {
        var msg = new ZONE_102_PROTOCOL.MSG_FISHINTERACTION() {
            CoreObject = _activeCoreObject,
            Suspect = SessionActor.ActorRef
        };

        TellOtherServices(msg);
    }

    private static Vector3 CompressLocation(Vector3 location) => new Vector3(
            (float) Math.Round(location.X / 4),
            (float) Math.Round(location.Y / 4),
            (float) Math.Round(location.Z / 4)
        );

    private static byte CompressDirection(float direction)
        => (byte) Math.Round(direction / Math.PI / 2 * 250);

    private static Vector3 DecompressLocation(Vector3 location) {
        var x = unchecked((short) location.X) * 4.0f;
        var y = unchecked((short) location.Y) * 4.0f;
        var z = unchecked((short) location.Z) * 4.0f;

        return new Vector3(x, y, z);
    }

    private static float DecompressDirection(float direction)
        => (float) (direction * Math.PI * 2 / 250);
}
