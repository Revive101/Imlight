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
        private IActorRef _bankedZoneRef;
        private bool _isTransferQueued;
        
        public ZoneService(SessionActor sessionActor) : base(sessionActor) { }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new ZoneService(parentActor));
        }

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONETRANSFER))]
        private void ReceiveZoneTransferRequest(ZONE_102_PROTOCOL.MSG_ZONETRANSFER message)
        {
            // Avoid duplicate transfer requests.
            if (_isTransferQueued)
                return;
            
            var character = GetActiveCharacter();
            var result = AskServer<ZONE_102_PROTOCOL.MSG_ZONETRANSFERRSP>(message);
            if (result.ErrorCode == 0 && message.SendToClient)
            {
                _isTransferQueued = true;
                
                // Ask the client if it's okay with being transferred.
                var msg = new GAME_5_PROTOCOL.MSG_ZONETRANSFERREQUEST
                {
                    ZoneName = message.ZoneName,
                    SendAck = 0
                };
                SendToSocket(msg);

                character.nextZone = message.ZoneName;
                character.nextLocation = message.Location;
                
                // Bank the zone actor reference. If the client is okay with transferring, we'll set the actual
                // zone ref to the banked one.
                _bankedZoneRef = result.ZoneActorRef;
            }
            
            if (!message.SendToClient)
            {
                _zoneRef = result.ZoneActorRef;
            }
            
            Sender.Tell(result);
        }
        
        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_ZONETRANSFERACK))]
        private void ReceiveZoneTransferAck(GAME_5_PROTOCOL.MSG_ZONETRANSFERACK message)
        {
            var account = GetSocketAccount();
            var character = GetActiveCharacter();

            // Remove the player from their current zone. We're doing an ask instead of a tell here to await the zone's
            // removal process for us.
            var removePlayerMsg = new ZONE_102_PROTOCOL.MSG_REMOVEPLAYER()
            {
                Player = SessionActor.ActorRef,
                GlobalId = GetActiveCoreObject().m_globalID,
                IsZoneTransfer = true
            };
            _ = _zoneRef.Ask(removePlayerMsg).Result;

            character.CreationData.m_location = character.nextZone;

            // We don't need to add the player to the new zone, as the attach service will do that.
            var serverTransfer = new GAME_5_PROTOCOL.MSG_SERVERTRANSFER()
            {
                IP = character.LastGameServerIp,
                TCPPort = character.LastGameServerPort,
                UDPPort = character.LastGameServerPort,
                UserID = account.ID,
                CharID = character.Id,
                ZoneName = character.nextZone,
                Location = character.nextLocation,
                Slot = 0,
                SessionSlot = 0,
                SessionID = 0,
                TargetPlayerID = character.Id,
                TransitionID = 1
            };
            SendToSocket(serverTransfer);

            _zoneRef = _bankedZoneRef;
            _isTransferQueued = false;
            _bankedZoneRef = null;
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

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECT))]
        private void ReceiveQueryObject(ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECT message)
        {
            if (_zoneRef is null) throw new Exception("Zone Reference was null.");
            
            _zoneRef.Forward(message);
        }

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_TRIGGERQUERY))]
        private void ReceiveZoneInteraction(ZONE_102_PROTOCOL.MSG_TRIGGERQUERY message)
        {
            if (_zoneRef is null) throw new Exception("Zone Reference was null.");
            
            _zoneRef.Forward(message);
        }
        
        [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_DISPOSE))]
        public override void ReceiveDispose(SERVICE_101_PROTOCOL.MSG_DISPOSE message)
        {
            base.ReceiveDispose(message);
            var globalId = GetActiveCoreObject()?.m_globalID;

            // If the zone reference is not null, we'll tell the zone to remove the player.
            _zoneRef?.Tell(new ZONE_102_PROTOCOL.MSG_REMOVEPLAYER()
            {
                Player = SessionActor.ActorRef,
                GlobalId = globalId ?? 0
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