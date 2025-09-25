/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
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