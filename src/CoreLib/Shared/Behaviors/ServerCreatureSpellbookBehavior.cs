/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.Game.Spells;
using static Imlight.Common.Caches.TypeCache;
using Imlight.CoreLib.WizardData.Models.World;
using Imlight.Common;

namespace Imlight.CoreLib.Shared.Behaviors;

public class ServerCreatureSpellbookBehavior : ServerSpellbookBehavior {
    public override bool NoTransfer { get; set; } = true;

    private readonly string _deckName;

    // ctor
    public ServerCreatureSpellbookBehavior(DeckBehaviorTemplate deckBehaviorTemplate) {
        _deckName = deckBehaviorTemplate.m_defaultDeck;
        var creatureSpellbook = GetCreatureSpellbook(_deckName);

        if (creatureSpellbook is null) {
            Logger.Warning("Deck {0} could not be found in the creature spellbook collection.",
                Logger.Args(_deckName));
            return;
        }

        AddAllSpellsFromSpellbook(creatureSpellbook);
    }

    private void AddAllSpellsFromSpellbook(CreatureSpellbook creatureSpellbook) {
        foreach (var spellId in creatureSpellbook.SpellTemplateIds) {
            var spell = SpellFactory.CreateSpellFromTemplate(spellId);

            if (spell is null) {
                Logger.Warning("Deck {0} had spell by template id {1} that could not be created.",
                    Logger.Args(_deckName, spellId));
                continue;
            }

            // Otherwise, add the spell to the spellbook.
            LearnSpell(spell);
        }
    }

    private static CreatureSpellbook GetCreatureSpellbook(string deckName) {
        var creatureSpellbook = CreatureSpellbookCollection.GetCreatureSpellbook(deckName);
        creatureSpellbook ??= CreatureSpellbookCollection.GetDefaultCreatureSpellbook();

        return creatureSpellbook;
    }
}
