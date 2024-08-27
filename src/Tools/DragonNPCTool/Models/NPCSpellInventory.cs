/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

namespace DragonNPCTool.Models;

public class NPCSpellEntry {
    public ulong TemplateID { get; set; }
    public ulong RequiredSpellID { get; set; }
    public int Level { get; set; }
}


public class NPCSpellInventory {
    public ulong TemplateID { get; set; }
    public List<NPCSpellEntry> Spells { get; set; }
}
