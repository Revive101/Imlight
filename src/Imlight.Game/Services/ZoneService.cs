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

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONETRANSFERREQUEST))]
        private void ReceiveZoneTransferRequest(ZONE_102_PROTOCOL.MSG_ZONETRANSFERREQUEST message)
        {
            var result = AskServer<ZONE_102_PROTOCOL.MSG_ZONETRANSFERREQUESTRSP>(message);
            
            // If the zone transfer request was successful, we'll set the zone reference.
            if (result.ErrorCode == 0)
            {
                _zoneRef = result.NewZone;
            }
            
            Sender.Tell(result);
        }

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_CREATENETWORKOBJECT))]
        private void ReceiveCreateNetworkObject(ZONE_102_PROTOCOL.MSG_CREATENETWORKOBJECT message)
        {
            if (_zoneRef is null) throw new Exception("Zone Reference was null.");
            
            _zoneRef.Tell(message);
        }

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST))]
        private void ReceiveZoneBroadcast(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST message)
        {
            if (_zoneRef is null) throw new Exception("Zone Reference was null.");
            
            _zoneRef.Tell(message);
        }

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECTS))]
        private void ReceiveQueryObjects(ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECTS message)
        {
            if (_zoneRef is null) throw new Exception("Zone Reference was null.");
            
            var result = _zoneRef.Ask(message).Result;
            Sender.Tell(result);
        }
    }
}