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

    public readonly Dictionary<string, ulong> Registry = [];

    // In database, only store the quest IDs to reduce storage size.
    // The quest instances are loaded from the quest instance database on demand.
    public readonly List<ulong> CurrentQuestIDs = [];
    [JsonIgnore] public List<QuestInstance> CurrentQuestInstances { get; set; } = [];

    public bool AddQuest(QuestInstance quest) {
        if (quest == null) {
            return false;
        }

        if (CurrentQuestIDs.Contains(quest.ID) || HasCompletedQuest(quest.QuestName)) {
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

        if (!CurrentQuestIDs.Contains(quest.ID) || HasCompletedQuest(quest.QuestName)) {
            return false;
        }

        CurrentQuestIDs.Remove(quest.ID);

        // Mark the quest as completed in the registry:
        AddToQuestRegistry(quest.QuestName, "Completed", 1);

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

        // Check the registry to find the completed quest key:
        // <quest_name>.completed
        var completedKey = $"{questName}_Completed";

        return Registry.ContainsKey(completedKey) && Registry[completedKey] > 0;
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

    public bool AddToRegistry(string entryName, ulong value) {
        if (string.IsNullOrWhiteSpace(entryName) || value == 0) {
            return false;
        }

        if (Registry.ContainsKey(entryName)) {
            Registry[entryName] += value;
        }
        else {
            Registry[entryName] = value;
        }

        return true;
    }

    public bool AddToQuestRegistry(string questName, string entryName, ulong value) {
        if (string.IsNullOrWhiteSpace(questName) || string.IsNullOrWhiteSpace(entryName) || value == 0) {
            return false;
        }

        // Same as the normal registry, except prefixed with the quest name.
        var fullEntryName = $"{questName}_{entryName}";

        return AddToRegistry(fullEntryName, value);
    }

    public bool RemoveFromRegistry(string entryName) {
        if (string.IsNullOrWhiteSpace(entryName)) {
            return false;
        }

        return Registry.Remove(entryName);
    }

    public bool RemoveFromQuestRegistry(string questName, string entryName) {
        if (string.IsNullOrWhiteSpace(questName) || string.IsNullOrWhiteSpace(entryName)) {
            return false;
        }

        var fullEntryName = $"{questName}_{entryName}";

        return RemoveFromRegistry(fullEntryName);
    }

    public bool SetRegistryValue(string entryName, ulong value) {
        if (string.IsNullOrWhiteSpace(entryName)) {
            return false;
        }

        Registry[entryName] = value;

        return true;
    }

    public bool SetQuestRegistryValue(string questName, string entryName, ulong value) {
        if (string.IsNullOrWhiteSpace(questName) || string.IsNullOrWhiteSpace(entryName)) {
            return false;
        }

        var fullEntryName = $"{questName}_{entryName}";
        Registry[fullEntryName] = value;

        return true;
    }

    public bool HasRegistryValue(string key) {
        if (string.IsNullOrWhiteSpace(key)) {
            return false;
        }

        return Registry.ContainsKey(key);
    }

    public bool HasQuestRegistryValue(string questName, string entryName) {
        if (string.IsNullOrWhiteSpace(questName) || string.IsNullOrWhiteSpace(entryName)) {
            return false;
        }

        var fullEntryName = $"{questName}_{entryName}";

        return HasRegistryValue(fullEntryName);
    }

    public ulong GetRegistryValue(string key) {
        if (string.IsNullOrWhiteSpace(key)) {
            return 0;
        }

        return Registry.TryGetValue(key, out var value) ? value : 0;
    }

    public ulong GetQuestRegistryValue(string questName, string entryName) {
        if (string.IsNullOrWhiteSpace(questName) || string.IsNullOrWhiteSpace(entryName)) {
            return 0;
        }

        var fullEntryName = $"{questName}.{entryName}";
        
        return GetRegistryValue(fullEntryName);
    }

    public ServerQuestBehavior GetClientBehaviorInstance()
        => throw new NotImplementedException();

}