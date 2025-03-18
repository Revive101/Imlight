/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Akka.Actor;
using Imcodec.IO;
using Imcodec.Math;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Zone.Supervisors;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Zone.Core;

/// <summary>
/// Represents a zone in the game world.
/// </summary>
public class Zone : ReceiveProtocolDispatcher, IWithTimers {

    private const ushort RESERVED_MOBILE_ID_MAX = ushort.MaxValue / 20; // 5% of the maximum mobile ID count is reserved for special objects.
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
    private readonly Lock _mobileIdLock = new();
    private readonly List<IActorRef> _supervisors = [];
    private readonly Stopwatch _zoneLoadTimer;
    private readonly Dictionary<IActorRef, IServerMessage> _pendingPlayerEvents = [];
    private readonly List<ushort> _mobileIdMap = [];
    private readonly Dictionary<IActorRef, bool> _supervisorLoadResults = [];
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
        _supervisors.Add(CreateSupervisor<ZoneTriggerSupervisor>());
        _supervisors.Add(CreateSupervisor<ZonePlayerSupervisor>());
        _supervisors.Add(CreateSupervisor<ZonePathSupervisor>());
        _supervisors.Add(CreateSupervisor<ZoneSigilSupervisor>());
        
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
    /// Closes the zone and stops all child actors.
    /// </summary>
    protected void CloseZone() {
        var msg = new ZONE_102_PROTOCOL.MSG_ZONECLOSED() {
            DynamicZoneId = _dynamicZoneId
        };

        foreach (var supervisor in _supervisors) {
            supervisor.Tell(msg);
        }

        // Inform any pending player events that the zone is closing.
        foreach (var (player, _) in _pendingPlayerEvents) {
            var rsp = new ZONE_102_PROTOCOL.MSG_ZONETRANSFERRSP {
                ZoneActorRef = Self,
                DynamicZoneId = _dynamicZoneId,
                ErrorCode = 1,
                MobileId = 0,
                ZoneDisplayName = ZoneName
            };

            player.Tell(rsp);
        }

        // Inform the supervisor (GameWorld) that the zone is closing.
        Context.Parent.Tell(msg);
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
            _pendingPlayerEvents[message.PlayerActor] = message;

            return;
        }

        InformZoneSupervisors(message.PlayerActor, message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER))]
    protected virtual void ReceiveRemovePlayer(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message) {
        if (_isLoading) {
            _pendingPlayerEvents.Remove(message.PlayerActor);

            return;
        }

        InformZoneSupervisors(message.PlayerActor, message);
        ReleaseObjectIdentifier(message.MobileId);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PLAYERMOVE))]
    protected virtual void ReceivePlayerMove(ZONE_102_PROTOCOL.MSG_PLAYERMOVE message) {
        if (_isLoading) {
            _pendingPlayerEvents[message.PlayerActor] = message;

            return;
        }

        // If the actor is not a part of this zone, do not bother processing.
        if (!_mobileIdMap.Contains(message.PlayerObject.m_nMobileID)) {
            return;
        }

        InformZoneSupervisors(message.PlayerActor, message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_CREATUREMOVE))]
    protected virtual void ReceiveCreatureMove(ZONE_102_PROTOCOL.MSG_CREATUREMOVE message) {
        // If the actor is not a part of this zone, do not bother processing.
        if (!_mobileIdMap.Contains(message.CreatureObject.m_nMobileID)) {
            return;
        }

        InformZoneSupervisors(message.CreatureActor, message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS))]
    private void ReceiveZoneLoadResults(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS message) {
        Logger.Debug("Zone {ZoneName} client data gathered.", Logger.Args(ZoneName));

        if (message.Error) {
            Logger.Error("Zone {ZoneName} failed to load because {ErrorMessage}", 
                Logger.Args(ZoneName, message.ErrorMessage));

            CloseZone();

            return;
        }

        _zoneLoadTimer.Restart();
        ZoneData = message.ZoneData;

        // Inform each supervisor of the loaded zone data. They are expected to give a reply
        // to inform the zone that they have loaded their data.
        _supervisorLoadResults.Clear();
        foreach (var supervisor in _supervisors) {
            _supervisorLoadResults[supervisor] = false;
            supervisor.Tell(message);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONESUPERVISORLOADRESULTS))]
    private void ReceiveSupervisorLoadComplete(ZONE_102_PROTOCOL.MSG_ZONESUPERVISORLOADRESULTS message) {
        _supervisorLoadResults[Sender] = true;

        Logger.Debug("Zone {ZoneName} supervisor {SupervisorName} loaded.", Logger.Args(ZoneName, message.SupervisorName));

        // If the supervisor load results are all true, then the zone is fully loaded.
        if (_supervisorLoadResults.All(x => x.Value)) {
            // Finally process any pending player events that couldn't occur because the zone was still loading.
            ProcessQueue();

            _zoneLoadTimer.Stop();
            Logger.Information("Zone {ZoneName} loaded in {Time}ms.", Logger.Args(ZoneName, _zoneLoadTimer.ElapsedMilliseconds));
            _isLoading = false;

            var startMsg = new ZONE_102_PROTOCOL.MSG_ZONESTART();

            foreach (var supervisor in _supervisors) {
                supervisor.Tell(startMsg);
            }
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADTIMER))]
    private void ReceiveZoneTimerEnd() {
        if (_isLoading) {
            Logger.Error("Zone {ZoneName} failed to load within the timeout.", Logger.Args(ZoneName));
            CloseZone();
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_GETRESERVEDMOBILEID))]
    private void ReceiveGetMobileId() {
        var rsp = new ZONE_102_PROTOCOL.MSG_GETRESERVEDMOBILEIDRSP {
            MobileID = GenerateReservedObjectIdentifier()
        };
        Sender.Tell(rsp);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_POSTEVENT))]
    private void ReceiveTriggerPost(ZONE_102_PROTOCOL.MSG_POSTEVENT message) {
        Logger.Verbose("Zone {ZoneName} received post event {EventName}.", Logger.Args(ZoneName, message.EventName));

        foreach (var supervisor in _supervisors) {
            supervisor.Tell(message);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUERYZONEENTITY))]
    private void ReceiveQueryEntityObject(ZONE_102_PROTOCOL.MSG_QUERYZONEENTITY message) {
        foreach (var supervisor in _supervisors) {
            supervisor.Forward(message);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST))]
    private void ReceiveZoneBroadcast(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST message) 
        => _supervisors.ForEach(supervisor => supervisor.Forward(message));

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL))]
    private void ReceiveRequestCombatSigil(ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL message)
        => _supervisors.ForEach(supervisor => supervisor.Forward(message));

    #endregion

    private IActorRef CreateSupervisor<T>() where T : ActorBase {
        var props = Akka.Actor.Props.Create(() => (T) Activator.CreateInstance(typeof(T), this));
        return Context.ActorOf(props, typeof(T).Name);
    }

    private void InformZoneSupervisors(IActorRef player, IServerMessage message) {
        var broadcast = new ZONE_102_PROTOCOL.MSG_ZONESUPERVISORBROADCAST {
            Sender = player,
            Messages = [message]
        };

        foreach (var supervisor in _supervisors) {
            supervisor.Forward(broadcast);
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
                var start = ZoneData.m_locationList.FirstOrDefault(x => x.m_locName == "Start");
                if (start is not null) {
                    actualLocation = (Vector4) start.m_location;
                    actualLocation.W = start.m_direction;

                    return actualLocation;
                }

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
                InformZoneSupervisors(playerActor, pendingEvent);
            }
        }
    }

    private ushort GenerateObjectIdentifier() {
        lock (_mobileIdLock) {
            // Find first available ID.
            for (ushort i = RESERVED_MOBILE_ID_MAX + 1; i <= ushort.MaxValue; i++) {
                if (!_mobileIdMap.Contains(i)) {
                    _mobileIdMap.Add(i);
                    return i;
                }
            }

            throw new InvalidOperationException("Failed to generate a mobile ID.");
        }
    }

    private ushort GenerateReservedObjectIdentifier() {
        lock (_mobileIdLock) {
            // Find first available ID in reserved range.
            for (ushort i = 1; i <= RESERVED_MOBILE_ID_MAX; i++) {
                if (!_mobileIdMap.Contains(i)) {
                    _mobileIdMap.Add(i);
                    return i;
                }
            }

            throw new InvalidOperationException("Failed to generate a reserved mobile ID - all IDs in use.");
        }
    }

    private void ReleaseObjectIdentifier(ushort mobileId) {
        lock (_mobileIdLock) {
            if (_mobileIdMap.Contains(mobileId)) {
                _mobileIdMap.Remove(mobileId);
            }
        }
    }

}