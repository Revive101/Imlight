/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * ZONE COMPONENT REGISTRY 
 * ========================================================================
 * 
 * PURPOSE:
 * Provides automatic discovery and registration of zone entity components
 * through reflection, enabling component-based architecture for game objects.
 * 
 * USAGE EXAMPLE:
 * var components = ZoneEntityComponentRegistry.GetRegisteredComponents();
 * foreach (var (componentType, shouldAttachMethod) in components) {
 *     var shouldAttach = (bool)shouldAttachMethod.Invoke(null, [template]);
 *     if (shouldAttach) {
 *         AddComponent(componentType);
 *     }
 * }
 * 
 * NOTE:
 * Uses System.Reflection to scan assemblies for component types at startup.
 * Components must implement IComponentFactory and inherit from ZoneEntityComponent.
 *
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;

namespace Imlight.CoreLib.Game.Zone.Core;

/// <summary>
/// Interface that defines a component's ability to determine if it should be added to an entity
/// </summary>
internal interface IComponentFactory {

    /// <summary>
    /// Determines if the component should be attached to the entity
    /// </summary>
    /// <param name="template">The template that the component is being attached to</param>
    /// <returns>True if the component should be attached, false otherwise</returns>
    static abstract bool ShouldAttachToEntity(CoreTemplate template);

}

/// <summary>
/// Registry for all available component types
/// </summary>
internal static class ZoneEntityComponentRegistry {

    private static readonly Dictionary<System.Type, MethodInfo> s_componentFactories = [];
    
    static ZoneEntityComponentRegistry() {
        var componentTypes = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && 
                       !t.IsInterface && 
                       typeof(IComponentFactory).IsAssignableFrom(t) &&
                       typeof(ZoneEntityComponent).IsAssignableFrom(t));

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

        Logger.Information("Registered {0} component factories", Logger.Args(s_componentFactories.Count));
    }

    public static IReadOnlyDictionary<System.Type, MethodInfo> GetRegisteredComponents() 
        => s_componentFactories;

}