/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.CoreLib.Game.Zone.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Components;

/// <summary>
/// Interface that defines a component's ability to determine if it should be added to an entity
/// </summary>
public interface IComponentFactory {

    bool ShouldAttachToEntity(CoreTemplate template);
    
}

/// <summary>
/// Registry for all available component types
/// </summary>
public static class ComponentRegistry {

    private static readonly List<Type> s_registeredComponents = [];

    static ComponentRegistry() {
        // Auto-discover and register all component types
        var componentTypes = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && !t.IsInterface 
                && typeof(IComponentFactory).IsAssignableFrom(t)
                && typeof(IZoneComponent).IsAssignableFrom(t));

        foreach (var type in componentTypes) {
            s_registeredComponents.Add(type);
        }
    }

    public static IEnumerable<Type> GetRegisteredComponents() => s_registeredComponents;
    
}