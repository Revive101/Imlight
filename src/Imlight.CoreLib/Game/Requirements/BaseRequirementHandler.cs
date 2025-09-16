/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Requirements;

/// <summary>
/// Interface that defines a requirement handler's ability to determine if it should be used for a given context
/// </summary>
public interface IRequirementHandlerFactory {

    /// <summary>
    /// Determines if the requirement handler should be used for the given requirement context
    /// </summary>
    /// <param name="context">The context containing requirements to check</param>
    /// <returns>True if the handler should be used, false otherwise</returns>
    static abstract bool ShouldAttachToContext(IRequirementContext context);

}

/// <summary>
/// Interface that defines a requirement handler's ability to evaluate a requirement
/// </summary>
public interface IRequirementHandler {

    /// <summary>
    /// Evaluates the requirement.
    /// </summary>
    /// <param name="context">The context containing all evaluation information.</param>
    bool Evaluate(IRequirementContext context);

}

/// <summary>
/// Base class for all requirement handlers.
/// </summary>
public abstract class BaseRequirementHandler<T> : IRequirementHandler, IRequirementHandlerFactory
    where T : Requirement {

    protected IRequirementContext CallingContext { get; private set; }

    private T _requirement;
    protected T Requirement => _requirement ??= CallingContext?.GetRequirements()?
        .FirstOrDefault(x => x is not null && x.GetType() == typeof(T)) as T;

    public static bool ShouldAttachToContext(IRequirementContext context)
        => context
            .GetRequirements()?
            .Any(x =>
                 x is not null &&
                 x.GetType() == typeof(T)) ?? false;

    public abstract bool Evaluate(IRequirementContext context);

    /// <summary>
    /// Initializes the handler with the calling context
    /// </summary>
    /// <param name="context">The requirement context</param>
    public void Initialize(IRequirementContext context) {
        CallingContext = context;
    }

}