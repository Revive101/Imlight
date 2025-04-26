/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Imlight.Common;
using Imlight.CoreLib.Game.Zone.Core;

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
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        var componentTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => {
                if (!t.IsClass || t.IsAbstract) {
                    return false;
                }

                if (t.IsGenericTypeDefinition) {
                    // Get the generic type parameter constraints.
                    var genericParams = t.GetGenericArguments();
                    if (genericParams.Length != 1) {
                        return false;
                    }

                    var param = genericParams[0];
                    var constraints = param.GetGenericParameterConstraints();

                    // Check if it inherits from BaseResultHandler<>
                    if (t.BaseType.IsGenericType
                        && t.BaseType.GetGenericTypeDefinition() == typeof(BaseResultHandler<>)) {
                        return true;
                    }
                }

                return false;
            });

        foreach (var type in componentTypes) {
            var shouldAttachMethod = type.GetMethod(
                "ShouldAttachToEntity",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy
            );

            if (shouldAttachMethod != null) {
                s_componentFactories.Add(type, shouldAttachMethod);
            }
            else {
                Logger.Warning("Could not find ShouldAttachToEntity method on: {0}",
                    Logger.Args(type.FullName));
            }
        }

        Logger.Information("Registered {0} result handlers", Logger.Args(s_componentFactories.Count));
    }

    public static IReadOnlyDictionary<Type, MethodInfo> GetRegisteredResultHandlers()
        => s_componentFactories;

}