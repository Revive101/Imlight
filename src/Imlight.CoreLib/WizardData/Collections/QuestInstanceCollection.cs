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

        var questInstance = session.Query<QuestInstance>(collectionName: CollectionName)
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

        var questInstance = session.Query<QuestInstance>(collectionName: CollectionName)
            .FirstOrDefault(q => q.ID == questInstanceID);
        if (questInstance == null) {
            return false;
        }

        session.Delete(questInstance);
        session.SaveChanges();

        return true;
    }

    /// <summary>
    /// Starts tracking progress for a specific goal within a quest instance.
    /// </summary>
    /// <param name="questInstanceID">The ID of the quest instance.</param>
    /// <param name="goalName">The name of the goal to start.</param>
    /// <returns>True if the goal was started successfully, false otherwise.</returns>
    public static bool StartQuestGoal(ulong questInstanceID, string goalName) {
        using var session = s_store.OpenSession();

        var questInstance = session.Query<QuestInstance>(collectionName: CollectionName)
            .FirstOrDefault(q => q.ID == questInstanceID);
        if (questInstance == null) {
            return false;
        }

        var goalInstance = questInstance.GoalProgress.FirstOrDefault(g => g.GoalName == goalName);
        if (goalInstance == null) {
            return false;
        }

        goalInstance.BeginGoal();
        session.SaveChanges();

        return true;
    }

    /// <summary>
    /// Increments the progress of a specific goal within a quest instance.
    /// </summary>
    /// <param name="questInstanceID">The ID of the quest instance.</param>
    /// <param name="goalName">The name of the goal to increment.</param>
    /// <returns>True if the goal was incremented successfully, false otherwise.</returns>
    public static bool IncrementQuestGoal(ulong questInstanceID, string goalName) {
        using var session = s_store.OpenSession();

        var questInstance = session.Query<QuestInstance>(collectionName: CollectionName)
            .FirstOrDefault(q => q.ID == questInstanceID);
        if (questInstance == null) {
            return false;
        }

        var goalInstance = questInstance.GoalProgress.FirstOrDefault(g => g.GoalName == goalName);
        if (goalInstance == null) {
            return false;
        }

        goalInstance.IncrementGoal();
        session.SaveChanges();

        return true;
    }

    /// <summary>
    /// Marks a specific goal within a quest instance as complete.
    /// </summary>
    /// <param name="questInstanceID">The ID of the quest instance.</param>
    /// <param name="goalName">The name of the goal to complete.</param> 
    /// <returns>True if the goal was completed successfully, false otherwise.</returns>
    public static bool CompleteQuestGoal(ulong questInstanceID, string goalName) {
        using var session = s_store.OpenSession();

        var questInstance = session.Query<QuestInstance>(collectionName: CollectionName)
            .FirstOrDefault(q => q.ID == questInstanceID);
        if (questInstance == null) {
            return false;
        }

        var goalInstance = questInstance.GoalProgress.FirstOrDefault(g => g.GoalName == goalName);
        if (goalInstance == null) {
            return false;
        }

        goalInstance.CompleteGoal();
        session.SaveChanges();

        return true;
    }

}