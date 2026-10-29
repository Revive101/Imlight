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
 * PET XP RESULT
 * ========================================================================
 *
 * PURPOSE:
 * Describes what a grant of pet XP did: how much XP the pet received and which levels it reached.
 *
 * USAGE EXAMPLE:
 * var result = PetProgression.AddXp(pet, xp, PetXpSource.Game);
 * if (result.Applied) { ... }
 *
 * NOTE:
 * A level lists the talent and derby power it expressed, which is what the level-up window shows.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Collections.Generic;

namespace Imlight.CoreLib.Game.Pet.Progression;

/// <summary>
/// A level a pet reached and what it earned there.
/// </summary>
/// <param name="Level">The level reached.</param>
/// <param name="NewTalent">The id of the combat talent expressed at this level, or 0 if none.</param>
/// <param name="NewDerbyPower">The id of the derby talent expressed at this level, or 0 if none.</param>
/// <param name="ActiveRating">The pet's active rating once this level's talents are expressed.</param>
internal sealed record PetLevelUp(byte Level, uint NewTalent, uint NewDerbyPower, byte ActiveRating);

/// <summary>
/// The outcome of a grant of pet XP.
/// </summary>
/// <param name="Granted">The XP the pet received, with any bonus for the source. 0 if the pet gained nothing.</param>
/// <param name="LevelUps">The levels the XP carried the pet up to, in order.</param>
internal sealed record PetXpResult(uint Granted, IReadOnlyList<PetLevelUp> LevelUps) {

    /// <summary>
    /// The result of a pet that cannot gain XP.
    /// </summary>
    internal static PetXpResult None { get; } = new(0, []);

    /// <summary>
    /// True if the pet received XP.
    /// </summary>
    internal bool Applied => Granted > 0;

    /// <summary>
    /// How many levels the XP carried the pet up.
    /// </summary>
    internal int LevelsGained => LevelUps.Count;

}
