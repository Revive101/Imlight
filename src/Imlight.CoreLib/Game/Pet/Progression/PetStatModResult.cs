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
 * PET STAT MOD RESULT
 * ========================================================================
 *
 * PURPOSE:
 * Describes what a set of stat changes did to a pet: the points that raised its stats and the points
 * its maximums turned away.
 *
 * USAGE EXAMPLE:
 * var result = PetProgression.ApplyStatMods(pet, snack.m_statModifierSet);
 * PetXpAward.Give(session, playerObj, wizard, pet, result.Xp, PetXpSource.Snack);
 *
 * NOTE:
 * Mods is a copy of the set that was applied, with the points each stat took in m_actualChange. That
 * copy is what a game result sends to the client.
 *
 * TODO:
 * - The community rule says a point is worth one XP whether a stat took it or not; no client data confirms it.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Pet.Progression;

/// <summary>
/// The outcome of applying a set of stat changes to a pet.
/// </summary>
/// <param name="Mods">A copy of the applied set whose m_actualChange holds the points each stat took.</param>
/// <param name="Applied">The points that raised a stat.</param>
/// <param name="Overflow">The points a stat could not take because it was at its maximum.</param>
internal sealed record PetStatModResult(PetStatModificationSet Mods, uint Applied, uint Overflow) {

    /// <summary>
    /// The XP the points are worth: one per point awarded, whether a stat took it or the maximum turned it away.
    /// </summary>
    internal uint Xp => (uint) Math.Min((ulong) Applied + Overflow, uint.MaxValue);

}
