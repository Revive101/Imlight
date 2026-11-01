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
 * PET GAME
 * ========================================================================
 *
 * PURPOSE:
 * The play of one pet game: what the client gets at INIT and START, what it does with the client's play
 * data, and when the game is over.
 *
 * USAGE EXAMPLE:
 * var game = PetGameFactory.Create(gameName);
 * game.Start(host);
 *
 * NOTE:
 * PetGamesService owns the session around the game: joining, energy, quitting, the end screen and the snack.
 * A game only plays, and calls IPetGameHost.Finish once with its outcome.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using Imcodec.IO;
using Imcodec.MessageLayer;

namespace Imlight.CoreLib.Game.Pet.Games;

/// <summary>
/// The play of one pet game, from START to its outcome.
/// </summary>
internal interface IPetGame {

    /// <summary>
    /// The median score of the game, which a skipped game is worth.
    /// </summary>
    float SkipScore { get; }

    /// <summary>
    /// Creates the data of MSG_PETGAMEINIT.
    /// </summary>
    /// <param name="session">The session the game runs in.</param>
    /// <returns>The data, empty if the game needs none.</returns>
    ByteString CreateInitData(PetGameSession session);

    /// <summary>
    /// Creates the data of MSG_PETGAMESTART.
    /// </summary>
    /// <param name="session">The session the game runs in.</param>
    /// <returns>The data, empty if the game needs none.</returns>
    ByteString CreateStartData(PetGameSession session);

    /// <summary>
    /// Starts play, right after MSG_PETGAMESTART went to the client.
    /// </summary>
    /// <param name="host">The session the game runs in.</param>
    void Start(IPetGameHost host);

    /// <summary>
    /// Takes play data from the client. Quitting and the snack pick never reach the game.
    /// </summary>
    /// <param name="host">The session the game runs in.</param>
    /// <param name="command">The type at the start of the data.</param>
    /// <param name="payload">The rest of the data, after the type.</param>
    void ReceiveData(IPetGameHost host, byte command, BitReader payload);

    /// <summary>
    /// Takes a client message of the game's own, one that is not MSG_PETGAMEDATA.
    /// </summary>
    /// <param name="host">The session the game runs in.</param>
    /// <param name="message">The message.</param>
    void ReceiveMessage(IPetGameHost host, IMessage message) { }

}
