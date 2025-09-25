/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResPostEventHandler : BaseResultHandler<ResPostEvent> {

    public override bool Execute(IResultContext context) {
        var zoneActor = context.GetZoneActor();
        if (zoneActor == null) {
            return false;
        }

        var msg = new ZONE_102_PROTOCOL.MSG_POSTEVENT {
            PlayerActor = context.GetPlayerRef(),
            PlayerGameObject = context.GetPlayerObj(),
            EventName = Result.m_eventName
        };

        zoneActor.Tell(msg);

        return true;
    }

}