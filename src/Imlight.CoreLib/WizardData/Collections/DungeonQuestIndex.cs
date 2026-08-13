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
 *
 * ========================================================================
 * DUNGEON QUEST INDEX
 * ========================================================================
 * 
 * PURPOSE:
 * Boot-time, data-derived index of DUNGEON quests: quests whose m_requirements
 * contain a ReqInZone (a quest-level requirement to be in the dungeon zone).
 * The requirement is both the marker and the grant gate: QuestService
 * force-adds a zone's quests while the player is in that zone, gated by the
 * quest's full requirements (chain prereqs via ReqHasEntry).
 * 
 * USAGE EXAMPLE:
 * var quests = DungeonQuestIndex.GetQuestsForZone(wizard.Zone);
 * 
 * NOTE:
 * Derived once from the loaded quest templates, so authoring a dungeon quest is
 * purely a data change. Only TOP-LEVEL ReqInZone requirements are considered,
 * matching what RequirementDispatcher can evaluate; a NOT-inverted ReqInZone
 * is a zone gate, not a marker, and is skipped.
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 08/13/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;

namespace Imlight.CoreLib.WizardData.Collections;

/// <summary>
/// Boot-time, data-derived index of DUNGEON quests: quests whose m_requirements
/// contain a ReqInZone (a quest-level requirement to be in the dungeon zone).
/// </summary>
internal static class DungeonQuestIndex {

    private static readonly Dictionary<string, List<QuestTemplate>> s_questsByZone
        = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> s_dungeonQuestNames
        = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object s_lock = new();
    private static bool s_built;

    public static IReadOnlyList<QuestTemplate> GetQuestsForZone(string zonePath) {
        Build();

        lock (s_lock) {
            return s_questsByZone.TryGetValue(zonePath ?? "", out var quests) ? quests : [];
        }
    }

    public static bool IsDungeonQuest(string questName) {
        Build();

        lock (s_lock) {
            return s_dungeonQuestNames.Contains(questName ?? "");
        }
    }

    private static void Build() {
        if (s_built) {
            return;
        }

        lock (s_lock) {
            if (s_built) {
                return;
            }

            foreach (var template in QuestTemplateCollection.GetAllQuests()) {
                var zones = GetInZoneRequirements(template);
                if (zones.Count == 0) {
                    continue;
                }

                s_dungeonQuestNames.Add(template.m_questName);
                foreach (var zone in zones) {
                    if (!s_questsByZone.TryGetValue(zone, out var quests)) {
                        quests = [];
                        s_questsByZone[zone] = quests;
                    }

                    quests.Add(template);
                }
            }

            // Order each zone's quests so chain prereqs come first: a quest requiring another indexed
            // quest's "Completed" registry entry sorts after it. Stable, so template order breaks ties.
            foreach (var zone in s_questsByZone.Keys.ToList()) {
                var quests = s_questsByZone[zone];
                s_questsByZone[zone] = quests
                    .OrderBy(q => quests.Count(other => IsPrereqOf(other, q)))
                    .ToList();
            }

            s_built = true;

            var total = s_dungeonQuestNames.Count;
            Logger.Information("Indexed {0} dungeon quest(s) across {1} zone(s).",
                Logger.Args(total, s_questsByZone.Count));
        }
    }

    private static List<string> GetInZoneRequirements(QuestTemplate template) {
        var zones = new List<string>();
        var requirements = template.m_requirements?.m_requirements;
        if (requirements is null) {
            return zones;
        }

        foreach (var requirement in requirements) {
            if (requirement is not ReqInZone inZone || inZone.m_applyNOT) {
                continue;
            }

            var zone = inZone.m_zoneName;
            if (!string.IsNullOrEmpty(zone) && !zones.Contains(zone, StringComparer.OrdinalIgnoreCase)) {
                zones.Add(zone);
            }
        }

        return zones;
    }

    private static bool IsPrereqOf(QuestTemplate prereq, QuestTemplate quest) {
        var requirements = quest.m_requirements?.m_requirements;
        if (requirements is null) {
            return false;
        }

        return requirements.Any(r => r is ReqHasEntry entry
            && entry.m_isQuestRegistry
            && string.Equals(entry.m_entryName, "Completed", StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.m_questName, prereq.m_questName, StringComparison.OrdinalIgnoreCase));
    }

}
