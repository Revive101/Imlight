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
 * 
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
 * Last Updated: 13.09.2026
 */

using Akka.Actor;
using Akka.Util.Internal;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using System;
using System.Collections.Generic;
using System.Linq;

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

    private readonly IReadOnlyList<(uint Id, string Name)> _mounts = new[]
    {
        (191229u, "Enchanted Broom (PERM)"),
        (191230u, "Purple Glider (PERM)"),
        (191231u, "Horned Sweeper (PERM)"),
        (191237u, "Chestnut Pony (PERM)"),
        (191238u, "White Mare (PERM)"),
        (191239u, "Black Stallion (PERM)"),
    };

    protected static Props Props(SessionActor parentActor)
            => Akka.Actor.Props.Create(() => new CrownShopService(parentActor));

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_SEGDATA_REQUEST))]
    private void ReceiveCrownShopSegDataRequest(WIZARD_12_PROTOCOL.MSG_PCS_SEGDATA_REQUEST message) {
        var wizard = GetActiveWizard();

        var segmentationInputData = new SegmentationInputData() {
            m_bIsValidSegmentationData = true,
            m_playerLevel = wizard.MagicSchoolBehavior.Level,
            m_playerSchoolOfFocus = "St", // First two letters of the school?!
            m_accountNDaysAged = 7172719,
            m_accountNDaysSinceLastLogin = 0,
            m_accountNDaysSinceLastPurchase = 0,
            m_accountNDaysLastCrownsPurchase = 0,
            m_accountIsMember = 0,
            m_accountIsCSR = 0,
            m_accountNCrownsSpent = 0,
            m_accountNCrownsInWallet = 1258291200,
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

        var msg = new WIZARD_12_PROTOCOL.MSG_PCS_SEGDATA_RESPONSE {
            Success = 1,
            Data = serializedData
        };

        SendToSocket(msg);
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_LIST_REQUEST))]
    private void ReceiveCrownShopListRequest(WIZARD_12_PROTOCOL.MSG_PCS_LIST_REQUEST message) {
        var wizard = GetActiveWizard();

        var items = new List<CrownShopItem>();
        foreach (var (id, name) in _mounts) {
            items.Add(new CrownShopItem {
                m_itemTemplateId = id,
                m_itemFlags = 0,
                m_goldCost = 0,
                m_crownsCost = 1,
                m_ticketCost = 0,
                m_displayPriority = "10:2176,19:2944,0:7104",
                m_strikethruCrowns = 0,
                m_strikethruGold = 0,
                m_description = name,
                m_saleID = 5129,
                m_recommendIfOwned = false,
                m_combatOnly = false,
                m_noGift = false,
                m_segReqsStatement = "",
                m_segReqsPoolsStatements = []
            });
        }

        var crownShopData = new CrownShopData {
            m_items = items,
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
                m_csvHasBadgeList = "DefeatMorganthe,KillMallistaire,FinishAR-PostLM-MAIN-001,FinishNV-CONA-MAIN-005,WinNightmare,FinishAR-PostWL-MAIN-001,Raid01_QuestComplete_01,Raid02_QuestComplete_01,FinishAllSelenopolisStoryQuests,Raid03_QuestComplete_01,FinishAllDarkmoorMainQuests,Raid04_QuestComplete_01"
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
}
