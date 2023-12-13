/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Game.Combat;
using static Imlight.Common.Caches.ServerTypeCache;
using static Imlight.Common.Caches.TypeCache;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.MessageLayer;
using Imlight.Common.ObjectProperty;

namespace Imlight.CoreLib.Game.Zone;

public class WizardZone : ReceiveProtocolDispatcher {
    private const ushort ReservedMobileIdMax = 1000;

    public string ZoneName { get; }
    public string ZoneDisplayName { get; set; }

    private readonly uint _dynamicZoneId;
    private readonly IActorRef _objectSupervisorRef;
    private readonly IActorRef _sigilSupervisorRef;
    private readonly IActorRef _duelSupervisorRef;
    private readonly List<Trigger> _triggers;
    private readonly Dictionary<IActorRef, CoreObject> _zonePlayers;
    private ushort _zoneObjectMobileIdCounter;

    // ctor
    public WizardZone(string zoneName) {
        ZoneName = zoneName;
        _dynamicZoneId = GenerateDynamicZoneId();
        _zonePlayers = new Dictionary<IActorRef, CoreObject>();
        _triggers = new List<Trigger>();

        // Create supervisor children. This just helps offload most of the work.
        _objectSupervisorRef = CreateObjectSupervisor();
        _sigilSupervisorRef = CreateSigilSupervisor();
        _duelSupervisorRef = CreateDuelSupervisor();

        // Load and initialize this zone.
        WizardZoneLoader.LoadZoneData(this, Self);
        Logger.Debug("Zone {ZoneName} created.", Logger.Args(ZoneName));
    }

    // Akka.NET ctor
    public static Props Props(string zoneName) {
        return Akka.Actor.Props.Create(() => new WizardZone(zoneName));
    }

    protected override void PreRestart(Exception reason, object message) {
        Logger.Error("Zone {ZoneName} restarts for: {Exception}", Logger.Args(ZoneName, reason));
        base.PreRestart(reason, message);
    }

    /// <summary>
    /// Broadcast a message to all the players in the zone.
    /// </summary>
    /// <param name="message">The <see cref="INetworkMessage" /> that will be broadcast.</param>
    private void Broadcast(IMessage message) {
        foreach (var player in _zonePlayers.Keys) {
            player.Tell(message);
        }
    }

    /// <summary>
    /// Broadcast a message to all the players in this zone, except to the player that broadcast it.
    /// </summary>
    /// <param name="sender">The <see cref="IActorRef" /> that this broadcast will ignore.</param>
    /// <param name="message">The <see cref="INetworkMessage" /> that will be broadcast.</param>
    private void BroadcastSelfless(IActorRef sender, IMessage message) {
        foreach (var player in _zonePlayers.Keys
                     .Where(player => !player.Equals(sender))) {
            player.Tell(message);
        }
    }

    /// <summary>
    /// Creates a <see cref="WizardZoneObjectSupervisor"/> as a child of this WizardZone.
    /// </summary>
    /// <returns>The actor reference pointing to the newly created actor.</returns>
    private IActorRef CreateObjectSupervisor() {
        var props = WizardZoneObjectSupervisor.Props(Self);
        return Context.ActorOf(props);
    }

    /// <summary>
    /// Creates a supervisor actor for the sigil in the wizard zone.
    /// </summary>
    /// <returns>The actor reference for the supervisor.</returns>
    private IActorRef CreateSigilSupervisor() {
        var props = WizardZoneSigilSupervisor.Props(Self);
        return Context.ActorOf(props);
    }

    /// <summary>
    /// Creates a supervisor actor for duels.
    /// </summary>
    /// <returns>The actor reference for the supervisor.</returns>
    private IActorRef CreateDuelSupervisor() {
        var props = DuelActorSupervisor.Props(Self);
        return Context.ActorOf(props);
    }

    /// <summary>
    /// Broadcasts the creation of a new <see cref="CoreObject"/> to each player in the zone.
    /// </summary>
    /// <param name="obj"></param>
    private void BroadcastObjectCreation(CoreObject obj) {
        var serializer = new CoreObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask(SerializerOptions.PropertyFlags.Public
                | SerializerOptions.PropertyFlags.Transmit
                | SerializerOptions.PropertyFlags.AuthorityTransmit);
        Broadcast(new GAME_5_PROTOCOL.MSG_NEWOBJECT { Data = serializer.Serialize(obj) });
    }

    /// <summary>
    /// Broadcasts to each zone object and player of a new arrival in the zone.
    /// </summary>
    /// <param name="message"></param>
    private void InformZoneObjectsOfJoin(ZONE_102_PROTOCOL.MSG_ADDPLAYER message) {
        // Forward the new player message to every zone object so that they may personally deal with this situation.
        _objectSupervisorRef.Tell(new ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST {
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
    private void InformZoneObjectsOfDeparture(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message) {
        // Forward the departure message to every zone object so that they may personally deal with this situation.
        _objectSupervisorRef.Tell(new ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST {
            Source = message.Player,
            Messages = new IServerMessage[] { message }
        });
    }

    /// <summary>
    /// Spawns each existing player in the zone for a client.
    /// </summary>
    /// <param name="client">The client in question.</param>
    private void SpawnPlayersForNewClient(IActorRef client) {
        // Now spawn each existing player.
        var serializer = new CoreObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask(SerializerOptions.PropertyFlags.Public
                | SerializerOptions.PropertyFlags.Transmit
                | SerializerOptions.PropertyFlags.AuthorityTransmit);
        foreach (var obj in _zonePlayers.Values) {
            var msg = new GAME_5_PROTOCOL.MSG_NEWOBJECT { Data = serializer.Serialize(obj) };
            client.Tell(msg);
        }
    }

    /// <summary>
    /// Generates a new <see cref="uint"/> value as a dynamic ID for this WizardZone.
    /// </summary>
    /// <returns>The ID generated.</returns>
    private static uint GenerateDynamicZoneId() {
        var random = new Random();
        return (uint) random.Next(0, int.MaxValue);
    }

    /// <summary>
    /// Generates a random, unused <see cref="ushort"/> object ID.
    /// </summary>
    /// <returns>The ID generated.</returns>
    private ushort GenerateMobileId() {
        // Avoid collisions as much as possible.
        ushort test;
        var r = new Random();
        while (true) {
            test = (ushort) r.Next(ReservedMobileIdMax, ushort.MaxValue);
            if (_zonePlayers.Values.Any(x => x.m_nMobileID == test)) {
                continue;
            }

            break;
        }

        return test;
    }

    /// <summary>
    /// Generate an unused mobile ID. Reserved for zone objects.
    /// </summary>
    /// <returns></returns>
    private ushort GenerateReservedMobileId() {
        if (_zoneObjectMobileIdCounter + 1 >= ReservedMobileIdMax) {
            throw new Exception($"Zone \"{ZoneName}\" reached the maximum reserved mobile ID count!");
        }

        return ++_zoneObjectMobileIdCounter;
    }

    private void SendZoneTransfer(IActorRef suspect, ServerTypeCache.ResTeleport resTeleport) {
        var msg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER {
            DestinationZone = resTeleport.m_destinationZone,
            DestinationLocation = resTeleport.m_destinationLoc,
            SendToClient = true
        };
        suspect.Tell(msg);
    }

    private void SendDisplayText(IActorRef suspect, ResDisplayText resDisplayText) {
        var msg = new GAME_5_PROTOCOL.MSG_CLIENTNOTIFYTEXT {
            NotifyText = resDisplayText.m_text,
            Type = resDisplayText.m_type,
        };
        suspect.Tell(msg);
    }

    private void SendPlaySound(IActorRef suspect, ResPlaySound resPlaySound) {
        // todo: implement
        var msg = new GAME_5_PROTOCOL.MSG_PLAYSOUND { SoundFilename = resPlaySound.m_soundName };
        suspect.Tell(msg);
    }

    #region Handlers

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONETRANSFER))]
    private void ReceiveZoneTransfer(ZONE_102_PROTOCOL.MSG_ZONETRANSFER message) {
        // This is step 1 of the zone transfer process.
        // There's nothing we need to do here except for telling the sender the zone details and allocating a dynamic
        // zone ID for them.
        Sender.Tell(new ZONE_102_PROTOCOL.MSG_ZONETRANSFERRSP {
            ZoneActorRef = Self,
            DynamicZoneId = _dynamicZoneId,
            ErrorCode = 0,
            MobileId = GenerateMobileId(),
            ZoneDisplayName = ZoneDisplayName
        });
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
    private void ReceiveAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message) {
        if (_zonePlayers.ContainsKey(message.Player)) {
            throw new Exception("Player actor already exists in this zone!");
        }

        // If the message did not provide a mobile ID, then we need to generate one.
        if (message.PlayerObject.m_nMobileID == 0) {
            message.PlayerObject.m_nMobileID = GenerateMobileId();
        }

        InformZoneObjectsOfJoin(message);
        SpawnPlayersForNewClient(message.Player);

        // Now we add the player, so they don't end up creating themselves when we spawn each zone object.
        _zonePlayers.Add(message.Player, message.PlayerObject);

        // Inform the player that they've been successfully added to the zone. We want to reply to the callee
        // and any services that may be waiting for this reply.
        var response = new ZONE_102_PROTOCOL.MSG_ADDPLAYERRSP { WizardGameObject = message.PlayerObject };
        Sender.Tell(response);
        message.Player.Tell(response);

        Logger.Debug("{Name} added to zone {ZoneName}.",
            Logger.Args(message.ActualWizardName, ZoneName));
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER))]
    private void ReceiveRemovePlayer(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message) {
        // Make sure this player is in this zone. This is just a sanity check, and with different race conditions
        // it's possible that this player is no longer in this zone. If that's the case, we just ignore this message.
        if (!_zonePlayers.TryGetValue(message.Player, out var obj)) {
            return;
        }

        // Don't send a torrent of messages to a disconnected socket.
        if (message.IsPlayerStillConnected) {
            InformZoneObjectsOfDeparture(message);
        }

        // Inform every other player that this object has been removed.
        Broadcast(new GAME_5_PROTOCOL.MSG_REMOVEOBJECT { GameObjectID = message.GlobalId });

        _zonePlayers.Remove(message.Player);

        // Wait a little while until sending the reply back, just in case the zone objects have not yet been cleaned.
        var s = Sender;
        Task.Run(async () => {
            await Task.Delay(TimeSpan.FromSeconds(1));
            var rsp = new ZONE_102_PROTOCOL.MSG_REMOVEPLAYERRSP();
            s.Tell(rsp);
        }).Wait();

        Logger.Debug("Player {Name} removed from zone {ZoneName}.",
            Logger.Args(message.Player.Path.Name, ZoneName));
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST))]
    private void ReceiveZoneBroadcast(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST message) {
        if (message.Selfless) {
            BroadcastSelfless(message.Sender, message.Message);
        }
        else {
            Broadcast(message.Message);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPATH))]
    private void ReceiveAddPath(ZONE_102_PROTOCOL.MSG_ADDPATH message) {
        _objectSupervisorRef.Forward(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDOBJECT))]
    private void ReceiveAddObject(ZONE_102_PROTOCOL.MSG_ADDOBJECT message) {
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

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDCOMBATSIGIL))]
    private void ReceiveAddCombatSigil(ZONE_102_PROTOCOL.MSG_ADDCOMBATSIGIL message) {
        // This message is first received on the WizardZone to give it a unqiue ID.
        // Then it is forwarded to the WizardZoneSigilSupervisor to create the actor.
        var id = GenerateReservedMobileId();
        message.CoreObject.m_nMobileID = id;

        // Inform the sigil supervisor to create an actor representation and add it to our zone objects list.
        var rsp = _sigilSupervisorRef
            .Ask<ZONE_102_PROTOCOL.MSG_ADDCOMBATSIGILRSP>(message)
            .Result;
        rsp.MobileId = id;

        Sender.Tell(rsp);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDCREATURE))]
    private void ReceiveAddCreature(ZONE_102_PROTOCOL.MSG_ADDCREATURE message) {
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
    private void ReceiveAddVolume(ZONE_102_PROTOCOL.MSG_ADDVOLUME message) {
        var id = GenerateReservedMobileId();
        message.CoreObject.m_nMobileID = id;
        message.CoreObject.m_debugName = message.Volume.m_volumeName;
        // Volumes are server side only, so no need for broadcast.

        // Inform the object supervisor to create an actor representation and add it to the zone objects list.
        _objectSupervisorRef.Tell(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDTRIGGER))]
    private void ReceiveAddTrigger(ZONE_102_PROTOCOL.MSG_ADDTRIGGER message) {
        _triggers.Add(message.Trigger);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_TRIGGER))]
    private void ReceiveActivateTrigger(ZONE_102_PROTOCOL.MSG_TRIGGER message) {
        var triggers = (
            from trigger in _triggers
            from vol in trigger.m_volumes
            where vol == message.TriggerName
            select trigger)
            .ToList();

        if (!triggers.Any()) {
            Logger.Debug("{Volume} {ZoneName} tried to activate trigger " +
                               "{TriggerName}, but no trigger was found in the zone",
                Logger.Args(nameof(WizardZoneVolume), ZoneName, message.TriggerName));
            return;
        }

        foreach (var result in triggers.SelectMany(trigger => trigger.m_results.m_results)) {
            switch (result) {
                case ServerTypeCache.ResTeleport resTeleport:
                    SendZoneTransfer(message.Suspect, resTeleport);
                    break;
                case ResDisplayText resDisplayText:
                    SendDisplayText(message.Suspect, resDisplayText);
                    break;
                case ResPlaySound resPlaySound:
                    SendPlaySound(message.Suspect, resPlaySound);
                    break;
            }
        }

        // Debug log all the triggers that were activated.
        foreach (var trigger in triggers) {
            Logger.Debug(
                "{WizardZoneVolume} {ZoneName} activated trigger {TriggerName}",
                Logger.Args(nameof(WizardZoneVolume), ZoneName, trigger.m_triggerName));
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_FISHINTERACTION))]
    private void ReceiveZoneInteraction(ZONE_102_PROTOCOL.MSG_FISHINTERACTION message) {
        // Broadcast to each zone object that a player is fishing for proximity reactions.
        var msgBroadcast = new ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST {
            Source = Sender,
            Messages = new IServerMessage[] { message }
        };
        _objectSupervisorRef.Tell(msgBroadcast);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL))]
    private void ReceiveRequestCombatSigil(ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL message) {
        _sigilSupervisorRef.Forward(message);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_STARTDUEL))]
    private void ReceiveStartDuel(COMBAT_106_PROTOCOL.MSG_STARTDUEL message) {
        _duelSupervisorRef.Forward(message);
    }

    #endregion
}
