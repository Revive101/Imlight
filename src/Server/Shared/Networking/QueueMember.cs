/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

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
        public ushort SessionId { get; }
        
        /// <summary>
        /// The IActorRef.
        /// </summary>
        public IActorRef ActorRef { get; }
        
        /// <summary>
        /// The INetworkMessage that will be sent back to the player when they leave the queue.
        /// </summary>
        public INetworkMessage EndMessage { get; }
        
        public QueueMember(ushort sessionId, IActorRef actorRef, INetworkMessage endMessage)
        {
            SessionId = sessionId;
            ActorRef = actorRef ?? throw new ArgumentNullException(nameof(actorRef));
            EndMessage = endMessage ?? throw new ArgumentNullException(nameof(endMessage));
        }
    }
}