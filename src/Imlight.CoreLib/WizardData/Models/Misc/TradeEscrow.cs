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
 * TRADE ESCROW
 * ========================================================================
 * 
 * PURPOSE:
 * The durable record of one treasure card swap in progress. Each side's offer is fixed at creation; the
 * flags say which database steps have happened, so a crashed swap can be settled from the document alone.
 * 
 * USAGE EXAMPLE:
 * var escrow = TradeEscrowCollection.Peek(tradeId);
 * 
 * NOTE:
 * A card is always in exactly one place: a stored spellbook, or here. Side A is the player who pressed Trade.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Imlight.CoreLib.WizardData.Models.Misc;

public sealed class TradeEscrow {

    public Guid TradeId { get; set; }
    public ulong CharIdA { get; set; }
    public ulong CharIdB { get; set; }
    public List<uint> CardsFromA { get; set; } = [];
    public List<uint> CardsFromB { get; set; } = [];
    public bool HeldA { get; set; }
    public bool HeldB { get; set; }
    public bool ClaimedA { get; set; }
    public bool ClaimedB { get; set; }
    public bool RefundedA { get; set; }
    public bool RefundedB { get; set; }
    public bool Cancelled { get; set; }
    public DateTime CreatedUtc { get; set; }

    [JsonIgnore] public bool IsCommitted => HeldA && HeldB && !Cancelled;

    [JsonIgnore] public bool IsSettled
        => (ClaimedA && ClaimedB) || (Cancelled && (!HeldA || RefundedA) && (!HeldB || RefundedB));

}

public enum EscrowOutcome {
    Done,
    AlreadyDone,
    NotOwned,
    Cancelled,
    NotCommitted,
    NotCancelled,
    Committed,
    NotSettled,
    NotParticipant,
    Missing,
    Failed,
}

public readonly record struct EscrowResult(EscrowOutcome Outcome, IReadOnlyList<uint> Cards) {

    public IReadOnlyList<uint> Given { get; init; }

    public static EscrowResult Of(EscrowOutcome outcome) => new(outcome, []);

}
