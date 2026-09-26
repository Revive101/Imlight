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
 * Last Updated: 09/26/2026
 */

using System;
using System.Collections.Generic;
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

    protected ReceiveProtocolDispatcher() {
        MessageHandlers = MessageHandlerTable.HandlersOf(GetType());
        ConfigureReceivers();
    }

    protected virtual void ConfigureReceivers() => Receive<object>(message => {
        var handler = MessageHandlerTable.DispatcherFor(GetType(), message.GetType());
        if (handler is null) {
            Unhandled(message);
            return;
        }

        handler(this, message);
    });

}
