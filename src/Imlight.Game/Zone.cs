using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imlight.Common;
using Imlight.Net;
using Imlight.Net.Messages;
using WizUnraveler;
using WizUnraveler.Cache;
using WizUnraveler.DML;
using static WizUnraveler.ObjectSerializer;
using Log = Serilog.Log;

namespace Imlight.Game
{
    public class Zone : ReceiveProtocolDispatcher
    {
        public string ZoneName { get; }
        public uint DynamicZoneId;
        public List<IActorRef> Players { get; }
        public Dictionary<ushort, TypeCache.CoreObject> CoreObjects { get; }

        public Zone(string zoneName)
        {
            this.ZoneName = zoneName;
            this.DynamicZoneId = GenerateDynamicZoneId();
            this.Players = new List<IActorRef>();
            this.CoreObjects = new Dictionary<ushort, TypeCache.CoreObject>();
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

        public void BroadcastSelfless(IActorRef sender, INetworkMessage message)
        {
            foreach (var player in Players
                         .Where(player => !player.Equals(sender)))
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

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_CREATENETWORKOBJECT))]
        private void ReceiveCreateNetworkObject(ZONE_102_PROTOCOL.MSG_CREATENETWORKOBJECT message)
        {
            // Give this network object a unique GUID.
            message.CoreObject.m_globalID = RandomGen.GenerateGUID();
            message.CoreObject.m_permID = RandomGen.GenerateGUID();
            message.CoreObject.m_nMobileID = GenerateMobileId();
            
            // Add the object to the zone's list of core objects.
            CoreObjects.Add(message.CoreObject.m_nMobileID, message.CoreObject);
            
            // Serialize the object and send it to the clients.
            var newObjMessage = new GAME_5_PROTOCOL.MSG_NEWOBJECT
            {
                Data = new CoreObjectSerializer()
                    .WithSerializerFlags(SerializerFlags.None)
                    .WithPropertyFlags(PropertyFlags.Public | PropertyFlags.Transmit | PropertyFlags.AuthorityTransmit)
                    .Serialize(message.CoreObject)
            };

            if (message.Selfless)
                BroadcastSelfless(message.Sender, newObjMessage);
            else
                Broadcast(newObjMessage);

            var mobileIdResponse = new ZONE_102_PROTOCOL.MSG_CREATENETWORKOBJECTRSP 
            {
                GlobalID = message.CoreObject.m_globalID,
                PermID = message.CoreObject.m_permID,
                MobileId = message.CoreObject.m_nMobileID
            };
            message.Sender.Tell(mobileIdResponse);
        }

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECTS))]
        private void ReceiveQueryZoneObjects(ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECTS message)
        {
            var response = new ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECTSRSP()
            {
                CoreObjects = this.CoreObjects.Values.ToList()
            };

            Sender.Tell(response);
        }

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST))]
        private void ReceiveZoneBroadcast(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST message)
        {
            // If this is a SERVERMOVE, save their position as well.
            if (message.Message.GetType() == typeof(GAME_5_PROTOCOL.MSG_SERVERMOVE))
            {
                var castMsg = (GAME_5_PROTOCOL.MSG_SERVERMOVE)message.Message;
                var playerInQuestion = CoreObjects.First(x => x.Key == castMsg.MobileID);

                if (playerInQuestion.Value is null) throw new Exception();

                // Normalize differentiating message values
                var x = unchecked((short)castMsg.LocationX) * 4.0f;
                var y = unchecked((short)castMsg.LocationY) * 4.0f;
                var z = unchecked((short)castMsg.LocationZ) * 4.0f;
                var direction = (float)(castMsg.Direction * Math.PI * 2 / 250);

                playerInQuestion.Value.m_location = new SharpDX.Vector3(x, y, z);
                // Can't figure out how orientation is calculated.
                // playerInQuestion.Value.m_orientation = new SharpDX.Vector3(direction, 0, 0);
            }

            if (message.Selfless)
                BroadcastSelfless(message.Sender, message.Message);
            else
                Broadcast(message.Message);
        }

        private ByteString SerializeCriticalObjects()
        {
            // Create a new BitIterator and prefix it with a count of the CoreObjects in this zone.
            var buffer = new BitIterator();

            var serializer = new CoreObjectSerializer();
            foreach (var t in CoreObjects.Values)
            {
                var coSerialized = serializer.Serialize(t);
                
                // Add the new serialization data to the buffer.
                buffer.WriteBytes(coSerialized);
            }

            return new ByteString(buffer.GetData());
        }
        
        private static uint GenerateDynamicZoneId()
        {
            var random = new Random();
            return (uint) random.Next(0, int.MaxValue);
        }

        private ushort GenerateMobileId()
        {
            ushort test;
            var r = new Random();
            while (true)
            {
                test = (ushort)r.Next(0, ushort.MaxValue);
                if (CoreObjects.Keys.Any(x => x == test))
                    continue;

                break;
            }

            return test;
        }
    }
}