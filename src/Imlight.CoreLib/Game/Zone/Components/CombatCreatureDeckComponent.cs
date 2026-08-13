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
using Imcodec.IO;
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
        // Collect every behavior from the creature's equipment items.
        var equipmentItemBehaviors = entity.Template.m_behaviors
            .OfType<EquipmentBehaviorTemplate>()
            .SelectMany(x => x.m_itemList)
            .Select(x => CoreObjectFactory.GetCoreTemplate(x))
            .Where(x => x != null)
            .SelectMany(x => x.m_behaviors ?? []);

        // Combine equipment item behaviors with the entity's own behaviors.
        // MobDeckBehaviorTemplate can appear in either location.
        var allBehaviors = entity.Template.m_behaviors
            .Concat(equipmentItemBehaviors);

        // MobDeckBehaviorTemplate stores spell names directly, if it exists.
        var mobDeck = allBehaviors.OfType<MobDeckBehaviorTemplate>().FirstOrDefault();
        if (mobDeck != null) {
            InitializeSpellbookFromNames(mobDeck.m_spellList);
        }
        else {
            // DeckBehaviorTemplate stores a deck name that maps to a SpiralDB spellbook.
            var deckBehavior = allBehaviors.OfType<DeckBehaviorTemplate>().FirstOrDefault();
            if (deckBehavior != null) {
                InitializeSpellbook(GetCreatureSpellbook(deckBehavior.m_defaultDeck));
            }
            else {
                // Neither deck type found — fall back to the default spellbook so the
                // creature can at least cast basic spells instead of passing all the time.
                Logger.Warning(
                    "{0} {1} has no deck behavior — falling back to default spellbook.",
                    Logger.Args(nameof(ZoneEntity), entity.ActiveGameObject.m_debugName)
                );
                InitializeSpellbook(CreatureSpellbookCollection.GetDefaultCreatureSpellbook());
            }
        }

        // A creature with an empty deck (deck absent from SpiralDB, or a mob deck with no usable
        // names) falls back to the default spellbook so it can cast instead of passing every round.
        if (Spells.Count == 0) {
            InitializeSpellbook(CreatureSpellbookCollection.GetDefaultCreatureSpellbook());
        }
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

    private void InitializeSpellbookFromNames(List<ByteString> spellNames) {
        foreach (var spellName in spellNames) {
            if (spellName.ToString().Length == 0) {
                continue;
            }

            var spell = SpellFactory.GetSpell(spellName.ToString());
            if (spell == null) {
                Logger.Error(
                    "{0} {1} has unknown spell \"{2}\" in MobDeckBehaviorTemplate.",
                    Logger.Args(nameof(ZoneEntity),
                                Entity.ActiveGameObject.m_debugName,
                                spellName)
                );
                continue;
            }

            Spells.Add(new SpellData {
                m_templateID = spell.m_templateID,
                m_quantity = 9999,
            });
        }
    }

    private static CreatureSpellbook GetCreatureSpellbook(string deckName) {
        if (string.IsNullOrEmpty(deckName)) {
            return CreatureSpellbookCollection.GetDefaultCreatureSpellbook();
        }

        var creatureSpellbook = CreatureSpellbookCollection.GetCreatureSpellbook(deckName);
        creatureSpellbook ??= CreatureSpellbookCollection.GetDefaultCreatureSpellbook();

        return creatureSpellbook;
    }

}