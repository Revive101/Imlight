/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Globalization;
using System.Numerics;
using Akka.Actor;
using WizUnraveler.Cache;
using Imlight.Common.Utilities;
using Imlight.Server.Database;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;
using Math = System.Math;

namespace Imlight.Server.Game.Services
{
    public class ZoneService : MessageService
    {
        private IActorRef _zoneRef;
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
            var zoneDetails = AskServer<ZONE_102_PROTOCOL.MSG_ZONETRANSFERRSP>(message);
            
            // If the zone is ready and we're sending to client, begin the zone transfer handshake with the client.
            if (zoneDetails.ErrorCode == 0 && message.SendToClient)
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
            }
            
            // If we're not sending this to client, this is an internal transfer, meaning we can immediately
            // setup the new details.
            if (!message.SendToClient)
            {
                _zoneRef = zoneDetails.ZoneActorRef;
            }
            
            Sender.Tell(zoneDetails);
        }
        
        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_ZONETRANSFERACK))]
        private void ReceiveZoneTransferAck(GAME_5_PROTOCOL.MSG_ZONETRANSFERACK message)
        {
            DoZoneTransfer();
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_ZONETRANSFERNACK))]
        private void ReceiveZoneTransferNack(GAME_5_PROTOCOL.MSG_ZONETRANSFERNACK message)
        {
            Log.Logger.Debug("Client was not OK with zone transfer! Possibly patching.");
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_RETRYTELEPORT))]
        private void ReceiveRetryTeleport(GAME_5_PROTOCOL.MSG_RETRYTELEPORT message)
        {
            DoZoneTransfer();
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

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_TRIGGERQUERY))]
        private void ReceiveZoneInteraction(ZONE_102_PROTOCOL.MSG_TRIGGERQUERY message)
        {
            if (_zoneRef is null) throw new Exception("Zone Reference was null.");
            
            _zoneRef.Forward(message);
        }
        
        [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_DISPOSE))]
        protected override void ReceiveDispose(SERVICE_101_PROTOCOL.MSG_DISPOSE message)
        {
            var globalId = GetActiveCoreObject()?.m_globalID;

            // If the zone reference is not null, we'll tell the zone to remove the player.
            _zoneRef?.Tell(new ZONE_102_PROTOCOL.MSG_REMOVEPLAYER()
            {
                Player = SessionActor.ActorRef,
                GlobalId = globalId ?? 0
            });
            _zoneRef = null;
            
            base.ReceiveDispose(message);
        }

        private void DoZoneTransfer()
        {
            var account = GetSocketAccount();
            var character = GetActiveCharacter();

            // Remove the player from their current zone. We're awaiting a reply so the zone can properly clean up
            // before we continue on potentially a different thread.
            var removePlayerMsg = new ZONE_102_PROTOCOL.MSG_REMOVEPLAYER()
            {
                Player = SessionActor.ActorRef,
                GlobalId = GetActiveCoreObject().m_globalID,
                IsPlayerStillConnected = true
            };
            _ = _zoneRef.Ask(removePlayerMsg).Result;

            // When we send this message, the client will disconnect from the current zone and reconnect to the next.
            // This means attach will happen again, so this is all we need to do here.
            var serverTransfer = new GAME_5_PROTOCOL.MSG_SERVERTRANSFER()
            {
                IP = character.LastGameServerIp,
                TCPPort = character.LastGameServerPort,
                UDPPort = character.LastGameServerPort,
                UserID = account.ID,
                CharID = character.Id,
                ZoneName = character.nextZone,
                Location = character.nextLocation, // Doesn't seem to do anything.
                Slot = 0,
                SessionSlot = 0,
                SessionID = 0,
                TargetPlayerID = character.Id,
                TransitionID = 1
            };
            SendToSocket(serverTransfer);
        }
        
        private TypeCache.CoreObject GetActiveCoreObject()
        {
            var msg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVECHARACTER();
            var response = AskOtherService<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(msg);

            return response.CharacterObject;
        }

        private Character GetActiveCharacter()
        {
            var msg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVECHARACTER();
            var response = AskOtherService<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(msg);

            return response.Character;
        }
    }
}