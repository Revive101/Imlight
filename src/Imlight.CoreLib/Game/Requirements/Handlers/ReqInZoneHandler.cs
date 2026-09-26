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
 * REQ IN ZONE REQUIREMENT HANDLER
 * ========================================================================
 * 
 * PURPOSE:
 * Evaluates ReqInZone: the player must be in the required zone. Also the
 * dungeon-quest force-add marker: a quest whose m_requirements require being
 * in the dungeon zone is auto-granted on entry (see DungeonQuestIndex and
 * QuestService.TryGrantDungeonQuests).
 * 
 * USAGE EXAMPLE:
 * 
 * NOTE:
 * Only top-level requirements are evaluated: RequirementList elements nested
 * inside a requirement list have no handler today, so ReqInZone must sit at
 * the quest's top level to both index and gate.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 08/13/2026
 */

using System;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Requirements.Handlers;

internal sealed class ReqInZoneHandler : BaseRequirementHandler<ReqInZone> {

    public override bool Evaluate(IRequirementContext context) {
        var wizard = context.GetWizard();
        if (wizard is null) {
            return false;
        }

        var zoneName = Requirement.m_zoneName;
        if (string.IsNullOrEmpty(zoneName)) {
            return false;
        }

        return string.Equals(wizard.Zone, zoneName, StringComparison.OrdinalIgnoreCase);
    }

}
