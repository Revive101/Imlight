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
 * BADGE DEFINITION
 * ========================================================================
 *
 * PURPOSE:
 * One SpiralDB badge record: the client-visible half of a badge plus its earn rules.
 *
 * USAGE EXAMPLE:
 * SpiralDB.TryGetBadgeDefinition(badgeId, out var definition);
 *
 * NOTE:
 * Id is the StringHash of the internal badge name. Name is only known for some records and, when set, must hash to Id.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Collections.Generic;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.WizardData.Models.World;

public class BadgeDefinition {

    public uint Id { get; set; }
    public string Name { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Info { get; set; } = string.Empty;
    public string ProgressInfo { get; set; } = string.Empty;
    public int Max { get; set; }
    public uint Index { get; set; }
    public string Outline { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public bool Dynamic { get; set; }
    public string Filter { get; set; } = string.Empty;
    public bool ExtraCredit { get; set; }
    public bool ShowTitle { get; set; } = true;
    public bool Retired { get; set; }
    public bool HasReward { get; set; }
    public string RewardOverrideDescription { get; set; } = string.Empty;
    public bool Overcount { get; set; }
    public uint OvercountInterval { get; set; }
    public string RewardLootTable { get; set; }
    public string OvercountLootTable { get; set; }
    public string RegistryName { get; set; }
    public List<string> Adjectives { get; set; } = [];
    public bool AutoAdd { get; set; }
    public RequirementList Requirements { get; set; }
    public List<uint> RequiredAutoAddBadges { get; set; } = [];
    public ResultList CompleteResults { get; set; }

}
