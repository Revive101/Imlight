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
 * TRADE SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Decides which treasure cards may be traded and which of them count toward the treasure book limit.
 * 
 * USAGE EXAMPLE:
 * if (!TreasureCardTradePolicy.IsTradable(templateId)) { ... }
 * 
 * NOTE:
 * Mirrors the client's vault list filter (PopulateLocalSpellVaultList, is_spell_entry_tradable): the
 * template must be a treasure card without the FLAG_NoTrade adjective, outside the CastleMagic school.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Trading;

internal static class TreasureCardTradePolicy {

    private const string NO_TRADE_FLAG = "FLAG_NoTrade";
    private const string CASTLE_MAGIC_SCHOOL = "CastleMagic";

    internal static bool IsTradable(uint templateId) {
        if (CoreObjectFactory.GetCoreTemplate(templateId) is not SpellTemplate template || !template.m_Treasure) {
            return false;
        }

        return template.m_sMagicSchoolName != CASTLE_MAGIC_SCHOOL
            && template.m_adjectives?.Any(x => x == NO_TRADE_FLAG) != true;
    }

    private static bool CountsTowardCapacity(uint templateId)
        => CoreObjectFactory.GetCoreTemplate(templateId) is not SpellTemplate template
            || template.m_sMagicSchoolName != CASTLE_MAGIC_SCHOOL;

    internal static int CountCapacityUsed(IEnumerable<uint> templateIds)
        => templateIds.Count(CountsTowardCapacity);

}
