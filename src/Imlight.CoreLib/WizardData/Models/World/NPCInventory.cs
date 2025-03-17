/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Imlight.Common.ObjectProperty.PropertyReflection;

namespace Imlight.CoreLib.WizardData.Models.World;

public class NPCInventory {
    public ulong TemplateID { get; set; }
    public List<GID> Inventory { get; set; } = new();
}
