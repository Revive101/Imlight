/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
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
    internal static Type FindHandlerForResult(Type resultType)
        => s_resultHandlers.Keys.FirstOrDefault(handlerType => {
            if (handlerType.IsGenericTypeDefinition) {
                var genericParam = handlerType.GetGenericArguments()[0];
                var constraints = genericParam.GetGenericParameterConstraints();

                return constraints.All(c => c.IsAssignableFrom(resultType));
            }
            else {
                var baseType = handlerType.BaseType;
                if (baseType?.IsGenericType == true &&
                    baseType.GetGenericTypeDefinition() == typeof(BaseResultHandler<>)) {
                    var handlerResultType = baseType.GetGenericArguments()[0];

                    return handlerResultType == resultType;
                }
            }

            return false;
        });

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