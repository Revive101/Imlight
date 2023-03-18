using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imlight.Net;
using Imlight.Net.Messages;
using Serilog;
using WizUnraveler;
using WizUnraveler.Cache;
using WizUnraveler.DML;

namespace Imlight.Game
{
    public class Zone : ReceiveProtocolDispatcher
    {
        public string ZoneName { get; }
        public uint DynamicZoneId;
        public List<IActorRef> Players { get; }
        public List<TypeCache.CoreObject> CoreObjects { get; }

        public Zone(string zoneName)
        {
            this.ZoneName = zoneName;
            this.DynamicZoneId = GenerateDynamicZoneId();
            this.Players = new List<IActorRef>();
            this.CoreObjects = new List<TypeCache.CoreObject>();
        }
        
        public static Props Props(string zoneName)
        {
            return Akka.Actor.Props.Create(() => new Zone(zoneName));
        }

        public void Broadcast(INetworkMessage message)
        {
            foreach (var player in Players)
            {
                player.Tell(message);
            }
        }
        
        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUERYZONE))]
        private void ReceiveQueryZone(ZONE_102_PROTOCOL.MSG_QUERYZONE message)
        {
            Sender.Tell(new ZONE_102_PROTOCOL.MSG_QUERYZONERSP
            {
                PlayerCount = (uint) Players.Count,
                CriticalObjects = SerializeCriticalObjects(),
                DynamicZoneId = DynamicZoneId
            });
        }

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
        private void ReceiveAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message)
        {
            if (Players.Contains(message.Player))
                throw new Exception("Player actor already exists on this server!");

            Players.Add(message.Player);

            Log.Logger.Debug($"Player {message.Player.Path.Name} added to zone {ZoneName}.");
        }
        
        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER))]
        private void ReceiveRemovePlayer(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message)
        {
            if (!Players.Contains(message.Player))
                throw new Exception("Player actor does not exist on this server!");
            
            Players.Remove(message.Player);
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_NEWOBJECT))]
        private void ReceiveNewObject(GAME_5_PROTOCOL.MSG_NEWOBJECT message)
        {
            // @todo: check if this data needs to be deserialized
            var deserializer = new CoreObjectSerializer();
            var deserializedData = deserializer.DeserializeCoreObject<TypeCache.CoreObject>(message.Data);
            CoreObjects.Add(deserializedData);

            Broadcast(message);
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REMOVEOBJECT))]
        private void ReceiveRemoveObject(GAME_5_PROTOCOL.MSG_REMOVEOBJECT message)
        {
            if (!TryGetCoreObject(message.GameObjectID, out var obj))
            {
                throw new Exception(); // @todo: make this descriptive
            }

            CoreObjects.Remove(obj);
            
            Broadcast(message);
        }
        
        private ByteString SerializeCriticalObjects()
        {
            // Create a new BitIterator and prefix it with a count of the CoreObjects in this zone.
            var buffer = new BitIterator();

            var serializer = new CoreObjectSerializer();
            foreach (var t in CoreObjects)
            {
                var coSerialized = serializer.SerializeCoreObject(t);
                
                // Add the new serialization data to the buffer.
                buffer.WriteBytes(coSerialized);
            }

            return new ByteString(buffer.GetData());
        }

        private bool TryGetCoreObject(ulong gameObjectId, out TypeCache.CoreObject obj)
        {
            obj = CoreObjects.First(x => x.m_globalID == gameObjectId);
            return obj is not null;
        }
        
        private static uint GenerateDynamicZoneId()
        {
            var random = new Random();
            return (uint) random.Next(0, int.MaxValue);
        }
    }
}