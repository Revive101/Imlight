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
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Imlight.Common;
using Imlight.CoreLib.WizardData.Models.World;
using Imcodec.ObjectProperty.TypeCache;
using Newtonsoft.Json;

namespace Imlight.CoreLib.WizardData;

public static class SpiralDB {

    private static readonly string s_spiralDBPath
        = ConfigurationManager.Settings["Database.SpiralDBPath"];

    private static readonly JsonSerializerSettings s_jsonSettings = new() {
        TypeNameHandling = TypeNameHandling.Auto,
        NullValueHandling = NullValueHandling.Ignore
    };

    // ── Creature Spellbooks ──────────────────────────────────────────
    private static readonly ConcurrentDictionary<string, CreatureSpellbook> s_creatureSpellbooks = new(StringComparer.OrdinalIgnoreCase);
    public static IReadOnlyDictionary<string, CreatureSpellbook> CreatureSpellbooks => s_creatureSpellbooks;

    // ── Drop Tables ──────────────────────────────────────────────────
    private static readonly ConcurrentDictionary<string, DropTable> s_dropTables = new(StringComparer.OrdinalIgnoreCase);
    public static IReadOnlyDictionary<string, DropTable> DropTables => s_dropTables;

    // ── Global Registry ──────────────────────────────────────────────
    public static GlobalRegistryModel GlobalRegistry { get; } = new();

    // ── NPC Inventories ──────────────────────────────────────────────
    private static readonly ConcurrentDictionary<ulong, NPCInventory> s_npcInventories = new();
    public static IReadOnlyDictionary<ulong, NPCInventory> NpcInventories => s_npcInventories;

    // ── NPC Spell Inventories ────────────────────────────────────────
    private static readonly ConcurrentDictionary<ulong, NPCSpellInventory> s_npcSpellInventories = new();
    public static IReadOnlyDictionary<ulong, NPCSpellInventory> NpcSpellInventories => s_npcSpellInventories;

    // ── Quest Templates ──────────────────────────────────────────────
    private static readonly List<QuestTemplate> s_questTemplates = [];
    private static readonly ConcurrentDictionary<string, QuestTemplate> s_questTemplatesByName = new(StringComparer.OrdinalIgnoreCase);
    public static IReadOnlyList<QuestTemplate> QuestTemplates => s_questTemplates;

    // ── Zone Data ────────────────────────────────────────────────────
    private static readonly ConcurrentDictionary<string, WizardZoneData> s_zoneData = new(StringComparer.OrdinalIgnoreCase);
    public static IReadOnlyDictionary<string, WizardZoneData> ZoneData => s_zoneData;

    /// <summary>
    /// Loads all spiralDB JSON files into memory.
    /// Must be called once on server boot before any collection is accessed.
    /// </summary>
    public static void Load() {
        var basePath = Path.GetFullPath(s_spiralDBPath);

        if (!Directory.Exists(basePath)) {
            Logger.Error("SpiralDB path does not exist: {0}", Logger.Args(basePath));

            return;
        }

        Logger.Information("Loading SpiralDB from {0}...", Logger.Args(basePath));

        LoadCreatureSpellbooks(basePath);
        LoadDropTables(basePath);
        LoadGlobalRegistry(basePath);
        LoadNpcInventories(basePath);
        LoadNpcSpellInventories(basePath);
        LoadQuestTemplates(basePath);
        LoadZoneData(basePath);

        Logger.Information(
            "SpiralDB loaded: {0} spellbooks, {1} drop tables, {2} NPC inventories, " +
            "{3} NPC spell inventories, {4} quest templates, {5} zone data entries.",
            Logger.Args(
                s_creatureSpellbooks.Count,
                s_dropTables.Count,
                s_npcInventories.Count,
                s_npcSpellInventories.Count,
                s_questTemplates.Count,
                s_zoneData.Count));
    }

    /// <summary>
    /// Gets a creature spellbook by deck name.
    /// </summary>
    public static CreatureSpellbook GetCreatureSpellbook(string deckName) {
        s_creatureSpellbooks.TryGetValue(deckName, out var spellbook);

        return spellbook;
    }

    /// <summary>
    /// Gets a drop table by name.
    /// </summary>
    public static DropTable GetDropTable(string tableName) {
        s_dropTables.TryGetValue(tableName, out var dropTable);

        return dropTable;
    }

    /// <summary>
    /// Gets an NPC inventory by template ID.
    /// </summary>
    public static bool TryGetNpcInventory(ulong templateID, out NPCInventory npcInventory)
        => s_npcInventories.TryGetValue(templateID, out npcInventory);

    /// <summary>
    /// Gets an NPC spell inventory by template ID.
    /// </summary>
    public static bool TryGetNpcSpellInventory(ulong templateID, out NPCSpellInventory npcSpellInventory)
        => s_npcSpellInventories.TryGetValue(templateID, out npcSpellInventory);

    /// <summary>
    /// Gets a quest template by name.
    /// </summary>
    public static QuestTemplate GetQuestByName(string questName) {
        s_questTemplatesByName.TryGetValue(questName, out var quest);

        return quest;
    }

    /// <summary>
    /// Checks whether a quest template exists by name.
    /// </summary>
    public static bool QuestExists(string questName) => s_questTemplatesByName.ContainsKey(questName);

    /// <summary>
    /// Gets zone data by zone name.
    /// </summary>
    public static WizardZoneData GetZoneData(string zoneName) {
        s_zoneData.TryGetValue(zoneName, out var zone);

        return zone;
    }

    /// <summary>
    /// Gets all zone data for random selection (April Fools, etc.).
    /// </summary>
    public static IReadOnlyCollection<WizardZoneData> GetAllZoneData()
        => (IReadOnlyCollection<WizardZoneData>) s_zoneData.Values;

    private static void LoadCreatureSpellbooks(string basePath) {
        var dir = Path.Combine(basePath, "CreatureSpellbook");
        if (!Directory.Exists(dir)) {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(dir, "*.json")) {
            try {
                var json = File.ReadAllText(file);
                var spellbook = JsonConvert.DeserializeObject<CreatureSpellbook>(
                    json, s_jsonSettings);
                if (spellbook != null) {
                    s_creatureSpellbooks[spellbook.DeckName] = spellbook;
                }
            }
            catch (Exception ex) {
                Logger.Warning("Failed to load creature spellbook {0}: {1}",
                    Logger.Args(Path.GetFileName(file), ex.Message));
            }
        }
    }

    private static void LoadDropTables(string basePath) {
        var dir = Path.Combine(basePath, "DropTables");
        if (!Directory.Exists(dir)) {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(dir, "*.json")) {
            try {
                var json = File.ReadAllText(file);
                var dropTable = JsonConvert.DeserializeObject<DropTable>(
                    json, s_jsonSettings);
                if (dropTable != null) {
                    s_dropTables[dropTable.Name] = dropTable;
                }
            }
            catch (Exception ex) {
                Logger.Warning("Failed to load drop table {0}: {1}",
                    Logger.Args(Path.GetFileName(file), ex.Message));
            }
        }
    }

    private static void LoadGlobalRegistry(string basePath) {
        var dir = Path.Combine(basePath, "GlobalRegistry");
        if (!Directory.Exists(dir)) {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(dir, "*.json")) {
            try {
                var json = File.ReadAllText(file);
                var registry = JsonConvert.DeserializeObject<GlobalRegistryModel>(
                    json, s_jsonSettings);
                if (registry != null) {
                    // Merge all GlobalRegistry files into one model
                    foreach (var kvp in registry.GlobalRegistryValues) {
                        GlobalRegistry.GlobalRegistryValues[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (Exception ex) {
                Logger.Warning("Failed to load global registry {0}: {1}",
                    Logger.Args(Path.GetFileName(file), ex.Message));
            }
        }
    }

    private static void LoadNpcInventories(string basePath) {
        var dir = Path.Combine(basePath, "NpcInventory");
        if (!Directory.Exists(dir)) {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(dir, "*.json")) {
            try {
                var json = File.ReadAllText(file);
                var inventory = JsonConvert.DeserializeObject<NPCInventory>(
                    json, s_jsonSettings);
                if (inventory != null) {
                    s_npcInventories[inventory.TemplateID] = inventory;
                }
            }
            catch (Exception ex) {
                Logger.Warning("Failed to load NPC inventory {0}: {1}",
                    Logger.Args(Path.GetFileName(file), ex.Message));
            }
        }
    }

    private static void LoadNpcSpellInventories(string basePath) {
        var dir = Path.Combine(basePath, "NpcSpellInventory");
        if (!Directory.Exists(dir)) {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(dir, "*.json")) {
            try {
                var json = File.ReadAllText(file);
                var inventory = JsonConvert.DeserializeObject<NPCSpellInventory>(
                    json, s_jsonSettings);
                if (inventory != null) {
                    s_npcSpellInventories[inventory.TemplateID] = inventory;
                }
            }
            catch (Exception ex) {
                Logger.Warning("Failed to load NPC spell inventory {0}: {1}",
                    Logger.Args(Path.GetFileName(file), ex.Message));
            }
        }
    }

    private static void LoadQuestTemplates(string basePath) {
        var dir = Path.Combine(basePath, "QuestTemplates");
        if (!Directory.Exists(dir)) {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(dir, "*.json")) {
            try {
                var json = File.ReadAllText(file);
                var quest = JsonConvert.DeserializeObject<QuestTemplate>(
                    json, s_jsonSettings);
                if (quest != null) {
                    s_questTemplates.Add(quest);
                    s_questTemplatesByName[quest.m_questName] = quest;
                }
            }
            catch (Exception ex) {
                Logger.Warning("Failed to load quest template {0}: {1}",
                    Logger.Args(Path.GetFileName(file), ex.Message));
            }
        }
    }

    private static void LoadZoneData(string basePath) {
        var dir = Path.Combine(basePath, "ZoneTransfer");
        if (!Directory.Exists(dir)) {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(dir, "*.json")) {
            try {
                var json = File.ReadAllText(file);
                var zone = JsonConvert.DeserializeObject<WizardZoneData>(
                    json, s_jsonSettings);
                if (zone != null) {
                    s_zoneData[zone.ZoneName] = zone;
                }
            }
            catch (Exception ex) {
                Logger.Warning("Failed to load zone data {0}: {1}",
                    Logger.Args(Path.GetFileName(file), ex.Message));
            }
        }
    }

}