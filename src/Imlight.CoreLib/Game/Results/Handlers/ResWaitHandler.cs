/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Networking;
using System;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResWaitHandler : BaseResultHandler<ResWait> {

    public override bool Execute(IResultContext context) {
        return true;
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_EXECUTEHANDLER))]
    public override void HandleExecute(CHARACTER_103_PROTOCOL.MSG_EXECUTEHANDLER message) {
        var replyTo = Sender;

        Context.System.Scheduler.ScheduleTellOnce(
            delay: TimeSpan.FromSeconds(Result.m_secondsToWait),
            receiver: replyTo,
            message: new CHARACTER_103_PROTOCOL.MSG_RESULTEXECUTED { Success = true },
            sender: Self
        );
    }

}