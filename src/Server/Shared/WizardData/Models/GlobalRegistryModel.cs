using System.Collections.Generic;

namespace Imlight.Server.Shared.WizardData.Models;

public class GlobalRegistryModel
{
    public Dictionary<string, float> GlobalRegistryValues { get; set; } = new();
}