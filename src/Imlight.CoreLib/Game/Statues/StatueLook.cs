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
 * Decides how a template can be a statue base, and puts a statue's captured look on its client object.
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
using System.Linq;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.Game.Statues;

internal static class StatueLook {

    private const string WIZARD_EQUIPMENT_BEHAVIOR_NAME = "WizardEquipmentBehavior";

    internal static StatueBaseKind GetBaseKind(CoreTemplate template) {
        var behaviors = template?.m_behaviors;
        if (behaviors is null) {
            return StatueBaseKind.None;
        }

        var looksLikeWizardNpc = behaviors.OfType<NPCBehaviorTemplate>().Any()
            && behaviors.OfType<WizardCharacterBehaviorTemplate>().Any()
            && behaviors.Any(x => x?.m_behaviorName.ToString() == WIZARD_EQUIPMENT_BEHAVIOR_NAME);
        var canFight = behaviors.OfType<DuelistBehaviorTemplate>().Any()
            || behaviors.OfType<MobDeckBehaviorTemplate>().Any();
        if (looksLikeWizardNpc && !canFight) {
            return StatueBaseKind.WizardNpc;
        }

        var hasPedestalBehavior = behaviors.OfType<PlayerStatueBehaviorTemplate>()
            .Any(x => x.m_settings is { Count: > 0 });

        return hasPedestalBehavior ? StatueBaseKind.Pedestal : StatueBaseKind.None;
    }

    internal static void Apply(ClientObject clientObject, PlayerStatue statue, StatueBaseKind kind) {
        if (kind == StatueBaseKind.Pedestal) {
            ApplyPedestal(clientObject, statue);
        } else if (kind == StatueBaseKind.WizardNpc) {
            ApplyWizardNpc(clientObject, statue);
        }
    }

    private static void ApplyPedestal(ClientObject clientObject, PlayerStatue statue) {
        if (!StatueBlob.TryBuild(statue, out var blob)) {
            return;
        }

        ReplaceBehavior<ClientPlayerStatueBehavior>(clientObject, pedestal => {
            pedestal.m_visualBlobs = [blob];

            return pedestal;
        });
    }

    private static void ApplyWizardNpc(ClientObject clientObject, PlayerStatue statue) {
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
        ReplaceBehavior<NPCBehavior>(clientObject, npc => {
            npc.m_isMonster = false;
            npc.m_wsNameOverride = statue.Name;

            return npc;
        });
    }

    private static void ReplaceBehavior<T>(ClientObject clientObject, Func<T, T> build) where T : BehaviorInstance {
        var index = clientObject.m_inactiveBehaviors.FindIndex(x => x is T);
        if (index < 0) {
            return;
        }

        clientObject.m_inactiveBehaviors[index] = build((T) clientObject.m_inactiveBehaviors[index]);
    }

}
