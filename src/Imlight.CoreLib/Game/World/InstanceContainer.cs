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
 * Last Updated: 3/18/2025
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
internal sealed class InstanceContainer(ulong instanceOwnerId) : ReceiveProtocolDispatcher, IWithTimers {

    public ITimerScheduler Timers { get; set; }

    private readonly ulong _instanceOwnerId = instanceOwnerId;
    private readonly List<uint> _dynamicZoneIds = [];
    private readonly Dictionary<string, IActorRef> _zones = [];

    public static Props Props(ulong instanceOwnerId) 
        => Akka.Actor.Props.Create(() => new InstanceContainer(instanceOwnerId));

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
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_INSTANCECONTAINERHASZONE))]
    public void ReceiveInstanceContainerHasZone(ZONE_102_PROTOCOL.MSG_INSTANCECONTAINERHASZONE message) 
        => Sender.Tell(new ZONE_102_PROTOCOL.MSG_INSTANCECONTAINERHASZONERSP {
            HasZone = _zones.ContainsKey(message.ZoneName)
        });

    private IActorRef CreateZone(string zoneName) {
        var zoneActorName = SanitizeZoneName(zoneName);
        var zoneId = GetNextDynamicZoneId();
        var zone = Context.ActorOf(Zone.Core.Zone.Props(zoneName, zoneId), zoneActorName);

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