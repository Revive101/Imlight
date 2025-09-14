/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Utilities;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Imlight.CoreLib.WizardData.Models.Player;

public class QuestInstance {

    /// <summary>
    /// The unique identifier for this quest instance.
    /// </summary>
    public ulong ID { get; set; }

    /// <summary>
    /// The character ID of the owner of this quest instance.
    /// </summary>
    public ulong OwnerCharId { get; set; }

    /// <summary>
    /// The locale name of the quest, matching the quest template.
    /// </summary>
    public string QuestTitle { get; set; } = string.Empty;

    public GoalInstance[] GoalProgress { get; set; } = [];

    public bool IsReadyForTurnIn() {
        foreach (var goal in GoalProgress) {
            if (goal.CurrentProgress != int.MaxValue) {
                return false;
            }
        }
        
        return true;
    }

    [JsonConstructor]
    public QuestInstance() { }

    // ctor
    public QuestInstance(QuestTemplate qTemplate, ulong ownerCharId) {
        OwnerCharId = ownerCharId;
        ID = RandomGen.GenerateGUID();
        QuestTitle = qTemplate.m_questName;

        // Initialize goal progress array based on the number of goals in the template.
        var goalInstances = new List<GoalInstance>();
        foreach (var gTemplate in qTemplate.m_goals) {
            var goalInstance = new GoalInstance(gTemplate, ownerCharId);
            goalInstances.Add(goalInstance);
        }
        GoalProgress = goalInstances.ToArray();
    }

}

public class GoalInstance {

    /// <summary>
    /// The unique identifier for this goal instance.
    /// </summary>
    public ulong ID { get; set; }

    /// <summary>
    /// The character ID of the owner of this goal instance.
    /// </summary>
    public ulong OwnerCharId { get; set; }

    /// <summary>
    /// The locale name of the goal, matching the goal template.
    /// </summary>
    public string GoalName { get; set; }

    /// <summary>
    /// The current progress of the goal.
    /// A value of -1 means the player does not have this goal yet.
    /// A value of 0 means the goal is in progress.
    /// A value of int.MaxValue indicates that the goal is completed.
    /// </summary>
    public int CurrentProgress { get; set; } = -1;

    [JsonConstructor]
    public GoalInstance() { }

    // ctor
    public GoalInstance(GoalTemplate gTemplate, ulong ownerCharId) {
        ID = RandomGen.GenerateGUID();
        OwnerCharId = ownerCharId;
        GoalName = gTemplate.m_goalName;
    }
    
}
