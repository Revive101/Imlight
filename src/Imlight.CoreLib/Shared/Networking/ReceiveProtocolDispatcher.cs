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
 * Provides a flexible message handling mechanism for network protocol 
 * dispatching using method attributes to route incoming messages.
 * 
 * USAGE EXAMPLE:
 * // Define a message handler method
 * [MessageHandler(typeof(SomeMessageType))]
 * private void HandleSomeMessage(SomeMessageType message) { }
 * 
 * NOTE:
 * - Extends Akka.NET ReceiveActor with dynamic message routing
 * - Uses reflection to map message types to handler methods
 * - Supports automatic message handler discovery
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Akka.Actor;

namespace Imlight.CoreLib.Shared.Networking;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class MessageHandlerAttribute(Type messageType) : Attribute {

    public Type MessageType { get; } = messageType;
    
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class InternalMessageHandlerAttribute(Type messageType) : MessageHandlerAttribute(messageType) { }

/// <summary>
/// An extension of a ReceiveActor that allows for receiving INetworkRecords directly to method attributes.
/// </summary>
public class ReceiveProtocolDispatcher : ReceiveActor {

    public Dictionary<Type, MethodInfo> MessageHandlers { get; private set; }

    private readonly Dictionary<Type, MethodInfo[]> _handlerCache = [];

    protected ReceiveProtocolDispatcher() {
        SetMessageHandlers();
        ConfigureReceivers();
    }

    protected virtual void ConfigureReceivers() => Receive<object>(message => {
        // Find the method that handles this message type
        var messageType = message.GetType();

        if (!_handlerCache.TryGetValue(messageType, out var handlers)) {
            handlers = MessageHandlers
                .Where(kvp => kvp.Key.IsAssignableFrom(messageType))
                .Select(kvp => kvp.Value)
                .ToArray();
            _handlerCache[messageType] = handlers;
        }

        if (handlers.Length == 0) {
            Unhandled(message);

            return;
        }

        // Invoke all methods that handle this message type
        foreach (var method in handlers) {
            var parameters = method.GetParameters();
            if (parameters.Length == 0) {
                method.Invoke(this, null);
            } else {
                method.Invoke(this, [message]);
            }
        }
    });

    private void SetMessageHandlers() {
        MessageHandlers = [];

        // Get all methods in this actor with a message handling attribute
        var methods = this
            .GetType()
            .GetMethods(BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic
                        | BindingFlags.FlattenHierarchy)
            .Where(method => method.GetCustomAttributes<MessageHandlerAttribute>().Any());

        foreach (var method in methods) {
            var type = method.GetCustomAttributes<MessageHandlerAttribute>().First().MessageType;
            MessageHandlers.Add(type, method);
        }
    }
    
}