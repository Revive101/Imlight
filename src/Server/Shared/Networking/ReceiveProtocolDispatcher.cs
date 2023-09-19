/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Akka.Actor;
using Imlight.Common.DML;
using Imlight.Common.Utilities;

namespace Imlight.Server.Shared.Networking;

/// <summary>
/// An extension of a ReceiveActor that allows for receiving INetworkRecords directly to method attributes.
/// </summary>
public class ReceiveProtocolDispatcher : ReceiveActor
{
    public Dictionary<System.Type, MethodInfo> MessageHandlers { get; private set; }

    protected ReceiveProtocolDispatcher()
    {
        SetMessageHandlers();
        ConfigureReceivers();
    }

    protected virtual void ConfigureReceivers()
    {
        Receive<INetworkMessage>(message =>
        {
            // Find the method that handles this message type
            if (MessageHandlers.TryGetValue(message.GetType(), out var method))
            {
                // Invoke the method with the message
                method.Invoke(this, new object[] { message });
            }
            else
            {
                // No handler for this message type
                Unhandled(message);
            }
        });
        Receive<IServerMessage>(message =>
        {
            // Find the method that handles this message type
            if (MessageHandlers.TryGetValue(message.GetType(), out var method))
            {
                // Invoke the method with the message
                method.Invoke(this, new object[] { message });
            }
            else
            {
                // No handler for this message type
                Unhandled(message);
            }
        });
    }
        
    private void SetMessageHandlers()
    {
        MessageHandlers = new Dictionary<System.Type, MethodInfo>();

        // Get all methods in this actor with a message handling attribute
        var methods = this
            .GetType()
            .GetMethods(BindingFlags.Instance 
                        | BindingFlags.Public 
                        | BindingFlags.NonPublic
                        | BindingFlags.FlattenHierarchy)
            .Where(method => method.GetCustomAttributes<MessageHandlerAttribute>().Any());

        foreach (var method in methods)
        {
            var paramType = method.GetParameters()[0].ParameterType;
            MessageHandlers.Add(paramType, method);
        }
    }
}