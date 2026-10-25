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
 * TRADE SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * One side of a trade: who it is, how far it has joined, whether it is ready, and the cards it offers.
 * 
 * USAGE EXAMPLE:
 * var offered = participant.Offer.TryGetValue(templateId, out var copies) ? copies : 0;
 * 
 * NOTE:
 * Offer maps a card template ID to the copies offered. The session and zone refs of the invitee are known
 * only once it has screened the invite. The directory also keeps here whether the player's movement is
 * locked, whether the session left, and the escrow step it is waiting on.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Collections.Generic;
using Akka.Actor;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Trading;

internal sealed class TradeParticipant(ulong charId, IActorRef session, IActorRef zone) {

    internal ulong CharId { get; } = charId;
    internal ulong ObjectId { get; } = Wizard.GetGameObjectId(charId);
    internal IActorRef Session { get; set; } = session;
    internal IActorRef Zone { get; set; } = zone;
    internal TradeJoinStatus Join { get; set; } = TradeJoinStatus.Pending;
    internal TradeReadyStatus Ready { get; set; }

    internal bool IsLocked { get; set; }
    internal bool IsGone { get; set; }
    internal TradeStep? PendingStep { get; set; }
    internal int StepAttempts { get; set; }

    internal Dictionary<uint, int> Offer { get; } = [];

}
