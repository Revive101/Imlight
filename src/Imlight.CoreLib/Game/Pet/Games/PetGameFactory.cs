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
 * PET GAME FACTORY
 * ========================================================================
 *
 * PURPOSE:
 * Creates the play of a pet game by the game's name.
 *
 * USAGE EXAMPLE:
 * var game = PetGameFactory.Create(gameName);
 *
 * NOTE:
 * A game without its own implementation gets PetGameStub.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

namespace Imlight.CoreLib.Game.Pet.Games;

/// <summary>
/// Creates pet games by name.
/// </summary>
internal static class PetGameFactory {

    /// <summary>
    /// Creates a new play of a game.
    /// </summary>
    /// <param name="gameName">The game name as it appears in PetGames.xml.</param>
    /// <returns>The game, never null.</returns>
    internal static IPetGame Create(string gameName)
        => gameName switch {
            PetGameDance.GAME_NAME => new PetGameDance(),
            _ => new PetGameStub(),
        };

}
