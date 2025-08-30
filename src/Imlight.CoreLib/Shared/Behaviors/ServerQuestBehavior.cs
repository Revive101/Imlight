/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerQuestBehavior : IClientBehaviorProvider<ServerQuestBehavior> {

    [JsonIgnore] public bool NoTransfer { get; set; } = true;

    public readonly List<string> Entries = [];

    public bool AddQuest(string questName) {
        if (string.IsNullOrWhiteSpace(questName)) {
            return false;
        }

        var completedSyntax = $"{questName}:Completed";
        if (Entries.Contains(questName) || Entries.Contains(completedSyntax)) {
            return false;
        }

        Entries.Add(questName);

        return true;
    }

    public bool RemoveQuest(string questName) {
        if (string.IsNullOrWhiteSpace(questName)) {
            return false;
        }

        var completedSyntax = $"{questName}:Completed";

        if (!Entries.Contains(questName) || !Entries.Contains(completedSyntax)) {
            return false;
        }

        _ = Entries.Remove(questName);
        _ = Entries.Remove(completedSyntax);

        return true;
    }

    public bool AddQuestGoal(string questName, string goalName) {
        if (string.IsNullOrWhiteSpace(questName) || string.IsNullOrWhiteSpace(goalName)) {
            return false;
        }

        var keyName = $"{questName}:{goalName}";
        var keyNameCompleted = $"{keyName}:Completed";

        if (Entries.Contains(keyName) || Entries.Contains(keyNameCompleted)) {
            return false;
        }

        Entries.Add(keyName);

        return true;
    }

    public bool MarkQuestCompleted(string questName) {
        if (string.IsNullOrWhiteSpace(questName)) {
            return false;
        }

        var completedSyntax = $"{questName}:Completed";

        if (Entries.Contains(completedSyntax)) {
            return false;
        }

        Entries.Add(questName);
        Entries.Add(completedSyntax);

        return true;
    }

    public bool MarkQuestGoalCompleted(string questName, string goalName) {
        if (string.IsNullOrWhiteSpace(questName) || string.IsNullOrWhiteSpace(goalName)) {
            return false;
        }

        var keyName = $"{questName}:{goalName}:Completed";

        if (Entries.Contains(keyName)) {
            return false;
        }

        Entries.Add(keyName);

        return true;
    }

    public bool HasCompletedQuest(string questName) {
        if (string.IsNullOrWhiteSpace(questName)) {
            return false;
        }

        var completedSyntax = $"{questName}:Completed";

        return Entries.Contains(completedSyntax);
    }

    public bool HasCompletedQuestGoal(string questName, string goalName) {
        if (string.IsNullOrWhiteSpace(questName) || string.IsNullOrWhiteSpace(goalName)) {
            return false;
        }

        var keyName = $"{questName}:{goalName}:Completed";

        return Entries.Contains(keyName);
    }

    public ServerQuestBehavior GetClientBehaviorInstance()
        => throw new NotImplementedException();

}