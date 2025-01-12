/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game;

public class GameWorld(GameServer server) : ReceiveProtocolDispatcher {
    private readonly Dictionary<string, IActorRef> _zones = [];
    private readonly GameServer _server = server;

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
        if (!_zones.TryGetValue(message.DestinationZone, out var value)) {
            if (message.IsPrivate) {
                // todo
                zone = CreatePublicZone(message.DestinationZone);
            }
            else {
                zone = CreatePublicZone(message.DestinationZone);
            }
        }
        else {
            zone = value;
        }

        // Forward the message to the zone actor we just created, or already have.
        zone.Forward(message);
    }

    private IActorRef CreatePublicZone(string zoneName) {
        var zoneActorName = SanitizeZoneName(zoneName);
        var zone = Context.ActorOf(Zone.Core.Zone.Props(zoneName, 1), zoneActorName);

        _zones.Add(zoneName, zone);

        // Log the new zone creation.
        Logger.Information("GameWorld created new zone: {ZoneName}",
            Logger.Args(zoneName));

        return zone;
    }

    private static string SanitizeZoneName(string zoneName) 
        => zoneName.Replace('/', '-');
}
