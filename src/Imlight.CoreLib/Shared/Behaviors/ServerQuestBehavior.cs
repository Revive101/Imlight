/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imlight.CoreLib.WizardData.Models.Player;
using Newtonsoft.Json;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerQuestBehavior : IClientBehaviorProvider<ServerQuestBehavior> {

    [JsonIgnore] public bool NoTransfer { get; set; } = true;

    public readonly List<string> CompletedQuests = [];

    // In database, only store the quest IDs to reduce storage size.
    // The quest instances are loaded from the quest instance database on demand.
    public readonly List<ulong> CurrentQuestIDs = [];

    [JsonIgnore] public List<QuestInstance> CurrentQuestInstances { get; set; } = [];

    public bool AddQuest(QuestInstance quest) {
        if (quest == null) {
            return false;
        }

        if (CurrentQuestIDs.Contains(quest.ID) || CompletedQuests.Contains(quest.QuestName)) {
            return false;
        }

        CurrentQuestIDs.Add(quest.ID);
        CurrentQuestInstances.Add(quest);

        return true;
    }

    public bool CompleteQuest(string questName) {
        if (string.IsNullOrWhiteSpace(questName)) {
            return false;
        }

        var quest = CurrentQuestInstances.Find(q => q.QuestName == questName);
        if (quest == null) {
            return false;
        }

        return CompleteQuest(quest);
    }

    public bool CompleteQuest(QuestInstance quest) {
        if (quest == null) {
            return false;
        }

        if (!CurrentQuestIDs.Contains(quest.ID) || CompletedQuests.Contains(quest.QuestName)) {
            return false;
        }

        CurrentQuestIDs.Remove(quest.ID);
        CompletedQuests.Add(quest.QuestName);

        return true;
    }

    public bool RemoveQuest(string questName) {
        if (string.IsNullOrWhiteSpace(questName)) {
            return false;
        }

        var quest = CurrentQuestInstances.Find(q => q.QuestName == questName);
        if (quest == null) {
            return false;
        }

        return RemoveQuest(quest);
    }

    public bool RemoveQuest(QuestInstance quest) {
        if (quest == null) {
            return false;
        }

        if (!CurrentQuestIDs.Contains(quest.ID)) {
            return false;
        }

        CurrentQuestIDs.Remove(quest.ID);

        return true;
    }

    public bool HasQuest(string questName) {
        if (string.IsNullOrWhiteSpace(questName)) {
            return false;
        }

        return CurrentQuestInstances.Any(q => q.QuestName == questName);
    }

    public bool HasCompletedQuest(string questName) {
        if (string.IsNullOrWhiteSpace(questName)) {
            return false;
        }

        return CompletedQuests.Contains(questName);
    }

    public bool StartQuestGoal(string questName, string goalName) {
        if (string.IsNullOrWhiteSpace(questName) || string.IsNullOrWhiteSpace(goalName)) {
            return false;
        }

        var quest = CurrentQuestInstances.Find(q => q.QuestName == questName);
        if (quest == null) {
            return false;
        }

        quest.StartGoal(goalName);

        return true;
    }

    public bool IncrementQuestGoal(string questName, string goalName, int amount = 1) {
        if (string.IsNullOrWhiteSpace(questName) || string.IsNullOrWhiteSpace(goalName) || amount <= 0) {
            return false;
        }

        var quest = CurrentQuestInstances.Find(q => q.QuestName == questName);
        if (quest == null) {
            return false;
        }

        quest.IncrementGoal(goalName);

        return true;
    }

    public bool CompleteQuestGoal(string questName, string goalName) {
        if (string.IsNullOrWhiteSpace(questName) || string.IsNullOrWhiteSpace(goalName)) {
            return false;
        }

        var quest = CurrentQuestInstances.Find(q => q.QuestName == questName);
        if (quest == null) {
            return false;
        }

        quest.CompleteGoal(goalName);

        return true;
    }

    public ServerQuestBehavior GetClientBehaviorInstance()
        => throw new NotImplementedException();

}