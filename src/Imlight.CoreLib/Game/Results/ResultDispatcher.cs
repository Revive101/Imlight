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
using Imlight.CoreLib.Game.Results.Contexts;
using Imlight.CoreLib.Shared.Packets;
using Type = System.Type;

namespace Imlight.CoreLib.Game.Results;

/// <summary>
/// Dispatches result execution across different contexts (zone triggers, quest completion, goal completion)
/// </summary>
public static class ResultDispatcher {

    private static readonly Dictionary<Type, MethodInfo> s_resultHandlers = [];

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
        var context = new GenericResultContext(results, playerRef, playerObj, replyTo, zoneActor, questName, goalName, triggerName);
        var executor = CreateExecutorInstance(actorContext, context);

        executor.Tell(new CHARACTER_103_PROTOCOL.MSG_EXECUTERESULTS());
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