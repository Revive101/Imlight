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
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Serilog;

namespace Imlight.CoreLib.Game;

internal class CommandDispatcher : ReceiveProtocolDispatcher {
    private static IActorRef _instance;
    public static IActorRef Instance => _instance;

    private IActorRef _senderContext;

    public CommandDispatcher() {
        _instance = Self;
    }

    public static Props Props() => Akka.Actor.Props.Create(() => new CommandDispatcher());

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_COMMAND))]
    public void ReceiveCommand(SERVER_100_PROTOCOL.MSG_COMMAND message) {
        Logger.Debug($"Received command: {message.CommandText}");

        // Setup context before parsing any commands.
        _senderContext = message.ActorRef;

        var parameters = message.CommandText.ToString().Split(' ');
        var command = parameters[0].ToLower();
        var arguments = parameters.Skip(1).ToArray();
        switch (command) {
            case "teleport": Teleport(arguments[0]); break;
        }
    }

    private void Teleport(string zone) {
        var actualZoneName = zone;
        var hasZone = AccessPassManager.DoesZoneExist(zone);
        if (!hasZone) {
            actualZoneName = AccessPassManager.GetContainedZoneName(zone);

            if (!AccessPassManager.DoesZoneExist(actualZoneName)) {
                Log.Error("Teleport command was given an invalid zone name {0}", Logger.Args(zone));
                return;
            }
        }

        var msg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER() {
            DestinationZone = actualZoneName,
            DestinationLocation = "Start",
            SendToClient = true
        };
        _senderContext.Tell(msg);
    }
}
