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
 * WIZARD PET RECORD
 * ========================================================================
 *
 * PURPOSE:
 * Server-side counters of one pet that its item behavior has no field for.
 *
 * USAGE EXAMPLE:
 * var wins = WizardPetCollection.GetGamesWon(charId, petGlobalId, game);
 *
 * NOTE:
 * The pet's level, XP, genes and ratings live in its item behavior, in the WizardItems collection.
 *
 * TODO:
 * 
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Collections.Generic;

namespace Imlight.CoreLib.WizardData.Models.Player;

/// <summary>
/// The server-only state of one pet.
/// </summary>
public sealed class WizardPetRecord {

    /// <summary>
    /// The character that owns the pet.
    /// </summary>
    public ulong CharId { get; set; }

    /// <summary>
    /// The global id of the pet item.
    /// </summary>
    public ulong PetGlobalId { get; set; }

    /// <summary>
    /// The games the pet won, by game name.
    /// </summary>
    public Dictionary<string, uint> GamesWon { get; set; } = [];

}
