/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */
using Akka.Actor;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Triggers;

internal sealed class ResSpawnHandler<T>(ZoneTrigger trigger) : BaseResultHandler<ResSpawn>(trigger) where T : Result {
    
    public override bool Execute(IActorRef playerRef, CoreObject playerObj) {
        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONESUPERVISORBROADCAST {
            Messages = [new ZONE_102_PROTOCOL.MSG_ZONEPATHSPAWN {
                SpawnObjectID = (uint) Result.m_spawnID
            }],
        };

        base.ZoneActor.Tell(broadcastMsg);
        
        return true;
    }

}