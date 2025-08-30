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

    public bool MarkQuestCompleted(string questName) {
        if (string.IsNullOrWhiteSpace(questName)) {
            return false;
        }

        if (Entries.Contains(questName)) {
            return false;
        }

        Entries.Add(questName);

        return true;
    }

    public bool MarkQuestGoalCompleted(string questName, string goalName) {
        if (string.IsNullOrWhiteSpace(questName) || string.IsNullOrWhiteSpace(goalName)) {
            return false;
        }

        var keyName = $"{questName}:{goalName}";

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

        return Entries.Contains(questName);
    }

    public bool HasCompletedQuestGoal(string questName, string goalName) {
        if (string.IsNullOrWhiteSpace(questName) || string.IsNullOrWhiteSpace(goalName)) {
            return false;
        }

        var keyName = $"{questName}:{goalName}";

        return Entries.Contains(keyName);
    }

    public ServerQuestBehavior GetClientBehaviorInstance()
        => throw new NotImplementedException();

}