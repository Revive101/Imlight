/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using Akka.Actor;
using Akka.Util.Internal;
using Imlight.Common.Caches;
using Imlight.CoreLib.Shared.Networking;

namespace Imlight.CoreLib.Game.Services;

internal class CommandService : MessageService {
    private IActorRef _dispatcherRef;

    public CommandService(SessionActor sessionActor) : base(sessionActor) { _dispatcherRef = CommandDispatcher.Instance; }

    protected static Props Props(SessionActor parentActor) {
        return Akka.Actor.Props.Create(() => new CommandService(parentActor));
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_COMMAND))]
    private void ReceiveCommand(GAME_5_PROTOCOL.MSG_COMMAND message) {
        _dispatcherRef.Forward(message);
    }
}
