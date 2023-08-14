using Newtonsoft.Json;

namespace Imlight.Server.WizardData.Models;

public class WizardWorldData
{
    [JsonProperty] public string StartingZone = "WizardCity/WC_Hub";
    [JsonProperty] public ushort StartingLevel = 1;
    [JsonProperty] public byte StartingWorld = 1;
    [JsonProperty] public int GoldPouchMax = 1000000;
    [JsonProperty] public WizardZoneEventData[] GlobalZoneEvents;
}