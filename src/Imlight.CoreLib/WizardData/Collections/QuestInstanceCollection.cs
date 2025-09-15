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

    /// <summary>
    /// Removes a quest instance from the database by character ID and quest name.
    /// </summary>
    /// <param name="charId">The character ID of the quest instance owner.</param>
    /// <param name="questName">The name of the quest instance to remove.</param>
    /// <returns>True if the quest instance was removed successfully, false otherwise.</returns>
    public static bool RemoveQuestInstance(ulong charId, string questName) {
        using var session = s_store.OpenSession();

        var questInstance = session.Query<QuestInstance>(CollectionName)
            .FirstOrDefault(q => q.OwnerCharId == charId && q.QuestName == questName);
        if (questInstance == null) {
            return false;
        }

        session.Delete(questInstance);
        session.SaveChanges();

        return true;
    }

    /// <summary>
    /// Removes a quest instance from the database by its ID.
    /// </summary>
    /// <param name="questInstanceID">The ID of the quest instance to remove.</param>
    /// <returns>True if the quest instance was removed successfully, false otherwise.</returns
    public static bool RemoveQuestInstance(ulong questInstanceID) {
        using var session = s_store.OpenSession();

        var questInstance = session.Query<QuestInstance>(CollectionName)
            .FirstOrDefault(q => q.ID == questInstanceID);
        if (questInstance == null) {
            return false;
        }

        session.Delete(questInstance);
        session.SaveChanges();

        return true;
    }

    /// <summary>
    /// Updates an existing quest instance in the database.
    /// </summary>
    /// <param name="questInstance">The quest instance to update.</param>
    /// <returns>True if the quest instance was updated successfully, false otherwise.</returns>
    public static bool UpdateQuestInstance(QuestInstance questInstance) {
        if (questInstance == null) {
            return false;
        }

        using var session = s_store.OpenSession();

        session.Store(questInstance);
        session.SaveChanges();

        return true;
    }

    /// <summary>
    /// Updates a quest instance in the database by character ID and quest name.
    /// </summary>
    /// <param name="charId">The character ID of the quest instance owner.</param>
    /// <param name="questName">The name of the quest instance to update.</param>
    /// <returns>True if the quest instance was updated successfully, false otherwise.</returns>
    public static bool UpdateQuestInstance(ulong charId, string questName) {
        using var session = s_store.OpenSession();

        var questInstance = session.Query<QuestInstance>(CollectionName)
            .FirstOrDefault(q => q.OwnerCharId == charId && q.QuestName == questName);
        if (questInstance == null) {
            return false;
        }

        session.Store(questInstance);
        session.SaveChanges();

        return true;
    }

}