/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Common.Serializable;
using Imlight.Common.Utilities;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;
using WizUnraveler.Cache;
using WizUnraveler.DML;
using WizUnraveler.Secrets;
using static WizUnraveler.Cache.TypeCache;
using static WizUnraveler.ObjectProperty.ObjectSerializer;
using static WizUnraveler.Secrets.ServerTypeCache;

namespace Imlight.Server.Game.Zone;

public class WizardZone : ReceiveProtocolDispatcher
{
    private const ushort ReservedMobileIdMax = 1000;
    
    public string ZoneName { get; }
    
    private readonly uint _dynamicZoneId;
    private readonly IActorRef _objectSupervisorRef;
    private readonly List<Trigger> _triggers;
    private readonly Dictionary<IActorRef, CoreObject> _zonePlayers;
    private ushort _zoneObjectMobileIdCounter;

    // ctor
    public WizardZone(string zoneName)
    {
        ZoneName = zoneName;
        _dynamicZoneId = GenerateDynamicZoneId();
        _zonePlayers = new Dictionary<IActorRef, CoreObject>();
        _objectSupervisorRef = CreateObjectSupervisor();
        _triggers = new List<Trigger>();

        // Load and initialize this zone.
        WizardZoneLoader.LoadZoneData(this, Self);
        Log.Logger.Debug("Zone {ZoneName} created.", ZoneName);
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
    /// Creates a <see cref="WizardZoneObjectSupervisor"/> as a child of this WizardZone.
    /// </summary>
    /// <returns>The actor reference pointing to the newly created actor.</returns>
    private IActorRef CreateObjectSupervisor()
    {
        var props = WizardZoneObjectSupervisor.Props(Self);
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
    
    /// <summary>
    /// Broadcasts to each zone object and player of a new arrival in the zone.
    /// </summary>
    /// <param name="message"></param>
    private void InformZoneObjectsOfJoin(ZONE_102_PROTOCOL.MSG_ADDPLAYER message)
    {
        // Forward the new player message to every zone object so that they may personally deal with this situation.
        _objectSupervisorRef.Tell(new ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST
        {
            Source = message.Player,
            Messages = new IServerMessage[] { message }
        });

        // Broadcast this new player to each existing player in the zone.
        BroadcastObjectCreation(message.PlayerObject);
    }

    /// <summary>
    /// Broadcasts to each zone object and player of a departure in the zone.
    /// </summary>
    /// <param name="message"></param>
    private void InformZoneObjectsOfDeparture(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message)
    {
        // Forward the departure message to every zone object so that they may personally deal with this situation.
        _objectSupervisorRef.Tell(new ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST
        {
            Source = message.Player,
            Messages = new IServerMessage[] { message }
        });
    }

    /// <summary>
    /// Spawns each existing player in the zone for a client.
    /// </summary>
    /// <param name="client">The client in question.</param>
    private void SpawnPlayersForNewClient(IActorRef client)
    {
        // Now spawn each existing player.
        var serializer = new CoreObjectSerializer()
            .WithSerializerFlags(SerializerFlags.None)
            .WithPropertyFlags(PropertyFlags.Public | PropertyFlags.Transmit | PropertyFlags.AuthorityTransmit);
        foreach (var obj in _zonePlayers.Values)
        {
            var msg = new GAME_5_PROTOCOL.MSG_NEWOBJECT { Data = serializer.Serialize(obj) };
            client.Tell(msg);
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
            test = (ushort)r.Next(ReservedMobileIdMax, ushort.MaxValue);
            if (_zonePlayers.Values.Any(x => x.m_nMobileID == test))
                continue;

            break;
        }

        return test;
    }

    /// <summary>
    /// Generate an unused mobile ID. Reserved for zone objects.
    /// </summary>
    /// <returns></returns>
    private ushort GenerateReservedMobileId()
    {
        if (_zoneObjectMobileIdCounter + 1 >= ReservedMobileIdMax)
            throw new Exception($"Zone \"{ZoneName}\" reached the maximum reserved mobile ID count!");
        return ++_zoneObjectMobileIdCounter;
    }

    #region Handlers
    
    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONETRANSFER))]
    private void ReceiveZoneTransfer(ZONE_102_PROTOCOL.MSG_ZONETRANSFER message)
    {
        // This is step 1 of the zone transfer process.
        // There's nothing we need to do here except for telling the sender the zone details.
        // We'll be waiting to receive MSG_ADDPLAYER before we do any object creation.
        Sender.Tell(new ZONE_102_PROTOCOL.MSG_ZONETRANSFERRSP
        {
            ZoneActorRef = Self,
            DynamicZoneId = _dynamicZoneId,
            ErrorCode = 0
        });
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
    private void ReceiveAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message)
    {
        if (_zonePlayers.ContainsKey(message.Player))
            throw new Exception("Player actor already exists in this zone!");
        
        // Generate an ID for this new player that is zone agnostic.
        message.PlayerObject.m_nMobileID = GenerateMobileId();

        InformZoneObjectsOfJoin(message);
        SpawnPlayersForNewClient(message.Player);
        
        // Now we add the player, so they don't end up creating themselves when we spawn each zone object.
        _zonePlayers.Add(message.Player, message.PlayerObject);

        // Inform the player that they've been successfully added to the zone.
        var response = new ZONE_102_PROTOCOL.MSG_ADDPLAYERRSP { PlayerObject = message.PlayerObject };
        message.Player.Tell(response);

        Log.Logger.Debug("Player {Name} added to zone {ZoneName}.", 
            message.Player.Path.Name, ZoneName);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER))]
    private void ReceiveRemovePlayer(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message)
    {
        if (!_zonePlayers.TryGetValue(message.Player, out var obj))
            throw new Exception($"Zone \"{ZoneName}\" tried to remove player it did not have.");

        // Don't send a torrent of messages to a disconnected socket.
        if (message.IsPlayerStillConnected) 
            InformZoneObjectsOfDeparture(message);
        
        // Inform every other player that this object has been removed.
        Broadcast(new GAME_5_PROTOCOL.MSG_REMOVEOBJECT { GameObjectID = message.GlobalId });
        
        _zonePlayers.Remove(message.Player);

        // Wait a little while until sending the reply back, just in case the zone objects have not yet been cleaned.
        var s = Sender;
        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            var rsp = new ZONE_102_PROTOCOL.MSG_REMOVEPLAYERRSP();
            s.Tell(rsp);
        }).Wait();

        Log.Logger.Debug("Player {Name} removed from zone {ZoneName}.",
            message.Player.Path.Name, ZoneName);
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
        _objectSupervisorRef.Forward(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDOBJECT))]
    private void ReceiveAddObject(ZONE_102_PROTOCOL.MSG_ADDOBJECT message)
    {
        // This message is received on the WizardZone to:
        // a. Give it a unique ID.
        // b. Broadcast its creation to every player in the zone.
        var id = GenerateReservedMobileId();
        message.CoreObject.m_nMobileID = id;
        BroadcastObjectCreation(message.CoreObject);

        // Inform the object supervisor to create an actor representation and add it to our zone objects list.
        var rsp = _objectSupervisorRef
            .Ask<ZONE_102_PROTOCOL.MSG_ADDOBJECTRSP>(message)
            .Result;
        rsp.MobileId = id;

        Sender.Tell(rsp);
    }
    
    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDCREATURE))]
    private void ReceiveAddCreature(ZONE_102_PROTOCOL.MSG_ADDCREATURE message)
    {
        // This message is received on the WizardZone to:
        // a. Give it a unique ID.
        // b. Broadcast its creation to every player in the zone.
        var id = GenerateReservedMobileId();
        message.CoreObject.m_nMobileID = id;
        BroadcastObjectCreation(message.CoreObject);
        
        // Inform the object supervisor to create an actor representation and the it to our zone objects list.
        _objectSupervisorRef.Tell(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDVOLUME))]
    private void ReceiveAddVolume(ZONE_102_PROTOCOL.MSG_ADDVOLUME message)
    {
        var id = GenerateReservedMobileId();
        message.CoreObject.m_nMobileID = id;
        message.CoreObject.m_debugName = message.Volume.m_volumeName;
        // Volumes are server side only, so no need for broadcast.
        
        // Inform the object supervisor to create an actor representation and add it to the zone objects list.
        _objectSupervisorRef.Tell(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDTRIGGER))]
    private void ReceiveAddTrigger(ZONE_102_PROTOCOL.MSG_ADDTRIGGER message)
    {
        _triggers.Add(message.Trigger);
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
            Log.Logger.Debug("{Volume} {ZoneName} tried to activate trigger " +
                               "{TriggerName}, but no trigger was found in the zone",
                nameof(WizardZoneVolume), ZoneName, message.TriggerName);
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

        // Debug log all the triggers that were activated.
        foreach (var trigger in triggers)
        {
            Log.Logger.Debug(
                "{WizardZoneVolume} {ZoneName} activated trigger {TriggerName}",
                nameof(WizardZoneVolume), ZoneName, trigger.m_triggerName);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_TRIGGERQUERY))]
    private void ReceiveZoneInteraction(ZONE_102_PROTOCOL.MSG_TRIGGERQUERY message)
    {
        // Broadcast to each zone object that a player is fishing for proximity reactions.
        var msgBroadcast = new ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST
        {
            Source = Sender,
            Messages = new IServerMessage[] { message }
        };
        _objectSupervisorRef.Tell(msgBroadcast);
    }

    #endregion
}