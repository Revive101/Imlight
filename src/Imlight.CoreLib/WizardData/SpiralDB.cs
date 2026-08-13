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
using System.Diagnostics;
using System.IO;
using Imlight.Common;
using Imlight.CoreLib.WizardData.Models.World;
using Imcodec.ObjectProperty.TypeCache;
using Newtonsoft.Json;

namespace Imlight.CoreLib.WizardData;

public static class SpiralDB {

    private static readonly string s_remote
        = ConfigurationManager.Settings["Database.SpiralDBRemote"];
    private static readonly string s_branch
        = ConfigurationManager.Settings["Database.SpiralDBBranch"];
    private static readonly string s_localPath
        = ConfigurationManager.Settings["Database.SpiralDBLocalPath"];
    private static readonly bool s_autoFetch
        = ConfigurationManager.Settings["Database.SpiralDBAutoFetch"].AsBool();
    private static readonly bool s_disableRemote
        = ConfigurationManager.Settings["Database.SpiralDBDisableRemote"].AsBool();
    private static readonly int s_fetchTimeoutSec
        = ConfigurationManager.Settings["Database.SpiralDBFetchTimeout"].AsInt();
    private static readonly bool s_rollbackOnFailure
        = ConfigurationManager.Settings["Database.SpiralDBRollbackOnFailure"].AsBool();

    private static readonly JsonSerializerSettings s_jsonSettings = new() {
        TypeNameHandling = TypeNameHandling.Auto,
        NullValueHandling = NullValueHandling.Ignore
    };

    private static ConcurrentDictionary<string, CreatureSpellbook> s_creatureSpellbooks
        = new(StringComparer.OrdinalIgnoreCase);
    private static ConcurrentDictionary<string, DropTable> s_dropTables
        = new(StringComparer.OrdinalIgnoreCase);
    private static GlobalRegistryModel s_globalRegistry = new();
    private static ConcurrentDictionary<ulong, NPCInventory> s_npcInventories = new();
    private static ConcurrentDictionary<ulong, NPCSpellInventory> s_npcSpellInventories = new();
    private static ConcurrentDictionary<ulong, NpcDropTable> s_npcDropTables = new();
    private static List<QuestTemplate> s_questTemplates = [];
    private static ConcurrentDictionary<string, QuestTemplate> s_questTemplatesByName
        = new(StringComparer.OrdinalIgnoreCase);
    private static ConcurrentDictionary<string, WizardZoneData> s_zoneData
        = new(StringComparer.OrdinalIgnoreCase);
    private static ConcurrentDictionary<ulong, NpcTreasureCardInventory> s_treasureCardInventories = new();

    public static IReadOnlyDictionary<string, CreatureSpellbook> CreatureSpellbooks => s_creatureSpellbooks;
    public static IReadOnlyDictionary<string, DropTable> DropTables => s_dropTables;
    public static GlobalRegistryModel GlobalRegistry => s_globalRegistry;
    public static IReadOnlyDictionary<ulong, NPCInventory> NpcInventories => s_npcInventories;
    public static IReadOnlyDictionary<ulong, NPCSpellInventory> NpcSpellInventories => s_npcSpellInventories;
    public static IReadOnlyDictionary<ulong, NpcDropTable> NpcDropTables => s_npcDropTables;
    public static IReadOnlyList<QuestTemplate> QuestTemplates => s_questTemplates;
    public static IReadOnlyDictionary<string, WizardZoneData> ZoneData => s_zoneData;
    public static IReadOnlyDictionary<ulong, NpcTreasureCardInventory> TreasureCardInventories => s_treasureCardInventories;

    /// <summary>
    /// Fetches the spiralDB repository (if remote is enabled) and loads all JSON
    /// files into memory. Must be called once on server boot before any collection
    /// is accessed.
    /// </summary>
    public static void Load() {
        var basePath = Path.GetFullPath(s_localPath);

        if (!s_disableRemote) {
            try {
                SyncRepository(basePath);
            }
            catch (Exception ex) {
                Logger.Error("Failed to sync SpiralDB repository: {0}", Logger.Args(ex.Message));
                if (s_rollbackOnFailure && Directory.Exists(basePath)) {
                    Logger.Information("Rolling back — using existing local SpiralDB data.");
                }
                else if (!Directory.Exists(basePath)) {
                    Logger.Error("No local SpiralDB data available and remote sync failed. " +
                                 "SpiralDB will be empty.");

                    return;
                }
            }
        }

        if (!Directory.Exists(basePath)) {
            Logger.Error("SpiralDB local path does not exist: {0}", Logger.Args(basePath));

            return;
        }

        Logger.Information("Loading SpiralDB from {0}...", Logger.Args(basePath));

        try {
            // Build into temporary containers so a parse failure leaves old data intact.
            var spellbooks = new ConcurrentDictionary<string, CreatureSpellbook>(StringComparer.OrdinalIgnoreCase);
            var dropTables = new ConcurrentDictionary<string, DropTable>(StringComparer.OrdinalIgnoreCase);
            var globalRegistry = new GlobalRegistryModel();
            var npcInventories = new ConcurrentDictionary<ulong, NPCInventory>();
            var npcSpellInventories = new ConcurrentDictionary<ulong, NPCSpellInventory>();
            var npcDropTables = new ConcurrentDictionary<ulong, NpcDropTable>();
            var questTemplates = new List<QuestTemplate>();
            var questTemplatesByName = new ConcurrentDictionary<string, QuestTemplate>(StringComparer.OrdinalIgnoreCase);
            var zoneData = new ConcurrentDictionary<string, WizardZoneData>(StringComparer.OrdinalIgnoreCase);
            var treasureCardInventories = new ConcurrentDictionary<ulong, NpcTreasureCardInventory>();

            var filesLoaded = 0;

            filesLoaded += LoadCreatureSpellbooks(basePath, spellbooks);
            filesLoaded += LoadDropTables(basePath, dropTables);
            filesLoaded += LoadGlobalRegistry(basePath, globalRegistry);
            filesLoaded += LoadNpcInventories(basePath, npcInventories);
            filesLoaded += LoadNpcSpellInventories(basePath, npcSpellInventories);
            filesLoaded += LoadNpcDropTables(basePath, npcDropTables);
            filesLoaded += LoadTreasureCardInventories(basePath, treasureCardInventories);
            filesLoaded += LoadQuestTemplates(basePath, questTemplates, questTemplatesByName);
            filesLoaded += LoadZoneData(basePath, zoneData);

            // Atomically swap.
            s_creatureSpellbooks = spellbooks;
            s_dropTables = dropTables;
            s_globalRegistry = globalRegistry;
            s_npcInventories = npcInventories;
            s_npcSpellInventories = npcSpellInventories;
            s_npcDropTables = npcDropTables;
            s_questTemplates = questTemplates;
            s_questTemplatesByName = questTemplatesByName;
            s_zoneData = zoneData;
            s_treasureCardInventories = treasureCardInventories;

            Logger.Information(
                "SpiralDB loaded {0} files: {1} spellbooks, {2} drop tables, {3} NPC inventories, " +
                "{4} NPC spell inventories, {5} NPC drop tables, {6} treasure card inventories, " +
                "{7} quest templates, {8} zone data entries.",
                Logger.Args(
                    filesLoaded,
                    s_creatureSpellbooks.Count,
                    s_dropTables.Count,
                    s_npcInventories.Count,
                    s_npcSpellInventories.Count,
                    s_npcDropTables.Count,
                    s_treasureCardInventories.Count,
                    s_questTemplates.Count,
                    s_zoneData.Count));
        }
        catch (Exception ex) {
            Logger.Error("Failed to load SpiralDB: {0}", Logger.Args(ex.Message));
            if (!s_rollbackOnFailure) {
                // Clear everything.
                s_creatureSpellbooks.Clear();
                s_dropTables.Clear();
                s_globalRegistry = new();
                s_npcInventories.Clear();
                s_npcSpellInventories.Clear();
                s_npcDropTables.Clear();
                s_treasureCardInventories.Clear();
                s_questTemplates.Clear();
                s_questTemplatesByName.Clear();
                s_zoneData.Clear();
            }
            // On rollback, the old references are still live — nothing to do.
        }
    }

    public static CreatureSpellbook GetCreatureSpellbook(string deckName) {
        s_creatureSpellbooks.TryGetValue(deckName, out var spellbook);

        return spellbook;
    }

    public static DropTable GetDropTable(string tableName) {
        s_dropTables.TryGetValue(tableName, out var dropTable);

        return dropTable;
    }

    public static bool TryGetNpcInventory(ulong templateID, out NPCInventory npcInventory)
        => s_npcInventories.TryGetValue(templateID, out npcInventory);

    public static bool TryGetNpcSpellInventory(ulong templateID, out NPCSpellInventory npcSpellInventory)
        => s_npcSpellInventories.TryGetValue(templateID, out npcSpellInventory);

    public static bool TryGetNpcDropTable(ulong templateID, out NpcDropTable npcDropTable)
        => s_npcDropTables.TryGetValue(templateID, out npcDropTable);

    public static bool TryGetTreasureCardInventory(ulong templateID, out NpcTreasureCardInventory inventory)
        => s_treasureCardInventories.TryGetValue(templateID, out inventory);

    public static QuestTemplate GetQuestByName(string questName) {
        if (questName is null) {
            return null;
        }

        s_questTemplatesByName.TryGetValue(questName, out var quest);

        return quest;
    }

    public static bool QuestExists(string questName)
        => s_questTemplatesByName.ContainsKey(questName);

    public static WizardZoneData GetZoneData(string zoneName) {
        s_zoneData.TryGetValue(zoneName, out var zone);

        return zone;
    }

    public static IReadOnlyCollection<WizardZoneData> GetAllZoneData()
        => (IReadOnlyCollection<WizardZoneData>) s_zoneData.Values;

    private static void SyncRepository(string basePath) {
        // @todo: maybe sometime in the future, allow for remotes not hosted on Github.
        var repoUrl = $"https://github.com/{s_remote}.git";

        if (!Directory.Exists(basePath)) {
            Logger.Information("Cloning SpiralDB from {0} (branch {1})...",
                Logger.Args(repoUrl, s_branch));

            var cloneArgs = $"clone --branch {s_branch} --depth 1 --single-branch {repoUrl} \"{basePath}\"";
            RunGit(cloneArgs);
        }
        else if (s_autoFetch) {
            Logger.Information("Fetching SpiralDB updates from {0} (branch {1})...",
                Logger.Args(repoUrl, s_branch));

            RunGit($"-C \"{basePath}\" fetch origin {s_branch}", s_fetchTimeoutSec);
            RunGit($"-C \"{basePath}\" checkout {s_branch}");
            RunGit($"-C \"{basePath}\" reset --hard origin/{s_branch}");

            Logger.Information("SpiralDB updated.");
        }
        else {
            Logger.Information("SpiralDB auto-fetch disabled — using existing local data.");
        }
    }

    private static void RunGit(string arguments, int timeoutSec = 0) {
        if (timeoutSec <= 0) {
            timeoutSec = s_fetchTimeoutSec;
        }

        var startInfo = new ProcessStartInfo("git", arguments) {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start git process. Is git installed?");

        if (!process.WaitForExit(timeoutSec * 1000)) {
            process.Kill();
            throw new TimeoutException($"Git command timed out after {timeoutSec}s: git {arguments}");
        }

        if (process.ExitCode != 0) {
            var err = process.StandardError.ReadToEnd().Trim();
            throw new InvalidOperationException(
                $"Git command failed (exit {process.ExitCode}): git {arguments}\n{err}");
        }
    }

    private static int LoadCreatureSpellbooks(string basePath,
                                              ConcurrentDictionary<string, CreatureSpellbook> target) {
        var dir = Path.Combine(basePath, "CreatureSpellbook");
        if (!Directory.Exists(dir)) {
            return 0;
        }

        var count = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json")) {
            try {
                var json = File.ReadAllText(file);
                var spellbook = JsonConvert.DeserializeObject<CreatureSpellbook>(json, s_jsonSettings);
                if (spellbook != null) {
                    target[spellbook.DeckName] = spellbook;
                    count++;
                }
            }
            catch (Exception ex) {
                Logger.Warning("Failed to load creature spellbook {0}: {1}",
                    Logger.Args(Path.GetFileName(file), ex.Message));
            }
        }

        return count;
    }

    private static int LoadDropTables(string basePath,
                                      ConcurrentDictionary<string, DropTable> target) {
        var dir = Path.Combine(basePath, "DropTables");
        if (!Directory.Exists(dir)) {
            return 0;
        }

        var count = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json")) {
            try {
                var json = File.ReadAllText(file);
                var dropTable = JsonConvert.DeserializeObject<DropTable>(json, s_jsonSettings);
                if (dropTable != null) {
                    target[dropTable.Name] = dropTable;
                    count++;
                }
            }
            catch (Exception ex) {
                Logger.Warning("Failed to load drop table {0}: {1}",
                    Logger.Args(Path.GetFileName(file), ex.Message));
            }
        }

        return count;
    }

    private static int LoadGlobalRegistry(string basePath, GlobalRegistryModel target) {
        var dir = Path.Combine(basePath, "GlobalRegistry");
        if (!Directory.Exists(dir)) {
            return 0;
        }

        var count = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json")) {
            try {
                var json = File.ReadAllText(file);
                var registry = JsonConvert.DeserializeObject<GlobalRegistryModel>(json, s_jsonSettings);
                if (registry != null) {
                    foreach (var kvp in registry.GlobalRegistryValues) {
                        target.GlobalRegistryValues[kvp.Key] = kvp.Value;
                    }

                    count++;
                }
            }
            catch (Exception ex) {
                Logger.Warning("Failed to load global registry {0}: {1}",
                    Logger.Args(Path.GetFileName(file), ex.Message));
            }
        }

        return count;
    }

    private static int LoadNpcInventories(string basePath,
                                          ConcurrentDictionary<ulong, NPCInventory> target) {
        var dir = Path.Combine(basePath, "NpcInventory");
        if (!Directory.Exists(dir)) {
            return 0;
        }

        var count = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json")) {
            try {
                var json = File.ReadAllText(file);
                var inventory = JsonConvert.DeserializeObject<NPCInventory>(json, s_jsonSettings);
                if (inventory != null) {
                    target[inventory.TemplateID] = inventory;
                    count++;
                }
            }
            catch (Exception ex) {
                Logger.Warning("Failed to load NPC inventory {0}: {1}",
                    Logger.Args(Path.GetFileName(file), ex.Message));
            }
        }

        return count;
    }

    private static int LoadNpcSpellInventories(string basePath,
                                               ConcurrentDictionary<ulong, NPCSpellInventory> target) {
        var dir = Path.Combine(basePath, "NpcSpellInventory");
        if (!Directory.Exists(dir)) {
            return 0;
        }

        var count = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json")) {
            try {
                var json = File.ReadAllText(file);
                var inventory = JsonConvert.DeserializeObject<NPCSpellInventory>(json, s_jsonSettings);
                if (inventory != null) {
                    target[inventory.TemplateID] = inventory;
                    count++;
                }
            }
            catch (Exception ex) {
                Logger.Warning("Failed to load NPC spell inventory {0}: {1}",
                    Logger.Args(Path.GetFileName(file), ex.Message));
            }
        }

        return count;
    }

    private static int LoadNpcDropTables(string basePath,
                                         ConcurrentDictionary<ulong, NpcDropTable> target) {
        var dir = Path.Combine(basePath, "NpcDropTable");
        if (!Directory.Exists(dir)) {
            return 0;
        }

        var count = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json")) {
            try {
                var json = File.ReadAllText(file);
                var dropTable = JsonConvert.DeserializeObject<NpcDropTable>(json, s_jsonSettings);
                if (dropTable != null) {
                    target[dropTable.TemplateID] = dropTable;
                    count++;
                }
            }
            catch (Exception ex) {
                Logger.Warning("Failed to load NPC drop table {0}: {1}",
                    Logger.Args(Path.GetFileName(file), ex.Message));
            }
        }

        return count;
    }

    private static int LoadTreasureCardInventories(string basePath,
                                                   ConcurrentDictionary<ulong, NpcTreasureCardInventory> target) {
        var dir = Path.Combine(basePath, "TreasureCardInventory");
        if (!Directory.Exists(dir)) {
            return 0;
        }

        var count = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json")) {
            try {
                var json = File.ReadAllText(file);
                var inventory = JsonConvert.DeserializeObject<NpcTreasureCardInventory>(json, s_jsonSettings);
                if (inventory != null) {
                    target[inventory.TemplateID] = inventory;
                    count++;
                }
            }
            catch (Exception ex) {
                Logger.Warning("Failed to load treasure card inventory {0}: {1}",
                    Logger.Args(Path.GetFileName(file), ex.Message));
            }
        }

        return count;
    }

    private static int LoadQuestTemplates(string basePath,
                                          List<QuestTemplate> targetList,
                                          ConcurrentDictionary<string, QuestTemplate> targetDict) {
        var dir = Path.Combine(basePath, "QuestTemplates");
        if (!Directory.Exists(dir)) {
            return 0;
        }

        var count = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json")) {
            try {
                var json = File.ReadAllText(file);
                var quest = JsonConvert.DeserializeObject<QuestTemplate>(json, s_jsonSettings);
                if (quest != null) {
                    targetList.Add(quest);
                    targetDict[quest.m_questName] = quest;
                    count++;
                }
            }
            catch (Exception ex) {
                Logger.Warning("Failed to load quest template {0}: {1}",
                    Logger.Args(Path.GetFileName(file), ex.Message));
            }
        }

        return count;
    }

    private static int LoadZoneData(string basePath,
                                    ConcurrentDictionary<string, WizardZoneData> target) {
        var dir = Path.Combine(basePath, "ZoneTransfer");
        if (!Directory.Exists(dir)) {
            return 0;
        }

        var count = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json")) {
            try {
                var json = File.ReadAllText(file);
                var zone = JsonConvert.DeserializeObject<WizardZoneData>(json, s_jsonSettings);
                if (zone != null) {
                    target[zone.ZoneName] = zone;
                    count++;
                }
            }
            catch (Exception ex) {
                Logger.Warning("Failed to load zone data {0}: {1}",
                    Logger.Args(Path.GetFileName(file), ex.Message));
            }
        }

        return count;
    }

}