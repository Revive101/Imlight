/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.CoreLib.Shared.Networking;

namespace Imlight.CoreLib.Game.Services;

internal class TrainService : MessageService {
    public TrainService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new TrainService(parentActor));

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_TRAIN))]
    private void ReceiveTrain(WIZARD_12_PROTOCOL.MSG_TRAIN message) { }
}
