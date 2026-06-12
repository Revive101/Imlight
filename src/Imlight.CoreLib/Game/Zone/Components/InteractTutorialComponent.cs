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