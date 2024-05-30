/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using SharpDX;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Game.Combat;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.MessageLayer;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.Shared.Character;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone;

/// <summary>
/// The WizardZone is the main actor for a zone. It is responsible for managing all the objects within the zone.
/// </summary>
public class WizardZone : ReceiveProtocolDispatcher, IWithTimers {
    private const ushort RESERVED_MOBILE_ID_MAX = 1000;
    private const uint HEAL_INTERVAL_PER_MINUTE_IN_SECONDS = 5;

    public string ZoneName { get; }
    public string ZoneDisplayName { get; set; }
    public WizZoneData ZoneData { get; set; }
    public ITimerScheduler Timers { get; set; }

    private readonly CoreObjectSerializer _coreObjectSerializer = new CoreObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask(SerializerOptions.PropertyFlags.Public
                | SerializerOptions.PropertyFlags.Transmit
                | SerializerOptions.PropertyFlags.AuthorityTransmit);
    private readonly uint _dynamicZoneId;
    private readonly IActorRef _objectSupervisorRef;
    private readonly IActorRef _sigilSupervisorRef;
    private readonly IActorRef _duelSupervisorRef;
    private readonly Dictionary<IActorRef, Wizard> _zonePlayers;
    private ushort _zoneObjectMobileIdCounter;

    // ctor
    public WizardZone(string zoneName) {
        ZoneName = zoneName;
        _dynamicZoneId = GenerateDynamicZoneId();
        _zonePlayers = new Dictionary<IActorRef, Wizard>();

        // Create supervisor children. This just helps offload most of the work.
        _objectSupervisorRef = CreateObjectSupervisor();
        _sigilSupervisorRef = CreateSigilSupervisor();
        _duelSupervisorRef = CreateDuelSupervisor();

        // Load and initialize this zone.
        WizardZoneLoader.LoadZoneData(this, Self);
        Logger.Debug("Zone {ZoneName} created.", Logger.Args(ZoneName));

        if (ZoneData.m_healingPerMinute > 0) {
            // Fire a message to self to start the heal tick.
            var delay = TimeSpan.FromSeconds(HEAL_INTERVAL_PER_MINUTE_IN_SECONDS);
            Timers.StartPeriodicTimer("healtick", new ZONE_102_PROTOCOL.MSG_HEALTICK(), delay);
        }
    }

    // Akka.NET ctor
    public static Props Props(string zoneName)
        => Akka.Actor.Props.Create(() => new WizardZone(zoneName));

    protected override void PreRestart(Exception reason, object message) {
        Logger.Error("Zone {ZoneName} restarts for: {Exception}", Logger.Args(ZoneName, reason));
        base.PreRestart(reason, message);
    }

    private void Broadcast(IMessage message) {
        foreach (var player in _zonePlayers.Keys) {
            player.Tell(message);
        }
    }

    private void BroadcastSelfless(IActorRef sender, IMessage message) {
        foreach (var player in _zonePlayers.Keys
                     .Where(player => !player.Equals(sender))) {
            player.Tell(message);
        }
    }

    private IActorRef CreateObjectSupervisor() {
        var props = WizardZoneObjectSupervisor.Props(Self);
        return Context.ActorOf(props);
    }

    private IActorRef CreateSigilSupervisor() {
        var props = WizardZoneSigilSupervisor.Props(Self);
        return Context.ActorOf(props);
    }

    private IActorRef CreateDuelSupervisor() {
        var props = CombatDuelActorSupervisor.Props(Self);
        return Context.ActorOf(props);
    }

    private void BroadcastObjectCreation(CoreObject obj)
        => Broadcast(new GAME_5_PROTOCOL.MSG_NEWOBJECT { Data = _coreObjectSerializer.Serialize(obj) });

    private void InformZoneObjectsOfJoin(ZONE_102_PROTOCOL.MSG_ADDPLAYER message) {
        var msg = new ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST {
            Source = message.Player,
            Messages = new IServerMessage[] { message }
        };

        // Forward the new player message to every zone object so that they may personally deal with this situation.
        _objectSupervisorRef.Tell(msg);
        _sigilSupervisorRef.Tell(msg);

        // Broadcast this new player to each existing player in the zone.
        BroadcastObjectCreation(message.PlayerObject);
    }

    private void InformZoneObjectsOfDeparture(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message) =>
        // Forward the departure message to every zone object so that they may personally deal with this situation.
        _objectSupervisorRef.Tell(new ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST {
            Source = message.Player,
            Messages = new IServerMessage[] { message }
        });

    private void SpawnPlayersForNewClient(IActorRef client) {
        // Now spawn each existing player.
        foreach (var obj in _zonePlayers.Values) {
            var playerObj = WizardObjectLoader.GetPlayerGameObject(obj);
            var msg = new GAME_5_PROTOCOL.MSG_NEWOBJECT { Data = _coreObjectSerializer.Serialize(playerObj) };
            client.Tell(msg);
        }
    }

    private static uint GenerateDynamicZoneId() {
        var random = new Random();
        return (uint) random.Next(0, int.MaxValue);
    }

    private ushort GenerateMobileId() {
        // Avoid collisions as much as possible.
        ushort test;
        var r = new Random();
        while (true) {
            test = (ushort) r.Next(RESERVED_MOBILE_ID_MAX, ushort.MaxValue);
            if (_zonePlayers.Values.Any(x => x.GameObject.m_nMobileID == test)) {
                continue;
            }

            break;
        }

        return test;
    }

    private ushort GenerateReservedMobileId() {
        if (_zoneObjectMobileIdCounter + 1 >= RESERVED_MOBILE_ID_MAX) {
            throw new Exception($"Zone \"{ZoneName}\" reached the maximum reserved mobile ID count!");
        }

        return ++_zoneObjectMobileIdCounter;
    }

    #region Handlers

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONETRANSFER))]
    private void ReceiveZoneTransfer(ZONE_102_PROTOCOL.MSG_ZONETRANSFER message) {
        var rsp = new ZONE_102_PROTOCOL.MSG_ZONETRANSFERRSP {
            ZoneActorRef = Self,
            DynamicZoneId = _dynamicZoneId,
            ErrorCode = 0,
            MobileId = GenerateMobileId(),
            ZoneDisplayName = ZoneDisplayName
        };

        // The location given is a ByteString. If it's coordinates, then we need to parse it.
        // Otherwise, it's a location within this zone. Search through locations to see if we can find it.
        var actualLocation = Vector3.Zero;
        var actualOrientation = 0.0f;

        var location = Util.GetVectorFromCompactString(message.DestinationLocation);
        if (location.X != 0 || location.Y != 0 || location.Z != 0) {
            actualLocation = new Vector3(location.X, location.Y, location.Z);
            actualOrientation = location.W;
        }
        else {
            // We need to find the location in this zone.
            var searchedLoc = ZoneData.m_locationList.FirstOrDefault(x => x.m_locName == message.DestinationLocation);
            if (searchedLoc is null) {
                // We weren't able to find the location. This is an error.
                Logger.Error("Zone {ZoneName} tried to transfer to an unknown location: {Location}",
                    Logger.Args(ZoneName, message.DestinationLocation));

                rsp.ErrorCode = 1;
            }
            else {
                // We found the location. Set the actual location and orientation.
                var locPos = searchedLoc.m_location;
                var locOri = searchedLoc.m_direction;
                actualLocation = locPos;
                actualOrientation = locOri;
            }
        }

        rsp.Location = actualLocation;
        rsp.Orientation = actualOrientation;
        Sender.Tell(rsp);
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
        _zonePlayers.Add(message.Player, message.Wizard);

        // Inform the player that they've been successfully added to the zone. We want to reply to the callee
        // and any services that may be waiting for this reply.
        var response = new ZONE_102_PROTOCOL.MSG_ADDPLAYERRSP { WizardGameObject = message.PlayerObject };
        Sender.Tell(response);
        message.Player.Tell(response);

        Logger.Debug("{Name} added to zone {ZoneName}.", Logger.Args(message.ActualWizardName, ZoneName));
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

        var rsp = new ZONE_102_PROTOCOL.MSG_REMOVEPLAYERRSP();
        Sender.Tell(rsp);

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
    private void ReceiveAddPath(ZONE_102_PROTOCOL.MSG_ADDPATH message)
        => _objectSupervisorRef.Forward(message);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDOBJECT))]
    private void ReceiveAddObject(ZONE_102_PROTOCOL.MSG_ADDOBJECT message) {
        // This message is received on the WizardZone to:
        // a. Give it a unique ID.
        // b. Broadcast its creation to every player in the zone.
        var id = GenerateReservedMobileId();
        message.CoreObject.m_nMobileID = id;

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

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_FISHINTERACTION))]
    private void ReceiveZoneInteraction(ZONE_102_PROTOCOL.MSG_FISHINTERACTION message) {
        // Broadcast to each zone object that a player is fishing for proximity reactions.
        var msgBroadcast = new ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST {
            Source = Sender,
            Messages = new IServerMessage[] { message }
        };

        // Creatures can only collide with sigils.
        if (message.IsCreature) {
            _sigilSupervisorRef.Tell(msgBroadcast);
        }
        else {
            _objectSupervisorRef.Tell(msgBroadcast);
            _sigilSupervisorRef.Tell(msgBroadcast);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL))]
    private void ReceiveRequestCombatSigil(ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL message)
        => _sigilSupervisorRef.Forward(message);

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_STARTDUEL))]
    private void ReceiveStartDuel(COMBAT_106_PROTOCOL.MSG_STARTDUEL message)
        => _duelSupervisorRef.Forward(message);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECT))]
    private void ReceiveObjectQuery(ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECT message)
        => _objectSupervisorRef.Forward(message);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_HEALTICK))]
    private void ReceiveHealTick(ZONE_102_PROTOCOL.MSG_HEALTICK message) {
        if (_zonePlayers.Count == 0) {
            return;
        }

        foreach (var (a, w) in _zonePlayers) {
            var currentWizardHealth = w.GameStats.m_currentHitpoints;
            var maxWizardHealth = w.GameStats.m_baseHitpoints;

            // If this wizard is max health, skip.
            if (currentWizardHealth >= maxWizardHealth) {
                continue;
            }

            // Update our Wizard server side.
            var healPerMinute = ZoneData.m_healingPerMinute;
            float healPercentage = (float) healPerMinute / (60 / HEAL_INTERVAL_PER_MINUTE_IN_SECONDS);
            float healAmount = healPercentage / 100 * maxWizardHealth;
            var newHealth = Math.Min(currentWizardHealth + (int) healAmount, maxWizardHealth);

            w.UpdateHealth(newHealth);

            // Inform the client about the new health changes.
            // The client has a max health increase effect applied, so sending it here would double the health client side.
            var magicSchool = w.MagicSchoolBehavior.MagicSchool;
            var level = w.MagicSchoolBehavior.Level;
            var baseStats = MagicLevelsConfig.GetPlayerLevelInfo(magicSchool, level);
            var normMaxHealth = baseStats.m_hitpoints;

            var networkMessage = new WIZARD_12_PROTOCOL.MSG_UPDATEHEALTH() {
                CharacterID = w.GameObject.m_globalID,
                NewHealth = newHealth,
                NewHealthMax = normMaxHealth,
                DisplayDiff = 1,
            };
            a.Tell(networkMessage);
        }
    }

    #endregion
}
