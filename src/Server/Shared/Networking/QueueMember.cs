using System;
using Akka.Actor;
using WizUnraveler.DML;

namespace Imlight.Server.Shared.Networking
{
    public class QueueMember
    {
        /// <summary>
        /// The unique session ID of this queue member.
        /// </summary>
        public ushort SessionIO { get; }
        
        /// <summary>
        /// The IActorRef.
        /// </summary>
        public IActorRef ActorRef { get; }
        
        /// <summary>
        /// The INetworkMessage that will be sent back to the player when they leave the queue.
        /// </summary>
        public INetworkMessage EndMessage { get; }
        
        public QueueMember(ushort sessionIo, IActorRef actorRef, INetworkMessage endMessage)
        {
            SessionIO = sessionIo;
            ActorRef = actorRef ?? throw new ArgumentNullException(nameof(actorRef));
            EndMessage = endMessage ?? throw new ArgumentNullException(nameof(endMessage));
        }
    }
}