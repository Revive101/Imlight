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
using WizUnraveler.ObjectProperty;
using WizUnraveler.Secrets;
using static WizUnraveler.Cache.TypeCache;
using static WizUnraveler.ObjectProperty.ObjectSerializer;
using static WizUnraveler.Secrets.ServerTypeCache;

namespace Imlight.Server.Game.Zone;

public class WizardZone : ReceiveProtocolDispatcher
{
    public string ZoneName { get; }
    
    private readonly uint _dynamicZoneId;
    private readonly IActorRef _objectSupervisorRef;
    private readonly IActorRef _pathSupervisorRef;
    private readonly IActorRef _volumeSupervisorRef;
    private readonly List<Trigger> _triggers;

    // TODO: I don't want to be saving CoreObjects here.
    private readonly Dictionary<IActorRef, CoreObject> _zoneCreatures;
    private readonly Dictionary<IActorRef, CoreObject> _zoneObjects;
    private readonly Dictionary<IActorRef, CoreObject> _zonePlayers;
    private readonly Dictionary<IActorRef, CoreObject> _zoneVolumes;

    // ctor
    public WizardZone(string zoneName)
    {
        ZoneName = zoneName;
        _dynamicZoneId = GenerateDynamicZoneId();
        _zonePlayers = new Dictionary<IActorRef, CoreObject>();
        _zoneCreatures = new Dictionary<IActorRef, CoreObject>();
        _zoneObjects = new Dictionary<IActorRef, CoreObject>();
        _zoneVolumes = new Dictionary<IActorRef, CoreObject>();

        _pathSupervisorRef = CreatePathSupervisor();
        _objectSupervisorRef = CreateObjectSupervisor();
        _volumeSupervisorRef = CreateVolumeSupervisor();
        _triggers = new List<Trigger>();
        // We don't need to create a PlayerSupervisor, as the GameServer manages that for us.

        // Load and initialize this zone.
        WizardZoneLoader.LoadZoneData(this, Self);

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
    /// <param name="message">The <see cref="INetworkMessage" /> that will be broadcast.</param>
    private void Broadcast(INetworkMessage message)
    {
        foreach (var player in _zonePlayers.Keys) 
            player.Tell(message);
    }

    /// <summary>
    /// Broadcast a message to all the players in this zone, except to the player that broadcast it.
    /// </summary>
    /// <param name="sender">The <see cref="IActorRef" /> that this broadcast will ignore.</param>
    /// <param name="message">The <see cref="INetworkMessage" /> that will be broadcast.</param>
    private void BroadcastSelfless(IActorRef sender, INetworkMessage message)
    {
        foreach (var player in _zonePlayers.Keys
                     .Where(player => !player.Equals(sender)))
            player.Tell(message);
    }

    /// <summary>
    /// Creates a <see cref="WizardZonePathSupervisor"/> as a child of this WizardZone.
    /// </summary>
    /// <returns>The actor reference pointing to the newly created actor.</returns>
    private IActorRef CreatePathSupervisor()
    {
        var props = WizardZonePathSupervisor.Props(Self);
        return Context.ActorOf(props);
    }

    /// <summary>
    /// Creates a <see cref="WizardZoneObjectSupervisor"/> as a child of this WizardZone.
    /// </summary>
    /// <returns>The actor reference pointing to the newly created actor.</returns>
    private IActorRef CreateObjectSupervisor()
    {
        var props = WizardZoneObjectSupervisor.Props(Self);
        return Context.ActorOf(props);
    }

    /// <summary>
    /// Creates a new <see cref="WizardZoneVolumeSupervisor"/> as a child of this WizardZone.
    /// </summary>
    /// <returns></returns>
    private IActorRef CreateVolumeSupervisor()
    {
        var props = WizardZoneVolumeSupervisor.Props(Self);
        return Context.ActorOf(props);
    }

    /// <summary>
    /// Broadcasts the creation of a new <see cref="CoreObject"/> to each player in the zone.
    /// </summary>
    /// <param name="obj"></param>
    private void BroadcastObjectCreation(CoreObject obj)
    {
        var serializer = new CoreObjectSerializer()
            .WithSerializerFlags(SerializerFlags.None)
            .WithPropertyFlags(PropertyFlags.Public | PropertyFlags.Transmit | PropertyFlags.AuthorityTransmit);
        Broadcast(new GAME_5_PROTOCOL.MSG_NEWOBJECT { Data = serializer.Serialize(obj) });
    }

    private void SpawnZoneObjectsForClient(IActorRef newClient)
    {
        // TODO: Make the WizardZoneObject responsible for this rather than the zone.
        SerializeAndSendObjects(newClient, _zoneObjects.Values);
        SerializeAndSendObjects(newClient, _zonePlayers.Values);
        SerializeAndSendObjects(newClient, _zoneCreatures.Values);
    }

    private void RemoveZoneObjectsForClient(IActorRef client)
    {
        RemoveObjects(client, _zoneObjects.Values);
        RemoveObjects(client, _zonePlayers.Values);
        RemoveObjects(client, _zoneCreatures.Values);
    }

    private void SerializeAndSendObjects(IActorRef client, IEnumerable<CoreObject> objects)
    {
        var serializer = new CoreObjectSerializer()
            .WithSerializerFlags(SerializerFlags.None)
            .WithPropertyFlags(PropertyFlags.Public | PropertyFlags.Transmit | PropertyFlags.AuthorityTransmit);
        foreach (var obj in objects)
        {
            var msg = new GAME_5_PROTOCOL.MSG_NEWOBJECT { Data = serializer.Serialize(obj) };
            client.Tell(msg);
        }
    }

    private void RemoveObjects(IActorRef client, IEnumerable<CoreObject> objects)
    {
        foreach (var obj in objects)
        {
            var msg = new GAME_5_PROTOCOL.MSG_REMOVEOBJECT { GameObjectID = obj.m_globalID };
            client.Tell(msg);
        }
    }

    private void InformVolumesOfNewPlayer(CoreObject newPlayer)
    {
        foreach (var vol in _zoneVolumes)
        {
            var msg = new ZONE_102_PROTOCOL.MSG_TRIGGERGRACE { CoreObject = newPlayer };
            vol.Key.Tell(msg);
        }
    }

    /// <summary>
    /// Generates a new <see cref="uint"/> value as a dynamic ID for this WizardZone.
    /// </summary>
    /// <returns>The ID generated.</returns>
    private static uint GenerateDynamicZoneId()
    {
        var random = new Random();
        return (uint)random.Next(0, int.MaxValue);
    }

    /// <summary>
    /// Generates a random, unused <see cref="ushort"/> object ID.
    /// </summary>
    /// <returns>The ID generated.</returns>
    private ushort GenerateMobileId()
    {
        // Avoid collisions as much as possible.
        ushort test;
        var r = new Random();
        while (true)
        {
            test = (ushort)r.Next(0, ushort.MaxValue);
            if (_zoneObjects.Values.Any(x => x.m_nMobileID == test)
                || _zonePlayers.Values.Any(x => x.m_nMobileID == test)
                || _zoneCreatures.Values.Any(x => x.m_nMobileID == test))
                continue;

            break;
        }

        return test;
    }

    private static KeyValuePair<IActorRef, CoreObject>? SearchObjectInZone(GID globalId,
        params Dictionary<IActorRef, CoreObject>[] dictionaries)
    {
        foreach (var dictionary in dictionaries)
        {
            foreach (var kvp in dictionary)
            {
                if (kvp.Value.m_globalID == globalId)
                    return kvp;
            }
        }

        return null;
    }

    #region Handlers
    
    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONETRANSFER))]
    private void ReceiveQueryZone(ZONE_102_PROTOCOL.MSG_ZONETRANSFER message)
    {
        Sender.Tell(new ZONE_102_PROTOCOL.MSG_ZONETRANSFERRSP
        {
            ZoneActorRef = Self,
            DynamicZoneId = _dynamicZoneId,
            ErrorCode = 0
        });
    }
    
    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECT))]
    private void ReceiveQueryObject(ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECT message)
    {
        var kvp = SearchObjectInZone(message.ObjectId, _zoneObjects, _zonePlayers, _zoneCreatures)!.Value;
        var rsp = new ZONE_102_PROTOCOL.MSG_OBJECTDETAILS()
        {
            CoreObject = kvp.Value,
            ObjectIdentity = kvp.Key
        };
        
        Sender.Tell(rsp);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
    private void ReceiveAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message)
    {
        if (_zonePlayers.Keys.Contains(message.Player))
            throw new Exception("Player actor already exists in this zone!");
        
        message.PlayerObject.m_nMobileID = GenerateMobileId();

        // Spawn the existing zone objects for our new player. Add them to the player list afterwards, so they don't
        // load themselves.
        SpawnZoneObjectsForClient(message.Player);
        _zonePlayers.Add(message.Player, message.PlayerObject);
        
        BroadcastObjectCreation(message.PlayerObject);

        // Inform each volume of this object, so that they may check if the player is within it's bounds and provide
        // them a grace period.
        InformVolumesOfNewPlayer(message.PlayerObject);

        // Inform the player that they've been successfully added to the zone.
        var response = new ZONE_102_PROTOCOL.MSG_ADDPLAYERRSP { PlayerObject = message.PlayerObject };
        message.Player.Tell(response);

        Log.Logger.Debug($"Player {message.Player.Path.Name} added to zone {ZoneName}.");
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER))]
    private void ReceiveRemovePlayer(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message)
    {
        if (!_zonePlayers.TryGetValue(message.Player, out var obj))
        {
            // fixme: This should become an exception inevitably. Due to race conditions, this ends up getting
            // triggered more than it should, so for now it just remains an error log.
            Log.Logger.Error($"Zone [{ZoneName}] tried to remove player it did not have.");
            return;
        }

        // Inform every Wizard101 client that this object has been removed.
        Broadcast(new GAME_5_PROTOCOL.MSG_REMOVEOBJECT { GameObjectID = obj.m_globalID });

        // Now, *actually* remove it from the zone.
        _zonePlayers.Remove(message.Player);

        // We only want to remove instanced objects for the client if they're transferring zones.
        // Otherwise, we'll just be sending a torrent of messages to a disconnected socket.
        if (message.IsZoneTransfer)
            RemoveZoneObjectsForClient(message.Player);

        Log.Logger.Debug($"Player {message.Player.Path.Name} removed from zone {ZoneName}.");
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST))]
    private void ReceiveZoneBroadcast(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST message)
    {
        if (message.Selfless)
            BroadcastSelfless(message.Sender, message.Message);
        else
            Broadcast(message.Message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPATH))]
    private void ReceiveAddPath(ZONE_102_PROTOCOL.MSG_ADDPATH message)
    {
        _pathSupervisorRef.Forward(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_OBJECTDETAILS))]
    private void ReceiveAddCreature(ZONE_102_PROTOCOL.MSG_OBJECTDETAILS message)
    {
        // This message is received on the WizardZone to:
        // a. Give it a unique ID.
        // b. Broadcast it's creation to every player in the zone.
        // c. Keep it's reference here so that new players will be told of it's data.
        var id = GenerateMobileId();
        message.CoreObject.m_nMobileID = id;

        BroadcastObjectCreation(message.CoreObject);

        _zoneCreatures.Add(message.ObjectIdentity, message.CoreObject);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDOBJECT))]
    private void ReceiveAddObject(ZONE_102_PROTOCOL.MSG_ADDOBJECT message)
    {
        // This message is received on the WizardZone to:
        // a. Give it a unique ID.
        // b. Broadcast it's creation to every player in the zone.
        // c. Keep it's reference here so that new players will be told of it's data.
        var id = GenerateMobileId();
        message.CoreObject.m_nMobileID = id;

        BroadcastObjectCreation(message.CoreObject);

        // The object has not been created as an actor yet. We'll tell the object supervisor about this object, and let
        // it handle the creation of this object. We will await the replies, since some of it's details are needed here.
        var rsp = _objectSupervisorRef
            .Ask<ZONE_102_PROTOCOL.MSG_ADDOBJECTRSP>(message)
            .Result;
        rsp.MobileId = id;

        _zoneObjects.Add(rsp.ActorRef, message.CoreObject);
        
        Sender.Tell(rsp);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDVOLUME))]
    private void ReceiveAddVolume(ZONE_102_PROTOCOL.MSG_ADDVOLUME message)
    {
        var id = GenerateMobileId();
        message.CoreObject.m_nMobileID = id;
        message.CoreObject.m_debugName = message.Volume.m_volumeName;
        
        // The object has not been created as an actor yet. We'll tell the volume supervisor about this object, and let
        // it handle the creation of this object. We will await the replies, since some of it's details are needed here.
        var rsp = _volumeSupervisorRef
            .Ask<ZONE_102_PROTOCOL.MSG_ADDOBJECTRSP>(message)
            .Result;
        
        _zoneVolumes.Add(rsp.ActorRef, message.CoreObject);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDTRIGGER))]
    private void ReceiveAddTrigger(ZONE_102_PROTOCOL.MSG_ADDTRIGGER message)
    {
        this._triggers.Add(message.Trigger);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_TRIGGER))]
    private void ReceiveActivateTrigger(ZONE_102_PROTOCOL.MSG_TRIGGER message)
    {
        var triggers = (
            from trigger in _triggers 
            from vol in trigger.m_volumes 
            where vol == message.TriggerName 
            select trigger)
            .ToList();

        if (!triggers.Any())
        {
            Log.Logger.Debug($"{nameof(WizardZoneVolume)} {ZoneName} tried to activate trigger " +
                               $"\"{message.TriggerName}\", but no trigger was found in the zone.");
            return;
        }

        // TODO: For now, we're only supporting the `ResTeleport`.
        foreach (var trigger in triggers
                     .Where(t => t.m_results.m_results.FirstOrDefault() is ServerTypeCache.ResTeleport))
        {
            foreach (var result in trigger.m_results.m_results)
            {
                var msg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER
                {
                    ZoneName = ((ServerTypeCache.ResTeleport)result).m_destinationZone,
                    Location = ((ServerTypeCache.ResTeleport)result).m_destinationLoc,
                    SendToClient = true
                };
                message.Suspect.Tell(msg);
            }
        }
        
        Log.Logger.Debug($"{nameof(WizardZoneVolume)} {ZoneName} activated trigger \"{triggers[0].m_triggerName}\".");
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_TRIGGERQUERY))]
    private void ReceiveZoneInteraction(ZONE_102_PROTOCOL.MSG_TRIGGERQUERY message)
    {
        // An object in the zone needs to inform every volume that it's fishing for a reaction. Forward the object to
        // every volume in the zone.
        foreach (var volume in _zoneVolumes.Keys)
        {
            volume.Forward(message);
        }
    }

    #endregion
}