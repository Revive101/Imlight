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
 * GAME WORLD MANAGEMENT SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Provides comprehensive zone management and player transfer functionality
 * using Akka.NET actor system for distributed zone loading and instancing.
 * 
 * USAGE EXAMPLE:
 * Create GameWorld actor using GameWorld.Props(gameServer)
 * Handle zone transfers and dynamic zone creation
 * 
 * NOTE:
 * Utilizes Akka.NET actor system for scalable world management.
 * Supports dynamic zone loading, instancing, and transfer mechanisms.
 * Implements timeout and error handling for zone loading.
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Util.Internal;
using Imcodec.Cryptography;
using Imlight.Common;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;

namespace Imlight.CoreLib.Game.World;

/// <summary>
/// Manages game world zone creation, loading, and player transfers.
/// </summary>
public class GameWorld : ReceiveProtocolDispatcher, IWithTimers {

    private const int LOAD_ZONE_TIMEOUT_IN_SECONDS = 15;
    private const int HARD_LIMIT_INSTANCE_THRESHHOLD = 12; // Raids are 12 players.
    private const string LOAD_ZONE_FAILURE_ERROR = "ERROR_EnterWorldFailed";
    private const string ZONE_DOES_NOT_EXIST_ERROR = "ERROR_FailedToCreate";

    public ITimerScheduler Timers { get; set; }

    private readonly Dictionary<string, IActorRef> _publicZones = [];
    private readonly List<uint> _dynamicZoneIds = [];
    private readonly Dictionary<ZONE_102_PROTOCOL.MSG_ZONETRANSFER, IActorRef> _awaitingTransfers = [];
    private readonly Dictionary<string, IActorRef> _zoneLoaderActors = [];
    private readonly GameServer _server;

    private readonly Dictionary<ulong, IActorRef> _instanceContainers = [];
    private readonly Dictionary<string, ulong> _instanceCreationCalledByMap = [];

    // ctor
    public GameWorld(GameServer server) {
        _server = server;

        // Preload NPC vendor data and trainer data.
        NpcInventoryCollection.PreloadInventories();
        NpcSpellInventoryCollection.PreloadNpcSpellInventories();
        CreatureSpellbookCollection.PreloadSpellbooks();
    }

    public static Props Props(GameServer server)
        => Akka.Actor.Props.Create(() => new GameWorld(server));

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONETRANSFER))]
    private void ReceiveZoneTransfer(ZONE_102_PROTOCOL.MSG_ZONETRANSFER message) {
        // First, make sure this zone is valid by checking the AccessPassManager.
        if (!AccessPassManager.DoesZoneExist(message.DestinationZone)) {
            Logger.Error("{Name} received invalid zone name {ZoneName}",
                Logger.Args(nameof(GameWorld), message.DestinationZone));

            var response = new ZONE_102_PROTOCOL.MSG_ZONETRANSFERRSP {
                ErrorCode = StringHash.Compute(ZONE_DOES_NOT_EXIST_ERROR)
            };
            Sender.Tell(response);

            return;
        }

        // If this owner has an instance container, we'll first check with it to see if it has the zone loaded.
        // If it does, we'll forward the transfer request to it.
        if (_instanceContainers.TryGetValue(message.OwnerCharId, out var instanceContainer)) {
            HandleInstancedZoneTransfer(message);
        }
        else {
            HandleOtherZoneTransfer(message);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADTIMER))]
    private void ReceiveZoneTimerEnd(ZONE_102_PROTOCOL.MSG_ZONELOADTIMER message) {
        // If the timer is reached, the zone did not load within the timeout.
        if (_zoneLoaderActors.TryGetValue(message.ZonePath, out var loaderRef)) {
            _zoneLoaderActors.Remove(message.ZonePath);
            Context.Stop(loaderRef);

            Logger.Error("Failed to load zone {ZoneName} within the timeout",
                Logger.Args(message.ZonePath));
        }

        // Reply to any zone transfer requests with failure.
        var reply = new ZONE_102_PROTOCOL.MSG_ZONETRANSFERRSP {
            ErrorMessage = "Failed to load zone within the timeout",
            ErrorCode = StringHash.Compute(LOAD_ZONE_FAILURE_ERROR)
        };
        var transfers = _awaitingTransfers.Where(t => t.Key.DestinationZone == message.ZonePath);
        foreach (var (transferMsg, transferActor) in transfers) {
            transferActor.Tell(reply);
            _awaitingTransfers.Remove(transferMsg);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS))]
    private void ReceiveZoneLoadResults(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS message) {
        // If the zone failed to load, inform anyone waiting for the zone to load.
        if (message.Error) {
            Logger.Error("Failed to load zone: {ErrorMessage}",
                Logger.Args(message.ErrorMessage));

            // Reply to any zone transfer requests with failure.
            var reply = new ZONE_102_PROTOCOL.MSG_ZONETRANSFERRSP {
                ErrorMessage = message.ErrorMessage,
                ErrorCode = StringHash.Compute(LOAD_ZONE_FAILURE_ERROR)
            };
            var transfers = _awaitingTransfers.Where(t => t.Key.DestinationZone == message.ZonePath);
            foreach (var (transferMsg, transferActor) in transfers) {
                transferActor.Tell(reply);
                _awaitingTransfers.Remove(transferMsg);
            }

            RemoveZoneLoader(message.ZonePath);

            return;
        }

        // The game world has no obligation to validate the zone data.
        // If the zone data is invalid, the zone actor will handle it, and the game world will be notified
        // with a `MSG_ZONECLOSED` message.
        var zonePath = message.ZonePath;

        // Search for the user who called for the creation of this zone.
        var ownerId = _instanceCreationCalledByMap[zonePath];
        _instanceCreationCalledByMap.Remove(zonePath);

        // Determine if this zone is instanced based on the hard limit.
        var isInstancedZone = message.ZoneData.m_nHardLimit <= HARD_LIMIT_INSTANCE_THRESHHOLD;
        if (isInstancedZone) {
            // Create a new instance container for this zone, if one does not already exist.
            if (!_instanceContainers.TryGetValue(ownerId, out var instanceContainer)) {
                instanceContainer = CreateInstanceContainer(ownerId);
            }

            // Inform the instance container of the load results.
            // This will cause the instance container to create the zone actor.
            instanceContainer.Tell(message);

            ProcessTransfersForInstanceContainer(zonePath);
        }
        else {
            // Create the new public zone and inform it of the load results.
            _publicZones[zonePath] = CreateZone(zonePath);
            _publicZones[zonePath].Tell(message);

            ProcessTransfersForPublicZone(zonePath);
        }

        RemoveZoneLoader(zonePath);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONECLOSED))]
    private void ReceiveZoneClosed(ZONE_102_PROTOCOL.MSG_ZONECLOSED message) {
        var dynamicId = message.DynamicZoneId;
        _dynamicZoneIds.Remove(dynamicId);
    }

    private static string SanitizeZoneName(string zoneName)
        => zoneName.Replace('/', '-');

    private void HandleInstancedZoneTransfer(ZONE_102_PROTOCOL.MSG_ZONETRANSFER message) {
        // If the public zone already exists, we can transfer the player immediately to it.
        if (_publicZones.TryGetValue(message.DestinationZone, out var zone)) {
            zone.Forward(message);

            return;
        }

        // Ask the instance container if it has the zone loaded.
        var hasZoneMsg = new ZONE_102_PROTOCOL.MSG_INSTANCECONTAINERHASZONE {
            ZoneName = message.DestinationZone
        };
        var timeOut = TimeSpan.FromSeconds(5);
        var response = _instanceContainers[message.OwnerCharId]
            .Ask<ZONE_102_PROTOCOL.MSG_INSTANCECONTAINERHASZONERSP>(hasZoneMsg, timeOut)
            .Result;

        // If the instance container does not have the zone loaded, we need to create it.
        if (!response.HasZone) {
            // Create the zone loader.
            CreateZoneLoader(message.DestinationZone);

            // We want to wait until the zone is fully loaded before transferring the player.
            _awaitingTransfers.Add(message, Sender);

            // Add the owner ID to the map so we know who called for the creation of this zone.
            _instanceCreationCalledByMap.Add(message.DestinationZone, message.OwnerCharId);

            return;
        }
        else {
            // If the instance container already exists, we can transfer the player immediately.
            _instanceContainers[message.OwnerCharId].Forward(message);
        }
    }

    public void HandleOtherZoneTransfer(ZONE_102_PROTOCOL.MSG_ZONETRANSFER message) {
        // Get the zone if it's already loaded; or, create a new one if it's not.
        IActorRef zone;
        if (!_publicZones.TryGetValue(message.DestinationZone, out var value)) {
            zone = CreateZoneLoader(message.DestinationZone);

            // We want to wait until the zone is fully loaded before transferring the player.
            _awaitingTransfers.Add(message, Sender);
            _instanceCreationCalledByMap.AddOrSet(message.DestinationZone, message.OwnerCharId);
        }
        else {
            // If the zone is already loaded, we can transfer the player immediately.
            zone = value;
            zone.Forward(message);
        }
    }

    private IActorRef CreateZoneLoader(string zonePath) {
        // Create the loader actor and prepare the loading of this zone.
        var loaderRef = Context.ActorOf(Akka.Actor.Props.Create(() => new ZoneLoader()));

        // Tell the loader to begin loading the zone and await the response.
        var msg = new ZONE_102_PROTOCOL.MSG_ZONELOADBEGIN { ZonePath = zonePath };
        loaderRef.Tell(msg);

        _zoneLoaderActors.Add(zonePath, loaderRef);

        // Send a message to ourselves to clean up the loader actor after a certain amount of time.
        // Receiving `MSG_ZONELOADRESULTS` from the loader actor will cancel this timer.
        // This means that receiving `MSG_ZONELOADTIMER` will only happen if the zone failed to load.
        var loadZoneTimeoutKey = $"loadZoneTimeout_{zonePath}";
        var loadZoneTimeoutTimespan = TimeSpan.FromSeconds(LOAD_ZONE_TIMEOUT_IN_SECONDS);
        var loadZoneTimeoutMsg = new ZONE_102_PROTOCOL.MSG_ZONELOADTIMER { ZonePath = zonePath };
        Timers.StartSingleTimer(loadZoneTimeoutKey, loadZoneTimeoutMsg, loadZoneTimeoutTimespan);

        return loaderRef;
    }

    private IActorRef CreateZone(string zoneName) {
        var zoneActorName = SanitizeZoneName(zoneName);
        var zoneId = GetNextDynamicZoneId();
        var zone = Context.ActorOf(Zone.Core.Zone.Props(zoneName, zoneId), zoneActorName);

        // Log the new zone creation.
        Logger.Information("Game world created new zone: {ZoneName}",
            Logger.Args(zoneName));

        return zone;
    }

    private IActorRef CreateInstanceContainer(ulong ownerId) {
        var zoneActorName = $"{nameof(InstanceContainer)}_{ownerId}"; // Append the owner ID to the zone name.

        var instanceContainer = Context.ActorOf(InstanceContainer.Props(ownerId), zoneActorName);
        _instanceContainers.Add(ownerId, instanceContainer);

        // Log the new instance container creation.
        Logger.Information("Game world creates new instance container {0}, owned by {1}",
            Logger.Args(zoneActorName, ownerId));

        return instanceContainer;
    }

    private void RemoveZoneLoader(string zonePath) {
        // Stop the timer.
        var loadZoneTimeoutKey = $"loadZoneTimeout_{zonePath}";
        Timers.Cancel(loadZoneTimeoutKey);

        if (zonePath is not null) {
            if (_zoneLoaderActors.TryGetValue(zonePath, out var loaderRef)) {
                _zoneLoaderActors.Remove(zonePath);
                Context.Stop(loaderRef);
            }
        }
    }

    private void ProcessTransfersForPublicZone(string zonePath) {
        var transfers = _awaitingTransfers.Where(t => t.Key.DestinationZone == zonePath);
        if (transfers is null || !transfers.Any()) {
            Logger.Error("{Name} received unexpected zone load result for {ZoneName}",
                Logger.Args(nameof(GameWorld), zonePath));

            return;
        }

        foreach (var (transferMsg, transferActor) in transfers) {
            _publicZones[zonePath].Tell(transferMsg, transferActor);

            _awaitingTransfers.Remove(transferMsg);
        }
    }

    private void ProcessTransfersForInstanceContainer(string zonePath) {
        var transfers = _awaitingTransfers.Where(t => t.Key.DestinationZone == zonePath);
        if (transfers is null || !transfers.Any()) {
            Logger.Error("{Name} received unexpected zone load result for {ZoneName}",
                Logger.Args(nameof(GameWorld), zonePath));

            return;
        }

        foreach (var (transferMsg, transferActor) in transfers) {
            _instanceContainers[transferMsg.OwnerCharId].Tell(transferMsg, transferActor);

            _awaitingTransfers.Remove(transferMsg);
        }
    }

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