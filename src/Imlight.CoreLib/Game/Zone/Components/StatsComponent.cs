/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Behaviors;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class StatsComponent : ZoneEntityComponent, IComponentFactory {

    // todo: not good !! this should just be WizGameStats
    public ServerWizGameStats Stats;

    public static bool ShouldAttachToEntity(CoreTemplate template) 
        => template is GameObjectTemplate gameObjectTemplate
        && gameObjectTemplate.m_behaviors.Any(x => x is NPCBehaviorTemplate);

    public StatsComponent(ZoneEntity entity) : base(entity) {
        var npcBehaviorTemplate = Entity.Template.m_behaviors
            .OfType<NPCBehaviorTemplate>()
            .First();

        // Try to parse the npcBehaviorTemplate.m_schoolOfFocus to a MagicSchool.
        var parsedSchool = MagicSchool.Balance;
        if (    npcBehaviorTemplate.m_schoolOfFocus != "" 
            && !Enum.TryParse(npcBehaviorTemplate.m_schoolOfFocus, out parsedSchool)) {
            Logger.Error("Failed to parse magic school {0} for creature {1}.",
                Logger.Args(npcBehaviorTemplate.m_schoolOfFocus, Entity.ActiveGameObject.m_globalID));

            return;
        }

        Stats = new(parsedSchool, npcBehaviorTemplate.m_nLevel);
    }

}