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
 * COMBAT CREATURE EQUIPMENT COMPONENT SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Manages equipment initialization and stat modifications for combat creatures 
 * based on predefined equipment behavior templates.
 * 
 * USAGE EXAMPLE:
 * 
 * NOTE:
 * Creatures carry equipment just as players do. These equipments are responsible
 * for school resistances, boosts, and other stats.
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Effects;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class CombatCreatureEquipmentComponent(ZoneEntity entity) 
    : ZoneEntityComponent(entity), IComponentFactory, IClientBehaviorProvider<ClientWizEquipmentBehavior> {

    public bool NoTransfer { get; set; } = false;

    private readonly ServerWizEquipmentBehavior _serverWizEquipmentBehavior = new() {
        EquippedItemIds = [],
        EquippedItems = [],
        SlotList = [],
    };
    private readonly List<WizItemTemplate> _equipmentTemplates = [];
    private StatsComponent _statsComponent;

    public static bool ShouldAttachToEntity(CoreTemplate template) 
        => template is GameObjectTemplate gameObjectTemplate
        && gameObjectTemplate.m_behaviors.Any(x => x is EquipmentBehaviorTemplate);

    public override void OnAwake() {
        // Get the EquipmentBehaviorTemplate from the entity.
        var equipmentBehaviorTemplate = Entity.Template.m_behaviors
            .OfType<EquipmentBehaviorTemplate>()
            .FirstOrDefault();
        if (equipmentBehaviorTemplate == null) {
            Logger.Error(
                "{0} {1} is missing {2}",
                Logger.Args(nameof(ZoneEntity), 
                            Entity.ActiveGameObject.m_debugName, 
                            nameof(EquipmentBehaviorTemplate)
                )
            );

            return;
        }
        InitializeEquipment(equipmentBehaviorTemplate);
        
        // Get the StatsComponent from the entity.
        _statsComponent = Entity.GetComponentOfType<StatsComponent>();
        if (_statsComponent == null) {
            Logger.Error(
                "{0} {1} is missing {2}",
                Logger.Args(nameof(ZoneEntity), 
                            Entity.ActiveGameObject.m_debugName, 
                            nameof(StatsComponent)
                )
            );

            return;
        }
        ApplyStats();
    }

    private void InitializeEquipment(EquipmentBehaviorTemplate equipmentBehaviorTemplate) {
        foreach (var itemTemplateId in equipmentBehaviorTemplate.m_itemList) {
            var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(itemTemplateId);
            if (template == null) {
                Logger.Error(
                    "{0} {1} is missing {2} (ItemTemplateId: {3})",
                    Logger.Args(nameof(ZoneEntity), 
                                Entity.ActiveGameObject.m_debugName, 
                                nameof(WizItemTemplate), 
                                itemTemplateId
                    )
                );

                continue;
            }

            var item = (WizClientObjectItem) CoreObjectFactory.FinalizeCoreObject(template.m_templateID);
            _serverWizEquipmentBehavior.ForceEquipItem(item);

            _equipmentTemplates.Add(template);
        }
    }

    private void ApplyStats() {
        foreach (var template in _equipmentTemplates) {
            CharacterEffectHelper.AddEffectsToGameStats(_statsComponent.Stats, template);
        }
    }

    public ClientWizEquipmentBehavior GetClientBehaviorInstance() 
        => _serverWizEquipmentBehavior?.GetClientBehaviorInstance() ?? null;
        
}