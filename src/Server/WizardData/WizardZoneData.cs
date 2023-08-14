/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;

namespace Imlight.Server.WizardData;

public class WizardZoneData
{
    public string ZoneName { get; set; }
    public List<WizardZoneEventData> Events { get; set; } = new();
}