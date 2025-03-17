/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.IO;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Wad;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Effects;

public class GameEffectRuleData : RootDirectoryResourceSingleton<GameEffectRuleData>, IMemoryStreamDisposable {
    
    protected override string DirectoryName => "GameEffectRuleData";
    private static Dictionary<string, WizardStatTable> s_statTables;

    protected override void AfterLoad() => ProcessStatTables(base.Files);

    /// <summary>
    /// Retrieves the WizardStatTable with the specified table name.
    /// </summary>
    /// <param name="tableName">The name of the table to retrieve.</param>
    /// <returns>The WizardStatTable with the specified table name, or null if the table is not found.</returns>
    public static WizardStatTable GetWizardStatTable(string tableName) {
        if (s_statTables is null) {
            Logger.Error("Stat tables were null. Cannot gather stat table.");

            return null;
        }

        if (!s_statTables.ContainsKey(tableName)) {
            return null;
        }

        return s_statTables[tableName];
    }

    private static void ProcessStatTables(Dictionary<FileEntry, MemoryStream> files) {
        s_statTables = [];
        foreach (var file in files) {
            var tableName = file.Key.FileName;
            var stream = file.Value;
            var table = ProcessStatTable(stream);

            var sanitizedTableName = SanitizeTableName(tableName);

            if (table is null) {
                Logger.Error("Could not process stat table {0}", Logger.Args(tableName));
                continue;
            }

            s_statTables.Add(sanitizedTableName, table);
        }

        Logger.Information("Loaded {0} stat tables", Logger.Args(s_statTables.Count));
    }

    private static string SanitizeTableName(string tableName) {
        var pathSplits = tableName.Split('/')[^1];
        var sanitized = pathSplits.Replace(".xml", "");
        return sanitized;
    }

    private static WizardStatTable ProcessStatTable(MemoryStream stream) {
        var serializer = new BindSerializer();
        if (!serializer.Deserialize<WizardStatTable>(stream.ToArray(), out var tableClass)) {
            Logger.Error("Failed to deserialize stat table");

            return null;
        }

        return tableClass;
    }

    public void DisposeStream() {
        foreach (var stream in base.Files.Values) {
            stream.Dispose();
        }
    }

}
