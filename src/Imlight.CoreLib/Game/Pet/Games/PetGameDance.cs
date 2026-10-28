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
 * PET GAME DANCE
 * ========================================================================
 *
 * PURPOSE:
 * The dance game: the server shows a sequence of moves, the client plays it back, and the server judges each round.
 *
 * USAGE EXAMPLE:
 * PetGameFactory.Create(PetGameDance.GAME_NAME) returns it.
 *
 * NOTE:
 * A move is the letter 'a' plus its index: 0 up, 1 right, 2 down, 3 left. Each round sends the whole sequence so
 * far, which is one move longer than the last round's. A wrong or malformed reply fails that round, and too many
 * failed rounds end the game with a score of 0. The score is 0 to 4, by the share of rounds played right.
 * INIT and START data are empty, as the client ignores them.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System;
using System.Collections.Generic;
using Imcodec.IO;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imlight.CoreLib.Game.Pet.Config;

namespace Imlight.CoreLib.Game.Pet.Games;

/// <summary>
/// The dance game.
/// </summary>
internal sealed class PetGameDance : IPetGame {

    internal const string GAME_NAME = "PetGameDance";

    private const byte MOVE_LETTER_BASE = (byte) 'a';
    private const int MOVE_KINDS = 4;
    private const int FIRST_ROUND_MOVES = 3;
    private const int MAX_FAILED_ROUNDS = 3;
    private const int MAX_SCORE = 4;

    private readonly List<byte> _sequence = [];
    private int _round;
    private int _correctRounds;
    private int _failedRounds;

    public float SkipScore => MAX_SCORE / 2f;

    public ByteString CreateInitData(PetGameSession session)
        => session.SerializeGameInfo();

    public ByteString CreateStartData(PetGameSession session)
        => ByteString.Empty;

    public void Start(IPetGameHost host) {
        for (var i = 0; i < FIRST_ROUND_MOVES - 1; i++) {
            _sequence.Add(NextMove());
        }

        SendNextRound(host);
    }

    public void ReceiveData(IPetGameHost host, byte command, BitReader payload) { }

    public void ReceiveMessage(IPetGameHost host, IMessage message) {
        if (message is not PET_9_PROTOCOL.MSG_PETGAMEDANCE reply || _round == 0) {
            return;
        }

        if (IsSequenceRepeated(reply.Moves)) {
            _correctRounds++;
        }
        else {
            _failedRounds++;
        }

        if (_failedRounds >= MAX_FAILED_ROUNDS) {
            host.Finish(new PetGameOutcome(0, Won: false));

            return;
        }

        if (_round >= PetRules.DanceRounds) {
            Finish(host);

            return;
        }

        SendNextRound(host);
    }

    private void SendNextRound(IPetGameHost host) {
        _sequence.Add(NextMove());
        _round++;
        host.Send(new PET_9_PROTOCOL.MSG_PETGAMEDANCE { Moves = _sequence.ToArray() });
    }

    private bool IsSequenceRepeated(ByteString reply) {
        byte[] moves = reply;

        return moves is not null && moves.AsSpan().SequenceEqual(_sequence.ToArray());
    }

    private void Finish(IPetGameHost host) {
        var score = (int) Math.Round(_correctRounds * (double) MAX_SCORE / PetRules.DanceRounds, MidpointRounding.AwayFromZero);
        score = Math.Clamp(score, 0, MAX_SCORE);
        host.Finish(new PetGameOutcome(score, Won: score >= PetRules.DanceWinScore));
    }

    private static byte NextMove()
        => (byte) (MOVE_LETTER_BASE + Random.Shared.Next(MOVE_KINDS));

}
