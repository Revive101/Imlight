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
 * PVP
 * ========================================================================
 * 
 * PURPOSE:
 * The persisted PvP ratings of a character: one record per league and season.
 * 
 * USAGE EXAMPLE:
 * var record = wizard.PvPEloBehavior.Records.FirstOrDefault(r => r.LeagueId == leagueId && !r.Ended);
 * 
 * NOTE:
 * Saved with the character document through WizardCollection.UpdateCharacterPvPEloBehavior. Record ids count up from 1 for each character.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/18/2026
 */

using System.Collections.Generic;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Shared.Behaviors;

public class ServerPvPEloBehavior {

    public List<PvPEloRecord> Records { get; set; } = [];
    public ulong NextRecordId { get; set; } = 1;

}
