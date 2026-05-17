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
