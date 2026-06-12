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