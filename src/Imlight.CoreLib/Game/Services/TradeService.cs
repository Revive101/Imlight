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
 * Receives the client's trade requests (create, join, ready, offer) and forwards them to the
 * TradeDirectory with this session's identity. Also screens the invites sent to this player, locks and
 * releases the player's movement for a trade, and runs the escrow steps that move treasure cards in and
 * out of this player's treasure book.
 *
 * USAGE EXAMPLE:
 * Attached to every game session by GameServiceFactory.
 *
 * NOTE:
 * The session's own wizard is always the trade source; hold, claim and refund run here because only the owning session may change its wizard.
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
using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;
using Imlight.Common;
using Imlight.CoreLib.Game.Trading;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

internal class TradeService(SessionActor sessionActor) : MessageService(sessionActor) {

    private const int TREASURE_BOOK_CAPACITY = 999;

    private static readonly uint s_shopState = StringHash.Compute("Shop");
    private static readonly uint s_notShoppingState = StringHash.Compute("NotShopping");

    private ulong _charId;
    private bool _isInvolved;
    private bool _isLocked;
    private bool _isLoggingOut;
    private Guid _escrowTradeId;
    private Wizard _escrowWizard;
    private readonly HashSet<Guid> _mirroredHolds = [];
    private readonly HashSet<Guid> _mirroredEnds = [];

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new TradeService(parentActor));

    #region Client requests

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_TRADE_CREATE))]
    private void ReceiveTradeCreate(WIZARD_12_PROTOCOL.MSG_TRADE_CREATE message) {
        var wizard = GetActiveWizard();
        if (wizard is null || !Wizard.TryGetCharacterId(message.TargetGID, out var targetCharId)
            || targetCharId == 0 || targetCharId == wizard.CharId) {
            return;
        }

        // The client opens its window before the server answers, so every refusal is sent as a RESULT.
        var zone = SessionActor.GetZoneActor();
        if (wizard.IsInDuel || zone is null) {
            SendResult(message.TargetGID, TradeError.CannotTradeNow);

            return;
        }

        if (!TryGetOnlinePlayer(targetCharId, out var target)) {
            SendResult(message.TargetGID, TradeError.TargetNotFound);

            return;
        }

        if (target.CurrentZone != wizard.Zone) {
            SendResult(message.TargetGID, TradeError.TargetNotInZone);

            return;
        }

        _charId = wizard.CharId;
        _isInvolved = true;

        TellDirectory(new TRADE_110_PROTOCOL.MSG_OPENTRADE {
            Session = SessionActor.ActorRef,
            Zone = zone,
            RequesterCharId = wizard.CharId,
            TargetCharId = targetCharId,
            TargetSessionPath = target.ActorPath
        });
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_TRADE_JOIN_STATUS))]
    private void ReceiveTradeJoinStatus(WIZARD_12_PROTOCOL.MSG_TRADE_JOIN_STATUS message) {
        if (message.PlayerStatus is not ((int) TradeJoinStatus.Left or (int) TradeJoinStatus.Joined) 
                              || !TryGetPeer(message.TargetGID, out var wizard, out var peerCharId)) {
            return;
        }

        TellDirectory(new TRADE_110_PROTOCOL.MSG_JOINCHANGED {
            CharId = wizard.CharId,
            Session = SessionActor.ActorRef,
            Zone = SessionActor.GetZoneActor(),
            PeerCharId = peerCharId,
            Join = (TradeJoinStatus) message.PlayerStatus
        });
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_TRADE_READY_STATUS))]
    private void ReceiveTradeReadyStatus(WIZARD_12_PROTOCOL.MSG_TRADE_READY_STATUS message) {
        if (!TryGetPeer(message.TargetGID, out var wizard, out var peerCharId)) {
            return;
        }

        TellDirectory(new TRADE_110_PROTOCOL.MSG_READYCHANGED {
            CharId = wizard.CharId,
            PeerCharId = peerCharId,
            Ready = (TradeReadyStatus) message.PlayerStatus
        });
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_TRADE_CHANGE_ITEM))]
    private void ReceiveTradeChangeItem(WIZARD_12_PROTOCOL.MSG_TRADE_CHANGE_ITEM message) {
        if (!TryGetPeer(message.TargetGID, out var wizard, out var peerCharId)) {
            return;
        }

        TellDirectory(new TRADE_110_PROTOCOL.MSG_OFFERCHANGED {
            CharId = wizard.CharId,
            PeerCharId = peerCharId,
            TemplateId = message.ItemTemplate,
            Enchant = message.ItemEnchant,
            Action = message.Action,
            OwnedCount = wizard.SpellbookBehavior.TreasureCardCount(message.ItemTemplate)
        });
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_TRADE_CHANGE_MONEY))]
    private void ReceiveTradeChangeMoney(WIZARD_12_PROTOCOL.MSG_TRADE_CHANGE_MONEY message)
        => Logger.Warning("Character {CharId} sent a trade gold change of {Amount}. Trades carry treasure cards only.",
            Logger.Args(GetActiveWizard()?.CharId, message.Amount));

    #endregion

    #region Directory messages

    [MessageHandler(typeof(TRADE_110_PROTOCOL.MSG_TRADEINVITE))]
    private void ReceiveTradeInvite(TRADE_110_PROTOCOL.MSG_TRADEINVITE message) {
        var wizard = GetActiveWizard();
        var zone = SessionActor.GetZoneActor();
        var screened = new TRADE_110_PROTOCOL.MSG_INVITESCREENED {
            TradeId = message.TradeId,
            RequesterCharId = message.RequesterCharId,
            Session = SessionActor.ActorRef,
            Zone = zone
        };

        screened.Error = wizard is null ? TradeError.CannotTradeNow : ScreenInvite(wizard, message, zone);
        if (screened.Error != TradeError.None || BuddyRelationshipCollection.HasBlocked(wizard.CharId, message.RequesterCharId)) {
            TellDirectory(screened);

            return;
        }

        _charId = wizard.CharId;
        _isInvolved = true;

        SendToSocket(new WIZARD_12_PROTOCOL.MSG_TRADE_REQUEST { TargetGID = Wizard.GetGameObjectId(message.RequesterCharId) });

        screened.Delivered = true;
        TellDirectory(screened);
    }

    [MessageHandler(typeof(TRADE_110_PROTOCOL.MSG_SETTRADESTATE))]
    private void ReceiveSetTradeState(TRADE_110_PROTOCOL.MSG_SETTRADESTATE message) {
        if (message.Trading) {
            _isInvolved = true;
            if (!_isLocked) {
                _isLocked = true;
                BroadcastState(s_shopState);
            }

            return;
        }

        var wasLocked = _isLocked;
        _isLocked = false;
        _isInvolved = false;
        if (wasLocked) {
            BroadcastState(s_notShoppingState);
        }
    }

    [MessageHandler(typeof(TRADE_110_PROTOCOL.MSG_TRADESTEP))]
    private void ReceiveTradeStep(TRADE_110_PROTOCOL.MSG_TRADESTEP message) {
        var wizard = GetActiveWizard();
        var done = new TRADE_110_PROTOCOL.MSG_TRADESTEPDONE {
            TradeId = message.TradeId,
            CharId = wizard?.CharId ?? 0,
            Step = message.Step
        };

        if (wizard is null || !_isInvolved) {
            done.Error = TradeError.Failed;
            TellDirectory(done);

            return;
        }

        _charId = wizard.CharId;
        _escrowTradeId = message.TradeId;
        _escrowWizard = wizard;

        done.Error = RunStep(wizard, message.TradeId, message.Step);
        TellDirectory(done);
    }

    #endregion

    #region Lifecycle

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_QUERY_LOGOUT))]
    private void ReceiveQueryLogout(GAME_5_PROTOCOL.MSG_QUERY_LOGOUT message)
        => AnnounceDeparture(isLogout: true, release: true);

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT))]
    private void ReceiveClientDisconnect(GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT message)
        => AnnounceDeparture(isLogout: true, release: true);

    protected override void OnPreDispose() {
        try {
            AnnounceDeparture(_isLoggingOut, release: false);
            SettleEscrow();
        } finally {
            base.OnPreDispose();
        }
    }

    private void AnnounceDeparture(bool isLogout, bool release) {
        _isLoggingOut |= isLogout;
        if (!_isInvolved) {
            return;
        }

        if (release && _isLocked) {
            BroadcastState(s_notShoppingState);
        }

        _isInvolved = false;
        _isLocked = false;

        TellDirectory(new TRADE_110_PROTOCOL.MSG_PARTICIPANTDEPARTED {
            CharId = _charId,
            Session = SessionActor.ActorRef,
            IsLogout = isLogout
        });
    }

    private void SettleEscrow() {
        if (_escrowTradeId == Guid.Empty || _escrowWizard is null) {
            return;
        }

        var result = TradeEscrowCollection.Settle(_escrowTradeId, _charId);
        if (result.Outcome == EscrowOutcome.Done) {
            Mirror(_escrowWizard, _escrowTradeId, isHold: false, result.Cards, result.Given);
        }

        _ = TradeEscrowCollection.DeleteIfSettled(_escrowTradeId);

        Logger.Debug("Trade {TradeId} settled for character {CharId} while the session ended: {Outcome}.",
            Logger.Args(_escrowTradeId, _charId, result.Outcome));
    }

    #endregion

    #region Steps

    private TradeError RunStep(Wizard wizard, Guid tradeId, TradeStep step) {
        if (step == TradeStep.Hold) {
            var refusal = CheckHold(wizard, tradeId);
            if (refusal != TradeError.None) {
                return refusal;
            }
        }

        var result = step switch {
            TradeStep.Hold => TradeEscrowCollection.Hold(tradeId, wizard.CharId),
            TradeStep.Claim => TradeEscrowCollection.Claim(tradeId, wizard.CharId),
            _ => TradeEscrowCollection.Refund(tradeId, wizard.CharId),
        };
        if (result.Outcome is not (EscrowOutcome.Done or EscrowOutcome.AlreadyDone)) {
            return TradeError.Failed;
        }

        Mirror(wizard, tradeId, step == TradeStep.Hold, result.Cards, result.Given);

        return TradeError.None;
    }

    private static TradeError CheckHold(Wizard wizard, Guid tradeId) {
        if (!TryPeek(tradeId, out var escrow) || (escrow.CharIdA != wizard.CharId && escrow.CharIdB != wizard.CharId)) {
            return TradeError.Failed;
        }

        var isA = escrow.CharIdA == wizard.CharId;
        var give = isA ? escrow.CardsFromA : escrow.CardsFromB;
        var receive = isA ? escrow.CardsFromB : escrow.CardsFromA;
        if (!wizard.SpellbookBehavior.HasTreasureCards(give)) {
            return TradeError.Failed;
        }

        var book = wizard.SpellbookBehavior.TreasureCardTemplateIds ?? [];
        var given = TreasureCardTradePolicy.CountCapacityUsed(give);
        var received = TreasureCardTradePolicy.CountCapacityUsed(receive);
        var projected = TreasureCardTradePolicy.CountCapacityUsed(book) - given + received;
        var bookGrows = received > given;

        return projected > TREASURE_BOOK_CAPACITY && bookGrows ? TradeError.CannotHold : TradeError.None;
    }

    private void Mirror(Wizard wizard,
                        Guid tradeId,
                        bool isHold,
                        IReadOnlyList<uint> cards,
                        IReadOnlyList<uint> given = null) {
        if (!isHold && given is { Count: > 0 }) {
            Mirror(wizard, tradeId, isHold: true, given);
        }

        if (!(isHold ? _mirroredHolds : _mirroredEnds).Add(tradeId)) {
            return;
        }

        var book = wizard.SpellbookBehavior;
        if (!isHold) {
            book.AddTreasureCards(cards);

            return;
        }

        if (!book.TryRemoveTreasureCards(cards)) {
            Logger.Error("Trade {TradeId}: the memory copy of character {CharId} did not hold the cards the hold took.",
                Logger.Args(tradeId, wizard.CharId));
        }
    }

    private static bool TryPeek(Guid tradeId, out TradeEscrow escrow) {
        try {
            escrow = TradeEscrowCollection.Peek(tradeId);

            return escrow is not null;
        } catch (Exception ex) {
            Logger.Error("Trade {TradeId}: the escrow document could not be read. {Exception}", Logger.Args(tradeId, ex));
            escrow = null;

            return false;
        }
    }

    #endregion

    #region Helpers

    private TradeError ScreenInvite(Wizard wizard, TRADE_110_PROTOCOL.MSG_TRADEINVITE message, IActorRef zone) {
        if (wizard.PrivacyOptions.AllowTradeRequest == 0) {
            return TradeError.NotAccepting;
        }

        if (wizard.IsInDuel) {
            return TradeError.CannotTradeNow;
        }

        if (_isLocked) {
            return TradeError.AlreadyTrading;
        }

        if (zone is null || !zone.Equals(message.RequesterZone)) {
            return TradeError.TargetNotInZone;
        }

        return TradeError.None;
    }

    private bool TryGetPeer(ulong targetGameObjectId, out Wizard wizard, out ulong peerCharId) {
        peerCharId = 0;
        wizard = GetActiveWizard();

        return wizard is not null
            && Wizard.TryGetCharacterId(targetGameObjectId, out peerCharId)
            && peerCharId != 0
            && peerCharId != wizard.CharId;
    }

    private void SendResult(ulong targetGameObjectId, TradeError status)
        => SendToSocket(new WIZARD_12_PROTOCOL.MSG_TRADE_RESULT { TargetGID = targetGameObjectId, Status = (uint) status });

    private void BroadcastState(uint state)
        => ZoneBroadcast(new GAME_5_PROTOCOL.MSG_ENTERSTATE {
            GameObjectID = GetActiveWizard().GameObjectID,
            State = state,
            Data = "",
            IgnoreIfCurrentStateIsOff = 0
        }, isSelfless: false);

    private static void TellDirectory(IServerMessage message)
        => TradeDirectory.Instance?.Tell(message);

    #endregion

}
