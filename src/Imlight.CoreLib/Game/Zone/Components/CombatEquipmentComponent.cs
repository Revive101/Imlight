/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.CoreLib.Game.Effects;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Resources;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class CombatEquipmentComponent(ZoneEntity entity) 
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