/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Akka.Actor;
using WizUnraveler.Cache;
using Imlight.Common.Utilities;
using Imlight.Server.Database;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;

namespace Imlight.Server.Game.Services
{
    public class ZoneService : MessageService
    {
        private IActorRef _zoneRef;
        
        public ZoneService(SessionActor sessionActor) : base(sessionActor) { }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new ZoneService(parentActor));
        }

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUERYZONE))]
        private void ReceiveZoneTransferRequest(ZONE_102_PROTOCOL.MSG_QUERYZONE message)
        {
            var result = AskServer<ZONE_102_PROTOCOL.MSG_QUERYZONERSP>(message);
            
            // If the zone request was successful, we'll set the zone reference.
            if (result.ErrorCode == 0)
            {
                _zoneRef = result.ZoneActorRef;
            }
            
            Sender.Tell(result);
        }
        
        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
        private void ReceiveAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message)
        {
            if (_zoneRef is null) throw new NullReferenceException(nameof(_zoneRef));
            
            _zoneRef.Forward(message);
        }

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST))]
        private void ReceiveZoneBroadcast(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST message)
        {
            if (_zoneRef is null) throw new Exception("Zone Reference was null.");
            
            _zoneRef.Tell(message);
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_ZONETRANSFERACK))]
        private void ReceiveZoneTransferAck(GAME_5_PROTOCOL.MSG_ZONETRANSFERACK message)
        {
            var account = GetSocketAccount();
            var character = GetActiveCharacter();

            // Remove the player from their current zone.
            var removePlayerMsg = new ZONE_102_PROTOCOL.MSG_REMOVEPLAYER()
            {
                Player = SessionActor.ActorRef,
                GlobalId = GetActiveCoreObject().m_globalID,
                IsZoneTransfer = true
            };
            _zoneRef.Tell(removePlayerMsg);

            character.CreationData.m_location = character.nextZone;

            // We don't need to add the player to the new zone, as the zone will do that for us on MSG_ATTACH.
            var serverTransfer = new GAME_5_PROTOCOL.MSG_SERVERTRANSFER()
            {
                IP = character.LastGameServerIp,
                TCPPort = character.LastGameServerPort,
                UDPPort = character.LastGameServerPort,
                UserID = account.ID,
                CharID = character.Id,
                ZoneName = character.nextZone,
                Location = "Start",
                Slot = 0,
                SessionSlot = 0,
                SessionID = 0,
                TargetPlayerID = character.Id,
                TransitionID = 1
            };
            SendToSocket(serverTransfer);
        }
        
        [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_DISPOSE))]
        public override void ReceiveDispose(SERVICE_101_PROTOCOL.MSG_DISPOSE message)
        {
            base.ReceiveDispose(message);
            var globalId = GetActiveCoreObject().m_globalID;

            // If the zone reference is not null, we'll tell the zone to remove the player.
            _zoneRef?.Tell(new ZONE_102_PROTOCOL.MSG_REMOVEPLAYER()
            {
                Player = SessionActor.ActorRef,
                GlobalId = globalId
            });

            _zoneRef = null;
        }
        
        private TypeCache.CoreObject GetActiveCoreObject()
        {
            var msg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVECHARACTER();
            var response = AskSessionServices<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(msg);

            return response.CharacterObject;
        }

        private Character GetActiveCharacter()
        {
            var msg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVECHARACTER();
            var response = AskSessionServices<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(msg);

            return response.Character;
        }
    }
}