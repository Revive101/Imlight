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