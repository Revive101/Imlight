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
 * PET GAME HOST
 * ========================================================================
 *
 * PURPOSE:
 * What a pet game may do with the session it runs in: read it, send the client messages and finish.
 *
 * USAGE EXAMPLE:
 * host.Send(new PET_9_PROTOCOL.MSG_PETGAMEDATA { Game = host.Session.GameName, Data = data });
 * host.Finish(new PetGameOutcome(score, Won: score >= winScore));
 *
 * NOTE:
 * PetGamesService is the host.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using Imcodec.MessageLayer;

namespace Imlight.CoreLib.Game.Pet.Games;

/// <summary>
/// The session a pet game runs in, as the game sees it.
/// </summary>
internal interface IPetGameHost {

    /// <summary>
    /// The session.
    /// </summary>
    PetGameSession Session { get; }

    /// <summary>
    /// Sends a message to the player's client.
    /// </summary>
    /// <param name="message">The message.</param>
    void Send(IMessage message);

    /// <summary>
    /// Ends play and gives the results. Only the first call counts.
    /// </summary>
    /// <param name="outcome">How the game went.</param>
    void Finish(PetGameOutcome outcome);

}
