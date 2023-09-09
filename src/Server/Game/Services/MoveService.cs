/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using Akka.Actor;
using WizUnraveler.Cache;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;
using Imlight.Server.Shared.Resources;
using WizUnraveler.ObjectProperty;

namespace Imlight.Server.Game.Services;

internal class MoveService : MessageService
{
    private const byte MarkManaCostPercent = 10;
    
    public MoveService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor)
    {
        return Akka.Actor.Props.Create(() => new MoveService(parentActor));
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVE))]
    private void ReceiveClientMove(GAME_5_PROTOCOL.MSG_CLIENTMOVE message)
    {
        // Restore actual location information, as it is compressed by a factor of 4 and unsigned.
        // Yaw is represented in radians in the client, but transmitted to the server as degrees.
        var position = new SharpDX.Vector3(
            unchecked((short)message.LocationX * 4), 
            unchecked((short)message.LocationY * 4), 
            unchecked((short)message.LocationZ * 4));

        var character = GetActiveCharacter();
        character.SetLocation(position);
        character.SetOrientation(message.Direction);

        // Broadcast the move to all other players in the zone.
        BroadcastClientMove(message);
        SendZoneInteractionFishRequest();
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVESTATE))]
    private void ReceiveClientMoveState(GAME_5_PROTOCOL.MSG_CLIENTMOVESTATE message)
    {
        var globalId = GetActiveCoreObject().m_globalID;
            
        var stateMsg = new GAME_5_PROTOCOL.MSG_MOVESTATE
        {
            NewState = message.NewState,
            GlobalID = globalId
        };
        ZoneBroadcast(stateMsg);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_JUMP))]
    private void ReceiveClientJump(GAME_5_PROTOCOL.MSG_JUMP message)
    {
        var excludeOriginator = message.ExcludeOriginator == 1;
        ZoneBroadcast(message, excludeOriginator);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_MARK_LOCATION))]
    private void ReceiveMarkLocation(GAME_5_PROTOCOL.MSG_MARK_LOCATION message)
    {
        var character = GetActiveCharacter();
        
        // If the character wouldn't have another mana to perform this action, return.
        if (character.GameStats.m_currentMana < character.GameStats.m_baseMana / MarkManaCostPercent)
        {
            var failedRsp = new GAME_5_PROTOCOL.MSG_MARK_LOCATION_RESPONSE
            {
                Result = 0,
                MarkType = "1"
            };
            SendToSocket(failedRsp);
            return;
        }
        
        character.SetMarkedLocation(character.Location, character.Orientation, character.Zone);
        
        // Todo: reduce the character's mana here by 10%.
        var rsp = new GAME_5_PROTOCOL.MSG_MARK_LOCATION_RESPONSE
        {
            Result = 1,
            ZoneName = character.Zone,
            ZoneType = 0,
            ZoneDisplayNameId = "test",
            LocationX = character.MarkedLocation.X,
            LocationY = character.MarkedLocation.Y,
            LocationZ = character.MarkedLocation.Z,
            Direction = character.Orientation.Z,
            MarkType = "1",
            InstanceId = new GID(1),
            CommonsZoneId = "0",
        };
        SendToSocket(rsp);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_RECALL_LOCATION))]
    private void ReceiveRecallLocation(GAME_5_PROTOCOL.MSG_RECALL_LOCATION message)
    {
        var character = GetActiveCharacter();
        var coreObj = GetActiveCoreObject();
        
        // If we are in the same zone as the marked location, teleport to it.
        if (character.MarkedZoneName == character.Zone)
        {
            var serverTeleportRsp = new GAME_5_PROTOCOL.MSG_SERVERTELEPORT
            {
                // Compress the location by a factor of 4 and convert to unsigned.
                Direction = (byte)(character.MarkedLocationOrientation.Z / Math.PI / 2 * 250),
                LocationX = (ushort)(character.MarkedLocation.X / 4),
                LocationY = (ushort)(character.MarkedLocation.Y / 4),
                LocationZ = (ushort)(character.MarkedLocation.Z / 4),
                MobileID = coreObj.m_nMobileID,
            };
            var recallRsp = new GAME_5_PROTOCOL.MSG_MARK_LOCATION_RESPONSE
            {
                Result = 1,
                MarkType = "1"
            };
            
            // Broadcast the server teleport.
            var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST()
            {
                Sender = SessionActor.ActorRef,
                Message = serverTeleportRsp,
                Selfless = false,
            };
            TellOtherServices(broadcastMsg);
            
            // Send the recall response to the client.
            SendToSocket(recallRsp);
        }
        // If we're not in the same zone, send a zone transfer prior to the server teleport.
        else
        {
            var zoneTransfer = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER
            {
                DestinationLocation =
                    Util.GetCompactStringFromVector(character.MarkedLocation, character.MarkedLocationOrientation),
                DestinationZone = character.MarkedZoneName,
                SendToClient = true,
            };
            TellOtherServices(zoneTransfer);
            
            var recallRsp = new GAME_5_PROTOCOL.MSG_MARK_LOCATION_RESPONSE
            {
                Result = 1,
                MarkType = "1"
            };
            SendToSocket(recallRsp);
        }
    }

    private void BroadcastClientMove(GAME_5_PROTOCOL.MSG_CLIENTMOVE message)
    {
        // Query the mobile ID from the CharacterService
        var mobileId = GetActiveCoreObject().m_nMobileID;
            
        var serverMoveMsg = new GAME_5_PROTOCOL.MSG_SERVERMOVE
        {
            LocationX = message.LocationX,
            LocationY = message.LocationY,
            LocationZ = message.LocationZ,
            Direction = message.Direction,
            MobileID = mobileId,
        };
        ZoneBroadcast(serverMoveMsg);
    }

    private void SendZoneInteractionFishRequest()
    {
        var characterObj = GetActiveCoreObject();
        var msg = new ZONE_102_PROTOCOL.MSG_TRIGGERQUERY()
        {
            CoreObject = characterObj,
            Suspect = SessionActor.ActorRef
        };
        TellOtherServices(msg);
    }
}