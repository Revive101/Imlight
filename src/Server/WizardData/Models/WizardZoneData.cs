/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using WizUnraveler.Secrets;

namespace Imlight.Server.WizardData.Models;

public class WizardZoneData
{
    public string ZoneName { get; set; }
    public List<WizardZoneEventData> Events { get; set; } = new();
    public List<WizardTeleportData> Teleports { get; set; } = new();
}

public class WizardTeleportData
{
    public string TriggerName { get; set; }
    public ServerTypeCache.ResTeleport Teleport { get; set; }
}