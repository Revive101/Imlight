/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResControlMusicHandler : BaseResultHandler<ResControlBackgroundMusic> {

    private uint _actionHash;

    public override bool Execute(IResultContext context) {
        if (_actionHash == 0) {
            if (Result == null) {
                Logger.Error("Tried to create a {0}, but the result was null.",
                    Logger.Args(GetType().Name));
                    
                return false;
            }

            _actionHash = StringHash.Compute(Result.m_action);
        }

        var msg = new WIZARD_12_PROTOCOL.MSG_CONTROLMUSIC {
            FadeTime = 2.0f,
            Action = (int) _actionHash,
        };

        context.GetPlayerRef().Tell(msg);

        return true;
    }

}