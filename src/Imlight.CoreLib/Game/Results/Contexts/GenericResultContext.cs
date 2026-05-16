/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Results.Contexts;

/// <summary>
/// Generic result context for any result execution scenario
/// </summary>
public class GenericResultContext(ResultList results,
                           IActorRef playerRef,
                           CoreObject playerObj,
                           IActorRef replyTo = null,
                           IActorRef zoneActor = null,
                           string questName = null,
                           string goalName = null,
                           string triggerName = null) : IResultContext {

    public IActorRef PlayerRef { get; } = playerRef;
    public CoreObject PlayerObj { get; } = playerObj;
    public IActorRef ReplyTo { get; } = replyTo;
    public IActorRef ZoneActor { get; } = zoneActor;
    public string QuestName { get; } = questName;
    public string GoalName { get; } = goalName;
    public string TriggerName { get; } = triggerName;

    private readonly ResultList _results = results;

    public IEnumerable<Result> GetResults() 
        => _results?.m_results ?? [];

    public IActorRef GetZoneActor() 
        => ZoneActor;

    public IActorRef GetPlayerRef() 
        => PlayerRef;

    public CoreObject GetPlayerObj() 
        => PlayerObj;

    public IActorRef GetReplyTo() 
        => ReplyTo;

    public bool IsQuestContext => !string.IsNullOrEmpty(QuestName);
    public bool IsGoalContext => !string.IsNullOrEmpty(GoalName);
    public bool IsTriggerContext => !string.IsNullOrEmpty(TriggerName);

}