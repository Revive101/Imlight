/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.CoreLib.Game.Minigames;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using System.Collections.Generic;

namespace Imlight.CoreLib.Game.Processes;

internal sealed class ProcessSupervisor : ReceiveProtocolDispatcher {

    private readonly Dictionary<uint, IActorRef> _processes = [];

    // Akka.net props
    internal static Props Props() 
        => Akka.Actor.Props.Create(() => new ProcessSupervisor());

    [MessageHandler(typeof(PROCESS_107_PROTOCOL.MSG_NEW_MINIGAME_PROCESS))]
    private void ReceiveNewMinigameProcess(PROCESS_107_PROTOCOL.MSG_NEW_MINIGAME_PROCESS message) {
        var minigameName = message.MinigameName;
        var processId = GenerateProcessId();
        var minigameIndex = message.MinigameIndex;

        // Create Akka props for the new process.
        var props = Akka.Actor.Props.Create(() => new MinigameProcess(minigameName, processId, minigameIndex));
        var processName = $"{minigameName}_{processId}";
        var processActorRef = Context.ActorOf(props, processName);

        // Inform the sender of the new process.
        var reply = new PROCESS_107_PROTOCOL.MSG_PROCESS_DETAILS {
            ProcessName = minigameName,
            ProcessActorRef = processActorRef,
            ProcessId = processId
        };
        Sender.Tell(reply);
    }

    private uint GenerateProcessId() {
        uint processId;
        var random = new System.Random();

        do {
            processId = (uint)random.Next(1, int.MaxValue);
        } while (_processes.ContainsKey(processId));

        return processId;
    }

}