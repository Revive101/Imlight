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
    }
}