/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.CoreLib.Game.Zone.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Components;

/// <summary>
/// Interface that defines a component's ability to determine if it should be added to an entity
/// </summary>
public interface IComponentFactory {

    static abstract bool ShouldAttachToEntity(CoreTemplate template);

}

/// <summary>
/// Registry for all available component types
/// </summary>
public static class ComponentRegistry {

    private static readonly Dictionary<Type, MethodInfo> s_componentFactories = [];
    
    static ComponentRegistry() {
        var componentTypes = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && 
                       !t.IsInterface && 
                       typeof(IComponentFactory).IsAssignableFrom(t) &&
                       typeof(BaseZoneComponent).IsAssignableFrom(t));

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

    public static IReadOnlyDictionary<Type, MethodInfo> GetRegisteredComponents() 
        => s_componentFactories;

}