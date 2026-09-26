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
 * RECEIVE PROTOCOL DISPATCHER
 * ========================================================================
 * 
 * PURPOSE:
 * Discovers [MessageHandler] methods once per receiving type and compiles one dispatcher per
 * (receiving type, message type) pair, shared by every instance of that type.
 * 
 * USAGE EXAMPLE:
 * var dispatch = MessageHandlerTable.DispatcherFor(receiver.GetType(), message.GetType());
 * dispatch?.Invoke(receiver, message);
 * 
 * NOTE:
 * Used by ReceiveProtocolDispatcher actors and by zone entity components, which are plain objects
 * hosted inside their entity actor. A message runs every handler whose declared type it is assignable
 * to, in discovery order.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Imlight.CoreLib.Shared.Networking;

internal static class MessageHandlerTable {

    private static readonly ConcurrentDictionary<Type, Dictionary<Type, MethodInfo>> s_handlersByReceiver = new();
    private static readonly ConcurrentDictionary<(Type Receiver, Type Message), Action<object, object>> s_dispatchers = new();

    public static Dictionary<Type, MethodInfo> HandlersOf(Type receiverType) {
        // Shared by every instance of the type; callers only read it.
        return s_handlersByReceiver.GetOrAdd(receiverType, Discover);
    }

    public static Action<object, object> DispatcherFor(Type receiverType, Type messageType)
        => s_dispatchers.GetOrAdd((receiverType, messageType), key => Build(key.Receiver, key.Message));

    private static Dictionary<Type, MethodInfo> Discover(Type receiverType) {
        var handlers = new Dictionary<Type, MethodInfo>();
        var methods = receiverType
            .GetMethods(BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic
                        | BindingFlags.FlattenHierarchy)
            .Where(method => method.GetCustomAttributes<MessageHandlerAttribute>().Any());

        foreach (var method in methods) {
            var type = method.GetCustomAttributes<MessageHandlerAttribute>().First().MessageType;
            handlers.Add(type, method);
        }

        return handlers;
    }

    private static Action<object, object> Build(Type receiverType, Type messageType) {
        var matching = HandlersOf(receiverType)
            .Where(kvp => kvp.Key.IsAssignableFrom(messageType))
            .Select(kvp => kvp.Value)
            .ToList();

        if (matching.Count == 0) {
            return null;
        }

        Action<object, object> combined = null;
        foreach (var method in matching) {
            var receiver = Expression.Parameter(typeof(object), "receiver");
            var message = Expression.Parameter(typeof(object), "message");
            var target = Expression.Convert(receiver, method.DeclaringType);
            var parameters = method.GetParameters();

            var call = parameters.Length == 0
                ? Expression.Call(target, method)
                : Expression.Call(target, method, Expression.Convert(message, parameters[0].ParameterType));
            combined += Expression.Lambda<Action<object, object>>(call, receiver, message).Compile();
        }

        return combined;
    }

}
