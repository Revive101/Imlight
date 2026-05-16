/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
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
