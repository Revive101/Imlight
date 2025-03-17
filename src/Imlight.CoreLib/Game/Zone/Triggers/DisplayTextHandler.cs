/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.CoreLib.Game.Zone.Core;
using static Imlight.Common.Caches.TypeCache;
using static Imlight.Common.Caches.ServerTypeCache;

namespace Imlight.CoreLib.Game.Zone.Triggers;

internal sealed class DisplayTextHandler<T>(ZoneTrigger trigger) : BaseResultHandler<ResDisplayText>(trigger) 
    where T : Result {

    public override void Execute(IActorRef playerRef, CoreObject playerObj)  {
        var msg = new GAME_5_PROTOCOL.MSG_CLIENTNOTIFYTEXT {
            NotifyText = Result.m_text,
            Type = Result.m_type
        };

        playerRef.Tell(msg);
    }

}