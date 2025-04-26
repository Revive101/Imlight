/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * PROCESS SUPERVISOR SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Manages the creation and tracking of dynamic game processes using Akka.NET actor system,
 * facilitating the generation of unique processes with dynamic ID allocation.
 * 
 * USAGE EXAMPLE:
 * var processSupervisor = Context.ActorOf(ProcessSupervisor.Props());
 * processSupervisor.Tell(new MSG_NEW_MINIGAME_PROCESS { 
 *     MinigameName = "ExampleMinigame", 
 *     MinigameIndex = 1 
 * });
 * 
 * NOTE:
 * Relies on Akka.NET actor system for process management.
 * Uses cryptographically weak random ID generation.
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System.Collections.Generic;
using Akka.Actor;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Processes;

/// <summary>
/// Supervises the creation and management of game processes using Akka.NET actor system.
/// </summary>
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