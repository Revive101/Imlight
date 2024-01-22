/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.Common.Formats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Imlight.CoreLib.Shared.Resources;

public class Locale : RootDirectoryResourceSingleton<Locale>, IMemoryStreamDisposable {
    protected override string DirectoryName => "Locale/English/";

    private static readonly string s_QustFilePrefix = "WizQst";
    private static Dictionary<string, Dictionary<string, string>> s_data = new();

    protected override void AfterLoad() {
        // There are aoubt ~5,000 files here. They take up about 200MB of memory.
        // At this point, they are all loaded into memory. This function should process the locales
        // and give us data we actually need.
        Logger.Information("Loaded {0} English locale files.", Logger.Args(Files.Count));

        // Process each file.
        s_data = new Dictionary<string, Dictionary<string, string>>();
        foreach (var file in Files) {
            var stream = file.Value;
            var record = file.Key;

            // We have no reason to keep any of these files in memory.
            if (record.FileName.StartsWith(s_QustFilePrefix)) {
                continue;
            }

            var data = ProcessLocaleFile(record, stream);

            // Santize the table name. Remove the path prefix and the file extension.
            var tableName = record.FileName;
            tableName = tableName.Replace(DirectoryName, "");
            tableName = tableName.Split('.')[0];

            s_data.Add(tableName, data);
        }

        // Drop each file from memory after processing.
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

        // Example: Interactables_0000008
        // Split the key into the table name and the ID.
        var parts = key.Split('_');
        if (parts.Length != 2) {
            return "";
        }
        var tableName = parts[0];
        var id = parts[1];

        // Search for the table by name
        if (!s_data.TryGetValue(tableName, out var table)) {
            return "";
        }

        // Search for the ID in the table
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

        // Search for the table by name
        if (!s_data.TryGetValue(tableName, out var table)) {
            return "";
        }

        // Search for the ID in the table
        if (!table.TryGetValue(key, out var value)) {
            return "";
        }

        return value;
    }

    public void DisposeStream() {
        foreach (var streams in base.Files.Values) {
            streams.Dispose();
        }
    }

    private static Dictionary<string, string> ProcessLocaleFile(FileRecord record, MemoryStream stream) {
        // Interpret the stream as an array of strings.
        var strings = ReadStrings(stream);
        var data = new Dictionary<string, string>();

        // The first string is the key, the second string is two lines down and is the value.
        // Skip the first line, which is just an echo of the file name.
        for (int i = 1; i < strings.Length; i += 3) {
            // If adding 3 to i would go out of bounds, then we are at the end of the file.
            if (i + 2 >= strings.Length) {
                break;
            }

            var key = strings[i];
            var value = strings[i + 2];

            // Throw exception if the key already exists.
            if (data.ContainsKey(key)) {
                throw new Exception($"Duplicate key {key} in {record.FileName}");
            }

            // Remove the '/r' that may exist at the end of the key.
            if (key.EndsWith("\r")) {
                key = key[..^1];
            }

            // Remove the '/r' that may exist at the end of the value.
            if (value.EndsWith("\r")) {
                value = value[..^1];
            }

            data.Add(key, value);
        }

        return data;
    }

    private static string[] ReadStrings(MemoryStream stream) {
        stream.Position = 0;

        var bytes = stream.ToArray();
        var stringArray = Encoding.Unicode.GetString(bytes).Split(new[] { Environment.NewLine }, StringSplitOptions.None);

        return stringArray;
    }
}
