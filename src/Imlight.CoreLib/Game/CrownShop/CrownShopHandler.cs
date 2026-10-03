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
 * COMBAT SPELL DECK MANAGEMENT SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Manages the Crown Shop configuration and runtime data. Loads Crown Shop tabs,
 * categories, and items from JSON configuration files, converts them into the
 * corresponding game objects, and serializes the complete Crown Shop data structure for client consumption.
 * 
 * USAGE EXAMPLE:
 * 
 * 
 * NOTE:
 * 
 * Created by: Phill030
 * Version: KALI 1.0
 * Last Updated: 28.09.2026
 */

using Imcodec.IO;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Text.Json;

namespace Imlight.CoreLib.Game.CrownShop;

public static class CrownShopHandler {
    private static readonly bool s_enabled
        = ConfigurationManager.Settings["CrownShop.Enabled"].AsBool();
    private static readonly string s_crownShopConfigPath
        = ConfigurationManager.Settings["CrownShop.Path"];
    private static readonly uint s_wishlishMaxSize
        = ConfigurationManager.Settings["CrownShop.WishlistMaxSize"].AsUInt();
    private static readonly uint s_wishlistSBExpansionSize
    = ConfigurationManager.Settings["CrownShop.WishlistSBExpansionSize"].AsUInt();
    private static readonly bool s_autoReloadOnChanges
        = ConfigurationManager.Settings["CrownShop.AutoReloadOnChanges"].AsBool();
    public static readonly uint s_maxBuyCount
        = ConfigurationManager.Settings["CrownShop.MaxBuyCount"].AsUInt();


    private static readonly JsonSerializerOptions _jsonSerializerOptions = new() {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private static FileSystemWatcher _watcher;
    private static DateTime _lastReloadTime = DateTime.MinValue;
    private static readonly object _reloadLock = new();



    private static List<CrownShopTabConfig> s_crownShopTabs = new();
    private static List<CrownShopCategoryConfig> s_crownShopCategories = new();

    private static Dictionary<ulong, CrownShopItem> s_items = new();


    private static ByteString s_serializedCrownShopData = new();

    public static void Load() {
        if (!s_enabled) {
            Logger.Information("CrownShop not initializing (disabled in config)");
            return;
        }

        var basePath = Path.GetFullPath(s_crownShopConfigPath);
        if (!Directory.Exists(basePath)) {
            Logger.Warning("CrownShop configuration directory not found: {0}", Logger.Args(basePath));
            return;
        }

        // Load data from config files
        lock(_reloadLock) {
            Logger.Information("Loading CrownShop configuration from: {0}", Logger.Args(s_crownShopConfigPath));

            var loadedTabs = new List<CrownShopTabConfig>();
            var loadedCategories = new List<CrownShopCategoryConfig>();

            for(int i = 0; i < 11; i++) {
                var tabFilePath = Path.Combine(basePath, $"Tab{i}.json");

                if(!File.Exists(tabFilePath)) {
                    Logger.Warning("CrownShop tab configuration file not found: {0}", Logger.Args(tabFilePath));
                    continue;
                }

                try {
                    var json = File.ReadAllText(tabFilePath);
                    var tab = JsonSerializer.Deserialize<CrownShopTabConfig>(json, _jsonSerializerOptions);

                    if(tab == null) {
                        Logger.Warning("CrownShop tab configuration file is empty or invalid: {0}", Logger.Args(tabFilePath));
                        continue;
                    }

                    loadedTabs.Add(tab);

                    foreach (var category in tab.Categories) {
                        loadedCategories.Add(category);
                    }

                } catch (Exception e) {
                    Logger.Warning("Failed to load CrownShop tab configuration file: {0}. Error: {1}", Logger.Args(tabFilePath, e.Message));
                }
            }

            s_crownShopTabs = loadedTabs;
            s_crownShopCategories = loadedCategories;

            var layoutTabs = new List<CrownShopCategoryMenu>();
            var layoutCategories = new List<CrownShopCategory>();
            var crownShopItems = new Dictionary<ulong, CrownShopItem>();

            foreach (var tab in s_crownShopTabs) {
                var layoutTab = new CrownShopCategoryMenu() {
                    m_ID = tab.TabId,
                    m_name = tab.Name,
                    m_iconResource = tab.Icon,
                    m_categoryIDs = tab.CategoryIds,
                    m_tags = tab.Tags,
                    m_description = tab.Description
                };
                layoutTabs.Add(layoutTab);

                foreach (var category in tab.Categories) {
                    var layoutCategory = new CrownShopCategory() {
                        m_ID = category.CategoryId,
                        m_parentTabID = category.ParentTabId,
                        m_name = category.Name,
                        m_description = category.Description,
                        m_iconResource = category.Icon,
                        m_tags = category.Tags,
                        m_allowMultipleBuy = category.Flags.AllowMultipleBuy,
                        m_forceDisallowMultipleBuy = category.Flags.ForceDisallowMultipleBuy,
                        m_dontFilterOwnedRecoItems = category.Flags.DontFilterOwnedRecoItems,
                        m_isHousesCategory = category.Flags.IsHousesCategory,
                        m_isEverythingCategory = category.Flags.IsEverythingCategory,
                        m_isGroupElixirsCategory = category.Flags.IsGroupElixirsCategory
                    };
                    layoutCategories.Add(layoutCategory);
                }

                foreach(var item in tab.Items) {

                    var displayPriority = new StringBuilder();
                    for(int i = 0; i < item.DisplayPriority.Count; i++) {
                        displayPriority.Append($"{item.DisplayPriority[i]}:{i},");
                    }
                    displayPriority.Length--; // Remove last comma

                    var crownShopItem = new CrownShopItem() {
                        m_itemTemplateId = item.TemplateId,
                        m_itemFlags = item.ItemFlags,
                        m_goldCost = item.GoldCost,
                        m_strikethruGold = item.StrikethruGold,
                        m_crownsCost = item.CrownsCost,
                        m_strikethruCrowns = item.StrikethruCrowns,
                        m_ticketCost = item.TicketCost,
                        m_displayPriority = displayPriority.ToString(),
                        m_combatOnly = item.CrownShopItemFlags.CombatOnly,
                        m_noGift = item.CrownShopItemFlags.NoGift,
                        m_recommendIfOwned = item.CrownShopItemFlags.RecommendIfOwned,
                        m_saleID = 0,
                        m_description = "",
                        m_segReqsPoolsStatements = [],
                        m_segReqsStatement = "",
                        // Example: HasBadge(Raid01_QuestComplete_01) = True
                        // Example: ExcludeIfNoSegData = True,HasBadge(FinishAR-PostLM-MAIN-001) = True
                        // Example: ExcludeIfNoSegData = True,NLevel &gt; 169,HasBadge(WinNightmare) = True,HasBadge(FinishAR-PostWL-MAIN-001) = True
                    };

                    crownShopItems[item.TemplateId] = crownShopItem;
                }
            }

            s_items = crownShopItems;

            var crownShopLayout = new CrownShopLayout() {
                m_tabs = layoutTabs,
                m_categories = layoutCategories
            };

            var crownShopData = new CrownShopData() {
                m_items = crownShopItems.Values.ToList(),
                m_crownShopLayout = crownShopLayout,
                m_wishlistMaxSize = (int) s_wishlishMaxSize,
                m_wishlistSBExpansionSize = (int) s_wishlistSBExpansionSize,
                // Probably has something todo with MSG_PCS_SEGDATA_RESPONSE
                m_crownShopSegReqsSummary = new(),
                // todo: recommended items?
                m_recomendedItems = new LevelData()
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

            s_serializedCrownShopData = serializedData;

            Logger.Information("CrownShop successfully initialized: {0} tabs, {1} categories.",
                Logger.Args(s_crownShopTabs.Count, s_crownShopCategories.Count));

            if (s_autoReloadOnChanges && _watcher == null) {
                SetupAutoReloadWatcher(basePath);
            }

        }
    }

    private static void SetupAutoReloadWatcher(string basePath) {
        try {
            _watcher = new FileSystemWatcher(basePath, "*.json") {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnConfigFileChanged;
            _watcher.Created += OnConfigFileChanged;
            _watcher.Renamed += (s, e) => OnConfigFileChanged(s, e);

            Logger.Information("CrownShop: Live hot-reload watcher enabled for {0}.", Logger.Args(s_crownShopConfigPath));
        } catch(Exception e) {
            Logger.Warning("Could not enable CrownShop auto-reload watcher: {0}", Logger.Args(e.Message));
        }
    }

    private static void OnConfigFileChanged(object sender, FileSystemEventArgs e) {
        if ((DateTime.UtcNow - _lastReloadTime).TotalMilliseconds < 500) {
            return;
        }

        _lastReloadTime = DateTime.UtcNow;
        Logger.Information("CrownShop file changed. Reloading...");
        Load();
    }

    public static ByteString GetCrownShopData() => s_serializedCrownShopData;

    public static List<CrownShopItem> GetCrownShopItems() => s_items.Values.ToList();
    public static bool TryGetCrownShopItem(ulong id, out CrownShopItem item)
        => s_items.TryGetValue(id, out item);

}
