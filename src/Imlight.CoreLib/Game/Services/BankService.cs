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
 * BANK SERVICE
 * ========================================================================
 *
 * PURPOSE:
 * Receives the bank window's requests (moves between the backpack, the bank and the shared bank, deletes)
 * and the bank streams other windows ask for. It knows whether the player has the bank open and hands the
 * rest to BankRequests.
 *
 * USAGE EXAMPLE:
 * The client sends MSG_MOVEINVTOBANK and its siblings from the bank window. Each one is answered with the
 * messages of its flow, see BankRequests.
 *
 * NOTE:
 * The bank counts as open from the moment the bank NPC lets the player in until the window is closed,
 * the zone changes or the session attaches again. A request outside that time is refused and logged.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imlight.Common;
using Imlight.CoreLib.Game.Bank;
using Imlight.CoreLib.Game.Zone.Components;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Services;

internal class BankService(SessionActor sessionActor) : MessageService(sessionActor) {

    private ulong _openBankEntityId;

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new BankService(parentActor));

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_BEGINBANKSESSION))]
    private void ReceiveBeginBankSession(CHARACTER_103_PROTOCOL.MSG_BEGINBANKSESSION message) {
        _openBankEntityId = message.EntityId;
        Logger.Debug("BankService of session {0} opens the bank at NPC {1}.",
            Logger.Args(SessionActor.SessionID, message.EntityId));
        CreateRequests().Open(message.EntityId);
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_MOVEINVTOBANK))]
    private void ReceiveMoveInventoryToBank(WIZARD_12_PROTOCOL.MSG_MOVEINVTOBANK message)
        => CreateRequests().InventoryToBank(message);

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_MOVEBANKTOINV))]
    private void ReceiveMoveBankToInventory(WIZARD_12_PROTOCOL.MSG_MOVEBANKTOINV message)
        => CreateRequests().BankToInventory(message);

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_MOVEBANKTOBANK))]
    private void ReceiveMoveBankToBank(WIZARD_12_PROTOCOL.MSG_MOVEBANKTOBANK message)
        => CreateRequests().BankToBank(message);

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_BANKDELETE))]
    private void ReceiveBankDelete(WIZARD_12_PROTOCOL.MSG_BANKDELETE message)
        => CreateRequests().Delete(message);

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_SHAREDBANKDELETEREAGENTORPETSNACK))]
    private void ReceiveSharedBankDeleteStack(WIZARD_12_PROTOCOL.MSG_SHAREDBANKDELETEREAGENTORPETSNACK message)
        => CreateRequests().DeleteSharedStack(message);

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_BANKCOUNT))]
    private void ReceiveBankCount(WIZARD2_53_PROTOCOL.MSG_BANKCOUNT message)
        => CreateRequests().SendBankCount();

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_AUCTIONREQUESTBANK))]
    private void ReceiveAuctionRequestBank(WIZARD_12_PROTOCOL.MSG_AUCTIONREQUESTBANK message)
        => CreateRequests().StreamBankFor(new WIZARD_12_PROTOCOL.MSG_AUCTIONREQUESTBANK());

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_QUICKSELLREQUESTBANK))]
    private void ReceiveQuickSellRequestBank(WIZARD_12_PROTOCOL.MSG_QUICKSELLREQUESTBANK message)
        => CreateRequests().StreamBankFor(new WIZARD_12_PROTOCOL.MSG_QUICKSELLREQUESTBANK());

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_DELIVERYINVOICEREQUESTBANK))]
    private void ReceiveDeliveryInvoiceRequestBank(WIZARD_12_PROTOCOL.MSG_DELIVERYINVOICEREQUESTBANK message)
        => CreateRequests().StreamBankFor(new WIZARD_12_PROTOCOL.MSG_DELIVERYINVOICEREQUESTBANK());

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_ITEMFINDERREQUESTBANK))]
    private void ReceiveItemFinderRequestBank(WIZARD2_53_PROTOCOL.MSG_ITEMFINDERREQUESTBANK message)
        => CreateRequests().StreamBankFor(new WIZARD2_53_PROTOCOL.MSG_ITEMFINDERREQUESTBANK());

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_DONESHOPPING))]
    private void ReceiveDoneShopping()
        => _openBankEntityId = 0;

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONETRANSFER))]
    private void ReceiveZoneTransfer(ZONE_102_PROTOCOL.MSG_ZONETRANSFER message)
        => _openBankEntityId = 0;

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE))]
    private void ReceiveAttachComplete(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE message)
        => _openBankEntityId = 0;

    private BankRequests CreateRequests()
        => new(GetActiveWizard(), SessionActor.ActorRef, IsBankOpen, UnequipItem);

    private bool IsBankOpen() {
        if (_openBankEntityId == 0) {
            return false;
        }

        var bank = GetZoneObject(_openBankEntityId)?.GetComponentOfType<InteractBankComponent>();

        return bank is not null && bank.IsInServiceRange(GetActiveGameObject());
    }

}
