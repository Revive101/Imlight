using Imcodec.Types;
using System;
using System.Collections.Generic;
using System.Text;

namespace Imlight.CoreLib.Game.CrownShop;

public class CrownShopTabConfig {
    public int TabId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public List<int> CategoryIds { get; set; } = new();
    public string Tags { get; set; } = "Seperate";
    public string Description { get; set; } = "0";
    public List<CrownShopCategoryConfig> Categories { get; set; } = new();
    public List<CrownShopItemConfig> Items { get; set; } = new();
}

public class CrownShopCategoryConfig {
    public int CategoryId { get; set; }
    public int ParentTabId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; } = "0";
    public string Icon { get; set; }
    public string Tags { get; set; } = "None";
    public CrownShopCategoryFlags Flags { get; set; } = new();
}
public class CrownShopCategoryFlags {
    public bool AllowMultipleBuy { get; set; } = true;
    public bool ForceDisallowMultipleBuy { get; set; } = false;
    public bool DontFilterOwnedRecoItems { get; set; } = false;
    public bool IsHousesCategory { get; set; } = false;
    public bool IsEverythingCategory { get; set; } = false;
    public bool IsGroupElixirsCategory { get; set; } = false;
}

public class CrownShopItemConfig {
    public ulong TemplateId { get; set; }
    public int ItemFlags { get; set; } = 0;
    public int GoldCost { get; set; }
    public int StrikethruGold { get; set; } = 0;
    public int CrownsCost { get; set; }
    public int StrikethruCrowns { get; set; } = 0;
    public int TicketCost { get; set; }
    public List<DisplayPriority> DisplayPriority { get; set; }
    public CrownShopItemFlags CrownShopItemFlags { get; set; } = new();
}

public class DisplayPriority {
    public int CategoryId { get; set; }
    public int Position { get; set; } = 1;
}

public class CrownShopItemFlags {
    public bool CombatOnly { get; set; } = false;
    public bool RecommendIfOwned { get; set; } = false;
    public bool NoGift { get; set; } = false;
}
