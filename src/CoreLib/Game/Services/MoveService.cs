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
