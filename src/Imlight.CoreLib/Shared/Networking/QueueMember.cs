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
 */

using System;
using Akka.Actor;
using Imcodec.MessageLayer;

namespace Imlight.CoreLib.Shared.Networking;

public class QueueMember {

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
    public IMessage EndMessage { get; }

    public QueueMember(ushort sessionId, IActorRef actorRef, IMessage endMessage) {
        SessionId = sessionId;
        ActorRef = actorRef ?? throw new ArgumentNullException(nameof(actorRef));
        EndMessage = endMessage ?? throw new ArgumentNullException(nameof(endMessage));
    }
    
}
