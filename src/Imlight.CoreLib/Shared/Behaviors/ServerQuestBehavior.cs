/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using Imcodec.ObjectProperty.TypeCache;
using Newtonsoft.Json;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerQuestBehavior : IClientBehaviorProvider<ServerQuestBehavior> {

    [JsonIgnore] public bool NoTransfer { get; set; } = true;

    public readonly List<string> CurrentQuests = [];
    public readonly List<string> CurrentGoals = [];
    public readonly List<string> CompletedQuests = [];
    public readonly List<string> CompletedGoals = [];

    [JsonIgnore] public List<QuestTemplate> Quests { get; set; } = [];

    public bool AddQuest(string questName) {
        if (string.IsNullOrWhiteSpace(questName)) {
            return false;
        }

        if (CurrentQuests.Contains(questName) || CompletedQuests.Contains(questName)) {
            return false;
        }

        CurrentQuests.Add(questName);

        return true;
    }

    public bool CompleteQuest(string questName) {
        if (string.IsNullOrWhiteSpace(questName)) {
            return false;
        }

        if (!CurrentQuests.Contains(questName) || CompletedQuests.Contains(questName)) {
            return false;
        }

        CurrentQuests.Remove(questName);
        CompletedQuests.Add(questName);

        return true;
    }

    public bool RemoveQuest(string questName) {
        if (string.IsNullOrWhiteSpace(questName)) {
            return false;
        }

        if (!CurrentQuests.Contains(questName)) {
            return false;
        }

        CurrentQuests.Remove(questName);

        return true;
    }

    public bool AddQuestGoal(string questName, string goalName) {
        if (string.IsNullOrWhiteSpace(questName) || string.IsNullOrWhiteSpace(goalName)) {
            return false;
        }

        if (!CurrentQuests.Contains(questName) || CurrentGoals.Contains(goalName) || CompletedGoals.Contains(goalName)) {
            return false;
        }

        var formattedGoalName = $"{questName}_{goalName}";
        CurrentGoals.Add(formattedGoalName);

        return true;
    }

    public bool CompleteQuestGoal(string questName, string goalName) {
        if (string.IsNullOrWhiteSpace(questName) || string.IsNullOrWhiteSpace(goalName)) {
            return false;
        }

        if (!CurrentQuests.Contains(questName) || !CurrentGoals.Contains(goalName) || CompletedGoals.Contains(goalName)) {
            return false;
        }

        var formattedGoalName = $"{questName}_{goalName}";

        CurrentGoals.Remove(formattedGoalName);
        CompletedGoals.Add(formattedGoalName);

        return true;
    }

    public bool HasCompletedQuest(string questName) {
        if (string.IsNullOrWhiteSpace(questName)) {
            return false;
        }

        return CompletedQuests.Contains(questName);
    }

    public bool HasCompletedQuestGoal(string questName, string goalName) {
        if (string.IsNullOrWhiteSpace(questName) || string.IsNullOrWhiteSpace(goalName)) {
            return false;
        }

        var formattedGoalName = $"{questName}_{goalName}";
        return CompletedGoals.Contains(formattedGoalName);
    }

    public ServerQuestBehavior GetClientBehaviorInstance()
        => throw new NotImplementedException();

}