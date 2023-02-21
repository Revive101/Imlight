using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WizUnraveler.DML;

namespace Imlight.Net
{
    public abstract class ActorMessageService : ReceiveActor
    {
        public static readonly string ASK_IDENTIFY = "IDENTIFY_YOURSELF";

        /// <summary>
        /// A HashSet of the messages this service is capable of handling.
        /// </summary>
        public abstract HashSet<Type> Messages { get; init; }
        protected IActorRef SessionActorRef { get; init; }

        public ActorMessageService()
        {
            ConfigureReceivers();
        }

        protected abstract void ConfigureReceivers();
    }
}
