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
 * MINIGAME MESSAGE PROTOCOL
 * ========================================================================
 *
 * PURPOSE:
 * Tells a minigame process which client messages open a round and which carry the final score,
 * across the ten minigame services.
 *
 * USAGE EXAMPLE:
 * if (MinigameProtocol.IsRoundStart(message)) { ... }
 * if (MinigameProtocol.TryGetScore(message, out var score)) { ... }
 *
 * NOTE:
 * Two protocols share the name MG3_MESSAGES (services 44 and 54), so every type is matched by its
 * generated protocol class. Soblocks (service 25) has no rewards message: it reports its score in
 * MSG_SOBLOCKS_ENDGAME and opens a round with MSG_SOBLOCKS_READY.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;

namespace Imlight.CoreLib.Game.Minigames;

/// <summary>
/// Classifies the messages a minigame client sends to its process.
/// </summary>
internal static class MinigameProtocol {

    private const byte SoblocksServiceId = 25;
    private const byte SoblocksReadyOrder = 13;
    private const byte ConnectOrder = 1;

    private static readonly byte[] s_serviceIds = [25, 40, 41, 42, 43, 44, 45, 46, 47, 54];

    internal static bool IsKnownService(byte serviceId)
        => Array.IndexOf(s_serviceIds, serviceId) >= 0;

    internal static bool IsRoundStart(IMessage message) {
        if (!IsKnownService(message.ServiceId)) {
            return false;
        }

        return message.ServiceId == SoblocksServiceId
            ? message.MessageOrder == SoblocksReadyOrder
            : message.MessageOrder == ConnectOrder;
    }

    internal static bool TryGetScore(IMessage message, out int score) {
        int? reported = message switch {
            SKULLRIDERS_MESSAGES_40_PROTOCOL.MSG_SKULLRIDERS_REWARDS rewards => rewards.score,
            DOODLEDOUG_MESSAGES_41_PROTOCOL.MSG_DOODLEDOUG_REWARDS rewards => rewards.score,
            MG1_MESSAGES_42_PROTOCOL.MSG_MG1_REWARDS rewards => rewards.score,
            MG2_MESSAGES_43_PROTOCOL.MSG_MG2_REWARDS rewards => rewards.score,
            MG3_MESSAGES_44_PROTOCOL.MSG_MG3_REWARDS rewards => rewards.score,
            MG4_MESSAGES_45_PROTOCOL.MSG_MG4_REWARDS rewards => rewards.score,
            MG5_MESSAGES_46_PROTOCOL.MSG_MG5_REWARDS rewards => rewards.score,
            MG6_MESSAGES_47_PROTOCOL.MSG_MG6_REWARDS rewards => rewards.score,
            MG3_MESSAGES_54_PROTOCOL.MSG_MG3_REWARDS rewards => rewards.score,
            SOBLOCKS_MESSAGES_25_PROTOCOL.MSG_SOBLOCKS_ENDGAME endGame => endGame.Score,
            _ => null
        };

        score = reported ?? -1;

        return reported.HasValue;
    }

}
