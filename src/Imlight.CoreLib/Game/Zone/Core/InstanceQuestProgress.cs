using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Zone.Core;

/// <summary>
/// The dungeon quest progress of one instance: the quests it finished and the goals it completed, and the zones
/// its container holds. Every zone of an instance container shares one, so a quest keeps its progress when the
/// player moves between the instance's zones, and the zones tell which quests belong to the instance.
/// </summary>
public sealed class InstanceQuestProgress {

    private readonly HashSet<string> _claims = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _completedQuests = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _completedGoals = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _zones = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();

    /// <summary>
    /// Records that the container holds the zone.
    /// </summary>
    public void AddZone(string zonePath) {
        lock (_lock) {
            _zones.Add(zonePath);
        }
    }

    /// <summary>
    /// Records that the container dropped the zone.
    /// </summary>
    public void RemoveZone(string zonePath) {
        lock (_lock) {
            _zones.Remove(zonePath);
        }
    }

    /// <summary>
    /// Records a quest step and returns whether it is the first time the instance saw it.
    /// </summary>
    public bool TryClaimStep(InstanceQuestClaimKind kind, string questName, string goalName) {
        lock (_lock) {
            if (!_claims.Add($"{kind}|{questName}|{goalName}")) {
                return false;
            }

            switch (kind) {
                case InstanceQuestClaimKind.GoalComplete:
                    if (!_completedGoals.TryGetValue(questName, out var goals)) {
                        goals = [];
                        _completedGoals[questName] = goals;
                    }

                    goals.Add(goalName);
                    break;
                case InstanceQuestClaimKind.QuestComplete:
                    _completedQuests.Add(questName);
                    break;
            }

            return true;
        }
    }

    /// <summary>
    /// The quests this instance finished and, per quest, the goals it completed in order.
    /// </summary>
    public ZONE_102_PROTOCOL.MSG_QUERYINSTANCEQUESTSRSP Snapshot() {
        lock (_lock) {
            return new() {
                IsInstance = true,
                Zones = [.. _zones],
                CompletedQuests = [.. _completedQuests],
                CompletedGoals = _completedGoals.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()),
            };
        }
    }

}
