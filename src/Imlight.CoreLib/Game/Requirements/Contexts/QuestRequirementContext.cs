/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
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