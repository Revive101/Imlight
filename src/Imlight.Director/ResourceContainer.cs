/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * RESOURCE DISCOVERY AND INITIALIZATION SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Automatically discovers and initializes all Imlight resource singleton classes
 * without requiring manual registration of each resource.
 * 
 * USAGE EXAMPLE:
 * var resourceContainer = new ResourceContainer();
 * // All resources are now discovered and initialized
 * 
 * NOTE:
 * This system uses System.Reflection and Expression compilation which can be
 * performance-intensive during startup. Resources must inherit from either 
 * RootSingleResourceSingleton<> or RootDirectoryResourceSingleton<> to be discovered.
 *
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.Director;

/// <summary>
/// Discovers and initializes all Imlight resources through reflection.
/// </summary>
/// <remarks>
/// Uses reflection to find classes inheriting from RootSingleResourceSingleton
/// and RootDirectoryResourceSingleton, then instantiates and initializes each one.
/// </remarks>
internal class ResourceContainer {

    internal ResourceContainer() {
        var baseTypes = new Type[] { 
            typeof(RootSingleResourceSingleton<>), 
            typeof(RootDirectoryResourceSingleton<>) 
        };
        var assembly = baseTypes[0].Assembly;

        foreach (var baseType in baseTypes) {
            foreach (var derivedType in assembly.GetTypes()
                .Where(t => !t.IsAbstract &&
                            !t.IsInterface &&
                            t.BaseType != null &&
                            t.BaseType.IsGenericType &&
                            t.BaseType.GetGenericTypeDefinition() == baseType)) {
                var instance = Activator.CreateInstance(derivedType);

                // Get a delegate to the method using an expression.
                var methodDelegate = CreateDelegate<Action>(derivedType, "Initialize");
                methodDelegate?.Invoke();
            }
        }
    }

    private static TDelegate CreateDelegate<TDelegate>(Type type, string methodName)
        where TDelegate : class {
        var methodInfo = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);

        if (methodInfo == null) {
            return null;
        }

        var instance = Expression.Parameter(type, "instance");
        var methodCall = Expression.Call(instance, methodInfo);

        return Expression.Lambda<TDelegate>(methodCall, instance).Compile();
    }
    
}
