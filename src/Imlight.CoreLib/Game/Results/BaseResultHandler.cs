/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Results;

/// <summary>
/// Interface that defines a result handler's ability to determine if it should be used for a given context
/// </summary>
public interface IResultHandlerFactory {

    /// <summary>
    /// Determines if the result handler should be used for the given result context
    /// </summary>
    /// <param name="context">The context containing results to check</param>
    /// <returns>True if the handler should be used, false otherwise</returns>
    static abstract bool ShouldAttachToContext(IResultContext context);

}

/// <summary>
/// Interface that defines a result handler's ability to execute a result
/// </summary>
public interface IResultHandler {

    /// <summary>
    /// Executes the result.
    /// </summary>
    /// <param name="context">The context containing all execution information.</param>
    bool Execute(IResultContext context);

}

/// <summary>
/// Base class for all result handlers.
/// </summary>
public abstract class BaseResultHandler<T> : ReceiveProtocolDispatcher, IResultHandler, IResultHandlerFactory
    where T : Result {

    protected IResultContext CallingContext { get; private set; }

    private T _result;
    protected T Result => _result ??= CallingContext?.GetResults()?
        .FirstOrDefault(x => x is not null && x.GetType() == typeof(T)) as T;

    public static bool ShouldAttachToContext(IResultContext context)
        => context
            .GetResults()?
            .Any(x =>
                 x is not null &&
                 x.GetType() == typeof(T)) ?? false;

    public abstract bool Execute(IResultContext context);

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_INITIALIZEHANDLER))]
    public void HandleInitialize(CHARACTER_103_PROTOCOL.MSG_INITIALIZEHANDLER message) {
        CallingContext = (IResultContext)message.Context;
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_EXECUTEHANDLER))]
    public virtual void HandleExecute(CHARACTER_103_PROTOCOL.MSG_EXECUTEHANDLER message) {
        var executeSuccess = Execute(CallingContext);
        var rsp = new CHARACTER_103_PROTOCOL.MSG_RESULTEXECUTED {
            Success = executeSuccess
        };
        Sender.Tell(rsp);   
    }

}