/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imlight.Common.Serializable;
using Imlight.Common.Utilities;
using Imlight.Server.Database;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;
using WizUnraveler.Cache;
using WizUnraveler.DML;
using WizUnraveler.Formats;
using WizUnraveler.IO;
using static WizUnraveler.Cache.TypeCache;
using static WizUnraveler.ObjectProperty.ObjectSerializer;

namespace Imlight.Server.Game.Zone
{
    public class WizardZone : ReceiveProtocolDispatcher
    {
        private const string ZONE_DATA_FILE_NAME = "gamedata.bin";
        private const string SPAWN_DATA_FILE_NAME = "spawnData.xml";
        private const string PATH_DATA_FILE_NAME = "pathData.xml";
        private const string NODE_DATA_FILE_NAME = "pathNodeData.bin";
        private const string VOLUME_DATA_FILE_NAME = "volumes.xml";
        private const string TRIGGER_DATA_FILE_NAME = "triggers.xml";
        
        public string ZoneName { get; }
        public uint DynamicZoneId { get; }
        public List<IActorRef> Players { get; } = new();
        public Dictionary<ushort, CoreObject> ZoneObjects { get; } = new();

        // Zone data fields.
        private List<SpawnObject> _spawners = new();
        private List<WizardZonePath> _paths = new();

        public WizardZone(string zoneName)
        {
            this.ZoneName = zoneName;
            this.DynamicZoneId = GenerateDynamicZoneId();

            SetZoneData(zoneName);
            
            Log.Logger.Debug($"Zone [{ZoneName}] created.");
        }
        
        public static Props Props(string zoneName)
        {
            return Akka.Actor.Props.Create(() => new WizardZone(zoneName));
        }

        private void Broadcast(INetworkMessage message)
        {
            foreach (var player in Players)
            {
                player.Tell(message);
            }
        }

        private void BroadcastSelfless(IActorRef sender, INetworkMessage message)
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
            
            // Spawn the existing zone objects for the new player.
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
            {
                Log.Logger.Warning($"Duplicate removal of player in zone: {this.ZoneName}.");
                return;
            }
            
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

        #region Zone Data

        private void SetZoneData(string name)
        {
            if (!ResourceManager.TryLoadFile(name, out var wad))
            {
                Log.Logger.Error($"Zone [{ZoneName}] tried to load its own data, but none was found " +
                                 $"in the {nameof(ResourceManager)}. This is fine, but the zone will not contain any " +
                                 $"objects or volumes.");
                return;
            }

            LoadZoneData(wad);
            LoadSpawnData(wad);
            LoadPathData(wad);
        }

        private void LoadZoneData(Wad wad)
        {
            var deSer = new FileSerializer();
            var zoneData = deSer.OpenClass<WizZoneData>(wad, ZONE_DATA_FILE_NAME);
    
            if (zoneData is not null)
                CreateZoneGameObjects(zoneData);
            else
                Log.Logger.Error($"Zone {ZoneName} could not load {ZONE_DATA_FILE_NAME} was missing or invalid.");
        }

        private void LoadSpawnData(Wad wad)
        {
            var deSer = new FileSerializer();
            var spawnData = deSer.OpenClass<SpawnManager>(wad, SPAWN_DATA_FILE_NAME);

            if (spawnData is not null)
                _spawners = spawnData.m_spawners;
            else
                Log.Logger.Error($"Zone {ZoneName} could not load {SPAWN_DATA_FILE_NAME} was missing or invalid.");
        }

        private void LoadPathData(Wad wad)
        {
            var deSer = new FileSerializer();
            
            // Load the zone paths.
            var pathData = deSer.OpenClass<PathManager_PathTemplateList>(wad, PATH_DATA_FILE_NAME);
            if (pathData is null)
                Log.Logger.Error($"Zone {ZoneName} could not load {PATH_DATA_FILE_NAME} was missing or invalid.");
            
            // Load each of the zone nodes.
            var nodeData = deSer.OpenClass<PathManager_NodeTemplateList>(wad, NODE_DATA_FILE_NAME);
            if (nodeData is null)
                Log.Logger.Error($"Zone {ZoneName} could not load {NODE_DATA_FILE_NAME} was missing or invalid.");
            
            if (pathData is not null && nodeData is not null)
                CreateZonePaths(pathData, nodeData);
        }


        private void CreateZoneGameObjects(WizZoneData zoneData)
        {
            foreach (var obj in zoneData.m_objectList
                         .Where(x => x is not null))
            {
                var newObj = CoreObjectFactory.CreateObjectFromInfo(obj);
                if (newObj is null) 
                    continue;

                // Create new instance agnostic ID for the object.
                var id = GenerateMobileId();
                newObj.m_nMobileID = id;
                ZoneObjects.Add(id, newObj);
            }
        }

        private void CreateZonePaths(PathManager_PathTemplateList paths, PathManager_NodeTemplateList nodes)
        {
            _paths = new List<WizardZonePath>();

            // Iterate through each path and create our own proprietary WizardZonePath type.
            foreach (var path in paths.m_pathList)
            {
                var wizPath = new WizardZonePath(path.m_id, path.m_name);
                _paths.Add(wizPath);
            
                // Iterate through all the given IDs of this path and search for them in the NodeTemplateList.
                foreach (var id in path.m_nodeIDs)
                {
                    // If a node is found, add it to the path.
                    var node = nodes.m_nodeList.Find(n => n.m_id == id);
                    if (node is not null)
                    {
                        wizPath.Nodes.Add(node);
                    }
                }
            }
        }
        
        #endregion
        
        private void AddObject(CoreObject obj)
        {
            // Broadcast the new object to each player in the zone.
            var serializer = new CoreObjectSerializer()
                .WithSerializerFlags(SerializerFlags.None)
                .WithPropertyFlags(PropertyFlags.Public | PropertyFlags.Transmit | PropertyFlags.AuthorityTransmit);
            Broadcast(new GAME_5_PROTOCOL.MSG_NEWOBJECT { Data = serializer.Serialize(obj) });
            
            ZoneObjects.Add(obj.m_nMobileID, obj);
        }
        
        private void RemoveObject(ulong objId)
        {
            // Inform every Wizard101 client that this object has been removed.
            Broadcast(new GAME_5_PROTOCOL.MSG_REMOVEOBJECT { GameObjectID = objId });
            
            // Now, *actually* remove it from the zone.
            var obj = ZoneObjects
                .First(x => x.Value.m_globalID == objId)
                .Value;
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
