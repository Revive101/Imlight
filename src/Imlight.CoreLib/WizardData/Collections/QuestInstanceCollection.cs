/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Raven.Client.Documents;
using Imlight.CoreLib.WizardData.Databases;
using System.Collections.Generic;
using Imlight.CoreLib.WizardData.Models.Player;
using System.Linq;

namespace Imlight.CoreLib.WizardData.Collections;

public static class QuestInstanceCollection {

    public const string CollectionName = "QuestInstances";
    private static readonly IDocumentStore s_store;

    static QuestInstanceCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    /// <summary>
    /// Adds a new quest instance to the database.
    /// </summary>
    /// <param name="questInstance">The quest instance to add.</param>
    /// <returns>True if the quest instance was added successfully, false otherwise.</returns>
    public static bool AddQuestInstance(QuestInstance questInstance) {
        if (questInstance == null) {
            return false;
        }

        using var session = s_store.OpenSession();

        session.Store(questInstance);
        var metadata = session.Advanced.GetMetadataFor(questInstance);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        session.SaveChanges();

        return true;
    }

}