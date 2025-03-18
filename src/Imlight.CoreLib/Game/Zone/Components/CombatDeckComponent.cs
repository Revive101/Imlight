/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class CombatDeckComponent : ZoneEntityComponent, IComponentFactory {

    public List<SpellData> Spells { get; } = [];

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate gameObjectTemplate
        && gameObjectTemplate.m_behaviors.Any(x => x is NPCBehaviorTemplate)
        && gameObjectTemplate.m_behaviors.Any(x => x is DuelistBehaviorTemplate);

    // ctor
    public CombatDeckComponent(ZoneEntity entity) : base(entity) { 
        var DeckBehaviorTemplate = entity.Template.m_behaviors
            .OfType<EquipmentBehaviorTemplate>()
            .SelectMany(x => x.m_itemList)
            .Select(x => CoreObjectFactory.GetCoreTemplate(x))
            .OfType<WizItemTemplate>()
            .SelectMany(x => x.m_behaviors)
            .OfType<DeckBehaviorTemplate>()
            .FirstOrDefault();

        if (DeckBehaviorTemplate == null) {
            Logger.Error(
                "{0} {1} is missing {2} (DeckName: {3})",
                Logger.Args(nameof(ZoneEntity), 
                            entity.ActiveGameObject.m_debugName, 
                            nameof(DeckBehaviorTemplate), 
                            DeckBehaviorTemplate.m_defaultDeck
                )
            );

            return;
        }

        var deckName = DeckBehaviorTemplate.m_defaultDeck;
        var creatureSpellbook = GetCreatureSpellbook(deckName);
        InitializeSpellbook(creatureSpellbook);
    }

    private void InitializeSpellbook(CreatureSpellbook spellbook) {
        foreach (var spellId in spellbook.SpellTemplateIds) {
            var spell = SpellFactory.GetSpell(spellId);

            if (spell == null) {
                Logger.Error(
                    "{0} {1} is missing {2} (SpellId: {3})",
                    Logger.Args(nameof(ZoneEntity), 
                                Entity.ActiveGameObject.m_debugName, 
                                nameof(spell), 
                                spellId
                    )
                );

                continue;
            }

            // Creatures have infinite spells in their spellbook.
            var spellData = new SpellData() {
                m_templateID = spellId,
                m_quantity = 9999,
            };
            Spells.Add(spellData);
        }
    }

    private static CreatureSpellbook GetCreatureSpellbook(string deckName) {
        var creatureSpellbook = CreatureSpellbookCollection.GetCreatureSpellbook(deckName);
        creatureSpellbook ??= CreatureSpellbookCollection.GetDefaultCreatureSpellbook();

        return creatureSpellbook;
    }

}