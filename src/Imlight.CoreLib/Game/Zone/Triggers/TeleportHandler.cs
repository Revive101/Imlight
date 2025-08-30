/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using System;

namespace Imlight.CoreLib.Game.Zone.Triggers;

internal sealed class TeleportHandler<T>(ZoneTrigger trigger) : BaseResultHandler<ResTeleport>(trigger)
    where T : Result {

    public override bool Execute(IActorRef playerRef, CoreObject playerObj) {
        if (playerObj is null) {
            //throw new InvalidOperationException("Player object is null.");

            return false;
        }

        var msg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER {
            DestinationZone = Result.m_destinationZone,
            DestinationLocation = Result.m_destinationLoc,
            SendToClient = true,
            OwnerCharId = playerObj.m_globalID
        };

        playerRef.Tell(msg);

        return true;
    }

}