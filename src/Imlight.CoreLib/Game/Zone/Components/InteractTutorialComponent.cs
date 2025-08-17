/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.World;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class InteractTutorialComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    public string ServiceName     => "TutorialActionService";
    public string NpcIcon         => null;
    public string NpcNameKey      => null;
    public string NpcTextKey      => null;
    public WizBangs WizBang       => WizBangs.None;
    public string StateName       => null;
    public string InteractWizBang => null;
    public string DisplayKey      => "";
    private GameObjectTemplate _template;

    private static readonly ObjectSerializer s_offeringsSerializer = new ObjectSerializer(Behaviors: SerializerFlags.None);

    public static bool ShouldAttachToEntity(CoreTemplate template) 
        // Attach if the template is an NPC and has TUT in its name
        // May be able to make this more general to questing NPCs later on.
        // For now, limiting this to tutorial NPCs
        => template is GameObjectTemplate goTemplate
        && goTemplate.m_objectName.ToString().Contains("TUT")
        && goTemplate.m_behaviors.Any(x => x is NPCBehaviorTemplate)
        && goTemplate.m_behaviors.Any(x => x is BehaviorTemplate xBehaviorTemplate && xBehaviorTemplate.m_behaviorName == "TutorialActionBehavior");
        
    public override void OnAwake() {
        if (Entity.Template is GameObjectTemplate goTemplate) {
            _template = goTemplate;
        }
    }
    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {

    }

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard playerCharacter) 
        => [
            new TutorialActionOption {
                m_persona = _template.m_objectName.ToString() + "_Persona",
                m_serviceName = ServiceName
            }
        ];
    
}