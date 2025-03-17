/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Triggers;

internal sealed class AddDynamodHandler<T>(ZoneTrigger trigger) : BaseResultHandler<ServerTypeCache.ResAddDynaMod>(trigger) 
    where T : Result {

    public override void Execute(IActorRef playerRef, CoreObject playerObj)  {
        var msg = new CHARACTER_103_PROTOCOL.MSG_ADDDYNAMOD {
            DynaMod = Result,
            ContextActor = playerRef
        };

        // Inform the zone of this state change. This will actually change the object state.
        Trigger.ZoneRef.Tell(msg);

        // Inform the player of this state change. This will remove the modification persistently.
        playerRef.Tell(msg);
    }

}

internal sealed class RemoveDynamodHandler<T>(ZoneTrigger trigger) : BaseResultHandler<ServerTypeCache.ResRemoveDynaMod>(trigger) 
    where T : Result {

    public override void Execute(IActorRef playerRef, CoreObject playerObj)  {
        var msg = new CHARACTER_103_PROTOCOL.MSG_REMOVEDYNAMOD {
            DynaMod = Result,
            ContextActor = playerRef
        };

        // Inform the zone of this state change. This will actually change the object state.
        Trigger.ZoneRef.Tell(msg);

        // Inform the player of this state change. This will remove the modification persistently.
        playerRef.Tell(msg);
    }

}