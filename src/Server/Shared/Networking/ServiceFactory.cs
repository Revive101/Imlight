using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using Imlight.Server.Shared.Packets;

namespace Imlight.Server.Shared.Networking
{
    public abstract class ServiceFactory : ReceiveActor
    {
        protected abstract HashSet<Type> UnloadedServiceTypes { get; set; }
        protected abstract HashSet<Type> LoadedServiceTypes { get; set; }

        public ServiceFactory()
        {
            ConfigureReceivers();
        }

        /// <summary>
        /// Configures the available actor receivers.
        /// </summary>
        private void ConfigureReceivers()
        {
            Receive<SERVICE_101_PROTOCOL.MSG_QUERYUNLOADEDSERVICES>(x 
                => GetUnloadedActorMessageServices());
            Receive<SERVICE_101_PROTOCOL.MSG_QUERYLOADEDSERVICES>(x 
                =>GetLoadedActorMessageServices());
        }

        /// <summary> 
        /// Returns a HashSet of ActorMessageServices to give to a SessionActor before the session is loaded.
        /// </summary>
        /// <returns></returns>
        private void GetUnloadedActorMessageServices()
        {
            var rsp = new SERVICE_101_PROTOCOL.MSG_SERVICESLIST()
            {
                Services = UnloadedServiceTypes.ToList()
            };
            
            Sender.Tell(rsp);
        }

        /// <summary>
        /// Returns a HashSet of ActorMessageServices to give to a SessionActor after the session is loaded.
        /// </summary>
        /// <returns></returns>
        private void GetLoadedActorMessageServices()
        {
            var rsp = new SERVICE_101_PROTOCOL.MSG_SERVICESLIST()
            {
                Services = LoadedServiceTypes.ToList()
            };
            
            Sender.Tell(rsp);
        }
    }
}
