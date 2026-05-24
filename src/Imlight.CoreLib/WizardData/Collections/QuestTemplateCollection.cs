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
*/

using Imcodec.ObjectProperty.TypeCache;
using System.Collections.Generic;

namespace Imlight.CoreLib.WizardData.Collections;

public static class QuestTemplateCollection {

    /// <summary>
    /// Get all quests from SpiralDB.
    /// </summary>
    /// <returns>A list of all quest templates.</returns>
    public static List<QuestTemplate> GetAllQuests() 
        => [.. SpiralDB.QuestTemplates];

    /// <summary>
    /// Get a quest by its name.
    /// </summary>
    /// <param name="questName">The name of the quest to retrieve.</param>
    /// <returns>The quest template if found; otherwise, null.</returns>
    public static QuestTemplate GetQuestByName(string questName) 
        => SpiralDB.GetQuestByName(questName);

    /// <summary>
    /// Check if a quest exists by its name.
    /// </summary>
    /// <param name="questName">The name of the quest to check.</param>
    /// <returns>True if the quest exists; otherwise, false.</returns>
    public static bool DoesQuestExist(string questName) 
        => SpiralDB.QuestExists(questName);

}