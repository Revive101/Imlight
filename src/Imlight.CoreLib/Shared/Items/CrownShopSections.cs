using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Shared.Items;

/// <summary>Native Crown Shop menus, using icons shipped with this client.</summary>
public static class CrownShopSections {
    public sealed record Section(int Id, int Tab, string Name, string Icon, string Tags);
    public sealed record Tab(int Id, string Name, string Icon);
    public static readonly Tab[] Tabs = [
        new(1, "Featured", "New_Items"), new(2, "Gold", "Gold"),
        new(3, "Mounts", "Mounts_Permanent"), new(4, "Pets", "Pets"),
        new(5, "Packs", "Cards"), new(6, "Gear", "Clothing"),
        new(7, "Housing", "Housing"), new(8, "Elixirs", "Elixirs"),
        new(9, "Gameplay", "Everything"),
    ];
    public static readonly Section[] Sections = [
        new(1, 1, "Everything", "Everything", ""),
        new(10, 2, "Gold", "Gold", "Gold"), new(11, 2, "Lunari & Tokens", "Lunari", "Lunari,TourneyTokens"),
        new(20, 3, "Permanent Mounts", "Mounts_Permanent", "Mount"),
        new(21, 3, "Rental Mounts", "Mounts_Rental", ""),
        new(30, 4, "Pets", "Pets", "Pet"), new(31, 4, "Pet Snack Packs", "Pet_Snack", "PetSnack,Snack,SnackPack,CCGPet"),
        new(40, 5, "Hoard & Lore Packs", "CCG", "BoosterPack,Booster,CCG"),
        new(41, 5, "Bundles", "Bundles", "Bundle"),
        new(42, 5, "Reagent Bundles", "Reagents", "ReagentBundle,Reagent,CCGCraft"),
        new(50, 6, "Hats", "Clothing", "Hat"), new(51, 6, "Robes", "Robes", "Robe"),
        new(52, 6, "Shoes", "Shoes", "Shoes"), new(53, 6, "Weapons", "Weapons", "Weapon,Wand,Staff,Sword"),
        new(54, 6, "Athames", "Athames", "Athame"), new(55, 6, "Amulets", "Amulets", "Amulet"),
        new(56, 6, "Rings", "Rings", "Ring"), new(57, 6, "Decks", "Cards", "Deck"),
        new(58, 6, "Hairstyles", "Hairstyles", "Wig"),
        new(59, 6, "Clothing Bundles", "Clothing_Sets", "SetGear"),
        new(60, 7, "Houses", "Housing", "Deed,Islands"),
        new(61, 7, "Furniture", "Furniture", "Housing,Furniture,Decoration,WallHangings,Outdoor,HouseGuest"),
        new(62, 7, "Furniture Sets", "Furniture_Sets", "SetHouse,CCGHousing"),
        new(63, 7, "Gardening", "Seeds", "CrownShop_Gardening_Tab,Seed,GardeningPlant,GardeningItem,GardeningSoil,GardeningPot"),
        new(64, 7, "Castle Blocks", "Building_Blocks", "CastleBlock"),
        new(65, 7, "Teleporters", "Teleporter", "GDN_HOUSE_teleporter"),
        new(66, 7, "Instruments", "Instruments", "MusicalInstrument"),
        new(67, 7, "Games & Mini Games", "Games", "Minigame"),
        new(70, 8, "Elixirs", "Elixirs", "Elixir"),
        new(71, 8, "Group Elixirs", "Elixirs_Group", "GroupElixir"),
        new(72, 8, "World Elixirs", "Elixirs_World", "WorldElixir"),
        new(73, 8, "Transformations", "Transformations", "Transformation"),
        new(80, 9, "Henchmen", "Henchmen", "Henchman"),
        new(81, 9, "Fishing", "Fishing", "Fishing"), new(82, 9, "Emotes", "Emotes", "Emote"),
        new(83, 9, "Teleport Effects", "Teleport_Effects", "TeleportEffect"),
        new(84, 9, "Other Items & Services", "Everything", "SpecialItem,Cantrips,Distinct"),
    ];
    public static string Icon(string name) => $"GUI/CrownShopIcons/Categories/{name}.dds";

    public static int Classify(WizItemTemplate item) {
        var tags = item.m_adjectiveList.ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool Has(string tag) => tags.Contains(tag);
        bool Behavior<T>() => item.m_behaviors.OfType<T>().Any();
        if (item is GoldAmountTemplate) return 10;
        if (item is LunariAmountTemplate or TourneyTokensAmountTemplate) return 11;
        if (Has("Mount") || Behavior<MountItemBehaviorTemplate>()) return Behavior<RentalBehaviorTemplate>() ? 21 : 20;
        if (Has("Pet") || Behavior<PetItemBehaviorTemplate>()) return 30;
        if (Has("CCGPet") || Has("SnackPack") || Has("PetSnack") || item.m_objectName.Contains("SnackPack", StringComparison.OrdinalIgnoreCase)) return 31;
        if (Has("CCGCraft") || Has("ReagentBundle") || item is ReagentItemTemplate) return 42;
        if (Has("SetGear")) return 59;
        if (Has("CCGHousing") || Has("SetHouse") || item is BoosterPackTemplate && item.m_objectName.Contains("Furniture", StringComparison.OrdinalIgnoreCase)) return 62;
        if (Has("CrownShop_Gardening_Tab")) return 63;
        if (item is BoosterPackTemplate) return 40;
        if (item is ItemBundleTemplate) return 41;
        if (Has("Henchman")) return 80;
        if (Behavior<WorldElixirBehaviorTemplate>()) return 72;
        if (Has("GroupElixir")) return 71;
        if (Has("Transformation")) return 73;
        if (Behavior<ElixirBehaviorTemplate>() || Behavior<ElixirBenefitBehaviorTemplate>() || Behavior<LevelUpElixirBehaviorTemplate>()) return 70;
        if (Behavior<DeedBehaviorTemplate>()) return 60;
        if (Behavior<SeedBehaviorTemplate>()) return 63;
        if (Behavior<HousingTeleporterBehaviorTemplate>()) return 65;
        if (Behavior<InteractiveMusicBehaviorTemplate>() || Behavior<HousingMusicBehaviorTemplate>()) return 66;
        if (Has("Wig")) return 58;
        // Specialized housing/gameplay tags take precedence over general Housing and Weapon tags.
        foreach (var id in new[] { 64, 67, 81, 82, 83, 63, 50, 51, 52, 54, 55, 56, 57, 53, 61 })
            if (Sections.First(section => section.Id == id).Tags.Split(',').Any(Has)) return id;
        if (Behavior<FurnitureInfoBehaviorTemplate>()) return 61;
        return 84;
    }

    // Purchase confirmation strips "CrownShopSWF_" from these names without a
    // length check. Literal captions render, but crash the native client on Buy.
    private static string TabKey(Tab tab) => "CrownShopSWF_Menu" + (tab.Id == 5 ? "Cards" : tab.Name);
    private static string SectionKey(Section section) => "CrownShopSWF_" + (section.Id switch {
        1 or 84 => "CategoryEverything", 10 => "CategoryGold", 11 => "Lunari",
        20 => "CategoryPermanentMounts", 21 => "CategoryRentalMounts",
        30 => "CategoryPets", 31 => "CategoryCCGPetSnacks",
        40 => "CategoryCCG", 41 => "CategoryBundles", 42 => "CategoryCCGReagent",
        50 => "CategoryHats", 51 => "CategoryRobes", 52 => "CategoryShoes",
        53 => "CategoryWeapons", 54 => "CategoryAthames", 55 => "CategoryAmulets",
        56 => "CategoryRings", 57 => "PermShopTab6", 58 => "CategoryWigs",
        59 => "CategoryClothingBundle", 60 => "CategoryHouses", 61 => "CategoryFurniture",
        62 => "CategoryCCGHousing", 63 => "CategoryGardening", 64 => "CategoryCastleBlocks",
        65 => "CategoryTeleporters", 66 => "CategoryInstruments", 67 => "CategoryMinigames",
        70 => "CategoryElixirs", 71 => "CategoryGroupElixirs", 72 => "CategoryWorldElixirs",
        73 => "CategoryTransformations", 80 => "CategoryHenchmen", 81 => "CategoryFishing",
        82 => "CategoryEmotes", 83 => "CategoryTeleportEffects",
        _ => throw new InvalidOperationException("Missing Crown Shop section localization"),
    });

    public static CrownShopLayout CreateLayout() => new() {
        m_tabs = Tabs.Select(tab => new CrownShopCategoryMenu {
            m_ID = tab.Id, m_name = TabKey(tab), m_description = TabKey(tab) + "_Desc", m_iconResource = Icon(tab.Icon),
            m_categoryIDs = Sections.Where(section => section.Tab == tab.Id).Select(section => section.Id).ToList(),
        }).ToList(),
        m_categories = Sections.Select(section => new CrownShopCategory {
            m_ID = section.Id, m_parentTabID = section.Tab, m_name = SectionKey(section),
            m_description = section.Id is 11 or 57 ? "CrownShopSWF_Default" : SectionKey(section) + "_Desc",
            m_iconResource = Icon(section.Icon), m_tags = section.Tags,
            m_dontFilterOwnedRecoItems = true, m_forceDisallowMultipleBuy = true,
            m_isEverythingCategory = section.Id == 1, m_isHousesCategory = section.Id == 60,
            m_isGroupElixirsCategory = section.Id == 71,
        }).ToList(),
    };
}
