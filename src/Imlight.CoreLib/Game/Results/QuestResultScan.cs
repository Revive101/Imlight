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
 * QUEST RESULT SCAN
 * ========================================================================
 *
 * PURPOSE:
 * Finds every result of one type in quest templates and result lists, so data-derived indexes
 * (badges a quest awards, objects a quest reveals) read the data instead of keeping a list.
 *
 * USAGE EXAMPLE:
 * var tags = QuestResultScan.Find<ResAddDynaMod>(QuestTemplateCollection.GetAllQuests());
 *
 * NOTE:
 * Walks the start and end results of a quest and the activate and complete results of its goals,
 * into every nested result list (conditionals, random results, ...). Dialog results are not walked.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Imcodec.ObjectProperty.TypeCache;
using Type = System.Type;

namespace Imlight.CoreLib.Game.Results;

internal static class QuestResultScan {

    internal static List<TResult> Find<TResult>(IEnumerable<QuestTemplate> quests) where TResult : Result {
        var found = new List<TResult>();
        var walkableProperties = new Dictionary<Type, PropertyInfo[]>();
        foreach (var quest in quests.Where(quest => quest is not null)) {
            Walk(quest.m_startResults, found, walkableProperties);
            Walk(quest.m_endResults, found, walkableProperties);
            foreach (var goal in quest.m_goals?.Where(goal => goal is not null) ?? []) {
                Walk(goal.m_activateResults, found, walkableProperties);
                Walk(goal.m_completeResults, found, walkableProperties);
            }
        }

        return found;
    }

    internal static List<TResult> Find<TResult>(ResultList results) where TResult : Result {
        var found = new List<TResult>();
        Walk(results, found, []);

        return found;
    }

    private static void Walk<TResult>(object node, List<TResult> found, Dictionary<Type, PropertyInfo[]> walkableProperties) where TResult : Result {
        if (node is TResult match) {
            found.Add(match);

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
                    Walk(item, found, walkableProperties);
                }
            } else {
                Walk(value, found, walkableProperties);
            }
        }
    }

    private static bool IsWalkable(Type type)
        => IsResultNode(type)
            || (type.IsGenericType && type.GetGenericArguments() is [var element] && typeof(IEnumerable).IsAssignableFrom(type) && IsResultNode(element));

    private static bool IsResultNode(Type type)
        => type == typeof(ResultList) || type == typeof(RandomResult) || typeof(Result).IsAssignableFrom(type);

}
