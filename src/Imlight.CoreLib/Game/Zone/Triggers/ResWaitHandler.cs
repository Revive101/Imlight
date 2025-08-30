/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */
using Akka.Actor;
using Imlight.CoreLib.Game.Zone.Core;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Networking;
using System;

namespace Imlight.CoreLib.Game.Zone.Triggers;

internal sealed class ResWaitHandler<T>(ZoneTrigger trigger) : BaseResultHandler<ResWait>(trigger) where T : Result {

    public override bool Execute(IActorRef playerRef, CoreObject playerObj)
        => true;

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_POSTEVENT))]
    public override void HandlePostEvent(ZONE_102_PROTOCOL.MSG_POSTEVENT message) {
        var replyTo = Sender;

        Context.System.Scheduler.ScheduleTellOnce(
            delay: TimeSpan.FromSeconds(Result.m_secondsToWait),
            receiver: replyTo,
            message: new ZONE_102_PROTOCOL.MSG_RESULTEXECUTED { Success = true },
            sender: Self
        );
    }

}