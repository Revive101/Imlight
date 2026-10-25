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
 * The rules of one treasure card trade between two players: who joined, what each side offers, who is
 * ready, and how far the escrow swap has got. It sends nothing; the TradeDirectory turns each result
 * into messages.
 * 
 * USAGE EXAMPLE:
 * if (session.TryChangeOffer(participant, templateId, 0, add: true, ownedCount) == TradeOfferResult.Changed) { ... }
 * 
 * NOTE:
 * Side A is the player who pressed Trade, side B the invitee. Every accepted offer change resets both
 * ready values to 0. ApplyStep names the escrow step the trade is waiting on; from ClaimA on the trade
 * is committed and can no longer fail on ownership.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.Game.Trading;

internal enum TradePhase {
    Inviting,
    Open,
    Applying,
    Ended,
}

internal enum TradeApplyStep {
    None,
    HoldA,
    HoldB,
    ClaimA,
    ClaimB,
    Done,
}

internal enum TradeStep : byte {
    Hold,
    Claim,
    Refund,
}

internal enum TradeJoinStatus {
    Pending = -1,
    Left = 0,
    Joined = 1,
}

internal enum TradeReadyStatus {
    NotReady,
    Ready,
    Confirmed,
}

internal enum TradeReadyResult {
    Applied,
    NotJoined,
    NotReady,
    Invalid,
    Ignored,
}

internal enum TradeOfferResult {
    Changed,
    NotJoined,
    Rejected,
    Ignored,
}

internal sealed class TradeSession {

    private const int MAX_DISTINCT_CARDS_PER_SIDE = 4;

    internal Guid TradeId { get; }
    internal TradeParticipant A { get; }
    internal TradeParticipant B { get; }
    internal TradePhase Phase { get; private set; } = TradePhase.Inviting;
    internal TradeApplyStep ApplyStep { get; private set; } = TradeApplyStep.None;
    internal TradeError AbortError { get; private set; }

    internal bool IsAborting => AbortError != TradeError.None;
    internal bool IsCommitted => ApplyStep >= TradeApplyStep.ClaimA;
    internal bool BothConfirmed => Phase == TradePhase.Open && A.Ready == TradeReadyStatus.Confirmed && B.Ready == TradeReadyStatus.Confirmed;

    internal TradeParticipant ApplyActor => ApplyStep switch {
        TradeApplyStep.HoldA or TradeApplyStep.ClaimA => A,
        TradeApplyStep.HoldB or TradeApplyStep.ClaimB => B,
        _ => null,
    };

    internal TradeSession(Guid tradeId, TradeParticipant requester, TradeParticipant invitee) {
        TradeId = tradeId;
        A = requester;
        B = invitee;

        A.Join = TradeJoinStatus.Joined;
    }

    internal TradeParticipant[] Participants => [A, B];

    internal TradeParticipant Find(ulong charId)
        => A.CharId == charId ? A : B.CharId == charId ? B : null;

    internal TradeParticipant PeerOf(TradeParticipant participant)
        => ReferenceEquals(participant, A) ? B : A;

    internal bool TryJoin(TradeParticipant participant) {
        if (Phase != TradePhase.Inviting || !ReferenceEquals(participant, B) || B.Join != TradeJoinStatus.Pending) {
            return false;
        }

        B.Join = TradeJoinStatus.Joined;
        Phase = TradePhase.Open;

        return true;
    }

    internal bool TryLeave(TradeParticipant participant) {
        if (Phase == TradePhase.Ended || IsCommitted) {
            return false;
        }

        participant.Join = TradeJoinStatus.Left;
        Phase = TradePhase.Ended;

        return true;
    }

    internal TradeReadyResult TryChangeReady(TradeParticipant participant, TradeReadyStatus ready) {
        if (Phase is TradePhase.Applying or TradePhase.Ended) {
            return TradeReadyResult.Ignored;
        }

        if (Phase != TradePhase.Open) {
            return TradeReadyResult.NotJoined;
        }

        if (!Enum.IsDefined(ready)) {
            return TradeReadyResult.Invalid;
        }

        if (ready == TradeReadyStatus.Confirmed && participant.Ready != TradeReadyStatus.Confirmed
            && (participant.Ready != TradeReadyStatus.Ready || PeerOf(participant).Ready == TradeReadyStatus.NotReady)) {
            return TradeReadyResult.NotReady;
        }

        participant.Ready = ready;

        return TradeReadyResult.Applied;
    }

    internal TradeOfferResult TryChangeOffer(TradeParticipant participant, uint templateId, uint enchant, bool add, int ownedCount) {
        if (Phase is TradePhase.Applying or TradePhase.Ended) {
            return TradeOfferResult.Ignored;
        }

        if (Phase != TradePhase.Open) {
            return TradeOfferResult.NotJoined;
        }

        if (enchant != 0) {
            return TradeOfferResult.Rejected;
        }

        var offer = participant.Offer;
        offer.TryGetValue(templateId, out var copies);

        var changed = add
            ? TryAddCopy(offer, templateId, copies, ownedCount)
            : TryRemoveCopy(offer, templateId, copies);
        if (!changed) {
            return TradeOfferResult.Rejected;
        }

        A.Ready = TradeReadyStatus.NotReady;
        B.Ready = TradeReadyStatus.NotReady;

        return TradeOfferResult.Changed;
    }

    private static bool TryAddCopy(Dictionary<uint, int> offer, uint templateId, int copies, int ownedCount) {
        var isNewCard = copies == 0;
        if (copies + 1 > ownedCount
            || (isNewCard && offer.Count >= MAX_DISTINCT_CARDS_PER_SIDE)
            || !TreasureCardTradePolicy.IsTradable(templateId)) {
            return false;
        }

        offer[templateId] = copies + 1;

        return true;
    }

    private static bool TryRemoveCopy(Dictionary<uint, int> offer, uint templateId, int copies) {
        if (copies == 0) {
            return false;
        }

        if (copies == 1) {
            offer.Remove(templateId);

            return true;
        }

        offer[templateId] = copies - 1;

        return true;
    }

    internal static bool TryParseAction(byte action, out bool add) {
        add = action == 1;

        return action is 0 or 1;
    }

    internal bool BeginApply() {
        if (!BothConfirmed) {
            return false;
        }

        Phase = TradePhase.Applying;
        ApplyStep = TradeApplyStep.HoldA;

        return true;
    }

    internal void AdvanceApply() {
        if (Phase != TradePhase.Applying || ApplyStep == TradeApplyStep.Done) {
            return;
        }

        ApplyStep++;
    }

    internal void BeginAbort(TradeError error)
        => AbortError = error;

    internal void End()
        => Phase = TradePhase.Ended;

    internal List<uint> GivenCards(TradeParticipant participant)
        => [.. participant.Offer.OrderBy(x => x.Key).SelectMany(x => Enumerable.Repeat(x.Key, x.Value))];

    internal uint ItemsGained(TradeParticipant participant)
        => (uint) PeerOf(participant).Offer.Values.Sum();

    internal uint ItemsLost(TradeParticipant participant)
        => (uint) participant.Offer.Values.Sum();

}
