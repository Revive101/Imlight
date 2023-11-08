/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common.Caches;
using System.Collections.Generic;

namespace Imlight.CoreLib.WizardData.Models;

public class WizardZoneData {
    public string ZoneName { get; set; }
    public List<WizardTeleportData> Teleports { get; set; } = new();
}

public class WizardTeleportData {
    public string TriggerName { get; set; }
    public ServerTypeCache.ResTeleport Teleport { get; set; }
}
