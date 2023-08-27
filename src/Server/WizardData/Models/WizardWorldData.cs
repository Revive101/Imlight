using Newtonsoft.Json;

namespace Imlight.Server.WizardData.Models;

public class WizardWorldData
{
    [JsonProperty] public WizardZoneEventData[] GlobalZoneEvents;
}