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
 * PET GAME STUB
 * ========================================================================
 *
 * PURPOSE:
 * Stands in for a pet game that is not built yet: it ends with a score of 0 as soon as it starts, so the
 * join, end screen and snack pick can run in the client.
 *
 * USAGE EXAMPLE:
 * PetGameFactory.Create returns it for every game without its own implementation.
 *
 * NOTE:
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using Imcodec.IO;

namespace Imlight.CoreLib.Game.Pet.Games;

/// <summary>
/// A pet game that ends at once with a score of 0.
/// </summary>
internal sealed class PetGameStub : IPetGame {

    public float SkipScore => 0;

    public ByteString CreateInitData(PetGameSession session)
        => session.SerializeGameInfo();

    public ByteString CreateStartData(PetGameSession session)
        => ByteString.Empty;

    public void Start(IPetGameHost host)
        => host.Finish(new PetGameOutcome(0, Won: false));

    public void ReceiveData(IPetGameHost host, byte command, BitReader payload) { }

}
