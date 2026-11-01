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
 * The refusal texts of the arena. MSG_ARENA_ERROR carries the string table id of a key; the client looks the
 * text up in its locale and shows "Unknown Arena Error" for an id with no text.
 * 
 * USAGE EXAMPLE:
 * SendToSocket(PvPArenaErrors.Message(PvPArenaErrors.AlreadyQueued));
 * 
 * NOTE:
 * Every key is <table>_<key> of an entry in Locale/en-US/Error.lang.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;

namespace Imlight.CoreLib.Game.PvP.Arena;

internal static class PvPArenaErrors {

    public const string ALREADY_QUEUED = "Error_AlreadyQueued";
    public const string ALREADY_IN_MATCH = "Error_ErrorAlreadyInMatch";
    public const string NOT_IN_LEVEL_RANGE = "Error_NotInLevelRange";
    public const string PLAYER_INELIGIBLE = "Error_PlayerIneligible";
    public const string INVALID_TOURNAMENT_TYPE = "Error_InvalidTournamentType";
    public const string INVALID_MATCH_NAME_ID = "Error_InvalidMatchNameID";
    public const string MATCH_NOT_ALLOWED = "Error_ErrorMatchNotAllowed";
    public const string PLAYER_DECLINED = "Error_PlayerDeclined";

    public static WIZARD_12_PROTOCOL.MSG_ARENA_ERROR Message(string key)
        => new() { Error = StringHash.Compute(key) };

}
