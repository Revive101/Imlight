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
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Results;

namespace Imlight.CoreLib.Game.Badges;

internal static class QuestBadgeAwards {

    internal static HashSet<string> Find(IEnumerable<QuestTemplate> quests)
        => ToNames(QuestResultScan.Find<ResAddBadge>(quests));

    internal static HashSet<string> Find(ResultList results)
        => ToNames(QuestResultScan.Find<ResAddBadge>(results));

    private static HashSet<string> ToNames(IEnumerable<ResAddBadge> awards)
        => awards.Select(award => award.m_badgeName)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToHashSet(StringComparer.Ordinal);

}
