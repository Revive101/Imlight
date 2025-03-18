/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Akka.Actor;

namespace Imlight.CoreLib.Shared.Networking;

/// <summary>
/// An extension of a ReceiveActor that allows for receiving INetworkRecords directly to method attributes.
/// </summary>
public class ReceiveProtocolDispatcher : ReceiveActor {

    public Dictionary<System.Type, MethodInfo> MessageHandlers { get; private set; }

    protected ReceiveProtocolDispatcher() {
        SetMessageHandlers();
        ConfigureReceivers();
    }

    protected virtual void ConfigureReceivers() => Receive<object>(message => {
        // Find the method that handles this message type
        var messageType = message.GetType();
        var handler = MessageHandlers
            .Where(kvp => kvp.Key.IsAssignableFrom(messageType))
            .Select(kvp => kvp.Value);

        if (!handler.Any()) {
            Unhandled(message);
        }

        // Invoke all methods that handle this message type
        foreach (var method in handler) {
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
