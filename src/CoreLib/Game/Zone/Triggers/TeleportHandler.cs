/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using System;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Triggers;

internal sealed class TeleportHandler<T>(ZoneTrigger trigger) : BaseResultHandler<ServerTypeCache.ResTeleport>(trigger) 
    where T : Result {

    public override void Execute(IActorRef playerRef, CoreObject playerObj)  {
        var msg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER {
            DestinationZone = Result.m_destinationZone,
            DestinationLocation = Result.m_destinationLoc,
            SendToClient = true
        };
        
        playerRef.Tell(msg);
    }

}