/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Zone.Core;

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