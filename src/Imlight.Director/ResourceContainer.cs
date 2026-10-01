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
using System.Reflection;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.Director;

/// <summary>
/// Discovers and initializes all Imlight resources through reflection.
/// </summary>
/// <remarks>
/// Uses reflection to find classes inheriting from RootSingleResourceSingleton
/// and RootDirectoryResourceSingleton, then initializes their shared instances.
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
                // Startup and request handlers must use the same Lazy<T>. Constructing
                // a separate object here leaves Instance uninitialized and makes the
                // first request reload static resource tables (e.g. duplicate spells).
                var instanceProperty = derivedType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                var instance = instanceProperty?.GetValue(null);
                if (instance?.GetType() != derivedType)
                    throw new InvalidOperationException($"Resource {derivedType.Name} must declare its own singleton type.");
            }
        }
    }

}
