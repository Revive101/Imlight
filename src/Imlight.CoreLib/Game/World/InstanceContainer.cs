/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * INSTANCE CONTAINER MANAGEMENT SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Manages zone instances for a specific player using Akka.NET actor system,
 * handling zone loading, transfer, and dynamic zone creation.
 * 
 * USAGE EXAMPLE:
 * Create InstanceContainer using InstanceContainer.Props(ownerId)
 * Handle zone transfers and dynamic zone loading for a specific player
 * 
 * NOTE:
 * Utilizes Akka.NET actor system for per-player zone instancing.
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using System;
using System.Collections.Generic;

namespace Imlight.CoreLib.Game.World;

/// <summary>
/// Manages zone instances for a specific player using Akka.NET actor system.
/// </summary>
internal sealed class InstanceContainer(ulong instanceOwnerId, bool removeWhenEmpty) : ReceiveProtocolDispatcher, IWithTimers {

    private const string IDLE_TIMER = "container-idle";
    private const int EMPTY_CONTAINER_MINUTES = 5; // todo: tune

    public ITimerScheduler Timers { get; set; }

    private readonly ulong _instanceOwnerId = instanceOwnerId;
    private readonly List<uint> _dynamicZoneIds = [];
    private readonly Dictionary<string, IActorRef> _zones = [];
    private readonly Dictionary<string, uint> _zoneDynamicIds = [];
    private readonly Zone.Core.InstanceQuestProgress _questProgress = new();

    public static Props Props(ulong instanceOwnerId, bool removeWhenEmpty)
        => Akka.Actor.Props.Create(() => new InstanceContainer(instanceOwnerId, removeWhenEmpty));

    protected override void PreStart() {
        base.PreStart();
        ArmEmptyTimer();
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONETRANSFER))]
    public void ReceiveZoneTransfer(ZONE_102_PROTOCOL.MSG_ZONETRANSFER message) {
        Logger.Debug("Container (owned by: {0}) received zone transfer request for zone: {1}",
            Logger.Args(_instanceOwnerId, message.DestinationZone));

        // Throw an exception if we don't have this zone.
        if (!_zones.ContainsKey(message.DestinationZone)) {
            throw new Exception($"Zone {message.DestinationZone} not found in instance container {_instanceOwnerId}");
        }

        // Otherwise, forward this transfer message to the zone.
        _zones[message.DestinationZone].Forward(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS))]
    public void ReceiveZoneLoadResults(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS message) {
        var zoneName = message.ZoneData.m_zoneName;
        var zoneActor = CreateZone(zoneName);
        zoneActor.Tell(message);

        _zones[zoneName] = zoneActor;
        _questProgress.AddZone(zoneName);
        Timers.Cancel(IDLE_TIMER);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_INSTANCECONTAINERHASZONE))]
    public void ReceiveInstanceContainerHasZone(ZONE_102_PROTOCOL.MSG_INSTANCECONTAINERHASZONE message) 
        => Sender.Tell(new ZONE_102_PROTOCOL.MSG_INSTANCECONTAINERHASZONERSP {
            HasZone = _zones.ContainsKey(message.ZoneName)
        });

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_MODIFYTRIGGEROBJECT))]
    public void ReceiveModifyTriggerObject(ZONE_102_PROTOCOL.MSG_MODIFYTRIGGEROBJECT message) {
        foreach (var zoneActor in _zones.Values) {
            if (zoneActor.Equals(Sender)) {
                continue;
            }

            zoneActor.Tell(new ZONE_102_PROTOCOL.MSG_MODIFYTRIGGEROBJECT {
                ObjectName = message.ObjectName,
                StateName = message.StateName,
                PlayerActor = message.PlayerActor,
                PlayerGameObject = message.PlayerGameObject,
                PlayerSpawned = message.PlayerSpawned,
                Relayed = true,
            }, Self);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_DROPINSTANCEZONE))]
    public void ReceiveDropInstanceZone(ZONE_102_PROTOCOL.MSG_DROPINSTANCEZONE message) {
        if (!_zones.Remove(message.ZoneName, out var zoneActor)) {
            return;
        }

        _questProgress.RemoveZone(message.ZoneName);

        if (_zoneDynamicIds.Remove(message.ZoneName, out var dynamicId)) {
            _dynamicZoneIds.Remove(dynamicId);
        }

        Logger.Information("Dropping instance zone {ZoneName} (owner {OwnerId})",
            Logger.Args(message.ZoneName, _instanceOwnerId));

        Context.Stop(zoneActor);
        ArmEmptyTimer();

        // A match container serves one match only, so nothing will ever transfer into it again.
        if (_zones.Count == 0 && removeWhenEmpty) {
            Context.Parent.Tell(new ZONE_102_PROTOCOL.MSG_MATCHCONTAINEREMPTY { OwnerId = _instanceOwnerId });
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_INSTANCECONTAINERIDLE))]
    public void ReceiveContainerIdle(ZONE_102_PROTOCOL.MSG_INSTANCECONTAINERIDLE message) {
        if (_zones.Count > 0) {
            return;
        }

        Context.Parent.Tell(new ZONE_102_PROTOCOL.MSG_INSTANCECONTAINEREMPTY { OwnerCharId = _instanceOwnerId });

        // The game world may refuse while a transfer for this owner is pending; ask again later.
        ArmEmptyTimer();
    }

    private void ArmEmptyTimer() {
        if (_zones.Count > 0) {
            return;
        }

        Timers.StartSingleTimer(IDLE_TIMER, new ZONE_102_PROTOCOL.MSG_INSTANCECONTAINERIDLE(),
            TimeSpan.FromMinutes(EMPTY_CONTAINER_MINUTES));
    }

    private IActorRef CreateZone(string zoneName) {
        var zoneActorName = SanitizeZoneName(zoneName);
        var zoneId = GetNextDynamicZoneId();
        _zoneDynamicIds[zoneName] = zoneId;

        // Every zone created under an instance container is, by definition, instanced.
        var zone = Context.ActorOf(Zone.Core.Zone.Props(zoneName, zoneId, true, _questProgress), zoneActorName);

        // Log the new zone creation.
        Logger.Information("Game world created new zone: {ZoneName}",
            Logger.Args(zoneName));

        return zone;
    }

    private static string SanitizeZoneName(string zoneName)
        => zoneName.Replace('/', '-');

    private uint GetNextDynamicZoneId() {
        uint id;
        var random = new Random();
        do {
            id = (uint) random.Next(1, int.MaxValue);
        } while (_dynamicZoneIds.Contains(id));

        _dynamicZoneIds.Add(id);

        return id;
    }

}