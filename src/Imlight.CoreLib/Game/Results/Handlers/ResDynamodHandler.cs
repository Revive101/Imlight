/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResDynamodHandler : BaseResultHandler<ResAddDynaMod> {

    public override bool Execute(IResultContext context) {
        var msg = new CHARACTER_103_PROTOCOL.MSG_ADDDYNAMOD {
            DynaMod = Result,
            ContextActor = context.GetPlayerRef()
        };

        var zoneActor = context.GetZoneActor();
        if (zoneActor != null) {
            zoneActor.Tell(msg);
        }

        context.GetPlayerRef().Tell(msg);

        return true;
    }

}

internal sealed class RemoveDynamodHandler : BaseResultHandler<ResRemoveDynaMod> {

    public override bool Execute(IResultContext context) {
        var msg = new CHARACTER_103_PROTOCOL.MSG_REMOVEDYNAMOD {
            DynaMod = Result,
            ContextActor = context.GetPlayerRef()
        };

        var zoneActor = context.GetZoneActor();
        if (zoneActor != null) {
            zoneActor.Tell(msg);
        }

        context.GetPlayerRef().Tell(msg);
        
        return true;
    }

}