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

namespace Imlight.Game
{
    public class Zone : ReceiveProtocolDispatcher
    {
        public string ZoneName { get; }
        public uint DynamicZoneId;
        public Dictionary<ushort, TypeCache.CoreObject> CriticalObjects { get; }
        public Dictionary<IActorRef, TypeCache.CoreObject> PlayerObjects { get; }

        public Zone(string zoneName)
        {
            this.ZoneName = zoneName;
            this.DynamicZoneId = GenerateDynamicZoneId();
            this.PlayerObjects = new Dictionary<IActorRef, TypeCache.CoreObject>();
            this.CriticalObjects = new Dictionary<ushort, TypeCache.CoreObject>();
        }
        
        public static Props Props(string zoneName)
        {
            return Akka.Actor.Props.Create(() => new Zone(zoneName));
        }

        public void Broadcast(INetworkMessage message)
        {
            foreach (var player in PlayerObjects.Keys)
            {
                player.Tell(message);
            }
        }

        public void BroadcastSelfless(IActorRef sender, INetworkMessage message)
        {
            foreach (var player in PlayerObjects.Keys
                         .Where(player => !player.Equals(sender)))
            {
                player.Tell(message);
            }
        }
        
        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUERYZONEDETAILS))]
        private void ReceiveQueryZone(ZONE_102_PROTOCOL.MSG_QUERYZONEDETAILS message)
        {
            Sender.Tell(new ZONE_102_PROTOCOL.MSG_QUERYZONEDETAILSRSP
            {
                PlayerCount = (uint) PlayerObjects.Count,
                CriticalObjects = CriticalObjects.Values.ToList(),
                PlayerObjects = PlayerObjects.Values.ToList(),
                DynamicZoneId = DynamicZoneId
            });
        }

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
        private void ReceiveAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message)
        {
            if (PlayerObjects.Keys.Contains(message.Player))
                throw new Exception("Player actor already exists on this server!");

            // Create new IDs for the player object.
            message.PlayerObject.m_globalID = RandomGen.GenerateGUID();
            message.PlayerObject.m_permID = RandomGen.GenerateGUID();
            message.PlayerObject.m_nMobileID = GenerateMobileId();
            
            BroadcastNewObject(message.Player, message.PlayerObject);
            SpawnPlayerObjectsForClient(message.Player);
            PlayerObjects.Add(message.Player, message.PlayerObject);

            var response = new ZONE_102_PROTOCOL.MSG_ADDPLAYERRSP { PlayerObject = message.PlayerObject };
            message.Player.Tell(response);

            Log.Logger.Debug($"Player {message.Player.Path.Name} added to zone {ZoneName}.");
        }
        
        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER))]
        private void ReceiveRemovePlayer(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message)
        {
            if (!PlayerObjects.Keys.Contains(message.Player))
                throw new Exception("Player actor does not exist on this server!");
            
            // Broadcast the removal of the player to all other players.
            var playerObjId = PlayerObjects.First(x => x.Key.Equals(message.Player)).Value.m_globalID;
            BroadcastDeleteObject(message.Player, playerObjId);
            
            PlayerObjects.Remove(message.Player);
        }

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST))]
        private void ReceiveZoneBroadcast(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST message)
        {
            if (message.Selfless)
                BroadcastSelfless(message.Sender, message.Message);
            else
                Broadcast(message.Message);
        }
        
        private void BroadcastNewObject(IActorRef sender, TypeCache.CoreObject obj)
        {
            var serializer = new CoreObjectSerializer()
                .WithSerializerFlags(SerializerFlags.None)
                .WithPropertyFlags(PropertyFlags.Public | PropertyFlags.Transmit | PropertyFlags.AuthorityTransmit);
            Broadcast(new GAME_5_PROTOCOL.MSG_NEWOBJECT
            {
                Data = serializer.Serialize(obj)
            });
        }
        
        private void BroadcastDeleteObject(IActorRef sender, ulong objId)
        {
            BroadcastSelfless(sender, new GAME_5_PROTOCOL.MSG_DELETEOBJECT { GameObjectID = objId });
        }

        private void SpawnPlayerObjectsForClient(IActorRef newClient)
        {
            var serializer = new CoreObjectSerializer()
                .WithSerializerFlags(SerializerFlags.None)
                .WithPropertyFlags(PropertyFlags.Public | PropertyFlags.Transmit | PropertyFlags.AuthorityTransmit);
            foreach (var obj in PlayerObjects.Values)
            {
                var msg = new GAME_5_PROTOCOL.MSG_NEWOBJECT()
                {
                    Data = serializer.Serialize(obj)
                };
                
                newClient.Tell(msg);
            }
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
                if (CriticalObjects.Keys.Any(x => x == test))
                    continue;

                break;
            }

            return test;
        }
    }
}
