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
    
    /// <summary>
    /// Initializes the handler with the calling context
    /// </summary>
    /// <param name="context">The result context</param>
    void Initialize(IResultContext context, Result result);

}

/// <summary>
/// Base class for all result handlers.
/// </summary>
public abstract class BaseResultHandler<T> : ReceiveProtocolDispatcher, IResultHandler, IResultHandlerFactory
    where T : Result {
    
    protected IResultContext CallingContext { get; private set; }
    protected T Result { get; private set; }

    public static bool ShouldAttachToContext(IResultContext context)
        => context
            .GetResults()?
            .Any(x =>
                 x is not null &&
                 x.GetType() == typeof(T)) ?? false;

    public abstract bool Execute(IResultContext context);

    public void Initialize(IResultContext context, Result result) {
        CallingContext = context;
        Result = result as T;
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_INITIALIZEHANDLER))]
    public void HandleInitialize(CHARACTER_103_PROTOCOL.MSG_INITIALIZEHANDLER message) {
        Initialize((IResultContext)message.Context, (Result)message.Result);
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_EXECUTEHANDLER))]
    public virtual void HandleExecute(CHARACTER_103_PROTOCOL.MSG_EXECUTEHANDLER message) {
        var executeSuccess = Execute(CallingContext);
        var rsp = new CHARACTER_103_PROTOCOL.MSG_RESULTEXECUTED {
            Success = executeSuccess
        };
        Sender.Tell(rsp);

        // Self-destruct after replying so handler actors don't accumulate
        // as orphan children of the ResultExecutorActor.
        Context.Stop(Self);
    }

}