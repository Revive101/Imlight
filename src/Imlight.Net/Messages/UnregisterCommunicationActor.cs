using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Net.Messages
{
    public class UnregisterCommunicationActor
    {
        public IActorRef ActorReference { get; init; }

        public UnregisterCommunicationActor(IActorRef actorReference)
        {
            ActorReference = actorReference;
        }
    }
}
