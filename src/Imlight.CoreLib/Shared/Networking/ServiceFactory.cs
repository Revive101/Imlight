/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * SERVICE FACTORY
 * ========================================================================
 * 
 * PURPOSE:
 * Provides an abstract base for creating and managing actor-based 
 * message services in a distributed networking system.
 * 
 * USAGE EXAMPLE:
 * // Inherit and define available service types
 * public class MyServiceFactory : ServiceFactory {
 *     protected override HashSet<Type> ServiceTypes { get; set; } = new HashSet<Type> {
 *         typeof(LoginService),
 *         typeof(ChatService)
 *     };
 * }
 * 
 * NOTE:
 * - Abstract base class for service type registration
 * - Supports querying unloaded actor message services
 * - Used in `SessionActor` initialization process
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Collections.Generic;
using Akka.Actor;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Shared.Networking;

public abstract class ServiceFactory : ReceiveActor {

    protected abstract HashSet<Type> ServiceTypes { get; set; }

    public ServiceFactory() => ConfigureReceivers();

    /// <summary>
    /// Configures the available actor receivers.
    /// </summary>
    private void ConfigureReceivers() 
        => Receive<SERVICE_101_PROTOCOL.MSG_QUERYUNLOADEDSERVICES>(x
            => GetUnloadedActorMessageServices());

    /// <summary>
    /// Returns a HashSet of ActorMessageServices to give to a SessionActor before the session is loaded.
    /// </summary>
    /// <returns></returns>
    private void GetUnloadedActorMessageServices() {
        var rsp = new SERVICE_101_PROTOCOL.MSG_SERVICESLIST() {
            Services = [.. ServiceTypes]
        };

        Sender.Tell(rsp);
    }

}
