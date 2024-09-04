/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;

namespace DragonNPCTool.Models;

public class CreatureSpellbook {
    public string DeckName { get; set; }
    public uint[] SpellTemplateIds { get; set; }

    // ctor
    public CreatureSpellbook(string deckName, uint[] spellTemplateIds) {
        DeckName = deckName;
        SpellTemplateIds = spellTemplateIds;
    }
}
