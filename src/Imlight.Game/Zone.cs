using System;
using System.Collections.Generic;
using Akka.Actor;
using Imlight.Net;
using Imlight.Net.Messages;
using WizUnraveler;

namespace Imlight.Game
{
    public class Zone : ReceiveProtocolDispatcher
    {
        public string ZoneName { get; }
        public uint DynamicZoneId;
        public List<IActorRef> Players { get; }

        public Zone(string zoneName)
        {
            this.ZoneName = zoneName;
            this.Players = new List<IActorRef>();
        }
        
        public static Props Props(string zoneName)
        {
            return Akka.Actor.Props.Create(() => new Zone(zoneName));
        }

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUERYZONE))]
        private void ReceiveQueryZone(ZONE_102_PROTOCOL.MSG_QUERYZONE message)
        {
            Sender.Tell(new ZONE_102_PROTOCOL.MSG_QUERYZONERSP
            {
                PlayerCount = (uint) Players.Count,
                CriticalObjects = new ByteString(),
                DynamicZoneId = DynamicZoneId
            });
        }

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
        private void ReceiveAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message)
        {
            if (Players.Contains(message.Player))
                throw new Exception("Player actor already exists on this server!");

                Players.Add(message.Player);
        }
        
        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER))]
        private void ReceiveRemovePlayer(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message)
        {
            if (!Players.Contains(message.Player))
                throw new Exception("Player actor does not exist on this server!");
            
            Players.Remove(message.Player);
        }
    }
}