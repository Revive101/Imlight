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
 *
 * ========================================================================
 * STATS COMPONENT
 * ========================================================================
 * 
 * PURPOSE:
 * Manages core statistical attributes for game entities, 
 * initializing game stats based on NPC behavior templates.
 * 
 * USAGE EXAMPLE:
 * 
 * NOTE:
 * Initializes game stats with magic school and level from NPC template.
 * 
 * TODO:
 * - Refactor Stats to use WizGameStats directly
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
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