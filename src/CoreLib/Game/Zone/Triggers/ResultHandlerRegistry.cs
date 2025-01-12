/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.CoreLib.Game.Zone.Components;
using Imlight.CoreLib.Game.Zone.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Triggers;

/// <summary>
/// Interface that defines a trigger's ability to determine if it should be added to an entity
/// </summary>
public interface IResultHandlerFactory {

    /// <summary>
    /// Determines if the trigger should be attached to the entity
    /// </summary>
    /// <param name="trigger">The trigger to check</param>
    /// <returns>True if the trigger should be attached, false otherwise</returns>
    static abstract bool ShouldAttachToEntity(ZoneTrigger trigger);

}

/// <summary>
/// Registry for all available trigger types
/// </summary>
public static class ResultHandlerRegistry {

    private static readonly Dictionary<Type, MethodInfo> s_componentFactories = [];
    
    static ResultHandlerRegistry() {
        var componentTypes = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && 
                       !t.IsInterface && 
                       typeof(IResultHandlerFactory).IsAssignableFrom(t) &&
                       typeof(BaseResultHandler).IsAssignableFrom(t));

        foreach (var componentType in componentTypes) {
            var shouldAttachMethod = componentType.GetMethod(
                "ShouldAttachToEntity", 
                BindingFlags.Public | BindingFlags.Static,
                [typeof(CoreTemplate)]
            );

            if (shouldAttachMethod != null) {
                s_componentFactories.Add(componentType, shouldAttachMethod);
            }
        }
    }

    public static IReadOnlyDictionary<Type, MethodInfo> GetRegisteredResultHandlers() 
        => s_componentFactories;

}