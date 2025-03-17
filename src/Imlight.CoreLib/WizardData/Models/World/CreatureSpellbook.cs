/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;

namespace Imlight.CoreLib.WizardData.Models.World;

public class CreatureSpellbook {
    public string DeckName { get; init; }
    public uint[] SpellTemplateIds { get; init; }

    // ctor
    public CreatureSpellbook(string deckName, uint[] spellTemplateIds) {
        DeckName = deckName;
        SpellTemplateIds = spellTemplateIds;
    }
}
