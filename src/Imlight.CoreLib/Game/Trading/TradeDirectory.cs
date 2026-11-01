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
 * The single authority for treasure card trades: invites, who joined, what each side offers, who is
 * ready, and the escrow swap once both confirmed. It answers each client with generated WIZARD_12 wire
 * messages sent straight to the participant's session.
 *
 * USAGE EXAMPLE:
 * Created once by the GameServerPool. Sessions talk to it through TradeService with TRADE_110_PROTOCOL
 * messages; it never touches a Wizard, only the TradeEscrow document.
 *
 * NOTE:
 * Trades live in memory only; the swap itself is kept in TradeEscrowCollection.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Generic;
using Akka.Actor;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Trading;

internal sealed class TradeDirectory : ReceiveProtocolDispatcher, IWithTimers {

    private const int MAX_STEP_ATTEMPTS = 3;

    private static readonly TimeSpan s_inviteTimeout
        = TimeSpan.FromSeconds(ConfigurationManager.Settings["Game Server.TradeInviteTimeoutSeconds"].AsUShort());
    private static readonly TimeSpan s_stepTimeout = TimeSpan.FromSeconds(15);

    private static string InviteTimerKey(Guid tradeId) => $"invite-{tradeId}";
    private static string ApplyTimerKey(Guid tradeId) => $"apply-{tradeId}";

    public static IActorRef Instance { get; private set; }

    public ITimerScheduler Timers { get; set; }

    private readonly Dictionary<ulong, TradeSession> _tradesByChar = [];
    private readonly Dictionary<Guid, TradeSession> _tradesById = [];

    public TradeDirectory()
        => Instance = Self;

    public static Props Props()
        => Akka.Actor.Props.Create(() => new TradeDirectory());

    protected override bool AroundReceive(Receive receive, object message) {
        // A restart would rebuild the directory empty and strand every open trade.
        try {
            return base.AroundReceive(receive, message);
        } catch (Exception ex) {
            Logger.Error("TradeDirectory failed to handle {MessageType}: {Exception}",
                Logger.Args(message.GetType().Name, ex));

            return true;
        }
    }

    #region Invite and join

    [MessageHandler(typeof(TRADE_110_PROTOCOL.MSG_OPENTRADE))]
    private void ReceiveOpenTrade(TRADE_110_PROTOCOL.MSG_OPENTRADE message) {
        var requesterId = message.RequesterCharId;
        var targetId = message.TargetCharId;
        if (requesterId == 0 || targetId == 0 || requesterId == targetId) {
            return;
        }

        if (_tradesByChar.TryGetValue(requesterId, out var leftBehind)) {
            var previous = leftBehind.Find(requesterId);
            if (previous.Session is not null && !previous.Session.Equals(message.Session)) {
                Depart(leftBehind, previous);
            }
        }

        // The target invited the requester already: pressing Trade back accepts that invite.
        if (_tradesByChar.TryGetValue(requesterId, out var invitedBy)
            && invitedBy.Phase == TradePhase.Inviting && invitedBy.A.CharId == targetId) {
            invitedBy.B.Session = message.Session;
            JoinInvitee(invitedBy, message.Zone);

            return;
        }

        if (_tradesByChar.ContainsKey(requesterId) || _tradesByChar.ContainsKey(targetId)) {
            Logger.Debug("Character {RequesterId} cannot trade with {TargetId}: a trade is already open.",
                Logger.Args(requesterId, targetId));
            message.Session.Tell(ResultMessage(Wizard.GetGameObjectId(targetId), TradeError.AlreadyTrading));

            return;
        }

        if (string.IsNullOrEmpty(message.TargetSessionPath)) {
            message.Session.Tell(ResultMessage(Wizard.GetGameObjectId(targetId), TradeError.TargetNotFound));

            return;
        }

        var trade = new TradeSession(
            Guid.NewGuid(),
            new TradeParticipant(requesterId, message.Session, message.Zone),
            new TradeParticipant(targetId, null, null)
        );
        _tradesById[trade.TradeId] = trade;
        _tradesByChar[requesterId] = trade;
        _tradesByChar[targetId] = trade;

        Timers.StartSingleTimer(
            InviteTimerKey(trade.TradeId),
            new TRADE_110_PROTOCOL.MSG_INVITEEXPIRED { TradeId = trade.TradeId },
            s_inviteTimeout
        );

        Context.ActorSelection(message.TargetSessionPath).Tell(new TRADE_110_PROTOCOL.MSG_TRADEINVITE {
            TradeId = trade.TradeId,
            RequesterCharId = requesterId,
            RequesterZone = message.Zone
        });

        Logger.Debug("Character {RequesterId} invited {TargetId} to trade {TradeId}.",
            Logger.Args(requesterId, targetId, trade.TradeId));
    }

    [MessageHandler(typeof(TRADE_110_PROTOCOL.MSG_INVITESCREENED))]
    private void ReceiveInviteScreened(TRADE_110_PROTOCOL.MSG_INVITESCREENED message) {
        if (!_tradesById.TryGetValue(message.TradeId, out var trade)) {
            if (message.Delivered) {
                DismissInvite(message);
            }

            return;
        }

        if (trade.Phase != TradePhase.Inviting) {
            return;
        }

        trade.B.Session = message.Session;
        trade.B.Zone = message.Zone;

        if (message.Delivered) {
            Send(trade.A, JoinStatusFor(trade, trade.A));
            Lock(trade.A);

            return;
        }

        if (message.Error != TradeError.None) {
            Send(trade.A, ResultMessage(trade.B.ObjectId, message.Error));
            EndTrade(trade);

            return;
        }

        trade.TryLeave(trade.B);
        Send(trade.A, JoinStatusFor(trade, trade.A));
        EndTrade(trade);
    }

    [MessageHandler(typeof(TRADE_110_PROTOCOL.MSG_INVITEEXPIRED))]
    private void ReceiveInviteExpired(TRADE_110_PROTOCOL.MSG_INVITEEXPIRED message) {
        if (!_tradesById.TryGetValue(message.TradeId, out var trade) || trade.Phase != TradePhase.Inviting) {
            return;
        }

        Logger.Debug("Trade {TradeId} invite expired.", Logger.Args(trade.TradeId));

        trade.TryLeave(trade.A);
        SendJoinStatuses(trade);
        EndTrade(trade);
    }

    [MessageHandler(typeof(TRADE_110_PROTOCOL.MSG_JOINCHANGED))]
    private void ReceiveJoinChanged(TRADE_110_PROTOCOL.MSG_JOINCHANGED message) {
        if (!TryGetTrade(message.CharId, message.PeerCharId, message.Session, out var trade, out var participant)) {
            return;
        }

        if (message.Join == TradeJoinStatus.Joined) {
            if (trade.Phase != TradePhase.Inviting || !ReferenceEquals(participant, trade.B) || participant.Session is null) {
                Logger.Debug("Character {CharId} cannot join trade {TradeId}.", Logger.Args(message.CharId, trade.TradeId));

                return;
            }

            JoinInvitee(trade, message.Zone);

            return;
        }

        if (message.Join == TradeJoinStatus.Left) {
            Leave(trade, participant);
        }
    }

    [MessageHandler(typeof(TRADE_110_PROTOCOL.MSG_PARTICIPANTDEPARTED))]
    private void ReceiveParticipantDeparted(TRADE_110_PROTOCOL.MSG_PARTICIPANTDEPARTED message) {
        if (!_tradesByChar.TryGetValue(message.CharId, out var trade)) {
            return;
        }

        // A zone transfer can attach the next session before the previous one finishes disposing.
        var participant = trade.Find(message.CharId);
        if (participant.Session is null || !participant.Session.Equals(message.Session)) {
            return;
        }

        Depart(trade, participant);
    }

    private static void DismissInvite(TRADE_110_PROTOCOL.MSG_INVITESCREENED message) {
        message.Session.Tell(new WIZARD_12_PROTOCOL.MSG_TRADE_JOIN_STATUS {
            TargetGID = Wizard.GetGameObjectId(message.RequesterCharId),
            TargetStatus = 0,
            PlayerStatus = -1
        });
        message.Session.Tell(new TRADE_110_PROTOCOL.MSG_SETTRADESTATE { Trading = false });
    }

    private void JoinInvitee(TradeSession trade, IActorRef inviteeZone) {
        var invitee = trade.B;
        invitee.Zone = inviteeZone;

        if (invitee.Zone is null || !invitee.Zone.Equals(trade.A.Zone)) {
            Send(invitee, ResultMessage(trade.A.ObjectId, TradeError.TargetNotInZone));
            trade.TryLeave(invitee);
            Send(trade.A, JoinStatusFor(trade, trade.A));
            EndTrade(trade);

            return;
        }

        if (!trade.TryJoin(invitee)) {
            return;
        }

        Timers.Cancel(InviteTimerKey(trade.TradeId));
        SendJoinStatuses(trade);
        Lock(trade.A);
        Lock(trade.B);

        Logger.Debug("Trade {TradeId} is open between {RequesterId} and {InviteeId}.",
            Logger.Args(trade.TradeId, trade.A.CharId, invitee.CharId));
    }

    private void Leave(TradeSession trade, TradeParticipant participant) {
        if (trade.Phase == TradePhase.Applying) {
            if (!trade.IsCommitted && !trade.IsAborting) {
                AbortApply(trade, TradeError.Failed);
            }

            return;
        }

        if (!trade.TryLeave(participant)) {
            return;
        }

        SendJoinStatuses(trade);
        EndTrade(trade);
    }

    private void Depart(TradeSession trade, TradeParticipant participant) {
        if (trade.Phase == TradePhase.Applying) {
            DepartDuringApply(trade, participant);

            return;
        }

        if (trade.TryLeave(participant)) {
            SendJoinStatuses(trade);
        }

        participant.IsGone = true;
        EndTrade(trade);
    }

    private void DepartDuringApply(TradeSession trade, TradeParticipant participant) {
        participant.IsGone = true;
        participant.PendingStep = null;

        if (trade.IsAborting) {
            if (!HasPendingStep(trade)) {
                FinishAbort(trade);
            }

            return;
        }

        if (!trade.IsCommitted) {
            AbortApply(trade, TradeError.Failed);

            return;
        }

        if (ReferenceEquals(trade.ApplyActor, participant)) {
            trade.AdvanceApply();
            SendStep(trade);
        }
    }

    #endregion

    #region Offers and ready

    [MessageHandler(typeof(TRADE_110_PROTOCOL.MSG_READYCHANGED))]
    private void ReceiveReadyChanged(TRADE_110_PROTOCOL.MSG_READYCHANGED message) {
        if (!TryGetTrade(message.CharId, message.PeerCharId, null, out var trade, out var participant)) {
            return;
        }

        var peer = trade.PeerOf(participant);
        switch (trade.TryChangeReady(participant, message.Ready)) {
            case TradeReadyResult.Applied:
                SendReadyStatuses(trade);
                if (trade.BothConfirmed) {
                    BeginApply(trade);
                }

                break;
            case TradeReadyResult.NotJoined:
                Send(participant, ResultMessage(peer.ObjectId, TradeError.NotJoined));

                break;
            case TradeReadyResult.NotReady:
                Send(participant, ResultMessage(peer.ObjectId, TradeError.NotReady));

                break;
            case TradeReadyResult.Invalid:
                EndAsTampered(trade, participant);

                break;
            default:
                Logger.Debug("Trade {TradeId} ignored a ready change from {CharId}.", Logger.Args(trade.TradeId, message.CharId));

                break;
        }
    }

    [MessageHandler(typeof(TRADE_110_PROTOCOL.MSG_OFFERCHANGED))]
    private void ReceiveOfferChanged(TRADE_110_PROTOCOL.MSG_OFFERCHANGED message) {
        if (!TryGetTrade(message.CharId, message.PeerCharId, null, out var trade, out var participant)) {
            return;
        }

        if (!TradeSession.TryParseAction(message.Action, out var add)) {
            EndAsTampered(trade, participant);

            return;
        }

        var peer = trade.PeerOf(participant);
        switch (trade.TryChangeOffer(participant, message.TemplateId, message.Enchant, add, message.OwnedCount)) {
            case TradeOfferResult.Changed:
                SendReadyStatuses(trade);
                Send(peer, new WIZARD_12_PROTOCOL.MSG_TRADE_CHANGE_ITEM {
                    TargetGID = participant.ObjectId,
                    ChangeGID = participant.ObjectId,
                    ItemTemplate = message.TemplateId,
                    ItemEnchant = message.Enchant,
                    Action = message.Action
                });

                break;
            case TradeOfferResult.NotJoined:
                Send(participant, ResultMessage(peer.ObjectId, TradeError.NotJoined));

                break;
            case TradeOfferResult.Rejected:
                EndAsTampered(trade, participant);

                break;
            default:
                Logger.Debug("Trade {TradeId} ignored an offer change from {CharId}.", Logger.Args(trade.TradeId, message.CharId));

                break;
        }
    }

    private void EndAsTampered(TradeSession trade, TradeParticipant offender) {
        Logger.Warning("Character {CharId} sent a trade request the rules refuse. Trade {TradeId} ends.",
            Logger.Args(offender.CharId, trade.TradeId));

        trade.TryLeave(offender);
        Send(offender, ResultMessage(trade.PeerOf(offender).ObjectId, TradeError.Failed));
        Send(trade.PeerOf(offender), JoinStatusFor(trade, trade.PeerOf(offender)));
        EndTrade(trade);
    }

    #endregion

    #region Swap

    private void BeginApply(TradeSession trade) {
        if (!trade.BeginApply()) {
            return;
        }

        var created = TradeEscrowCollection.Create(
            trade.TradeId, trade.A.CharId, trade.B.CharId, trade.GivenCards(trade.A), trade.GivenCards(trade.B));
        if (created.Outcome is not (EscrowOutcome.Done or EscrowOutcome.AlreadyDone)) {
            Logger.Error("Trade {TradeId} could not store its escrow document: {Outcome}.", Logger.Args(trade.TradeId, created.Outcome));
            FinishWithError(trade, TradeError.Failed);

            return;
        }

        SendStep(trade);
    }

    private void SendStep(TradeSession trade) {
        while (trade.IsCommitted && trade.ApplyActor is { IsGone: true }) {
            trade.AdvanceApply();
        }

        var actor = trade.ApplyActor;
        if (actor is null) {
            CompleteTrade(trade);

            return;
        }

        var step = trade.IsCommitted ? TradeStep.Claim : TradeStep.Hold;
        actor.PendingStep = step;
        actor.StepAttempts++;
        actor.Session.Tell(new TRADE_110_PROTOCOL.MSG_TRADESTEP { TradeId = trade.TradeId, Step = step });

        StartStepTimer(trade);
    }

    [MessageHandler(typeof(TRADE_110_PROTOCOL.MSG_TRADESTEPDONE))]
    private void ReceiveTradeStepDone(TRADE_110_PROTOCOL.MSG_TRADESTEPDONE message) {
        if (!_tradesById.TryGetValue(message.TradeId, out var trade) || trade.Phase != TradePhase.Applying) {
            if (message.Error == TradeError.None && message.Step != TradeStep.Hold) {
                _ = TradeEscrowCollection.DeleteIfSettled(message.TradeId);
            }

            return;
        }

        var participant = trade.Find(message.CharId);
        if (participant is null || participant.PendingStep != message.Step) {
            return;
        }

        participant.PendingStep = null;

        if (trade.IsAborting) {
            ReceiveRefundDone(trade, participant, message);

            return;
        }

        if (!ReferenceEquals(trade.ApplyActor, participant)) {
            return;
        }

        if (message.Error == TradeError.None) {
            participant.StepAttempts = 0;
            trade.AdvanceApply();
            SendStep(trade);

            return;
        }

        if (message.Step == TradeStep.Hold) {
            AbortApply(trade, message.Error);

            return;
        }

        RetryOrSkipClaim(trade, participant);
    }

    private void ReceiveRefundDone(TradeSession trade, TradeParticipant participant, TRADE_110_PROTOCOL.MSG_TRADESTEPDONE message) {
        if (message.Step != TradeStep.Refund) {
            return;
        }

        if (message.Error != TradeError.None && participant.StepAttempts < MAX_STEP_ATTEMPTS && CanReach(participant)) {
            SendRefund(trade, participant);

            return;
        }

        if (!HasPendingStep(trade)) {
            FinishAbort(trade);
        }
    }

    private void RetryOrSkipClaim(TradeSession trade, TradeParticipant participant) {
        if (participant.StepAttempts < MAX_STEP_ATTEMPTS) {
            SendStep(trade);

            return;
        }

        Logger.Error("Trade {TradeId} could not claim for character {CharId}. The escrow stays until it is settled.",
            Logger.Args(trade.TradeId, participant.CharId));
        participant.PendingStep = null;
        participant.StepAttempts = 0;
        trade.AdvanceApply();
        SendStep(trade);
    }

    private void StartStepTimer(TradeSession trade)
        => Timers.StartSingleTimer(
            ApplyTimerKey(trade.TradeId),
            new TRADE_110_PROTOCOL.MSG_APPLYTIMEOUT { TradeId = trade.TradeId },
            s_stepTimeout
        );

    [MessageHandler(typeof(TRADE_110_PROTOCOL.MSG_APPLYTIMEOUT))]
    private void ReceiveApplyTimeout(TRADE_110_PROTOCOL.MSG_APPLYTIMEOUT message) {
        if (!_tradesById.TryGetValue(message.TradeId, out var trade) || trade.Phase != TradePhase.Applying) {
            return;
        }

        if (trade.IsAborting) {
            Logger.Warning("Trade {TradeId} did not hear back from every refund. The escrow stays until it is settled.",
                Logger.Args(trade.TradeId));
            FinishAbort(trade);

            return;
        }

        if (!trade.IsCommitted) {
            Logger.Warning("Trade {TradeId} timed out waiting for a hold.", Logger.Args(trade.TradeId));
            AbortApply(trade, TradeError.Failed);

            return;
        }

        var actor = trade.ApplyActor;
        if (actor is null) {
            return;
        }

        RetryOrSkipClaim(trade, actor);
    }

    private void AbortApply(TradeSession trade, TradeError error) {
        var cancel = TradeEscrowCollection.Cancel(trade.TradeId);
        switch (cancel.Outcome) {
            case EscrowOutcome.Committed:
                while (!trade.IsCommitted) {
                    trade.AdvanceApply();
                }

                foreach (var participant in trade.Participants) {
                    participant.PendingStep = null;
                    participant.StepAttempts = 0;
                }

                SendStep(trade);

                return;
            case EscrowOutcome.Done or EscrowOutcome.AlreadyDone:
                break;
            default:
                Logger.Error("Trade {TradeId} could not be cancelled: {Outcome}. The escrow stays until it is settled.",
                    Logger.Args(trade.TradeId, cancel.Outcome));
                FinishWithError(trade, error);

                return;
        }

        trade.BeginAbort(error);

        foreach (var participant in trade.Participants) {
            participant.StepAttempts = 0;
            if (!CanReach(participant)) {
                participant.PendingStep = null;

                continue;
            }

            SendRefund(trade, participant);
        }

        if (!HasPendingStep(trade)) {
            FinishAbort(trade);
        }
    }

    private void SendRefund(TradeSession trade, TradeParticipant participant) {
        participant.PendingStep = TradeStep.Refund;
        participant.StepAttempts++;
        participant.Session.Tell(new TRADE_110_PROTOCOL.MSG_TRADESTEP { TradeId = trade.TradeId, Step = TradeStep.Refund });

        StartStepTimer(trade);
    }

    private static void DeleteSettledEscrow(TradeSession trade) {
        var deleted = TradeEscrowCollection.DeleteIfSettled(trade.TradeId);
        if (deleted.Outcome is not (EscrowOutcome.Done or EscrowOutcome.Missing)) {
            Logger.Debug("Trade {TradeId} keeps its escrow document: {Outcome}.", Logger.Args(trade.TradeId, deleted.Outcome));
        }
    }

    private void FinishAbort(TradeSession trade) {
        DeleteSettledEscrow(trade);
        FinishWithError(trade, trade.AbortError);
    }

    private void FinishWithError(TradeSession trade, TradeError error) {
        foreach (var participant in trade.Participants) {
            Send(participant, ResultMessage(trade.PeerOf(participant).ObjectId, error));
        }

        EndTrade(trade);
    }

    private void CompleteTrade(TradeSession trade) {
        DeleteSettledEscrow(trade);

        foreach (var participant in trade.Participants) {
            Send(participant, ResultMessage(
                trade.PeerOf(participant).ObjectId, TradeError.None, trade.ItemsGained(participant), trade.ItemsLost(participant)));
        }

        Logger.Information("Trade {TradeId} between {CharIdA} and {CharIdB} finished.",
            Logger.Args(trade.TradeId, trade.A.CharId, trade.B.CharId));

        EndTrade(trade);
    }

    #endregion

    #region Helpers

    private bool TryGetTrade(ulong charId, ulong peerCharId, IActorRef session, out TradeSession trade, out TradeParticipant participant) {
        participant = null;

        if (!_tradesByChar.TryGetValue(charId, out trade)) {
            Logger.Debug("Character {CharId} has no open trade.", Logger.Args(charId));

            return false;
        }

        participant = trade.Find(charId);
        if (trade.PeerOf(participant).CharId != peerCharId) {
            Logger.Debug("Character {CharId} named {PeerCharId}, who is not its trade partner.", Logger.Args(charId, peerCharId));

            return false;
        }

        if (session is not null && participant.Session is not null && !participant.Session.Equals(session)) {
            return false;
        }

        return true;
    }

    private void EndTrade(TradeSession trade) {
        trade.End();
        Timers.Cancel(InviteTimerKey(trade.TradeId));
        Timers.Cancel(ApplyTimerKey(trade.TradeId));
        _tradesById.Remove(trade.TradeId);

        foreach (var participant in trade.Participants) {
            if (_tradesByChar.TryGetValue(participant.CharId, out var current) && ReferenceEquals(current, trade)) {
                _tradesByChar.Remove(participant.CharId);
            }

            participant.Session?.Tell(new TRADE_110_PROTOCOL.MSG_SETTRADESTATE { Trading = false });
            participant.IsLocked = false;
        }
    }

    private static void Lock(TradeParticipant participant) {
        if (participant.IsLocked || !CanReach(participant)) {
            return;
        }

        participant.IsLocked = true;
        participant.Session.Tell(new TRADE_110_PROTOCOL.MSG_SETTRADESTATE { Trading = true });
    }

    private static bool CanReach(TradeParticipant participant)
        => participant.Session is not null && !participant.IsGone;

    private static bool HasPendingStep(TradeSession trade)
        => trade.A.PendingStep is not null || trade.B.PendingStep is not null;

    private static void Send(TradeParticipant participant, IMessage message) {
        if (CanReach(participant)) {
            participant.Session.Tell(message);
        }
    }

    private static void SendJoinStatuses(TradeSession trade) {
        Send(trade.A, JoinStatusFor(trade, trade.A));
        Send(trade.B, JoinStatusFor(trade, trade.B));
    }

    private static void SendReadyStatuses(TradeSession trade) {
        Send(trade.A, ReadyStatusFor(trade, trade.A));
        Send(trade.B, ReadyStatusFor(trade, trade.B));
    }

    private static WIZARD_12_PROTOCOL.MSG_TRADE_JOIN_STATUS JoinStatusFor(TradeSession trade, TradeParticipant to) {
        var peer = trade.PeerOf(to);

        return new WIZARD_12_PROTOCOL.MSG_TRADE_JOIN_STATUS {
            TargetGID = peer.ObjectId,
            TargetStatus = (int) peer.Join,
            PlayerStatus = (int) to.Join
        };
    }

    private static WIZARD_12_PROTOCOL.MSG_TRADE_READY_STATUS ReadyStatusFor(TradeSession trade, TradeParticipant to) {
        var peer = trade.PeerOf(to);

        return new WIZARD_12_PROTOCOL.MSG_TRADE_READY_STATUS {
            TargetGID = peer.ObjectId,
            TargetStatus = (int) peer.Ready,
            PlayerStatus = (int) to.Ready
        };
    }

    private static WIZARD_12_PROTOCOL.MSG_TRADE_RESULT ResultMessage(ulong partnerObjectId, TradeError status, uint gained = 0, uint lost = 0)
        => new() {
            TargetGID = partnerObjectId,
            ItemsGained = gained,
            ItemsLost = lost,
            Status = (uint) status
        };

    #endregion

}
