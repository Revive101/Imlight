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
 * PET GAME OUTCOME
 * ========================================================================
 *
 * PURPOSE:
 * How a pet game went, as the game judged it.
 *
 * USAGE EXAMPLE:
 * host.Finish(new PetGameOutcome(score, Won: true));
 *
 * NOTE:
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

namespace Imlight.CoreLib.Game.Pet.Games;

/// <summary>
/// How a pet game went.
/// </summary>
/// <param name="Score">The score the end screen shows, which the XP is worked out from.</param>
/// <param name="Won">True if the game counts as a win for the pet.</param>
internal sealed record PetGameOutcome(float Score, bool Won);
