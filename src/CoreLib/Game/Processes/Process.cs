/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using System;
using System.Collections.Generic;

namespace Imlight.CoreLib.Game.Processes;

internal abstract class Process : ReceiveProtocolDispatcher, IWithTimers {

    private const string ACTIVITY_CHECK_LOCK = "activity-check";
    private const uint ACTIVITY_CHECK_INTERVAL_IN_SECONDS = 600; // 10 Minutes

    public ITimerScheduler Timers { get; set; }

    protected List<IActorRef> Participants { get; set; } = [];

    private readonly string _processName;
    private readonly uint _processId;

    private bool _hadActivity;

    // ctor
    protected Process(string processName, uint processId, params IActorRef[] participants) {
        this._processName = processName;
        this._processId = processId;
        this._hadActivity = false;
        this.Participants.AddRange(participants);

        // Start a periodic timer to check for activity.
        var timespan = TimeSpan.FromSeconds(ACTIVITY_CHECK_INTERVAL_IN_SECONDS);
        var msg = new PROCESS_107_PROTOCOL.MSG_PROCESS_ACTIVITY_CHECK();
        Timers.StartPeriodicTimer(ACTIVITY_CHECK_LOCK, msg, timespan);
    }

    [MessageHandler(typeof(IServerMessage))]
    private void ReceiveElse() 
        => _hadActivity = true;

    [MessageHandler(typeof(PROCESS_107_PROTOCOL.MSG_PROCESS_ACTIVITY_CHECK))]
    private void ReceiveProcessLifeCycle() {
        if (!_hadActivity) {
            KillProcess();
        }

        _hadActivity = false;
    }

    private void KillProcess() {
        // Inform the supervisor that the process has been killed.
        var supervisor = Context.Parent;
        var killedMsg = new PROCESS_107_PROTOCOL.MSG_PROCESS_KILLED {
            ProcessId = _processId
        };
        supervisor.Tell(killedMsg);

        // Inform the participants that the process has been killed.
        foreach (var participant in Participants) {
            participant.Tell(killedMsg);
        }

        Logger.Debug("Process {0} killed after {1} seconds of inactivity.",
            Logger.Args(_processName, ACTIVITY_CHECK_INTERVAL_IN_SECONDS));

        Timers.Cancel(ACTIVITY_CHECK_LOCK);
        Context.Stop(Self);
    }

}