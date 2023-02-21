using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Imlight.Common;

namespace Imlight.Net
{
    public abstract class ActorServiceFactory : ReceiveActor
    {
        public const string UNLOADED_SERVICES_ASK = "AskForUnloadedMessageServices";
        public const string LOADED_SERVICES_ASK = "AskForLoadedMessageServices";

        /// <summary>
        /// Configures the available actor receivers.
        /// </summary>
        protected abstract void ConfigureReceivers();

        /// <summary>
        /// Returns a HashSet of ActorMessageServices to give to a SessionActor before the session is loaded.
        /// </summary>
        /// <returns></returns>
        protected abstract void GetUnloadedActorMessageServices();

        /// <summary>
        /// Returns a HashSet of ActorMessageServices to give to a SessionActor after the session is loaded.
        /// </summary>
        /// <returns></returns>
        protected abstract void GetLoadedActorMessageServices();
    }
}
