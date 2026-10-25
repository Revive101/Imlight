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
 * A season template together with the year the season starts in.
 * 
 * USAGE EXAMPLE:
 * PvPLeagueFactory.TryGetSeason(seasonId, out var season); var year = season.Year;
 * 
 * NOTE:
 * The template stores month, day, hour and minute only. The year comes from the Seasons<year> directory the
 * template was loaded from. Seasons outside such a directory (the beta, gamma and test seasons) have no year.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.PvP.Data;

internal sealed record PvPSeasonEntry(PvPSeasonTemplate Template, int? Year);
