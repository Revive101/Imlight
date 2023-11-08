using System;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.Common.MessageLayer;

public class MessageDispatcher {
    private static readonly Dictionary<byte, MessageProtocol> _protocols = new();

    // ctor
    static MessageDispatcher() {
        // Get any type that implements MessageProtocol.
        var protocolTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(MessageProtocol).IsAssignableFrom(type) && !type.IsAbstract);

        // Create an instance of each type and add it to the dictionary.
        foreach (var protocolType in protocolTypes) {
            var protocol = Activator.CreateInstance(protocolType) as MessageProtocol;

            if (protocol is not null) {
                _protocols.Add(protocol.ServiceId, protocol);
            }
            else {
                throw new Exception($"Failed to create instance of {nameof(protocolType)}.");
            }
        }
    }

    /// <summary>
    /// Dispatches a message to the appropriate protocol based on the service ID and message ID.
    /// </summary>
    /// <param name="serviceId">The ID of the service.</param>
    /// <param name="messageid">The ID of the message.</param>
    /// <returns>The message that was dispatched.</returns>
    public static IMessage Dispatch(byte serviceId, byte messageid) {
        if (_protocols.TryGetValue(serviceId, out var protocol)) {
            return protocol.Dispatch(messageid);
        }
        else {
            throw new Exception($"No protocol found for service id {serviceId}.");
        }
    }
}
