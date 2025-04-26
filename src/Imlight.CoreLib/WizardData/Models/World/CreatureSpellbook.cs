/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

namespace Imlight.CoreLib.WizardData.Models.World;

public class CreatureSpellbook(string deckName, uint[] spellTemplateIds) {

    public string DeckName { get; init; } = deckName;
    public uint[] SpellTemplateIds { get; init; } = spellTemplateIds;
    
}
