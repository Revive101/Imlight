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
    
    /// <summary>
    /// Initializes the handler with the calling context
    /// </summary>
    /// <param name="context">The requirement context</param>
    void Initialize(IRequirementContext context, Requirement requirement);

}

/// <summary>
/// Base class for all requirement handlers.
/// </summary>
public abstract class BaseRequirementHandler<T> : IRequirementHandler, IRequirementHandlerFactory
    where T : Requirement {
    
    protected IRequirementContext CallingContext { get; private set; }
    protected T Requirement { get; private set; }

    public static bool ShouldAttachToContext(IRequirementContext context)
        => context
            .GetRequirements()?
            .Any(x => x is not null && x.GetType() == typeof(T)) ?? false;

    public abstract bool Evaluate(IRequirementContext context);

    public void Initialize(IRequirementContext context, Requirement requirement) {
        CallingContext = context;
        Requirement = requirement as T;
    }
    
}