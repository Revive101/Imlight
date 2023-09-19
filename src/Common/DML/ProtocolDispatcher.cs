/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.Common.DML;

public static class ProtocolDispatcher
{
    private static readonly Dictionary<byte, INetworkProtocol> Protocols = new();

    static ProtocolDispatcher()
    {
        // Get any class that inherits from INetworkProtocol.
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => typeof(INetworkProtocol).IsAssignableFrom(p) && !p.IsInterface);
        
        // Iterate through each type. get the ServiceId field and add it to the dictionary.
        foreach (var type in types)
        {
            var instance = (INetworkProtocol)Activator.CreateInstance(type);
            var serviceIdProperty = type.GetProperty(nameof(INetworkProtocol.ServiceId));
        
            if (serviceIdProperty != null)
            {
                var serviceId = (byte)serviceIdProperty.GetValue(instance)!;
                Protocols.Add(serviceId, instance);
            }
        }
    }

    public static INetworkProtocol Dispatch(byte serviceId)
    {
        if (!Protocols.ContainsKey(serviceId))
            throw new InvalidOperationException($"No protocol by service ID [{serviceId}] found!");

        return Protocols[serviceId];
    }
}