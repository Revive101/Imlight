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
 * LOCALE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages localization data retrieval and processing for English 
 * language resources, providing efficient lookup of localized strings.
 * 
 * USAGE EXAMPLE:
 * // Retrieve a localized name from a specific table
 * string characterName = Locale.GetEnglishName("CharacterNames", "001");
 * 
 * // Retrieve a localized name using a full key
 * string interactableName = Locale.GetEnglishName("Interactables_00000008");
 * 
 * // Retrieve first and last name keys for a full name
 * var (firstKey, lastKey) = Locale.GetEnglishNameKeys("WC-NPCs_00000024");
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 07/26/2025
 */

using System;
using System.Collections.Generic;
using System.Text;
using Imcodec.Wad;
using Imlight.Common;

namespace Imlight.CoreLib.Shared.Resources;

internal class Locale : RootDirectoryResourceSingleton<Locale>, IMemoryStreamDisposable {

    protected override string DirectoryName => "Locale/en-US/";

    private static readonly string s_qustFilePrefix = "WizQst";
    private static Dictionary<string, Dictionary<string, string>> s_data = [];
    private static Dictionary<string, (string firstKey, string lastKey)> s_namePartsMapping = [];

    protected override void AfterLoad() {
        // There are aoubt ~5,000 files here. They take up about 200MB of memory.
        // At this point, they are all loaded into memory. This function should process the locales
        // and give us data we actually need.

        Logger.Information("Loaded {0} English locale files.",
            Logger.Args(Files.Count));

        s_data = [];
        foreach (var file in Files) {
            var stream = file.Value;
            var record = file.Key;

            if (record.FileName.StartsWith(s_qustFilePrefix)) {
                continue;
            }

            var data = ProcessLocaleFile(record, stream);

            var tableName = record.FileName;
            tableName = tableName.Replace(DirectoryName, "");
            tableName = tableName.Split('.')[0];

            s_data.Add(tableName, data);
        }

        ProcessNameParts();
        DisposeStream();
    }

    /// <summary>
    /// Retrieves the English name associated with the specified key.
    /// </summary>
    /// <param name="key">The key used to retrieve the English name.</param>
    /// <returns>The English name associated with the specified key, or an empty string if the key is not found.</returns>
    public static string GetEnglishName(string key) {
        if (key == "") {
            return "";
        }

        var parts = key.Split('_');
        if (parts.Length != 2) {
            return "";
        }
        var tableName = parts[0];
        var id = parts[1];

        if (!s_data.TryGetValue(tableName, out var table)) {
            return "";
        }

        if (!table.TryGetValue(id, out var value)) {
            return "";
        }

        return value;
    }

    /// <summary>
    /// Retrieves the English name from the specified table and key.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="key">The key to search for in the table.</param>
    /// <returns>The English name associated with the specified key in the table, or an empty string if not found.</returns>
    public static string GetEnglishName(string tableName, string key) {
        if (key == "" || tableName == "") {
            return "";
        }

        if (!s_data.TryGetValue(tableName, out var table)) {
            return "";
        }

        if (!table.TryGetValue(key, out var value)) {
            return "";
        }

        return value;
    }

    /// <summary>
    /// Retrieves the first and last name keys for a full name entry.
    /// </summary>
    /// <param name="fullNameKey">The full key (e.g., "WC-NPCs_00000024") of the full name entry.</param>
    /// <returns>A tuple containing the first name key and last name key, or empty strings if no mapping exists.</returns>
    public static (string firstNameKey, string lastNameKey) GetEnglishNameKeys(string fullNameKey) {
        if (string.IsNullOrEmpty(fullNameKey)) {
            return ("", "");
        }

        if (s_namePartsMapping.TryGetValue(fullNameKey, out var nameParts)) {
            return nameParts;
        }

        return ("", "");
    }

    public void DisposeStream()
        => Files.Clear();

    private static Dictionary<string, string> ProcessLocaleFile(FileEntry record, Memory<byte>? stream) {
        var strings = ReadStrings(stream);
        var data = new Dictionary<string, string>();

        for (int i = 1; i < strings.Length; i += 3) {
            if (i + 2 >= strings.Length) {
                break;
            }

            var key = strings[i];
            var value = strings[i + 2];

            if (key.EndsWith("\r")) {
                key = key[..^1];
            }

            if (value.EndsWith("\r")) {
                value = value[..^1];
            }

            if (data.ContainsKey(key)) {
                Logger.Warning("Duplicate key {0} in {1}.",
                    Logger.Args(key, record.FileName));

                continue;
            }

            data.Add(key, value);
        }

        return data;
    }

    private static string[] ReadStrings(Memory<byte>? stream) {
        if (stream is null) {
            return [];
        }

        var bytes = stream?.ToArray();
        var stringArray = Encoding.Unicode
            .GetString(bytes)
            .Split([Environment.NewLine], StringSplitOptions.None);

        return stringArray;
    }

    private static void ProcessNameParts() {
        s_namePartsMapping = [];

        if (!s_data.TryGetValue("WC-NPCs", out var table)) {
            Logger.Warning("WC-NPCs locale table not found; cannot process name parts mapping.");

            return;
        }

        var tableName = "WC-NPCs";
        foreach (var entry in table) {
            var key = entry.Key;
            var value = entry.Value.Trim();

            if (string.IsNullOrEmpty(value) || !value.Contains(' ')) {
                continue;
            }

            var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) {
                continue;
            }

            var firstName = parts[0];
            var lastName = parts[1];

            string? firstNameKey = null;
            string? lastNameKey = null;

            foreach (var searchEntry in table) {
                var searchValue = searchEntry.Value.Trim();

                if (firstNameKey == null && searchValue == firstName) {
                    firstNameKey = searchEntry.Key;
                }

                if (lastNameKey == null && searchValue == lastName) {
                    lastNameKey = searchEntry.Key;
                }

                if (firstNameKey != null && lastNameKey != null) {
                    break;
                }
            }

            if (firstNameKey != null && lastNameKey != null) {
                var fullKey = $"{tableName}_{key}";
                var firstFullKey = $"{tableName}_{firstNameKey}";
                var lastFullKey = $"{tableName}_{lastNameKey}";
                s_namePartsMapping[fullKey] = (firstFullKey, lastFullKey);
            }
        }

        Logger.Information("Processed {0} NPC name part mappings.",
            Logger.Args(s_namePartsMapping.Count));
    }

}