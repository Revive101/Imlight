/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Cryptography;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Implementations;

namespace Imlight.CoreLib.Game.World;

public class GameWorld : ReceiveProtocolDispatcher, IWithTimers {

    private const int LOAD_ZONE_TIMEOUT_IN_SECONDS = 15;
    private const string LOAD_ZONE_FAILURE_ERROR = "ERROR_EnterWorldFailed";

    public ITimerScheduler Timers { get; set; }

    private readonly Dictionary<string, IActorRef> _publicZones = [];
    private readonly List<uint> _dynamicZoneIds = [];
    private readonly Dictionary<ZONE_102_PROTOCOL.MSG_ZONETRANSFER, IActorRef> _awaitingTransfers = [];
    private readonly Dictionary<string, IActorRef> _zoneLoaderActors = [];
    private readonly GameServer _server;

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
                ErrorCode = 1
            };
            Sender.Tell(response);

            return;
        }

        // Get the zone if it's already loaded; or, create a new one if it's not.
        IActorRef zone;
        if (!_publicZones.TryGetValue(message.DestinationZone, out var value)) {
            zone = CreateZoneLoader(message.DestinationZone);

            // We want to wait until the zone is fully loaded before transferring the player.
            _awaitingTransfers.Add(message, Sender);
        }
        else {
            // If the zone is already loaded, we can transfer the player immediately.
            zone = value;
            zone.Forward(message);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADTIMER))]
    private void ReceiveZoneTimerEnd(ZONE_102_PROTOCOL.MSG_ZONELOADTIMER message) {
        // If the timer is reached, the zone did not load within the timeout.
        if (_zoneLoaderActors.TryGetValue(message.ZonePath, out var loaderRef)) {
            _zoneLoaderActors.Remove(message.ZonePath);
            Context.Stop(loaderRef);

            Logger.Error("{Name} failed to load zone {ZoneName} within the timeout",
                Logger.Args(nameof(GameWorld), message.ZonePath));
        }

        // Reply to any zone transfer requests with failure.
        var reply = new ZONE_102_PROTOCOL.MSG_ZONETRANSFERRSP {
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
        // ! DO NOT HANDLE ZONE LOAD ERRORS HERE. THEY ARE HANDLED BY THE ZONE ITSELF.
        // ! If the zone fails to load, it will send us MSG_ZONECLOSED.
        var zonePath = message.ZoneData.m_zoneName;

        // Create the new zone and inform it of the load results.
        _publicZones[zonePath] = CreateZone(zonePath);
        _publicZones[zonePath].Tell(message);

        RemoverZoneLoader(zonePath);
        ProcessTransfersForZone(zonePath);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONECLOSED))]
    private void ReceiveZoneClosed(ZONE_102_PROTOCOL.MSG_ZONECLOSED message) {
        var dynamicId = message.DynamicZoneId;
        _dynamicZoneIds.Remove(dynamicId);
    }

    private static string SanitizeZoneName(string zoneName)
        => zoneName.Replace('/', '-');

    private IActorRef CreateZoneLoader(string zonePath) {
        // Create the loader actor and prepare the loading of this zone.
        var loaderRef = Context.ActorOf(Akka.Actor.Props.Create(() => new ZoneLoader()));

        // Tell the loader to begin loading the zone and await the response.
        var msg = new ZONE_102_PROTOCOL.MSG_ZONELOADBEGIN { ZonePath = zonePath };
        loaderRef.Tell(msg);

        _zoneLoaderActors.Add(zonePath, loaderRef);

        // Send a message to ourselves to clean up the loader actor after a certain amount of time.
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
        Logger.Information("GameWorld created new zone: {ZoneName}",
            Logger.Args(zoneName));

        return zone;
    }

    private void RemoverZoneLoader(string zonePath) {
        if (_zoneLoaderActors.TryGetValue(zonePath, out var loaderRef)) {
            _zoneLoaderActors.Remove(zonePath);
            Context.Stop(loaderRef);

            // Stop the timer.
            var loadZoneTimeoutKey = $"loadZoneTimeout_{zonePath}";
            Timers.Cancel(loadZoneTimeoutKey);
        }
    }

    private void ProcessTransfersForZone(string zonePath) {
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
