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
 * PET RULES
 * ========================================================================
 *
 * PURPOSE:
 * Holds the pet rules the client files do not define. Each one has a default and can be
 * overridden in the [Pets] section of Imlight.ini.
 *
 * USAGE EXAMPLE:
 * pet.m_maximumHappiness = (uint) PetRules.MaximumHappiness;
 *
 * NOTE:
 * Rules that exist in a client file are read from that file, never from here. Everyone counts as a member.
 *
 * TODO:
 * - Only game and snack XP get the member bonus, community sources do not say whether combat XP does.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using Imlight.Common;
using Imlight.CoreLib.Game.Pet.Progression;

namespace Imlight.CoreLib.Game.Pet.Config;

/// <summary>
/// The server-side pet rules, with their defaults.
/// </summary>
internal static class PetRules {

    private const int DEFAULT_STARTING_HAPPINESS = 50;
    private const int DEFAULT_MAXIMUM_HAPPINESS = 100;
    private const int DEFAULT_MEMBER_XP_MULTIPLIER = 2;
    private const int DEFAULT_GAME_ABORT_REFUND_PERCENT = 100;
    private const int DEFAULT_DANCE_ROUNDS = 5;
    private const int DEFAULT_DANCE_WIN_SCORE = 2;

    internal static int StartingHappiness { get; } = Math.Max(0, ConfigurationManager.GetValue("Pets.StartingHappiness", DEFAULT_STARTING_HAPPINESS));
    internal static int MaximumHappiness { get; } = Math.Max(1, ConfigurationManager.GetValue("Pets.MaximumHappiness", DEFAULT_MAXIMUM_HAPPINESS));
    internal static int MemberXpMultiplier { get; } = Math.Max(1, ConfigurationManager.GetValue("Pets.MemberXpMultiplier", DEFAULT_MEMBER_XP_MULTIPLIER));

    internal static int GameAbortRefundPercent { get; } = Math.Clamp(ConfigurationManager.GetValue("Pets.GameAbortRefundPercent", DEFAULT_GAME_ABORT_REFUND_PERCENT), 0, 100);

    // todo: confirm the number of dance rounds against live.
    internal static int DanceRounds { get; } = Math.Max(1, ConfigurationManager.GetValue("Pets.DanceRounds", DEFAULT_DANCE_ROUNDS));

    // todo: confirm the dance score that counts as a win against live.
    internal static int DanceWinScore { get; } = Math.Max(0, ConfigurationManager.GetValue("Pets.DanceWinScore", DEFAULT_DANCE_WIN_SCORE));

    /// <summary>
    /// Gets the factor applied to XP from a source, for a wizard who counts as a member.
    /// </summary>
    /// <param name="source">Where the XP comes from.</param>
    /// <returns>The factor, 1 when the source gets no bonus.</returns>
    internal static int GetXpMultiplier(PetXpSource source)
        => source is PetXpSource.Game or PetXpSource.Snack ? MemberXpMultiplier : 1;

}
