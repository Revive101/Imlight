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
using Imlight.CoreLib.WizardData.Models.Player;
using SharpDX;

namespace Imlight.CoreLib.Game.Services;

internal class MoveService : MessageService, IWithTimers {
    private const byte MARK_MANA_COST_PERCENT = 20;
    private const int FISH_INTERACTION_INTERVAL_IN_MILLI = 250;
    private const int MOVE_THRESHOLD_IN_MILLI = 1000;

    public ITimerScheduler Timers { get; set; }

    private readonly TimeSpan _fishInteractionInterval
        = TimeSpan.FromMilliseconds(FISH_INTERACTION_INTERVAL_IN_MILLI);
    private readonly TimeSpan _moveThreshold
        = TimeSpan.FromMilliseconds(MOVE_THRESHOLD_IN_MILLI);
    private TypeCache.CoreObject _activeCoreObject;
    private Wizard _wizard;
    private DateTime _lastMoveTime;
    private bool _sentStopMoveState;

    public MoveService(SessionActor sessionActor) : base(sessionActor) {
        // Instead of fishing for zone interactions per move, we'll start an interval of x milliseconds
        // to check for zone interactions. This will enable the player to interact with the zone
        // even if they aren't moving.
        var intervalMsg = new ZONE_102_PROTOCOL.MSG_PLAYERMOVEINTERVAL();
        Timers.StartPeriodicTimer("interaction", intervalMsg, _fishInteractionInterval, _fishInteractionInterval);
    }

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new MoveService(parentActor));

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVE))]
    private void ReceiveClientMove(GAME_5_PROTOCOL.MSG_CLIENTMOVE message) {
        // WizardService saves the location and orientation of the wizard.
        // MoveService will broadcast the move to all other players in the zone and
        // deals with interactions.
        _activeCoreObject ??= GetActiveGameObject();
        _wizard ??= GetActiveWizard();

        // Update the last move time.
        _lastMoveTime = DateTime.Now;
        _sentStopMoveState = false;

        // Broadcast the move to all other players in the zone.
        BroadcastClientMove(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PLAYERMOVEINTERVAL))]
    private void ReceiveZoneInteractionInterval(ZONE_102_PROTOCOL.MSG_PLAYERMOVEINTERVAL message) {
        if (_activeCoreObject is null) {
            return;
        }

        // Fish for interactions within the zone.
        SendZoneInteractionFishRequest();

        // While we're here, we're going to check to see if the player has been idle for
        // too long. In such a case, we'll send a move state message to the client.
        if (DateTime.Now - _lastMoveTime > _moveThreshold && !_sentStopMoveState) {
            var moveStateMsg = new GAME_5_PROTOCOL.MSG_CLIENTMOVESTATE { NewState = 0 };
            BroadcastClientMoveState(moveStateMsg);

            // Set the flag to true so we don't send the move state message again.
            // This will be reset the next time we notice the client move again.
            _sentStopMoveState = true;
        }
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVESTATE))]
    private void ReceiveClientMoveState(GAME_5_PROTOCOL.MSG_CLIENTMOVESTATE message) {
        _activeCoreObject ??= GetActiveGameObject();
        BroadcastClientMoveState(message);

        _lastMoveTime = DateTime.Now;
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
}
