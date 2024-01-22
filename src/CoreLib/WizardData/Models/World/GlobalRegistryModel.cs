/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;

namespace Imlight.CoreLib.WizardData.Models.World;

public class GlobalRegistryModel {
    public Dictionary<string, float> GlobalRegistryValues { get; set; } = new();
}
