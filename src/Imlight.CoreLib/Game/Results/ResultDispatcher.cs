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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Requirements;
using Imlight.CoreLib.Game.Requirements.Contexts;
using Imlight.CoreLib.Game.Results.Contexts;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using Type = System.Type;

namespace Imlight.CoreLib.Game.Results;

/// <summary>
/// Dispatches result execution across different contexts (zone triggers, quest completion, goal completion)
/// </summary>
public static class ResultDispatcher {

    private static readonly Dictionary<Type, MethodInfo> s_resultHandlers = [];
    private const float QUERY_WIZARD_TIMEOUT_SECONDS = 5.0f;

    static ResultDispatcher()
        => RegisterResultHandlers();

    /// <summary>
    /// Executes a ResultList by creating a new executor instance
    /// </summary>
    /// <param name="actorContext">The actor context to create the executor in</param>
    /// <param name="results">The result list to execute</param>
    /// <param name="playerRef">The player actor reference</param>
    /// <param name="playerObj">The player object</param>
    /// <param name="replyTo">Actor to reply to when execution is complete</param>
    /// <param name="zoneActor">The zone actor reference (if applicable)</param>
    /// <param name="questName">The quest name (if applicable)</param>
    /// <param name="goalName">The goal name (if applicable)</param>
    /// <param name="triggerName">The trigger name (if applicable)</param>
    public static void ExecuteResults(IActorContext actorContext,
                                     ResultList results,
                                     IActorRef playerRef,
                                     CoreObject playerObj,
                                     IActorRef replyTo = null,
                                     IActorRef zoneActor = null,
                                     string questName = null,
                                     string goalName = null,
                                     string triggerName = null) {
        // Results carry their own requirements in the data; evaluate them here, before the executor
        // is created, so an executor actor only ever handles results whose requirements were met.
        var filteredResults = FilterResultsByRequirements(
            results, playerRef, playerObj, zoneActor, questName, goalName, triggerName);
        var context = new GenericResultContext(filteredResults, playerRef, playerObj, replyTo, zoneActor, questName, goalName, triggerName);
        var executor = CreateExecutorInstance(actorContext, context);

        executor.Tell(new CHARACTER_103_PROTOCOL.MSG_EXECUTERESULTS());
    }

    private static ResultList FilterResultsByRequirements(ResultList results,
                                                           IActorRef playerRef,
                                                           CoreObject playerObj,
                                                           IActorRef zoneActor,
                                                           string questName,
                                                           string goalName,
                                                           string triggerName) {
        if (results?.m_results is null || results.m_results.Count == 0
            || !results.m_results.Any(r => r?.m_requirements is not null)) {
            return results;
        }

        // Query the wizard once; requirement handlers evaluate against it.
        Wizard wizard = null;
        if (playerRef is not null) {
            try {
                wizard = playerRef
                    .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(
                        new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD(),
                        TimeSpan.FromSeconds(QUERY_WIZARD_TIMEOUT_SECONDS)).Result?.Wizard;
            }
            catch (Exception ex) {
                Logger.Error("Failed to query wizard for result requirements: {0}", Logger.Args(ex.Message));
            }
        }

        var passingResults = new ResultList {
            m_results = []
        };
        foreach (var result in results.m_results) {
            if (result is null) {
                continue;
            }

            if (result.m_requirements is not null) {
                var requirementContext = new GenericRequirementContext(
                    requirements: result.m_requirements,
                    playerRef: playerRef,
                    playerObj: playerObj,
                    wizard: wizard,
                    zoneRef: zoneActor,
                    questName: questName,
                    goalName: goalName,
                    triggerName: triggerName
                );
                if (!RequirementDispatcher.EvaluateRequirements(result.m_requirements, requirementContext)) {
                    continue;
                }
            }

            passingResults.m_results.Add(result);
        }

        return passingResults;
    }

    /// <summary>
    /// Finds the appropriate handler type for a given result type (exposed for ResultExecutorActor)
    /// </summary>
    internal static Type FindHandlerForResult(Type resultType, IResultContext context) {
        foreach (var kv in s_resultHandlers) {
            var handlerType = kv.Key;
            var shouldAttachMethod = kv.Value;

            // Call the static ShouldAttachToContext(context)
            var shouldAttach = (bool) shouldAttachMethod.Invoke(null, [context]);
            if (!shouldAttach) {
                continue;
            }

            // If it's an open generic, close it on the actual resultType
            if (handlerType.IsGenericTypeDefinition) {
                return handlerType.MakeGenericType(resultType);
            }

            // For concrete handlers, verify the handler's generic argument matches the result type.
            if (handlerType.BaseType?.IsGenericType == true) {
                var handlerResultType = handlerType.BaseType.GetGenericArguments()[0];
                if (handlerResultType == resultType) {
                    return handlerType;
                }
            }
        }

        return null;
    }

    private static IActorRef CreateExecutorInstance(IActorContext actorContext, IResultContext resultContext)
        => actorContext.ActorOf(
            Props.Create(() => new ResultExecutorActor(resultContext)),
            $"ResultExec_{Guid.NewGuid():N}"
        );

    private static void RegisterResultHandlers() {
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
                        t.BaseType.GetGenericTypeDefinition() == typeof(BaseResultHandler<>)) {
                        return true;
                    }
                }
                else {
                    var baseType = t.BaseType;
                    if (baseType?.IsGenericType == true &&
                        baseType.GetGenericTypeDefinition() == typeof(BaseResultHandler<>)) {
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
                s_resultHandlers.Add(type, shouldAttachMethod);
            }
            else {
                Logger.Warning("Could not find ShouldAttachToContext method on: {0}",
                    Logger.Args(type.FullName));
            }
        }

        Logger.Information("Registered {0} result handlers", Logger.Args(s_resultHandlers.Count));
    }

}