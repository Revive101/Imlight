/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;

namespace Imlight.Server.WizardData.Models;

public enum WizardZoneEventObjectAdjectiveType
{
    PrefixedWith,
    Contains,
    SuffixedWith,
    Raw
}

public class WizardZoneEventData
{
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsEnabled { get; set; }
    public bool EnabledByDefault { get; set; }
    // These dates are for developer semantics only, they are not used by the server.
    public DateTime StartDate { get; set; } = new DateTime();
    public DateTime EndDate { get; set; } = new DateTime();
    public List<string> ObjectAdjectiveWhitelist { get; set; } = new();
    public WizardZoneEventObjectAdjectiveType ObjectAdjectiveType { get; set; }
}