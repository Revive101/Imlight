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

public class QuestRequirementContext(RequirementList requirements,
                             IActorRef playerRef,
                             CoreObject playerObj,
                             Wizard wizard,
                             string questName = null,
                             string goalName = null) : IRequirementContext {

    private readonly RequirementList _requirements = requirements;
    private readonly IActorRef _playerRef = playerRef;
    private readonly CoreObject _playerObj = playerObj;
    private readonly Wizard _wizard = wizard;
    private readonly string _questName = questName;
    private readonly string _goalName = goalName;

    public RequirementList GetFullRequirementList() => _requirements;
    public List<Requirement> GetRequirements() => _requirements?.m_requirements;
    public IActorRef GetPlayerRef() => _playerRef;
    public CoreObject GetPlayerObj() => _playerObj;
    public Wizard GetWizard() => _wizard;
    public IActorRef GetZoneRef() => null; // Not applicable for quest contexts
    public string GetQuestName() => _questName;
    public string GetGoalName() => _goalName;
    public string GetTriggerName() => null; // Not applicable for quest contexts

}