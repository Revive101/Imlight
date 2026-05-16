/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResDisplayTextHandler : BaseResultHandler<ResDisplayText> {

    public override bool Execute(IResultContext context) {
        var msg = new GAME_5_PROTOCOL.MSG_CLIENTNOTIFYTEXT {
            NotifyText = Result.m_text,
            Type = Result.m_type
        };

        context.GetPlayerRef().Tell(msg);
        
        return true;
    }

}