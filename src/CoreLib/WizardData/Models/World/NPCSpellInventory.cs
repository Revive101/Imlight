/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;

namespace Imlight.CoreLib.WizardData.Models.World;

public class NPCSpellEntry {
    public ulong TemplateID { get; set; }
    public string SpellName { get; set; }
    public string DisplayKey { get; set; }
    public string RequiredSpell { get; set; }
    public string RequiredQuest { get; set; }
    public int Level { get; set; }
    public int TrainingCost { get; set; }
}

public class NPCSpellInventory {
    public ulong TemplateID { get; set; }
    public List<NPCSpellEntry> Spells { get; set; }
}
