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
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Results;

/// <summary>
/// Actor responsible for executing results sequentially for a single activation instance.
/// Creates handler actors, executes results one at a time, then self-destructs when complete.
/// </summary>
public class ResultExecutorActor(IResultContext context) : ReceiveProtocolDispatcher, IWithTimers {

    private const uint RESULT_HANDLER_TIMEOUT_MS = 30000;

    private readonly IResultContext _context = context;
    private readonly Queue<Result> _resultQueue = new();
    private bool _isExecuting;
    private bool _allSuccessful = true;
    private IActorRef _replyTo;

    public ITimerScheduler Timers { get; set; }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_EXECUTERESULTS))]
    private void HandleExecuteResults(CHARACTER_103_PROTOCOL.MSG_EXECUTERESULTS message) {
        _replyTo = _context.GetReplyTo();

        var results = _context.GetResults();
        if (results == null || !results.Any()) {
            SendFinalReply(true);
            return;
        }

        var resultsList = results.Where(r => r != null).ToList();
        if (resultsList.Count == 0) {
            SendFinalReply(true);
            return;
        }

        foreach (var result in resultsList) {
            _resultQueue.Enqueue(result);
        }

        ProcessNextResult();
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_RESULTHANDLERCOMPLETED))]
    private void ReceiveResultHandlerCompleted(CHARACTER_103_PROTOCOL.MSG_RESULTHANDLERCOMPLETED message) {
        _isExecuting = false;

        if (!message.Success) {
            _allSuccessful = false;
            Logger.Warning("Result handler for type {0} reported failure.",
                Logger.Args(message.ResultType.Name));
        } 

        ProcessNextResult();
    }

    private void ProcessNextResult() {
        if (_isExecuting) {
            return;
        }

        if (_resultQueue.Count == 0) {
            SendFinalReply(_allSuccessful);
            return;
        }

        var result = _resultQueue.Dequeue();
        var resultType = result.GetType();

        // Special case: ResWait doesn't need a handler, just schedule a delay.
        if (result is ResWait resWait) {
            _isExecuting = true;

            Timers.StartSingleTimer(
                key: $"ResWait_{Guid.NewGuid():N}",
                msg: new CHARACTER_103_PROTOCOL.MSG_RESULTHANDLERCOMPLETED {
                    Success = true,
                    ResultType = resultType
                },
                timeout: TimeSpan.FromMilliseconds(resWait.m_secondsToWait * 1000)
            );

            return;
        }

        var handlerType = ResultDispatcher.FindHandlerForResult(resultType, _context);

        if (handlerType is null) {
            Logger.Warning("No handler registered for result type: {0}",
                Logger.Args(resultType.Name));

            ProcessNextResult();
            
            return;
        }

        try {
            _isExecuting = true;

            // Create the handler actor, tell them to initialize with the given context, then execute.
            var props = Props.Create(handlerType);
            var handlerActor = Context.ActorOf(props, $"Handler_{resultType.Name}_{Guid.NewGuid():N}");

            handlerActor.Tell(new CHARACTER_103_PROTOCOL.MSG_INITIALIZEHANDLER { Context = _context, Result = result });
            var executeMessage = new CHARACTER_103_PROTOCOL.MSG_EXECUTEHANDLER();
            var executeTimeout = TimeSpan.FromMilliseconds(RESULT_HANDLER_TIMEOUT_MS);

            handlerActor.Ask<CHARACTER_103_PROTOCOL.MSG_RESULTEXECUTED>(executeMessage, executeTimeout)
                .ContinueWith(t => new CHARACTER_103_PROTOCOL.MSG_RESULTHANDLERCOMPLETED {
                    Success = t.IsCompletedSuccessfully && t.Result?.Success == true,
                    ResultType = resultType
                })
                .PipeTo(Self);
        }
        catch (Exception ex) {
            _isExecuting = false;
            _allSuccessful = false;
            Logger.Error("Failed to create/execute handler for result type {0}: {1}",
                Logger.Args(resultType.Name, ex.Message));

            ProcessNextResult();
        }
    }

    private void SendFinalReply(bool success) {
        _replyTo?.Tell(new CHARACTER_103_PROTOCOL.MSG_RESULTEXECUTED { Success = success });
        Context.Stop(Self);
    }

}