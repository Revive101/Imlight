/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Results;

/// <summary>
/// Actor responsible for executing results asynchronously for a single activation instance.
/// Creates handler actors, executes all results, then self-destructs when complete.
/// </summary>
public class ResultExecutorActor(IResultContext context) : ReceiveProtocolDispatcher {

    private const uint RESULT_HANDLER_TIMEOUT_MS = 30000;

    private readonly IResultContext _context = context;
    private int _pendingResults;
    private bool _allSuccessful = true;
    private IActorRef _replyTo;

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

        _pendingResults = resultsList.Count;

        // Create handler actors for each result and execute them.
        foreach (var result in resultsList) {
            var resultType = result.GetType();
            var handlerType = ResultDispatcher.FindHandlerForResult(resultType);

            if (handlerType is null) {
                Logger.Warning("No handler registered for result type: {0}",
                    Logger.Args(resultType.Name));

                _pendingResults--;
                if (_pendingResults == 0) {
                    SendFinalReply(_allSuccessful);
                }

                continue;
            }

            try {
                // Create the handler actor, tell them to initialize with the given context, then execute.
                var props = Props.Create(handlerType);
                var handlerActor = Context.ActorOf(props, $"Handler_{resultType.Name}_{Guid.NewGuid():N}");

                handlerActor.Tell(new CHARACTER_103_PROTOCOL.MSG_INITIALIZEHANDLER { Context = _context });

                var executeMessage = new CHARACTER_103_PROTOCOL.MSG_EXECUTEHANDLER();
                var executeTimeout = TimeSpan.FromMilliseconds(RESULT_HANDLER_TIMEOUT_MS);
                var executeSuccess = handlerActor.Ask<CHARACTER_103_PROTOCOL.MSG_RESULTEXECUTED>(executeMessage, executeTimeout);

                if (executeSuccess.Result is null || !executeSuccess.Result.Success) {
                    _allSuccessful = false;
                }

                _pendingResults--;
                if (_pendingResults == 0) {
                    SendFinalReply(_allSuccessful);
                }
            }
            catch (Exception ex) {
                _allSuccessful = false;
                Logger.Error("Failed to create/execute handler for result type {0}: {1}",
                    Logger.Args(resultType.Name, ex.Message));

                _pendingResults--;
                if (_pendingResults == 0) {
                    SendFinalReply(_allSuccessful);
                }
            }
        }
    }

    private void SendFinalReply(bool success) {
        _replyTo?.Tell(new CHARACTER_103_PROTOCOL.MSG_RESULTEXECUTED { Success = success });
        Context.Stop(Self);
    }

}