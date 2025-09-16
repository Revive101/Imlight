/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Requirements.Contexts;

public class ZoneRequirementContext(RequirementList requirements,
                            IActorRef playerRef,
                            CoreObject playerObj,
                            Wizard wizard,
                            IActorRef zoneRef = null,
                            string triggerName = null) : IRequirementContext {

    private readonly RequirementList _requirements = requirements;
    private readonly IActorRef _playerRef = playerRef;
    private readonly CoreObject _playerObj = playerObj;
    private readonly Wizard _wizard = wizard;
    private readonly IActorRef _zoneRef = zoneRef;
    private readonly string _triggerName = triggerName;

    public List<Requirement> GetRequirements() => _requirements?.m_requirements;
    public IActorRef GetPlayerRef() => _playerRef;
    public CoreObject GetPlayerObj() => _playerObj;
    public Wizard GetWizard() => _wizard;
    public IActorRef GetZoneRef() => _zoneRef;
    public string GetQuestName() => null; // Not applicable for zone contexts
    public string GetGoalName() => null; // Not applicable for zone contexts
    public string GetTriggerName() => _triggerName;

}