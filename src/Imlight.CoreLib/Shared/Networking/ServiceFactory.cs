/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Shared.Networking;

public abstract class ServiceFactory : ReceiveActor {
    protected abstract HashSet<Type> ServiceTypes { get; set; }

    public ServiceFactory() {
        ConfigureReceivers();
    }

    /// <summary>
    /// Configures the available actor receivers.
    /// </summary>
    private void ConfigureReceivers() {
        Receive<SERVICE_101_PROTOCOL.MSG_QUERYUNLOADEDSERVICES>(x
            => GetUnloadedActorMessageServices());
    }

    /// <summary>
    /// Returns a HashSet of ActorMessageServices to give to a SessionActor before the session is loaded.
    /// </summary>
    /// <returns></returns>
    private void GetUnloadedActorMessageServices() {
        var rsp = new SERVICE_101_PROTOCOL.MSG_SERVICESLIST() {
            Services = ServiceTypes.ToList()
        };

        Sender.Tell(rsp);
    }
}
