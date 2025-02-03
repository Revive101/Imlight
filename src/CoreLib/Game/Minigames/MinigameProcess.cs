/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.CoreLib.Game.Processes;
using Imlight.CoreLib.Shared.Networking;

internal abstract class MinigameProcess(string processName, uint processId, params IActorRef[] participants) 
    : Process(processName, processId, participants) {

    protected bool IsMinigameActive { get; set; }

}