using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imlight.Common;
using Imlight.Net;
using Imlight.Net.Messages;
using Imlight.Resources;
using WizUnraveler;
using WizUnraveler.Cache;
using WizUnraveler.Data;
using WizUnraveler.DML;
using WizUnraveler.ObjectProperty;
using static WizUnraveler.Cache.TypeCache;
using static WizUnraveler.ObjectSerializer;

namespace Imlight.Game
{
    public class Zone : ReceiveProtocolDispatcher
    {
        public const string ZONE_DATA_FILE_NAME = "gamedata.bin";
        private const string SPAWN_DATA_FILE_NAME = "spawnData.xml";
        private const string PATH_DATA_FILE_NAME = "pathData.xml";
        private const string NODE_DATA_FILE_NAME = "pathNodeData.bin";
        
        public string ZoneName { get; }
        public uint DynamicZoneId { get; }
        public List<IActorRef> Players { get; } = new();
        public Dictionary<ushort, CoreObject> ZoneObjects { get; } = new();

        // Zone data fields.
        // @todo: avoid allocation of all this data.
        private WizZoneData _zoneData;
        private List<SpawnObject> _spawners = new();
        private List<PathObjectTemplate> _pathObjects = new();
        private List<NodeObject> _nodeObjects = new();

        public Zone(string zoneName)
        {
            this.ZoneName = zoneName;
            this.DynamicZoneId = GenerateDynamicZoneId();

            SetZoneData(zoneName);
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
        
        #region Handlers
        
        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUERYZONEDETAILS))]
        private void ReceiveQueryZone(ZONE_102_PROTOCOL.MSG_QUERYZONEDETAILS message)
        {
            Sender.Tell(new ZONE_102_PROTOCOL.MSG_QUERYZONEDETAILSRSP
            {
                PlayerCount = (uint)Players.Count,
                DynamicZoneId = DynamicZoneId
            });
        }

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
        private void ReceiveAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message)
        {
            if (Players.Contains(message.Player))
                throw new Exception("Player actor already exists in this zone!");

            // Mobile ID is an instance agnostic ID that is used to identify a player.
            message.PlayerObject.m_nMobileID = GenerateMobileId();
            
            SpawnZoneObjectsForClient(message.Player);
            AddObject(message.PlayerObject);
            
            Players.Add(message.Player);

            // Inform the player that they've been successfully added to the zone.
            var response = new ZONE_102_PROTOCOL.MSG_ADDPLAYERRSP { PlayerObject = message.PlayerObject };
            message.Player.Tell(response);

            Log.Logger.Debug($"Player {message.Player.Path.Name} added to zone {ZoneName}.");
        }
        
        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER))]
        private void ReceiveRemovePlayer(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message)
        {
            if (!Players.Contains(message.Player))
                throw new Exception("Player actor does not exist on this server!");
            
            RemoveObject(message.GlobalId);
            
            // We only want to remove instanced objects for the client if they're transferring zones.
            // Otherwise, we'll just be sending a torrent of messages to a disconnected socket.
            if (message.IsZoneTransfer) 
                RemoveZoneObjectsForClient(message.Player);

            Players.Remove(message.Player);
        }

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST))]
        private void ReceiveZoneBroadcast(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST message)
        {
            if (message.Selfless)
                BroadcastSelfless(message.Sender, message.Message);
            else
                Broadcast(message.Message);
        }
        
        #endregion
        
        private void AddObject(CoreObject obj)
        {
            var serializer = new CoreObjectSerializer()
                .WithSerializerFlags(SerializerFlags.None)
                .WithPropertyFlags(PropertyFlags.Public | PropertyFlags.Transmit | PropertyFlags.AuthorityTransmit);
            Broadcast(new GAME_5_PROTOCOL.MSG_NEWOBJECT { Data = serializer.Serialize(obj) });
            
            ZoneObjects.Add(obj.m_nMobileID, obj);
        }
        
        private void RemoveObject(ulong objId)
        {
            Broadcast(new GAME_5_PROTOCOL.MSG_REMOVEOBJECT() { GameObjectID = objId });
            
            var obj = ZoneObjects.First(x => x.Value.m_globalID == objId).Value;
            ZoneObjects.Remove(obj.m_nMobileID);
        }

        private void SpawnZoneObjectsForClient(IActorRef newClient)
        {
            var serializer = new CoreObjectSerializer()
                .WithSerializerFlags(SerializerFlags.None)
                .WithPropertyFlags(PropertyFlags.Public | PropertyFlags.Transmit | PropertyFlags.AuthorityTransmit);
            foreach (var obj in ZoneObjects.Values)
            {
                var msg = new GAME_5_PROTOCOL.MSG_NEWOBJECT() { Data = serializer.Serialize(obj) };
                newClient.Tell(msg);
            }
        }

        private void RemoveZoneObjectsForClient(IActorRef client)
        {
            foreach (var go in ZoneObjects.Values)
            { 
                var msg = new GAME_5_PROTOCOL.MSG_REMOVEOBJECT() { GameObjectID = go.m_globalID };
                client.Tell(msg);
            }
        }

        private void SetZoneData(string name)
        {
            if (!ResourceManager.LoadWad(name, out var wad))
            {
                Log.Logger.Error($"Zone {name} loaded, but no wad was found in the gamedata directory.");
                return;
            }

            // Load the zone data.
            var zoneLoadResult = ResourceManager.LoadFile<WizZoneData>(wad, ZONE_DATA_FILE_NAME, out var zoneData);
            if (zoneLoadResult) _zoneData = zoneData;
            else Log.Logger.Error($"Zone {name} loaded, but zone data was missing or invalid.");
            
            // Load the spawn data.
            var spawnDataLoadResult = ResourceManager.LoadFile<SpawnManager>(wad, SPAWN_DATA_FILE_NAME, out var spawnData);
            if (spawnDataLoadResult) _spawners = spawnData.m_spawners;
            else Log.Logger.Error($"Zone {name} loaded, but spawn data was missing or invalid.");
            
            // Load the path template data.
            var pathListLoadResult = ResourceManager.LoadFile<PathManager_PathTemplateList>(wad, PATH_DATA_FILE_NAME, out var pathData);
            if (pathListLoadResult) _pathObjects = pathData.m_pathList;
            else Log.Logger.Error($"Zone {name} loaded, but path data was missing or invalid.");

            // Load the node template data.
            var nodeListLoadResult = ResourceManager.LoadFile<PathManager_NodeTemplateList>(wad, NODE_DATA_FILE_NAME, out var nodeData);
            if (nodeListLoadResult) _nodeObjects = nodeData.m_nodeList;
            else Log.Logger.Error($"Zone {name} loaded, but node data was missing or invalid.");
            
            if (zoneLoadResult) SetGameObjects();
            if (spawnDataLoadResult && pathListLoadResult && nodeListLoadResult) SetPathData();
        }

        private void SetGameObjects()
        {
            foreach (var obj in _zoneData.m_objectList
                         .Where(x => x is not null))
            {
                var newObj = CoreObjectFactory.CreateObjectFromInfo(obj);
                if (newObj is null) continue;

                // Create new instance agnostic ID for the object.
                var id = GenerateMobileId();
                newObj.m_nMobileID = id;

                ZoneObjects.Add(id, newObj);
            }
        }

        private void SetPathData()
        {
            //@todo: Implement path data.
        }

        private static uint GenerateDynamicZoneId()
        {
            var random = new Random();
            return (uint) random.Next(0, int.MaxValue);
        }

        private ushort GenerateMobileId()
        {
            // Avoid collisions as much as possible.
            ushort test;
            var r = new Random();
            while (true)
            {
                test = (ushort)r.Next(0, ushort.MaxValue);
                if (ZoneObjects.Keys.Any(x => x == test))
                    continue;

                break;
            }

            return test;
        }
    }
}
