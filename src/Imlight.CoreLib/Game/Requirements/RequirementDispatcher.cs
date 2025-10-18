/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * REQUIREMENT SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Dispatches requirement evaluation across different contexts (zone triggers, quest checks, interactions)
 * providing a centralized system for validating player state against game conditions.
 * 
 * USAGE EXAMPLE:
 * var meetsRequirements = RequirementDispatcher.EvaluateRequirements(
 *     requirements: questTemplate.m_requirements,
 *     context: new QuestRequirementContext(playerRef, playerObj, questName)
 * );
 * 
 * NOTE:
 * Requirements are evaluated using individual handlers that inherit from BaseRequirementHandler<T>.
 * Handlers are automatically discovered and registered at startup using reflection.
 * The system supports AND/OR logic and NOT inversion through the base Requirement properties.
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 09/16/2025
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Type = System.Type;

namespace Imlight.CoreLib.Game.Requirements;

/// <summary>
/// Dispatches requirement evaluation across different contexts (zone triggers, quest checks, interactions)
/// </summary>
public static class RequirementDispatcher {

    private static readonly Dictionary<Type, MethodInfo> s_requirementHandlers = [];

    // ctor
    static RequirementDispatcher()
        => RegisterRequirementHandlers();

    /// <summary>
    /// Evaluates a RequirementList against the provided context.
    /// </summary>
    /// <param name="requirements">The requirement list to evaluate</param>
    /// <param name="context">The context providing player and game state information</param>
    /// <returns>True if requirements are met based on their logical operators, false otherwise</returns>
    public static bool EvaluateRequirements(RequirementList requirements, IRequirementContext context) {
        if (requirements?.m_requirements == null || requirements.m_requirements.Count == 0) {
            return true;
        }

        var andResults = new List<bool>();
        var orResults = new List<bool>();

        foreach (var requirement in requirements.m_requirements) {
            if (requirement == null) {
                continue;
            }

            var requirementMet = EvaluateIndividualRequirement(requirement, context);

            // Apply NOT operator if specified
            if (requirement.m_applyNOT) {
                requirementMet = !requirementMet;
            }

            switch (requirement.m_operator) {
                case Operator.ROP_AND:
                    andResults.Add(requirementMet);
                    continue;
                case Operator.ROP_OR:
                    orResults.Add(requirementMet);
                    continue;
                default:
                    // Default to AND for unknown operators
                    andResults.Add(requirementMet);
                    continue;
            }
        }

        // Evaluate AND requirements: all must be true.
        var andResult = andResults.Count == 0 || andResults.All(result => result);

        // Evaluate OR requirements: at least one must be true.
        var orResult = orResults.Count == 0 || orResults.Any(result => result);

        // Both AND and OR groups must pass for the overall result to be true.
        return andResult && orResult;
    }

    /// <summary>
    /// Evaluates a single requirement against the provided context.
    /// </summary>
    /// <param name="requirement">The individual requirement to evaluate</param>
    /// <param name="context">The context providing player and game state information</param>
    /// <returns>True if the requirement is met, false otherwise</returns>
    public static bool EvaluateIndividualRequirement(Requirement requirement, IRequirementContext context) {
        var requirementType = requirement.GetType();
        var handlerType = FindHandlerForRequirement(requirementType, context);

        if (handlerType == null) {
            Logger.Warning("No handler found for requirement type: {0}",
                Logger.Args(requirementType.Name));

            return false;
        }

        try {
            var handler = Activator.CreateInstance(handlerType) as IRequirementHandler;
            handler.Initialize(context, requirement);  // Pass the specific requirement

            return handler?.Evaluate(context) ?? false;
        }
        catch (Exception ex) {
            Logger.Error("Error evaluating requirement {0}: {1}",
                Logger.Args(requirementType.Name, ex.Message));

            return false;
        }
    }

    /// <summary>
    /// Finds the appropriate handler type for a given requirement type.
    /// </summary>
    internal static Type FindHandlerForRequirement(Type requirementType, IRequirementContext context) {
        foreach (var kv in s_requirementHandlers) {
            var handlerType = kv.Key;
            var shouldAttachMethod = kv.Value;

            // Call the static ShouldAttachToContext(context).
            var shouldAttach = (bool) shouldAttachMethod.Invoke(null, [context]);
            if (shouldAttach) {
                // If it's an open generic, close it on the actual requirementType.
                if (handlerType.IsGenericTypeDefinition) {
                    return handlerType.MakeGenericType(requirementType);
                }

                return handlerType;
            }
        }

        return null;
    }

    private static void RegisterRequirementHandlers() {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        var handlerTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => {
                if (!t.IsClass || t.IsAbstract) {
                    return false;
                }

                if (t.IsGenericTypeDefinition) {
                    var genericParams = t.GetGenericArguments();
                    if (genericParams.Length != 1) {
                        return false;
                    }

                    if (t.BaseType?.IsGenericType == true &&
                        t.BaseType.GetGenericTypeDefinition() == typeof(BaseRequirementHandler<>)) {
                        return true;
                    }
                }
                else {
                    var baseType = t.BaseType;
                    if (baseType?.IsGenericType == true &&
                        baseType.GetGenericTypeDefinition() == typeof(BaseRequirementHandler<>)) {
                        return true;
                    }
                }

                return false;
            });

        foreach (var type in handlerTypes) {
            var shouldAttachMethod = type.GetMethod(
                "ShouldAttachToContext",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy
            );

            if (shouldAttachMethod != null) {
                s_requirementHandlers.Add(type, shouldAttachMethod);
            }
            else {
                Logger.Warning("Could not find ShouldAttachToContext method on: {0}",
                    Logger.Args(type.FullName));
            }
        }

        Logger.Information("Registered {0} requirement handlers",
            Logger.Args(s_requirementHandlers.Count));
    }

}