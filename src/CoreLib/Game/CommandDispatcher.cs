/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.Shared.Networking;
using Serilog.Debugging;

namespace Imlight.CoreLib.Game;

internal class CommandDispatcher : ReceiveProtocolDispatcher {
    private static IActorRef _instance;
    public static IActorRef Instance => _instance;

    public CommandDispatcher() {
        _instance = Self;
    }

    public static Props Props() => Akka.Actor.Props.Create(() => new CommandDispatcher());
    
    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_COMMAND))]
    public void ReceiveCommand(GAME_5_PROTOCOL.MSG_COMMAND message) {
        Logger.Debug($"Received command: {message.Command}");
    }
}
