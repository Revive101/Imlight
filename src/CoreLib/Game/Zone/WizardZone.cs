/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using Akka.Actor;
using SharpDX;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Game.Combat;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;
using static Imlight.Common.Caches.TypeCache;
using Imlight.Common.IO;

namespace Imlight.CoreLib.Game.Zone;

/// <summary>
/// The WizardZone is the main actor for a zone. It is responsible for managing all the objects within the zone.
/// </summary>
public class WizardZoneLegacy : ReceiveProtocolDispatcher, IWithTimers {
    /// <summary>
    /// The maximum number of reserved mobile IDs. These are used for objects that are not players.
    /// </summary>
    private const ushort RESERVED_MOBILE_ID_MAX = 1000;

    /// <summary>
    /// The interval at which healing occurs in the zone.
    /// </summary>
    private const int HEAL_INTERVAL_PER_MINUTE_IN_SECONDS = 5;

    /// <summary>
    /// A vector that represents a failed location.
    /// </summary>
    private static readonly Vector4 s_locationFailedGiveaway = new(float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue);

    public string ZoneName { get; }
    public string ZoneDisplayName { get {
        if (ZoneData is null) {
            return ZoneName;
        }

        return ZoneData.m_zoneDisplayName;
    }}
    public WizZoneData ZoneData { get; set; }
    public ITimerScheduler Timers { get; set; }

    private readonly uint _dynamicZoneId;
    private readonly IActorRef _objectSupervisorRef;  // Supervisor for all objects in this zone.
    private readonly IActorRef _sigilSupervisorRef;   // Supervisor for all sigils in this zone.
    private readonly IActorRef _duelSupervisorRef;    // Supervisor for all duels in this zone.
    private readonly IActorRef _playerSupervisorRef;  // Supervisor for all players in this zone.
    private readonly IActorRef _triggerSupervisorRef; // Supervisor for all triggers in this zone.
    private readonly string _mobileIdLock = string.Empty;
    private ushort _reservedMobileIdCounter;
    private ushort _nonreservedMobileIdCounter = RESERVED_MOBILE_ID_MAX + 1;

    // ctor
    public WizardZoneLegacy(string zoneName) {
        ZoneName = zoneName;
        _dynamicZoneId = GenerateDynamicZoneId();

        // Create supervisor children. This just helps offload most of the work.
        _objectSupervisorRef = CreateObjectSupervisor();
        _sigilSupervisorRef = CreateSigilSupervisor();
        _duelSupervisorRef = CreateDuelSupervisor();
        _playerSupervisorRef = CreatePlayerSupervisor();
        _triggerSupervisorRef = CreateTriggerSupervisor();

        // Load and initialize this zone.
        WizardZoneLoader.LoadZoneData(this, Self);
        Logger.Debug("Zone {ZoneName} created.", Logger.Args(ZoneName));

        if (ZoneData.m_healingPerMinute > 0) {
            // Calculate how much healing happens on interval.
            var healingPerMin = ZoneData.m_healingPerMinute;
            var healingPerSec = healingPerMin / 60.0f;
            var healingPerTick = healingPerSec * HEAL_INTERVAL_PER_MINUTE_IN_SECONDS;

            // Fire a message to self to start the heal tick.
            var delay = TimeSpan.FromSeconds(HEAL_INTERVAL_PER_MINUTE_IN_SECONDS);
            var msg = new ZONE_102_PROTOCOL.MSG_HEALTICK {
                MaxHealthPercent = healingPerTick
            };
            Timers.StartPeriodicTimer("healtick", msg, delay);
        }
    }

    // Akka.NET ctor
    public static Props Props(string zoneName)
        => Akka.Actor.Props.Create(() => new WizardZoneLegacy(zoneName));

    protected override void PreRestart(Exception reason, object message) {
        Logger.Error("Zone {ZoneName} restarts for: {Exception}", Logger.Args(ZoneName, reason));
        base.PreRestart(reason, message);
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

    private IActorRef CreatePlayerSupervisor() {
        var props = WizardZonePlayerSupervisor.Props(Self);
        return Context.ActorOf(props);
    }

    private IActorRef CreateTriggerSupervisor() {
        var props = WizardZoneTriggerSupervisor.Props();
        return Context.ActorOf(props);
    }

    protected void InformZoneEntitesOfPlayerEnter(ZONE_102_PROTOCOL.MSG_ADDPLAYER message) {
        var msg = new ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST {
            Source = message.Player,
            Messages = new IServerMessage[] { message }
        };

        // Forward the new player message to every zone object so that they may personally deal with this situation.
        _objectSupervisorRef.Tell(msg);
        _sigilSupervisorRef.Tell(msg);
        _duelSupervisorRef.Tell(msg);

        _playerSupervisorRef.Forward(message);
    }

    protected void InformZoneEntitiesOfPlayerExit(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message) {
        var msg = new ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST {
            Source = message.Player,
            Messages = new IServerMessage[] { message }
        };

        // Forward the remove player message to every zone object so that they may personally deal with this situation.
        // These supervisors supervise non-player entities, so we don't need to inform them.
        if (!message.IsPlayerStillConnected) {
            _objectSupervisorRef.Tell(msg);
            _sigilSupervisorRef.Tell(msg);
            _duelSupervisorRef.Tell(msg);
        }

        // We do, however, want to inform players that a player has left.
        _playerSupervisorRef.Forward(message);
    }

    protected static uint GenerateDynamicZoneId() {
        // This could be done on the server rather than the zone, but the chances of collision
        // are so low that it's not worth the extra work.
        var random = new Random();
        return (uint) random.Next(0, int.MaxValue);
    }

    protected ushort IncremementObjectIdentifiers() {
        lock (_mobileIdLock) {
            if (_nonreservedMobileIdCounter + 1 >= ushort.MaxValue) {
                throw new Exception($"Zone \"{ZoneName}\" reached the maximum mobile ID count!");
            }

            return ++_nonreservedMobileIdCounter;
        }
    }

    protected ushort DecrementObjectIdentifiers() {
        lock (_mobileIdLock) {
            if (_nonreservedMobileIdCounter - 1 <= RESERVED_MOBILE_ID_MAX) {
                _nonreservedMobileIdCounter = RESERVED_MOBILE_ID_MAX + 1;
                return 0;
            }

            return --_nonreservedMobileIdCounter;
        }
    }

    protected ushort GenerateReservedMobileId() {
        if (_reservedMobileIdCounter + 1 >= RESERVED_MOBILE_ID_MAX) {
            throw new Exception($"Zone \"{ZoneName}\" reached the maximum reserved mobile ID count!");
        }

        return ++_reservedMobileIdCounter;
    }

    protected void CloseZone() {
        // Inform all other zone entities that the zone is closing.
        var msg = new ZONE_102_PROTOCOL.MSG_ZONECLOSED();
        _playerSupervisorRef.Tell(msg);
        _objectSupervisorRef.Tell(msg);
        _sigilSupervisorRef.Tell(msg);
        _duelSupervisorRef.Tell(msg);

        // Stop the zone.
        Context.Stop(Self);
    }

    #region Handlers

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONETRANSFER))]
    protected virtual void ReceiveZoneTransfer(ZONE_102_PROTOCOL.MSG_ZONETRANSFER message) {
        // Received when a player wants to transfer into this zone.
        // All we want to do is return details about the zone and the actual coordinates of where they want to be located.
        // If the actor accepts, they'll send a ZONE_102_PROTOCOL.MSG_ADDPLAYER message.
        var rsp = new ZONE_102_PROTOCOL.MSG_ZONETRANSFERRSP {
            ZoneActorRef = Self,
            DynamicZoneId = _dynamicZoneId,
            ErrorCode = 0,
            MobileId = IncremementObjectIdentifiers(),
            ZoneDisplayName = ZoneDisplayName
        };

        var actualLocation = GetLocationFromString(message.DestinationLocation);
        if (actualLocation == s_locationFailedGiveaway) {
            // We weren't able to find the location. This is an error.
            Logger.Error("Zone {ZoneName} tried to transfer to an unknown location: {Location}",
                Logger.Args(ZoneName, message.DestinationLocation));

            rsp.ErrorCode = 1;
            Sender.Tell(rsp);
            return;
        }

        rsp.Location = (Vector3) actualLocation;
        rsp.Orientation = actualLocation.W;
        Sender.Tell(rsp);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
    protected virtual void ReceiveAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message) {
        // Inform all other zone entitites of the new player.
        InformZoneEntitesOfPlayerEnter(message);

        var rsp = new ZONE_102_PROTOCOL.MSG_ADDPLAYERRSP {
            WizardGameObject = message.PlayerObject,
        };
        message.Player.Tell(rsp);

        Logger.Debug("{Name} added to zone {ZoneName}.", Logger.Args(message.ActualWizardName, ZoneDisplayName));
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER))]
    protected virtual void ReceiveRemovePlayer(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message) {
        // Inform all other zone entities of the player leaving.
        InformZoneEntitiesOfPlayerExit(message);
        DecrementObjectIdentifiers();

        var rsp = new ZONE_102_PROTOCOL.MSG_REMOVEPLAYERRSP();
        Sender.Tell(rsp);

        Logger.Debug("Player {Name} removed from zone {ZoneName}.",
            Logger.Args(message.Player.Path.Name, ZoneDisplayName));
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PLAYERCOUNTUPDATE))]
    protected virtual void ReceivePlayerCountUpdate(ZONE_102_PROTOCOL.MSG_PLAYERCOUNTUPDATE message) {
        // todo: start the countdown to close this zone if it's empty.
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST))]
    protected virtual void ReceiveZoneBroadcast(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST message)
        => _playerSupervisorRef.Forward(message);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPATH))]
    protected virtual void ReceiveAddPath(ZONE_102_PROTOCOL.MSG_ADDPATH message)
        => _objectSupervisorRef.Forward(message);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDOBJECT))]
    protected virtual void ReceiveAddObject(ZONE_102_PROTOCOL.MSG_ADDOBJECT message) {
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
    protected virtual void ReceiveAddCombatSigil(ZONE_102_PROTOCOL.MSG_ADDCOMBATSIGIL message) {
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
    protected virtual void ReceiveAddCreature(ZONE_102_PROTOCOL.MSG_ADDCREATURE message) {
        // This message is received on the WizardZone to:
        // a. Give it a unique ID.
        // b. Broadcast its creation to every player in the zone.
        var id = GenerateReservedMobileId();
        message.CoreObject.m_nMobileID = id;

        // Inform the object supervisor to create an actor representation and the it to our zone objects list.
        _objectSupervisorRef.Tell(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDVOLUME))]
    protected virtual void ReceiveAddVolume(ZONE_102_PROTOCOL.MSG_ADDVOLUME message) {
        var id = GenerateReservedMobileId();
        message.CoreObject.m_nMobileID = id;
        message.CoreObject.m_debugName = message.Volume.m_volumeName;
        // Volumes are server side only, so no need for broadcast.

        // Inform the object supervisor to create an actor representation and add it to the zone objects list.
        _objectSupervisorRef.Tell(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDTRIGGER))]
    protected virtual void ReceiveAddTrigger(ZONE_102_PROTOCOL.MSG_ADDTRIGGER message)
        => _triggerSupervisorRef.Forward(message);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_POSTEVENT))]
    protected virtual void ReceivePostEvent(ZONE_102_PROTOCOL.MSG_POSTEVENT message)
        => _triggerSupervisorRef.Forward(message);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PLAYERMOVE))]
    protected virtual void ReceiveZoneInteraction(ZONE_102_PROTOCOL.MSG_PLAYERMOVE message) {
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
    protected virtual void ReceiveRequestCombatSigil(ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL message)
        => _sigilSupervisorRef.Forward(message);

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_STARTDUEL))]
    protected virtual void ReceiveStartDuel(COMBAT_106_PROTOCOL.MSG_STARTDUEL message)
        => _duelSupervisorRef.Forward(message);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECT))]
    protected virtual void ReceiveObjectQuery(ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECT message)
        => _objectSupervisorRef.Forward(message);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_HEALTICK))]
    protected virtual void ReceiveHealTick(ZONE_102_PROTOCOL.MSG_HEALTICK message) {
        _playerSupervisorRef.Forward(message);
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_ADDDYNAMOD))]
    protected virtual void ReceiveAddDynaMod(CHARACTER_103_PROTOCOL.MSG_ADDDYNAMOD message) {
        // Inform all zone objects of this state change.
        _objectSupervisorRef.Tell(message);
        _sigilSupervisorRef.Tell(message);
    }

    #endregion

    protected Vector4 GetLocationFromString(ByteString location) {
        var actualLocation = Vector4.Zero;

        // Try to parse the location as a vector: does it have a non-zero value?
        // Otherwise, we need to find the location in this zone.
        var parsedLoc = Util.GetVectorFromCompactString(location);
        if (parsedLoc.X != 0 || parsedLoc.Y != 0 || parsedLoc.Z != 0) {
            actualLocation = parsedLoc;
        }
        else {
            var searchedLoc = ZoneData.m_locationList.FirstOrDefault(x => x.m_locName == location);
            if (searchedLoc is null) {
                return s_locationFailedGiveaway;
            }
            else {
                // We found the location. Set the actual location and orientation.
                actualLocation = (Vector4) searchedLoc.m_location;
                actualLocation.W = searchedLoc.m_direction;
            }
        }

        return actualLocation;
    }
}
