/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.CoreLib.Shared.Packets;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResSpawnHandler : BaseResultHandler<ResSpawn> {
    
    public override bool Execute(IResultContext context) {
        var zoneActor = context.GetZoneActor();
        if (zoneActor == null) {
            return false;
        }

        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONESUPERVISORBROADCAST {
            Messages = [new ZONE_102_PROTOCOL.MSG_ZONEPATHSPAWN {
                SpawnObjectID = (uint) Result.m_spawnID
            }],
        };

        zoneActor.Tell(broadcastMsg);
        
        return true;
    }

}