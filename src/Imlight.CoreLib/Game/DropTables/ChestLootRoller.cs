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
 * CHEST LOOT ROLLER
 * ========================================================================
 *
 * PURPOSE:
 * Finds and rolls the loot of a chest from the drop tables its minigame result names, or else from the
 * NpcDropTable of its template id, the same data mobs use.
 *
 * USAGE EXAMPLE:
 * var tables = ChestLootRoller.ResolveTableNames(namedTables, templateId);
 * if (tables is not null) {
 *     var rolled = ChestLootRoller.Roll(tables, playerRef, playerObject, wizard);
 * }
 *
 * NOTE:
 * Tables that do not resolve are skipped; a chest with none pays no loot. A table name that is not in the drop tables is
 * reported once per chest template and name.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Models.World;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Imlight.CoreLib.Game.DropTables;

/// <summary>
/// Rolls the loot of a chest from named drop tables or its template's NpcDropTable.
/// </summary>
internal static class ChestLootRoller {

    private static readonly ConcurrentDictionary<(ulong, string), byte> s_reportedTableNames = new();

    internal static IReadOnlyList<string> ResolveTableNames(IReadOnlyList<string> namedTables, ulong chestTemplateId) {
        var resolved = ResolvingNames(namedTables, chestTemplateId);
        if (resolved.Count > 0) {
            return resolved;
        }

        return NpcDropTableCollection.TryGetDropTable(chestTemplateId, out var npcDropTable)
            ? ResolvingNames(npcDropTable.DropTableNames, chestTemplateId)
            : [];
    }

    internal static DropTableResult Roll(IReadOnlyList<string> tableNames, IActorRef playerRef, CoreObject playerObj, Wizard wizard)
        => tableNames.Count == 0
            ? new DropTableResult()
            : DropTableRoller.Roll([.. tableNames], playerRef, playerObj, wizard);

    private static List<string> ResolvingNames(IReadOnlyList<string> tableNames, ulong chestTemplateId) {
        var resolved = new List<string>();
        foreach (var tableName in tableNames ?? []) {
            if (!string.IsNullOrWhiteSpace(tableName) && DropTableCollection.GetDropTable(tableName) is not null) {
                resolved.Add(tableName);

                continue;
            }

            if (s_reportedTableNames.TryAdd((chestTemplateId, tableName ?? ""), 0)) {
                Logger.Warning("Chest {Template} names the drop table {Table}, which is not in the drop tables",
                    Logger.Args(chestTemplateId, tableName));
            }
        }

        return resolved;
    }

}
