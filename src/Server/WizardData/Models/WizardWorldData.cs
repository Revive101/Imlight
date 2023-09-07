/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Newtonsoft.Json;

namespace Imlight.Server.WizardData.Models;

public class WizardWorldData
{
    [JsonProperty] public WizardZoneEventData[] GlobalZoneEvents;
}