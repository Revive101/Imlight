/* Copyright (C) Revive101 Development Team - All Rights Reserved
* Unauthorized copying of this file, via any medium is strictly prohibited
* Proprietary and confidential.
*/

using Imlight.Common.ObjectProperty.PropertyReflection;

namespace DragonNPCTool.Models;
public class NPCInventory {

    public ulong TemplateID { get; set; }
    public List<GID> Inventory { get; set; } = new();
}
