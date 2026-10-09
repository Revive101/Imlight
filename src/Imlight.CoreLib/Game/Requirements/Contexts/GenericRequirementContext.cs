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
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Requirements.Contexts;

public class GenericRequirementContext(RequirementList requirements,
                               IActorRef playerRef,
                               CoreObject playerObj,
                               Wizard wizard,
                               IActorRef zoneRef = null,
                               string questName = null,
                               string goalName = null,
                               string triggerName = null) : IRequirementContext, IZoneStateContext {

    private readonly RequirementList _requirements = requirements;
    private readonly IActorRef _playerRef = playerRef;
    private readonly CoreObject _playerObj = playerObj;
    private readonly Wizard _wizard = wizard;
    private readonly IActorRef _zoneRef = zoneRef;
    private readonly string _questName = questName;
    private readonly string _goalName = goalName;
    private readonly string _triggerName = triggerName;

    public RequirementList GetFullRequirementList() => _requirements;
    public List<Requirement> GetRequirements() => _requirements?.m_requirements;
    public IActorRef GetPlayerRef() => _playerRef;
    public CoreObject GetPlayerObj() => _playerObj;
    public Wizard GetWizard() => _wizard;
    public IActorRef GetZoneRef() => _zoneRef;
    public string GetQuestName() => _questName;
    public string GetGoalName() => _goalName;
    public string GetTriggerName() => _triggerName;

    /// <summary>
    /// The tokens, counters and trigger states of the zone, when the check runs for a zone trigger.
    /// </summary>
    public Game.Zone.Core.ZoneScriptState ScriptState { get; init; }

    /// <summary>
    /// Judges the completion of dungeon quests by the instance, when a player's entry grants them.
    /// </summary>
    public System.Func<string, bool?> InstanceQuestCompleted { get; init; }

    /// <summary>
    /// The zones of the player's instance container, when the player is in an instance; ReqInZone then holds for any of them.
    /// </summary>
    public System.Collections.Generic.IReadOnlyCollection<string> InstanceZones { get; init; }

}