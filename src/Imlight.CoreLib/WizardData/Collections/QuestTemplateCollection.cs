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

using System.Linq;
using Raven.Client.Documents;
using Imlight.CoreLib.WizardData.Databases;
using Imcodec.ObjectProperty.TypeCache;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Imlight.CoreLib.WizardData.Collections;

public static class QuestTemplateCollection {

    public const string CollectionName = "QuestTemplates";
    private static readonly IDocumentStore s_store;

    private static readonly List<QuestTemplate> s_cachedQuests = [];
    private static readonly Lock s_lockObject = new();

    static QuestTemplateCollection() {
        s_store = WorldDatabase.Instance.Store;
    }

    /// <summary>
    /// Get all quests from the database.
    /// </summary>
    /// <returns>A list of all quest templates.</returns>
    public static List<QuestTemplate> GetAllQuests() {
        lock (s_lockObject) {
            using var session = s_store.OpenSession();
            if (s_cachedQuests.Count <= 0) {
                s_cachedQuests.AddRange([.. session
                    .Query<QuestTemplate>(collectionName: CollectionName)
                    .ToList()
                ]);
            }

            return [.. s_cachedQuests];
        }
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