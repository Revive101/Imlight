/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Results.Contexts;

/// <summary>
/// Result context for quest-related result executions
/// </summary>
public class QuestResultContext(ResultList results,
                                IActorRef zoneActor,
                                string questName,
                                IActorRef playerRef,
                                CoreObject playerObj,
                                IActorRef replyTo = null,
                                string goalName = null) : IResultContext {

    public string QuestName { get; } = questName;
    public string GoalName { get; } = goalName;
    public bool IsGoalContext => !string.IsNullOrEmpty(GoalName);
    public IActorRef PlayerRef { get; } = playerRef;
    public CoreObject PlayerObj { get; } = playerObj;
    public IActorRef ReplyTo { get; } = replyTo;

    private readonly ResultList _results = results;
    private readonly IActorRef _zoneActor = zoneActor;

    public IEnumerable<Result> GetResults() 
        => _results?.m_results ?? [];

    public IActorRef GetZoneActor() 
        => _zoneActor;

    public IActorRef GetPlayerRef() 
        => PlayerRef;

    public CoreObject GetPlayerObj() 
        => PlayerObj;

    public IActorRef GetReplyTo() 
        => ReplyTo;

}