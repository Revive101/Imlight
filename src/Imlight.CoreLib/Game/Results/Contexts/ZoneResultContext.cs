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

using System.Collections.Generic;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Zone.Core;

namespace Imlight.CoreLib.Game.Results.Contexts;

/// <summary>
/// Result context for zone trigger executions
/// </summary>
public class ZoneResultContext(ZoneTrigger trigger,
                               IActorRef playerRef,
                               CoreObject playerObj,
                               IActorRef replyTo = null) : IResultContext {

    public Zone.Core.Zone Zone => Trigger.Zone;
    public ZoneTrigger Trigger { get; } = trigger;
    public IActorRef PlayerRef { get; } = playerRef;
    public CoreObject PlayerObj { get; } = playerObj;
    public IActorRef ReplyTo { get; } = replyTo;

    public IEnumerable<Result> GetResults() 
        => Trigger.TriggerData?.m_results?.m_results ?? [];

    public IActorRef GetZoneActor() 
        => Trigger.ZoneRef;

    public IActorRef GetPlayerRef() 
        => PlayerRef;

    public CoreObject GetPlayerObj() 
        => PlayerObj;

    public IActorRef GetReplyTo() 
        => ReplyTo;

}