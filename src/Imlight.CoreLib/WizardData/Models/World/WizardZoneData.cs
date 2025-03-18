/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imcodec.ObjectProperty.TypeCache;
using System.Collections.Generic;

namespace Imlight.CoreLib.WizardData.Models.World;

public class WizardZoneData {

    public string ZoneName { get; set; }
    public List<WizardTeleportData> Teleports { get; set; } = [];

}

public class WizardTeleportData {

    public string TriggerName { get; set; }
    public ResTeleport Teleport { get; set; }

}
