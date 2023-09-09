using System.Collections.Generic;

namespace Imlight.Server.WizardData.Models;

public class GlobalRegistryModel
{
    public Dictionary<string, float> GlobalRegistryValues { get; set; } = new();
}