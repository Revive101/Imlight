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
 * COMBAT CREATURE DECK
 * ========================================================================
 * 
 * PURPOSE:
 * Manages spelldecks for combat-enabled creature entities, initializing 
 * their spellbooks based on specific behavior templates.
 * 
 * USAGE EXAMPLE:
 * var deckComponent = new CombatCreatureDeckComponent(zoneEntity);
 * var availableSpells = deckComponent.Spells;
 * 
 * NOTE:
 * Requires prior initialization of CoreObjectFactory and SpellFactory.
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
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class CombatCreatureDeckComponent : ZoneEntityComponent, IComponentFactory {

    public List<SpellData> Spells { get; } = [];

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate gameObjectTemplate
        && gameObjectTemplate.m_behaviors.Any(x => x is NPCBehaviorTemplate)
        && gameObjectTemplate.m_behaviors.Any(x => x is DuelistBehaviorTemplate);

    // ctor
    public CombatCreatureDeckComponent(ZoneEntity entity) : base(entity) { 
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
                "{0} {1} is missing {2}",
                Logger.Args(nameof(ZoneEntity), 
                            entity.ActiveGameObject.m_debugName, 
                            nameof(DeckBehaviorTemplate) 
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