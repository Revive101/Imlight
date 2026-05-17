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

using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.World;
using Raven.Client.Documents;
using System.Linq;

namespace Imlight.CoreLib.WizardData.Collections;

public static class DropTableCollection {

    public const string CollectionName = "DropTables";
    private static readonly IDocumentStore s_store;

    static DropTableCollection() {
        s_store = WorldDatabase.Instance.Store;
    }

    /// <summary>
    /// Adds a new drop table to the database.
    /// </summary>
    /// <param name="dropTable">The drop table to add.</param>
    /// <returns>True if the drop table was added successfully, false otherwise.</returns>
    public static DropTable GetDropTable(string tableName) {
        using var session = s_store.OpenSession();

        var dropTable = session.Query<DropTable>(collectionName: CollectionName)
            .FirstOrDefault(dt => dt.Name == tableName);

        return dropTable;
    }
    
}
