using System;
using Akka.Actor;
using Imlight.Net;
using Imlight.Net.Messages;
using WizUnraveler.Cache;

namespace Imlight.Game.Services
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
            
            // If the zone transfer request was successful, we'll set the zone reference.
            if (result.ErrorCode == 0)
            {
                _zoneRef = result.NewZone;
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
        
        [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_DISPOSE))]
        public override void ReceiveDispose(SERVICE_101_PROTOCOL.MSG_DISPOSE message)
        {
            base.ReceiveDispose(message);

            // If the zone reference is not null, we'll tell the zone to remove the player.
            if (_zoneRef is not null)
                _zoneRef.Tell(new ZONE_102_PROTOCOL.MSG_REMOVEPLAYER() { Player = SessionActor.ActorRef });

            _zoneRef = null;
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_ZONETRANSFERACK))]
        private void ReceiveZoneTransferAck(GAME_5_PROTOCOL.MSG_ZONETRANSFERACK message)
        {
            // TESTING PURPOSES ONLY, is this method in the appropriate class?
            var account = GetSocketAccount();
            var character = account.Characters[0];

            var serverTransfer = new GAME_5_PROTOCOL.MSG_ZONETRANSFER()
            {
                //IP = "127.0.0.1",
                //TCPPort = 12333,
                //UDPPort = 12333,
                //UserID = account.ID,
                //CharID = character.ID,
                ZoneName = "WizardCity/WC_Ravenwood",
                //Location = "Start",
                Slot = 0,
                SessionSlot = 0,
                SessionID = 0,
                //TargetPlayerID = character.ID,
                TransitionID = 1
            };
            SendToSocket(serverTransfer);
        }
    }
}