/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * CROWNSHOP SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Handles the functionality for the crown shop
 * 
 * 
 * USAGE EXAMPLE:
 *
 *
 * 
 * NOTE:
 * 
 * There are following (possible) itemFlags:
 *  0x0001 (Bit 0)	FLAG_NoTrade	The purchased item cannot be placed in the Shared Bank or traded to other characters on the account.
 *  0x0002 (Bit 1)	FLAG_NoAuction	The purchased item cannot be auctioned at the Bazaar.
 *  0x0004 (Bit 2)	FLAG_NoSell	The item cannot be sold to regular vendors for gold.
 *  0x0008 (Bit 3)	FLAG_NoDrop	Item cannot be deleted / dropped from inventory without extra confirmation.
 *  0x0010 (Bit 4)	FLAG_No_PvP	Item cannot be used in PvP (Ranked or Practice).
 *  0x0020 (Bit 5)	FLAG_CrownsOnly	Flags the item as a Crowns-exclusive item in the UI (shows Crowns badge).
 *  0x0040 (Bit 6)	FLAG_NoGift	Disallows gifting this specific item to friends (similar to m_noGift).
 *  0x0080 (Bit 7)	FLAG_Retired	Marks the item as retired / legacy (often hidden or archived).
 *  0x0100 (Bit 8)	FLAG_NoDye	Item cannot be dyed in the Dye Shop.
 *  0x0200 (Bit 9)	FLAG_PvPCurrencyOnly	Item can only be purchased with PvP Arena Tickets / currency.
 *  0x0400 (Bit 10)	FLAG_ArenaPointsOnly	Item is restricted to Arena point purchases.
 *  0x0800 (Bit 11)	FLAG_DoubleConfirmDrop	Requires double confirmation when trashing/deleting the item.
 *  0x1000 (Bit 12)	FLAG_NoBargain	Prevents discount / bargain calculations on the item.
 * 
 * TODO:
 * - Item purchasing
 * - Removing from wishlist (& Wishlist privacy)
 * - Daily Spiral
 * - Populate store
 * - find out what flags do
 * 
 * Created by: Phill030
 * Version: KALI 1.0
 * Last Updated: 14.09.2026
 */

using Akka.Actor;
using Imcodec.CoreObject;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Imlight.CoreLib.Game.Services;


internal class CrownShopService(SessionActor sessionActor) : MessageService(sessionActor) {

    // One tab can have multiple categories (Gear -> Hat, Robe, Shoes, etc.)
    private readonly IReadOnlyList<CrownShopCategoryMenu> _tabs = new[] {
        new CrownShopCategoryMenu() {
            m_name = "CrownShopSWF_DailySpiral",
            m_ID = 36,
            m_description = "0",
            m_iconResource = "GUI/CrownShopIcons/Categories/DailySpiral.dds",
            m_categoryIDs = new List<int>() { 0,25,2,4,16,8,15,13,5,7 },
            m_tags = "Splash"
        },
        new CrownShopCategoryMenu() {
            m_name = "CrownShopSWF_MenuFeatured",
            m_ID = 37,
            m_description = "Featured",
            m_iconResource = "GUI/CrownShopIcons/Categories/Sale_Items.dds",
            m_categoryIDs = new List<int>() { 0 },
            m_tags = "Seperate"
        },
        new CrownShopCategoryMenu() {
            m_name = "CrownShopSWF_MenuCards",
            m_ID = 38,
            m_description = "0",
            m_iconResource = "GUI/CrownShopIcons/Categories/Cards.dds",
            m_categoryIDs = new List<int>() { 25,26,20 },
            m_tags = "Seperate"
        },
        new CrownShopCategoryMenu() {
            m_name = "CrownShopSWF_MenuMounts",
            m_ID = 39,
            m_description = "0",
            m_iconResource = "GUI/CrownShopIcons/Categories/Mounts_Permanent.dds",
            m_categoryIDs = new List<int>() { 2,3 },
            m_tags = "Seperate"
        },
        new CrownShopCategoryMenu() {
            m_name = "CrownShopSWF_MenuPets",
            m_ID = 40,
            m_description = "0",
            m_iconResource = "GUI/CrownShopIcons/Categories/Pets.dds",
            m_categoryIDs = new List<int>() { 4, 26 },
            m_tags = "Seperate"
        },
        new CrownShopCategoryMenu() {
            m_name = "CrownShopSWF_MenuGold",
            m_ID = 41,
            m_description = "0",
            m_iconResource = "GUI/CrownShopIcons/Categories/Gold.dds",
            m_categoryIDs = new List<int>() { 1, 47 },
            m_tags = "Seperate"
        },
        new CrownShopCategoryMenu() {
            m_name = "CrownShopSWF_MenuElixirs",
            m_ID = 42,
            m_description = "0",
            m_iconResource = "GUI/CrownShopIcons/Categories/Elixirs.dds",
            m_categoryIDs = new List<int>() { 16,34,35 },
            m_tags = "Seperate"
        },
        new CrownShopCategoryMenu() {
            m_name = "CrownShopSWF_MenuGear",
            m_ID = 43,
            m_description = "0",
            m_iconResource = "GUI/CrownShopIcons/Categories/Clothing.dds",
            m_categoryIDs = new List<int>() { 8, 15, 13, 10, 9, 11, 17, 23, 12, 14 },
            m_tags = "Seperate"
        },
        new CrownShopCategoryMenu() {
            m_name = "CrownShopSWF_MenuHousing",
            m_ID = 44,
            m_description = "0",
            m_iconResource = "GUI/CrownShopIcons/Categories/Housing.dds",
            m_categoryIDs = new List<int>() { 5,7,30,27,6,24,21,22 },
            m_tags = "Seperate"
        },
        new CrownShopCategoryMenu() {
            m_name = "CrownShopSWF_MenuGameplay",
            m_ID = 45,
            m_description = "0",
            m_iconResource = "GUI/CrownShopIcons/Categories/Everything.dds",
            m_categoryIDs = new List<int>() { 18,33,28,29,31,32,48,19 },
            m_tags = "Seperate"
        },
        new CrownShopCategoryMenu() {
            m_name = "CrownShopSWF_Wishlist",
            m_ID = 46,
            m_description = "0",
            m_iconResource = "GUI/CrownShopIcons/Categories/Wishlist.dds",
            m_categoryIDs = [],
            m_tags = "Wishlist"
        }
    };

    private readonly IReadOnlyList<CrownShopCategory> _categories = new[] {
    new CrownShopCategory() {
        m_name = "CrownShopSWF_CategoryFeatured",
        m_ID = 0,
        m_parentTabID = 37,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Sale_Items.dds",
        m_tags = "Featured",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = false,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },

    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryGold",
        m_ID = 1,
        m_parentTabID = 41,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Gold.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = true,
        m_allowMultipleBuy = false,
        m_forceDisallowMultipleBuy = true,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryPermanentMounts",
        m_ID = 2,
        m_parentTabID = 39,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Mounts_Permanent.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryRentalMounts",
        m_ID = 3,
        m_parentTabID = 39,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Mounts_Rental.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = false,
        m_forceDisallowMultipleBuy = true,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryPets",
        m_ID = 4,
        m_parentTabID = 40,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Pets.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryHouses",
        m_ID = 5,
        m_parentTabID = 44,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Housing.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = false,
        m_forceDisallowMultipleBuy = true,
        m_isHousesCategory = true,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryFurniture",
        m_ID = 6,
        m_parentTabID = 44,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Furniture.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = true,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryGardening",
        m_ID = 7,
        m_parentTabID = 44,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Seeds.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = true,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryClothingBundle",
        m_ID = 8,
        m_parentTabID = 43,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Clothing_Sets.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryRobes",
        m_ID = 9,
        m_parentTabID = 43,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Robes.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryShoes",
        m_ID = 10,
        m_parentTabID = 43,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Shoes.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryHats",
        m_ID = 11,
        m_parentTabID = 43,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Clothing.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryRings",
        m_ID = 12,
        m_parentTabID = 43,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Rings.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryAmulets",
        m_ID = 13,
        m_parentTabID = 43,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Amulets.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryAthames",
        m_ID = 14,
        m_parentTabID = 43,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Athames.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryWeapons",
        m_ID = 15,
        m_parentTabID = 43,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Weapons.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryElixirs",
        m_ID = 16,
        m_parentTabID = 42,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Elixirs.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = true,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryTransformations",
        m_ID = 17,
        m_parentTabID = 43,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Transformations.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryHenchmen",
        m_ID = 18,
        m_parentTabID = 45,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Henchmen.dds",
        m_tags = "OpenToDuringCombat",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = false,
        m_forceDisallowMultipleBuy = true,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryEverything",
        m_ID = 19,
        m_parentTabID = 45,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Everything.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = false,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = true,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryBoosters",
        m_ID = 20,
        m_parentTabID = 38,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/CCG.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = true,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryTeleporters",
        m_ID = 21,
        m_parentTabID = 44,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Teleporter.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = true,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryInstruments",
        m_ID = 22,
        m_parentTabID = 44,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Instruments.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = true,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryWigs",
        m_ID = 23,
        m_parentTabID = 43,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Hairstyles.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryMinigames",
        m_ID = 24,
        m_parentTabID = 44,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Games.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = true,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryCCG",
        m_ID = 25,
        m_parentTabID = 38,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Booster.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryCCGPetSnacks",
        m_ID = 26,
        m_parentTabID = 40,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Pet_Snack.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = true,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryCCGHousing",
        m_ID = 27,
        m_parentTabID = 44,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Furniture_Sets.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryCCGReagent",
        m_ID = 28,
        m_parentTabID = 45,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Reagents.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryFishing",
        m_ID = 29,
        m_parentTabID = 45,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Fishing.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = true,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryCastleBlocks",
        m_ID = 30,
        m_parentTabID = 44,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Building_Blocks.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = true,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryEmotes",
        m_ID = 31,
        m_parentTabID = 45,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Emotes.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = false,
        m_forceDisallowMultipleBuy = true,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryTeleportEffects",
        m_ID = 32,
        m_parentTabID = 45,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Teleport_Effects.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = false,
        m_forceDisallowMultipleBuy = true,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryBundles",
        m_ID = 33,
        m_parentTabID = 45,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Bundles.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = false,
        m_allowMultipleBuy = true,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryGroupElixirs",
        m_ID = 34,
        m_parentTabID = 42,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Elixirs_Group.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = true,
        m_allowMultipleBuy = false,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = true
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_CategoryWorldElixirs",
        m_ID = 35,
        m_parentTabID = 42,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Elixirs_World.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = true,
        m_allowMultipleBuy = false,
        m_forceDisallowMultipleBuy = true,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_Lunari",
        m_ID = 47,
        m_parentTabID = 41,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Lunari.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = true,
        m_allowMultipleBuy = false,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    },
    new CrownShopCategory
    {
        m_name = "CrownShopSWF_Raid",
        m_ID = 48,
        m_parentTabID = 45,
        m_description = "0",
        m_iconResource = "GUI/CrownShopIcons/Categories/Bundles.dds",
        m_tags = "None",
        m_dontFilterOwnedRecoItems = true,
        m_allowMultipleBuy = false,
        m_forceDisallowMultipleBuy = false,
        m_isHousesCategory = false,
        m_isEverythingCategory = false,
        m_isGroupElixirsCategory = false
    }
};


    private static List<CrownShopItem> s_catalogCache;
    private static readonly object s_catalogLock = new();
    private static readonly Lazy<HashSet<ulong>> s_boosterPackIds = new(() =>
        CoreObjectFactory.TemplateManifest.m_serializedTemplates
            .Where(t => t.m_filename.Contains("BoosterPack", StringComparison.OrdinalIgnoreCase))
            .Select(t => (ulong) t.m_id)
            .ToHashSet()
    );

    private static bool IsBoosterPack(ulong templateId)
        => s_boosterPackIds.Value.Contains(templateId);

    protected static Props Props(SessionActor parentActor)
            => Akka.Actor.Props.Create(() => new CrownShopService(parentActor));

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_SEGDATA_REQUEST))]
    private void ReceiveCrownShopSegDataRequest(WIZARD_12_PROTOCOL.MSG_PCS_SEGDATA_REQUEST message) {
        var wizard = GetActiveWizard();

        var segmentationInputData = new SegmentationInputData() {
            m_bIsValidSegmentationData = true,
            m_playerLevel = wizard.MagicSchoolBehavior.Level,
            m_playerSchoolOfFocus = "St", // First two letters of the school?!
            m_accountNDaysAged = (int) (DateTime.Now - wizard.Account.CreationTime).TotalDays,
            m_accountNDaysSinceLastLogin = 0,
            m_accountNDaysSinceLastPurchase = 0,
            m_accountNDaysLastCrownsPurchase = 0,
            m_accountIsMember = 0,
            m_accountIsCSR = 0,
            m_accountNCrownsSpent = 0,
            m_accountNCrownsInWallet = wizard.Account.Crowns,
            m_accountNDaysSinceItemPurchased = [],
            m_numOfParticularItemInInventory = [],
            m_numItemsOfCategoryInInventory = [],
            m_playerHasBadge = [], // Used for calculating Requirements
            m_accountNHighestWorld = 0
        };

        var serializer = new ObjectSerializer(
            Versionable: false,
            Behaviors: SerializerFlags.None
        );
        var propertyFlags = PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit;
        if (!serializer.Serialize(segmentationInputData, propertyFlags, out var serializedData)) {
            Logger.Error("Failed to serialize SegmentationInputData");
            return;
        }

        SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_SEGDATA_RESPONSE {
            Success = (byte)1,
            Data = serializedData
});

        // We need to call this to "sync" the CrownShop Crown-Balance, else it just says 0
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_CROWNBALANCE {
            Failure = (byte)0,
            TotalCrowns = wizard.Account.Crowns,
            CharacterID = wizard.CharId,
            CacheBalanceForCSSegmentation = (byte)1
        });
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_LIST_REQUEST))]
    private void ReceiveCrownShopListRequest(WIZARD_12_PROTOCOL.MSG_PCS_LIST_REQUEST message) {
        var wizard = GetActiveWizard();

        var crownShopData = new CrownShopData {
            m_items = GetOrCreateCatalog(),
            m_crownShopLayout = new CrownShopLayout {
                m_categories = _categories.ToList(),
                m_tabs = _tabs.ToList(),
            },
            m_recomendedItems = new LevelData() {
                m_level = wizard.MagicSchoolBehavior.Level,
                m_categoryData = new List<CategoryData>()
            },
            m_crownShopSegReqsSummary = new CrownShopSegReqsSummary() {
                m_anySegReqsRelyOnWebData = false,
                m_csvNItemsList = "",
                m_csvNItemsCategoryList = "",
                m_csvNDaysSinceItemPurchasedList = "",
                m_csvHasBadgeList = ""
            },
            m_wishlistMaxSize = 30,
            m_wishlistSBExpansionSize = 10
        };

        var serializer = new ObjectSerializer(
            Versionable: false,
            Behaviors: SerializerFlags.SerializeFlags | SerializerFlags.Compress
        );

        var propertyFlags = PropertyFlags.Prop_Save | PropertyFlags.Prop_Public;
        if (!serializer.Serialize(crownShopData, propertyFlags, out var serializedData)) {
            Logger.Error("Failed to serialize CrownShopData");
            return;
        }

        uint nextUpdateId = message.UpdateID == 0 ? 1 : message.UpdateID + 1;
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_LIST_RESPONSE {
            Data = serializedData,
            Updates = "",
            UpdateID = nextUpdateId,
            Error = 0,
        });
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_UPDATEUSERWISHLIST))]
    private void ReceiveWishlistUpdate(WIZARD_12_PROTOCOL.MSG_PCS_UPDATEUSERWISHLIST message) { }

    // This checks if the item *can* be bought (NOT if the player has enough money) (Time ran out, etc.)
    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_PRICE_LOCK_REQUEST))]
    private void ReceivePriceLockReq(WIZARD_12_PROTOCOL.MSG_PCS_PRICE_LOCK_REQUEST message) {
        // TODO: There must be a list with itemId, saleId and prices stored somewhere in the DB
        Logger.Information("Received MSG_PCS_PRICE_LOCK_REQUEST");

        var msg = new WIZARD_12_PROTOCOL.MSG_PCS_PRICE_LOCK_RESPONSE {
            CostCrowns = 1,
            CostGold = 0,
            CostTickets = 0,
            Error = 0,
            Item = message.Item
        };

        SendToSocket(msg);
    }

    // The client requests to buy [N amount] of this item (and possibly gift it to another player),
    // which will be removed(?) from the list of things they can buy (if enabled).
    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_REQUEST))]
    private void ReceivePurchaseRequest(WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_REQUEST message) {
        var wizard = GetActiveWizard();
        Logger.Information("Received MSG_PCS_PURCHASE_REQUEST");
        Logger.Information(JsonSerializer.Serialize(message));

        // todo: cost needs to be compared to the item stored in the DB, or else the client could bypass this!!
        var amountToPay = message.Count * message.Cost;
        if (wizard.Account.Crowns < amountToPay) {
            var msg = new WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_RESPONSE {
                Item = message.Item,
                Error = 1,
                Cost = amountToPay,
                Count = message.Count,
                Gifted = (byte)(message.Recipient == 0 ? 0 : 1),
                Type = message.Type
            };
            SendToSocket(msg);
            return;
        }

        // Booster packs should NEVER be added to the inventory! If done so, the client is unable to join anymore!
        if (IsBoosterPack(message.Item)) {
            OpenBoosterPack(wizard, message.Item);
        } else {
            // Add item to inventory

            // Check if the item is emote or teleport effect
            var template = CoreObjectFactory.GetCoreTemplate(message.Item);
            var customEmote = template?.m_behaviors?.OfType<CustomEmoteBehaviorTemplate>().FirstOrDefault();
            if (customEmote != null && customEmote.m_bitFieldNumber >= 0) {
                if (customEmote.m_emoteType == CustomEmoteType.CE_Teleport) {
                    wizard.UnlockCustomTeleportEffect(customEmote.m_bitFieldNumber);
                }
                else {
                    wizard.UnlockCustomEmote(customEmote.m_bitFieldNumber);
                }
            }

            // todo: serialize the item only once   
            var coSerializer = new CoreObjectSerializer(
                behaviors: Imcodec.ObjectProperty.SerializerFlags.None
            );
            for (uint i = 0; i < message.Count; i++) {
                if (!wizard.AddItemToInventory(message.Item, out WizClientObjectItem itemCoreObject)) {
                    Logger.Warning("Could not add item to inventory.");

                    var msg = new WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_RESPONSE {
                        Item = message.Item,
                        Error = 1,
                        Cost = amountToPay,
                        Count = message.Count,
                        Gifted = (byte) (message.Recipient == 0 ? 0 : 1),
                        Type = message.Type
                    };
                    SendToSocket(msg);
                    return;
                }

                if (!coSerializer.Serialize(itemCoreObject, 24, out var serializedItem)) {
                    Logger.Warning("Failed to serialize core object.");
                    return;
                }

                SendToSocket(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM {
                    GlobalID = wizard.GameObjectID,
                    SerializedItem = serializedItem
                });
            }
        }

        
        wizard.Account.SetCrowns(wizard.Account.Crowns - amountToPay);
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_RESPONSE {
            Item = message.Item,
            Error = 0,
            Cost = amountToPay,
            Count = message.Count,
            Gifted = (byte) (message.Recipient == 0 ? 0 : 1),
            Type = message.Type
        });

        // Sync crowns
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_CROWNBALANCE {
            Failure = (byte) 0,
            TotalCrowns = wizard.Account.Crowns,
            CharacterID = wizard.CharId,
            CacheBalanceForCSSegmentation = (byte) 1
        });
    }

    private void OpenBoosterPack(Wizard wizard, ulong packTemplateId) {
        var lootItems = new List<LootInfo>();

        for (int i = 0; i < 7; i++) {
            lootItems.Add(new ItemLootInfo {
                m_lootType = LOOT_TYPE.LOOT_TYPE_ITEM,
                m_itemID = (GID) packTemplateId,
                m_numItems = 1
            });
        }

        var lootInfoList = new LootInfoList {
            m_loot = lootItems,
            m_goldInfo = null,
            m_lootRarityList = new LootRarityList { m_loot = [] }
        };

        var serializer = new ObjectSerializer(Versionable: false);
        if (serializer.Serialize(lootInfoList, 4, out var serializedLoot)) {
            SendToSocket(new WIZARD_12_PROTOCOL.MSG_LOOT {
                GlobalID = wizard.GameObjectID,
                LootList = serializedLoot
            });
        }
    }

    private static List<CrownShopItem> GetOrCreateCatalog() {
        if (s_catalogCache != null) {
            return s_catalogCache;
        }

        lock (s_catalogLock) {
            if (s_catalogCache != null) {
                return s_catalogCache;
            }

            var catalog = new List<CrownShopItem>();
            var templates = CoreObjectFactory.TemplateManifest.m_serializedTemplates;
            foreach (var entry in templates) {
                string path = entry.m_filename;
                ulong id = entry.m_id;
                string displayPriority = null;

                // Permanent Mounts
                if (path.StartsWith("ObjectData/Mounts/", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains("1Day", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains("7Day", StringComparison.OrdinalIgnoreCase)) {
                    displayPriority = "2:1,19:1,0:1"; // Cat 2: Permanent Mounts, 19: Everything
                }

                // Card Packs
                else if (path.StartsWith("ObjectData/BoosterPack-Set-", StringComparison.OrdinalIgnoreCase)
                         && !path.Contains("Dummy", StringComparison.OrdinalIgnoreCase)) {
                    displayPriority = "20:1,19:1,0:1"; // Cat 20: Boosters / Packs
                }

                // Pets
                else if (path.StartsWith("ObjectData/Pets/", StringComparison.OrdinalIgnoreCase)) {
                    displayPriority = "4:1,19:1,0:1"; // Cat 4: Pets
                }

                // Elixirs
                else if (path.Contains("Elixir", StringComparison.OrdinalIgnoreCase)
                         && path.StartsWith("ObjectData/", StringComparison.OrdinalIgnoreCase)) {
                    displayPriority = "16:1,19:1,0:1"; // Cat 16: Elixirs
                }

                // Special Sets / Bundles
                else if (path.StartsWith("ObjectData/SpecialSets/", StringComparison.OrdinalIgnoreCase)) {
                    displayPriority = "8:1,19:1,0:1"; // Cat 8: Clothing Bundles
                }

                else if (path.StartsWith("ObjectData/Housing/Deeds/", StringComparison.OrdinalIgnoreCase)
                         || path.EndsWith("PropertyDeed.xml", StringComparison.OrdinalIgnoreCase)) {
                    displayPriority = "5:1,19:1,0:1"; // Cat 5: Houses (Tab 44)
                }

                else if (path.StartsWith("ObjectData/Emotes/")) {
                    if (path.Contains("Teleport", StringComparison.OrdinalIgnoreCase)) {
                        displayPriority = "32:1,19:1,0:1"; // Cat 32: Teleport Effects (Tab 45)
                    }
                    else {
                        displayPriority = "31:1,19:1,0:1"; // Cat 31: Emotes (Tab 45)
                    }
                }

                if (displayPriority != null) {
                    catalog.Add(new CrownShopItem {
                        m_itemTemplateId = id,
                        m_itemFlags = 0,
                        m_goldCost = 0,
                        m_crownsCost = 1,
                        m_ticketCost = 0,
                        m_displayPriority = displayPriority,
                        m_strikethruCrowns = 0,
                        m_strikethruGold = 0,
                        m_description = "", // Client pulls the real name/desc via Template ID
                        m_saleID = 5129,
                        m_recommendIfOwned = false,
                        m_combatOnly = false,
                        m_noGift = false,
                        m_segReqsStatement = "",
                        m_segReqsPoolsStatements = []
                    });
                }
            }

            s_catalogCache = catalog;
            Logger.Information("CrownShop: Loaded {0} special items into the Crown Shop.", Logger.Args(s_catalogCache.Count));
            return s_catalogCache;
        }
    }
}
