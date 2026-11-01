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
 * QUEST BADGE AWARDS
 * ========================================================================
 *
 * PURPOSE:
 * Finds the badge names that quests and badge completion results award through ResAddBadge, so the catalog knows which badges Imlight can hand out.
 *
 * USAGE EXAMPLE:
 * var names = QuestBadgeAwards.Find(QuestTemplateCollection.GetAllQuests());
 *
 * NOTE:
 * Walks the start and end results of a quest and the activate and complete results of its goals, 
 * into every nested result list (conditionals, random results, ...). Dialog results are not walked.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Imcodec.ObjectProperty.TypeCache;
using Type = System.Type;

namespace Imlight.CoreLib.Game.Badges;

internal static class QuestBadgeAwards {

    internal static HashSet<string> Find(IEnumerable<QuestTemplate> quests) {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var walkableProperties = new Dictionary<Type, PropertyInfo[]>();
        foreach (var quest in quests.Where(quest => quest is not null)) {
            Walk(quest.m_startResults, names, walkableProperties);
            Walk(quest.m_endResults, names, walkableProperties);
            foreach (var goal in quest.m_goals?.Where(goal => goal is not null) ?? []) {
                Walk(goal.m_activateResults, names, walkableProperties);
                Walk(goal.m_completeResults, names, walkableProperties);
            }
        }

        return names;
    }

    internal static HashSet<string> Find(ResultList results) {
        var names = new HashSet<string>(StringComparer.Ordinal);
        Walk(results, names, []);

        return names;
    }

    private static void Walk(object node, HashSet<string> names, Dictionary<Type, PropertyInfo[]> walkableProperties) {
        if (node is ResAddBadge award) {
            if (!string.IsNullOrEmpty(award.m_badgeName)) {
                names.Add(award.m_badgeName);
            }

            return;
        }

        if (node is null) {
            return;
        }

        var type = node.GetType();
        if (!walkableProperties.TryGetValue(type, out var properties)) {
            properties = [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetIndexParameters().Length == 0 && IsWalkable(property.PropertyType))];
            walkableProperties[type] = properties;
        }

        foreach (var property in properties) {
            var value = property.GetValue(node);
            if (value is IEnumerable items) {
                foreach (var item in items) {
                    Walk(item, names, walkableProperties);
                }
            } else {
                Walk(value, names, walkableProperties);
            }
        }
    }

    private static bool IsWalkable(Type type)
        => IsResultNode(type)
            || (type.IsGenericType && type.GetGenericArguments() is [var element] && typeof(IEnumerable).IsAssignableFrom(type) && IsResultNode(element));

    private static bool IsResultNode(Type type)
        => type == typeof(ResultList) || type == typeof(RandomResult) || typeof(Result).IsAssignableFrom(type);

}
