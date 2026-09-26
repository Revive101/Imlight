using Imcodec.ObjectProperty.TypeCache;
using System;
using System.Collections.Generic;

namespace Imlight.CoreLib.WizardData.Models.World;

public class BoosterPackModel {
    public ulong TemplateID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public PackType PackType { get; set; } = PackType.TreasureCards;
    public List<string> Slots { get; set; } = [];
    public Dictionary<string, List<BoosterDropItem>> Drops { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}


public class BoosterDropItem {
    public ulong Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; } // "TreasureCard" or "Item" (null inherits from PackType)
}


public enum PackType {
    TreasureCards,
    Item,
    Mixed
}
