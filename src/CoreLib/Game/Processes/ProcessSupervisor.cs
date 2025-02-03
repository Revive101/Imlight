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

        // Create Akka props for the new process.
        var props = GetMinigameProps(minigameName, processId);
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

    private static Props GetMinigameProps(string minigameName, uint processId) {
        var processName = $"{minigameName}_{processId}";

        return minigameName switch {
            "SkullRiders"   => Akka.Actor.Props.Create(() => new SkullRidersProcess(processName, processId)),
            "Soblocks"      => Akka.Actor.Props.Create(() => new SorceryStonesProcess(processName, processId)),
            "DoodleDoug"    => Akka.Actor.Props.Create(() => new DoodleDougProcess(processName, processId)),
            "concentration" => Akka.Actor.Props.Create(() => new ConcentrationProcess(processName, processId)),
            "HotShots"      => Akka.Actor.Props.Create(() => new HotShotsProcess(processName, processId)),
            "ChooChooZoo"   => Akka.Actor.Props.Create(() => new ChooChooProcess(processName, processId)),
            "PotionMotion"  => Akka.Actor.Props.Create(() => new PotionMotionProcess(processName, processId)),
            "Dueling_Diego" => Akka.Actor.Props.Create(() => new DuelingDiegoProcess(processName, processId)),
            _ => throw new System.NotImplementedException($"Minigame '{minigameName}' is not implemented."),
        };
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