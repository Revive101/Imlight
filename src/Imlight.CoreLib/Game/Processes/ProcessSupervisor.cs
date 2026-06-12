/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
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