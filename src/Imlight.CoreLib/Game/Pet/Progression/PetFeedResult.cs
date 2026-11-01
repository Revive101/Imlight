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
 * PET FEED RESULT
 * ========================================================================
 *
 * PURPOSE:
 * Describes what feeding a pet snack from the snack bag did.
 *
 * USAGE EXAMPLE:
 * var result = PetFeeding.Feed(wizard, pet, snackTemplateId, wholeStack: false);
 * if (result.Fed) { ... }
 *
 * NOTE:
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Pet.Progression;

/// <summary>
/// The outcome of feeding a pet from the snack bag.
/// </summary>
/// <param name="Snack">The snack stack after the pet ate, which is empty once the last snack went. Null if nothing was eaten.</param>
/// <param name="Eaten">How many snacks the pet ate.</param>
internal sealed record PetFeedResult(ClientPetSnackItem Snack, int Eaten) {

    /// <summary>
    /// The result of a feed that was refused.
    /// </summary>
    internal static PetFeedResult None { get; } = new(null, 0);

    /// <summary>
    /// True if the pet ate.
    /// </summary>
    internal bool Fed => Eaten > 0;

}
