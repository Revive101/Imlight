/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using Raven.Client.Documents;
using Imlight.CoreLib.WizardData.Databases;
using Imcodec.ObjectProperty.TypeCache;
using System;
using System.Collections.Generic;

namespace Imlight.CoreLib.WizardData.Collections;

public static class QuestCollection {

    public const string CollectionName = "QuestTemplates";
    private static readonly IDocumentStore s_store;

    private static readonly List<QuestTemplate> s_cachedQuests = [];

    static QuestCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    /// <summary>
    /// Get all quests from the database.
    /// </summary>
    /// <returns>A list of all quest templates.</returns>
    public static List<QuestTemplate> GetAllQuests() {
        using var session = s_store.OpenSession();

        if (s_cachedQuests.Count == 0) {
            s_cachedQuests.AddRange([.. session.Query<QuestTemplate>(collectionName: CollectionName)]);
        }

        return s_cachedQuests;
    }

    /// <summary>
    /// Get a quest by its name.
    /// </summary>
    /// <param name="questName">The name of the quest to retrieve.</param>
    /// <returns>The quest template if found; otherwise, null.</returns>
    public static QuestTemplate GetQuestByName(string questName) {
        using var session = s_store.OpenSession();

        // Check the cache first!
        var cachedQuest = s_cachedQuests.FirstOrDefault(q => q.m_questName.Equals(questName, StringComparison.OrdinalIgnoreCase));
        if (cachedQuest != null) {
            return cachedQuest;
        }

        return session.Query<QuestTemplate>(collectionName: CollectionName)
                      .FirstOrDefault(q => q.m_questName.Equals(questName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Check if a quest exists by its name.
    /// </summary>
    /// <param name="questName">The name of the quest to check.</param>
    /// <returns>True if the quest exists; otherwise, false.</returns>
    public static bool DoesQuestExist(string questName) {
        using var session = s_store.OpenSession();

        // Check the cache first!
        if (s_cachedQuests.Any(q => q.m_questName.Equals(questName, StringComparison.OrdinalIgnoreCase))) {
            return true;
        }

        return session.Query<QuestTemplate>(collectionName: CollectionName)
                      .Any(q => q.m_questName.Equals(questName, StringComparison.OrdinalIgnoreCase));
    }

}