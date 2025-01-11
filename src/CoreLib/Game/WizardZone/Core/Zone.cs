/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.Common.IO;
using Imlight.CoreLib.Game.WizardZone.Supervisors;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.WizardZone.Core;

/// <summary>
/// Represents a zone in the game world.
/// </summary>
public class Zone : ReceiveProtocolDispatcher, IWithTimers {

    public const ushort RESERVED_OBJECT_ID_MIN = 0;
    public const ushort RESERVED_OBJECT_ID_MAX = 500;
    public const ushort RESERVED_VOLUME_ID_MIN = 501;
    public const ushort RESERVED_VOLUME_ID_MAX = 600;

    private const ushort RESERVED_MOBILE_ID_MAX = 1000;
    private const ushort ZONE_LOAD_TIMEOUT_IN_SECONDS = 30;
    private const ushort ZONE_SUPERVISOR_LOAD_TIMEOUT_IN_SECONDS = 10;
    private static readonly Vector4 s_locationFailedGiveaway = new(float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue);

    /// <summary>
    /// The zone data as loaded from game client data.
    /// </summary>
    public WizZoneData ZoneData { get; private set; }

    /// <summary>
    /// The zone path, formatted as it would be in the access pass.
    /// </summary>
    public string ZonePath { get; init; }

    /// <summary>
    /// The pretty name of the zone, as displayed in the game client.
    /// If the zone data is not loaded, this will be the same as <see cref="ZonePath"/>.
    /// </summary>
    public string ZoneName {
        get {
            if (ZoneData is null) {
                return ZonePath;
            }

            return ZoneData.m_zoneDisplayName;
        }
    }

    public ITimerScheduler Timers { get; set; }

    private readonly uint _dynamicZoneId;
    private readonly string _mobileIdLock = string.Empty;
    private readonly List<IActorRef> _supervisors = [];
    private readonly IActorRef _loaderRef;
    private readonly Stopwatch _zoneLoadTimer;
    private readonly Dictionary<IActorRef, IServerMessage> _pendingPlayerEvents = [];
    private ushort _reservedMobileIdCounter;
    private ushort _nonreservedMobileIdCounter;
    private bool _isLoading;

    /// <summary>
    /// Creates a new zone from the path of the zone, formatted as it would be in the access pass.
    /// </summary>
    /// <param name="zonePath">The path of the zone, formatted as it would be in the access pass.</param>
    /// <param name="dynamicZoneId">The dynamic zone ID of the zone.</param>
    public Zone(string zonePath, uint dynamicZoneId) {
        this.ZonePath = zonePath;
        this._dynamicZoneId = dynamicZoneId;
        this._isLoading = true;
        this._zoneLoadTimer = new Stopwatch();

        _supervisors.Add(CreateSupervisor<ZoneObjectSupervisor>());
        _supervisors.Add(CreateSupervisor<ZoneVolumeSupervisor>());

        // Create the loader actor and prepare the loading of this zone.
        _loaderRef = Context.ActorOf(Akka.Actor.Props.Create(() => new ZoneLoader()));

        // Tell the loader to begin loading the zone and await the response.
        var msg = new ZONE_102_PROTOCOL.MSG_ZONELOADBEGIN { ZonePath = zonePath };
        _loaderRef.Tell(msg);

        // Send a message to self in case the zone fails to load within the timeout.
        var time = TimeSpan.FromSeconds(ZONE_LOAD_TIMEOUT_IN_SECONDS);
        Timers.StartSingleTimer("ZoneLoadTimeout", new ZONE_102_PROTOCOL.MSG_ZONELOADTIMER(), time);

        _zoneLoadTimer.Restart();
        _isLoading = true;

        Logger.Debug("Zone {ZoneName} begins load.", Logger.Args(ZoneName));
    }

    // Props
    public static Props Props(string zonePath, uint dynamicZoneId)
        => Akka.Actor.Props.Create(() => new Zone(zonePath, dynamicZoneId));

    protected override void PreRestart(Exception reason, object message) {
        Logger.Error("Zone {ZoneName} restarts for: {Exception}", Logger.Args(ZoneName, reason));
        base.PreRestart(reason, message);
    }

    /// <summary>
    /// Generates a new object identifier for a non-reserved object.
    /// </summary>
    /// <returns>The generated object identifier.</returns>
    /// <exception cref="Exception">Thrown if the zone has reached the maximum mobile ID count.</exception>
    protected ushort GenerateObjectIdentifier() {
        lock (_mobileIdLock) {
            if (_nonreservedMobileIdCounter + 1 >= ushort.MaxValue) {
                throw new Exception($"Zone \"{ZoneName}\" reached the maximum mobile ID count!");
            }

            return ++_nonreservedMobileIdCounter;
        }
    }

    /// <summary>
    /// Generates a new object identifier for a reserved object.
    /// </summary>
    /// <returns>The generated object identifier.</returns>
    /// <exception cref="Exception">Thrown if the zone has reached the maximum reserved mobile ID count.</exception>
    protected ushort GeneratedReservedObjectIdentifier() {
        lock (_mobileIdLock) {
            if (_reservedMobileIdCounter + 1 >= RESERVED_MOBILE_ID_MAX) {
                throw new Exception($"Zone \"{ZoneName}\" reached the maximum reserved mobile ID count!");
            }

            return ++_reservedMobileIdCounter;
        }
    }

    /// <summary>
    /// Releases an object identifier for a non-reserved object.
    /// </summary>
    /// <returns>The released object identifier.</returns>
    /// <exception cref="Exception">Thrown if the object identifier is already at the minimum value.</exception>
    protected ushort ReleaseObjectIdentifier() {
        lock (_mobileIdLock) {
            if (_nonreservedMobileIdCounter - 1 <= RESERVED_MOBILE_ID_MAX) {
                _nonreservedMobileIdCounter = RESERVED_MOBILE_ID_MAX + 1;
                return 0;
            }

            return --_nonreservedMobileIdCounter;
        }
    }

    /// <summary>
    /// Releases an object identifier for a reserved object.
    /// </summary>
    /// <returns>The released object identifier.</returns>
    /// <exception cref="Exception">Thrown if the object identifier is already at the minimum value.</exception>
    protected ushort ReleaseReservedObjectIdentifier() {
        lock (_mobileIdLock) {
            if (_reservedMobileIdCounter - 1 <= 0) {
                _reservedMobileIdCounter = 0;
                return 0;
            }

            return --_reservedMobileIdCounter;
        }
    }

    /// <summary>
    /// Closes the zone and stops all child actors.
    /// </summary>
    protected void CloseZone() {
        var msg = new ZONE_102_PROTOCOL.MSG_ZONECLOSED();
        foreach (var supervisor in _supervisors) {
            supervisor.Tell(msg);
        }

        // Inform any pending player events that the zone is closing.
        foreach (var (player, _) in _pendingPlayerEvents) {
            var rsp = new ZONE_102_PROTOCOL.MSG_ZONETRANSFERRSP {
                ZoneActorRef = Self,
                DynamicZoneId = _dynamicZoneId,
                ErrorCode = 1,
                MobileId = GenerateObjectIdentifier(),
                ZoneDisplayName = ZoneName
            };

            player.Tell(rsp);
        }

        Context.Stop(Self);
    }

    #region Handlers

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONETRANSFER))]
    protected virtual void ReceiveZoneTransfer(ZONE_102_PROTOCOL.MSG_ZONETRANSFER message) {
        if (_isLoading) {
            _pendingPlayerEvents[Sender] = message;

            return;
        }

        ProcessZoneTransfer(message, Sender);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
    protected virtual void ReceiveAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message) {
        if (_isLoading) {
            _pendingPlayerEvents[Sender] = message;

            return;
        }

        InformZoneEntitiesOfPlayerEvent(message.Player, message);

        var rsp = new ZONE_102_PROTOCOL.MSG_ADDPLAYERRSP {
            WizardGameObject = message.PlayerObject
        };
        message.Player.Tell(rsp);

        Logger.Debug("{Name} added to zone {ZoneName}.",
            Logger.Args(message.ActualWizardName, ZoneName));
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER))]
    protected virtual void ReceiveRemovePlayer(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message) {
        if (_isLoading) {
            _pendingPlayerEvents.Remove(Sender);

            return;
        }

        InformZoneEntitiesOfPlayerEvent(message.Player, message);
        ReleaseObjectIdentifier();

        var rsp = new ZONE_102_PROTOCOL.MSG_REMOVEPLAYERRSP();
        Sender.Tell(rsp);

        Logger.Debug("Player {Name} removed from zone {ZoneName}.",
            Logger.Args(message.Player.Path.Name, ZoneName));
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PLAYERMOVE))]
    protected virtual void ReceiveOnPlayerMove(ZONE_102_PROTOCOL.MSG_PLAYERMOVE message) {
        if (_isLoading) {
            _pendingPlayerEvents[Sender] = message;

            return;
        }

        InformZoneEntitiesOfPlayerEvent(message.PlayerActor, message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS))]
    private void ReceiveZoneLoadResults(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS message) {
        _loaderRef.Tell(PoisonPill.Instance);
        _zoneLoadTimer.Stop();

        if (message.Error) {
            Logger.Error("Zone {ZoneName} failed to load because {ErrorMessage}", Logger.Args(ZoneName, message.ErrorMessage));
            CloseZone();

            return;
        }

        ZoneData = message.ZoneData;

        // Inform each supervisor of the loaded zone data. They are expected to give a reply
        // to inform the zone that they have loaded their data.
        var timer = new Stopwatch();
        foreach (var supervisor in _supervisors) {
            timer.Restart();
            var timeout = TimeSpan.FromSeconds(ZONE_SUPERVISOR_LOAD_TIMEOUT_IN_SECONDS);
            _ = supervisor.Ask(message, timeout);

            Logger.Debug("Supervisor {SupervisorName} for zone {ZoneName} loaded in {Time}ms.",
                Logger.Args(supervisor.Path.Name, ZoneName, timer.ElapsedMilliseconds));
        }

        Logger.Information("Zone {ZoneName} loaded in {Time}ms.", Logger.Args(ZoneName, _zoneLoadTimer.ElapsedMilliseconds));
        _isLoading = false;

        // Finally process any pending player events that couldn't occur because the zone was still loading.
        ProcessQueue();
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADTIMER))]
    private void ReceiveZoneTimerEnd(ZONE_102_PROTOCOL.MSG_ZONELOADTIMER message) {
        if (_isLoading) {
            Logger.Error("Zone {ZoneName} failed to load within the timeout.", Logger.Args(ZoneName));
            CloseZone();
        }
    }

    #endregion

    private IActorRef CreateSupervisor<T>() where T : ActorBase {
        var props = Akka.Actor.Props.Create(() => (T) Activator.CreateInstance(typeof(T), Self, this));
        return Context.ActorOf(props, typeof(T).Name);
    }

    private void InformZoneEntitiesOfPlayerEvent(IActorRef player, IServerMessage message) {
        var broadcast = new ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST {
            Source = player,
            Messages = [message]
        };

        foreach (var supervisor in _supervisors) {
            supervisor.Tell(broadcast);
        }
    }

    private Vector4 GetLocationFromString(ByteString location) {
        var actualLocation = Vector4.Zero;

        var parsedLoc = Util.GetVectorFromCompactString(location);
        if (parsedLoc.X != 0 || parsedLoc.Y != 0 || parsedLoc.Z != 0) {
            actualLocation = parsedLoc;
        }
        else {
            var searchedLoc = ZoneData.m_locationList.FirstOrDefault(x => x.m_locName == location);
            if (searchedLoc is null) {
                return s_locationFailedGiveaway;
            }
            actualLocation = (Vector4) searchedLoc.m_location;
            actualLocation.W = searchedLoc.m_direction;
        }

        return actualLocation;
    }

    private void ProcessZoneTransfer(ZONE_102_PROTOCOL.MSG_ZONETRANSFER message, IActorRef sender) {
        var rsp = new ZONE_102_PROTOCOL.MSG_ZONETRANSFERRSP {
            ZoneActorRef = Self,
            DynamicZoneId = _dynamicZoneId,
            ErrorCode = 0,
            MobileId = GenerateObjectIdentifier(),
            ZoneDisplayName = ZoneName
        };

        var actualLocation = GetLocationFromString(message.DestinationLocation);
        if (actualLocation == s_locationFailedGiveaway) {
            Logger.Error("Zone {ZoneName} tried to transfer to unknown location: {Location}",
                Logger.Args(ZoneName, message.DestinationLocation));
            rsp.ErrorCode = 1;
            sender.Tell(rsp);

            return;
        }

        rsp.Location = (Vector3) actualLocation;
        rsp.Orientation = actualLocation.W;
        sender.Tell(rsp);
    }

    private void ProcessQueue() {
        foreach (var (playerActor, pendingEvent) in _pendingPlayerEvents) {
            if (pendingEvent is ZONE_102_PROTOCOL.MSG_ZONETRANSFER transfer) {
                ProcessZoneTransfer(transfer, playerActor);
            }
            else {
                InformZoneEntitiesOfPlayerEvent(playerActor, pendingEvent);
            }
        }
    }

}