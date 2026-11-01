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
 * One rated season of a character in one league: the rating, the rank index the client reads and the win, loss and draw counts.
 * 
 * USAGE EXAMPLE:
 * wizard.PvPEloBehavior.Records.Add(new PvPEloRecord { RecordId = 1, LeagueId = leagueId, SeasonId = seasonId, Elo = 1500 });
 * 
 * NOTE:
 * Mirrors the client AdvPvPEloRecord. StartTime is the season start in Unix seconds, which the client computes itself and compares.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/18/2026
 */

namespace Imlight.CoreLib.WizardData.Models.Player;

public class PvPEloRecord {

    public ulong RecordId { get; set; }
    public uint LeagueId { get; set; }
    public uint SeasonId { get; set; }
    public uint StartTime { get; set; }
    public bool Ended { get; set; }
    public int Elo { get; set; }
    public int RankIndex { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }

}
