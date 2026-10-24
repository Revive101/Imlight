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
 * STATUE LOOK
 * ========================================================================
 *
 * PURPOSE:
 * Decides whether a template can be a statue base, and puts a statue's captured look on its client object.
 *
 * USAGE EXAMPLE:
 * StatueEntity.GetClientObject calls StatueLook.Apply on top of the base object.
 *
 * NOTE:
 * The shared definition is never mutated: the avatar is copied and the equipment list is a new list.
 *
 * TODO:
 *
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.Cryptography;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.Game.Statues;

internal static class StatueLook {

    internal static bool IsPlayerBase(CoreTemplate template) {
        var behaviors = template?.m_behaviors;
        if (behaviors is null) {
            return false;
        }

        return HasBehavior<WizardCharacterBehavior>(behaviors)
            && HasBehavior<ClientWizEquipmentBehavior>(behaviors)
            && HasBehavior<ClientWizPlayerNameBehavior>(behaviors)
            && !behaviors.OfType<NPCBehaviorTemplate>().Any();
    }

    internal static void Apply(ClientObject clientObject, PlayerStatue statue) {
        ReplaceLook(clientObject, statue);
        ReplaceBehavior<ClientWizPlayerNameBehavior>(clientObject, name => CreateNameBehavior(statue) with {
            m_behaviorTemplateNameID = name.m_behaviorTemplateNameID,
        });
    }

    private static void ReplaceLook(ClientObject clientObject, PlayerStatue statue) {
        ReplaceBehavior<WizardCharacterBehavior>(clientObject, avatar => statue.Appearance with {
            m_behaviorTemplateNameID = avatar.m_behaviorTemplateNameID,
        });
        ReplaceBehavior<ClientWizEquipmentBehavior>(clientObject, equipment => {
            equipment.m_itemList = [];
            equipment.m_slotList = [];
            equipment.m_equipmentSets = [];
            equipment.m_publicItemList = [.. statue.Equipment];

            return equipment;
        });
    }

    private static ClientWizPlayerNameBehavior CreateNameBehavior(PlayerStatue statue)
        => new ServerWizPlayerNameBehavior {
            NameIndices = statue.NameKeys,
            Gender = statue.Appearance.m_eGender,
            Race = statue.Appearance.m_eRace,
            NameOverride = statue.Name,
        }.GetClientBehaviorInstance();

    private static bool HasBehavior<T>(IEnumerable<BehaviorTemplate> behaviors) where T : BehaviorInstance
        => behaviors.Any(x => x is not null && BehaviorCache.AllocateBehavior(StringHash.Compute(x.m_behaviorName)) is T);

    private static void ReplaceBehavior<T>(ClientObject clientObject, Func<T, T> build) where T : BehaviorInstance {
        var index = clientObject.m_inactiveBehaviors.FindIndex(x => x is T);
        if (index < 0) {
            return;
        }

        clientObject.m_inactiveBehaviors[index] = build((T) clientObject.m_inactiveBehaviors[index]);
    }

}
