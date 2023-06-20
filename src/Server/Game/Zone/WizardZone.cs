/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        public string ZoneName { get; }
        public uint DynamicZoneId { get; }
        public List<IActorRef> Players { get; } = new();
        public Dictionary<ushort, CoreObject> ZoneObjects { get; } = new();

        // Zone data fields.
        private List<IActorRef> _pathActorRefs = new();

        // ctor
        public WizardZone(string zoneName)
        {
            this.ZoneName = zoneName;
            this.DynamicZoneId = GenerateDynamicZoneId();

            // Load and initialize this zone.
            WizardZoneLoader.LoadZoneData(this, Self, Context);

            Log.Logger.Debug($"Zone [{ZoneName}] created.");
        }
        
        // Akka.NET ctor
        public static Props Props(string zoneName)
        {
            return Akka.Actor.Props.Create(() => new WizardZone(zoneName));
        }

        /// <summary>
        /// Broadcast a message to all the players in the zone.
        /// </summary>
        /// <param name="message">The <see cref="INetworkMessage"/> that will be broadcast.</param>
        private void Broadcast(INetworkMessage message)
        {
            foreach (var player in Players)
            {
                player.Tell(message);
            }
        }

        /// <summary>
        /// Broadcast a message to all the players in this zone, except to the player that broadcast it.
        /// </summary>
        /// <param name="sender">The <see cref="IActorRef"/> that this broadcast will ignore.</param>
        /// <param name="message">The <see cref="INetworkMessage"/> that will be broadcast.</param>
        private void BroadcastSelfless(IActorRef sender, INetworkMessage message)
        {
            foreach (var player in Players
                         .Where(player => !player.Equals(sender)))
            {
                player.Tell(message);
            }
        }
        
        #region Handlers
        
        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUERYZONE))]
        private void ReceiveQueryZone(ZONE_102_PROTOCOL.MSG_QUERYZONE message)
        {
            Sender.Tell(new ZONE_102_PROTOCOL.MSG_QUERYZONERSP
            {
                ZoneActorRef = Self,
                ZoneObjects = this.ZoneObjects.Values.ToArray(),
                DynamicZoneId = this.DynamicZoneId,
                ErrorCode = 0
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

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDOBJECT))]
        private void ReceiveNewObject(ZONE_102_PROTOCOL.MSG_ADDOBJECT message)
        {
            AddObject(message.CoreObject);
        }
        
        #endregion

        private void AddObject(CoreObject obj)
        {
            // Generate a new zone ID for the object.
            var id = GenerateMobileId();
            obj.m_nMobileID = id;
            
            // Broadcast the new object to each player in the zone.
            var serializer = new CoreObjectSerializer()
                .WithSerializerFlags(SerializerFlags.None)
                .WithPropertyFlags(PropertyFlags.Public | PropertyFlags.Transmit | PropertyFlags.AuthorityTransmit);
            Broadcast(new GAME_5_PROTOCOL.MSG_NEWOBJECT { Data = serializer.Serialize(obj) });
            
            ZoneObjects.Add(id, obj);
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
