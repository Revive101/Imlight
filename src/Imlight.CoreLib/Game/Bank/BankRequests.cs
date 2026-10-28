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
 * BANK REQUESTS
 * ========================================================================
 *
 * PURPOSE:
 * Does what the bank window asks of the server: moves between the backpack, the bank and the shared bank,
 * deletes, the bank count and the streams other windows ask for. Every request is checked, done through the
 * atomic paths (Wizard transfers for the character's own lists, BankCollection for anything that touches the
 * shared bank) and answered with the messages the client needs.
 *
 * USAGE EXAMPLE:
 * var requests = new BankRequests(wizard, SessionActor.ActorRef, IsBankOpen, UnequipItem);
 * requests.InventoryToBank(message);
 *
 * NOTE:
 * The client messages go in the order the client needs: the destination add (it looks the destination up
 * when the confirm arrives), the source remove (the confirm recounts from the client's lists), then the
 * confirm. A refusal is a confirm with the failure set, and the client often shows nothing for it, so every
 * refusal is logged. The client does not take off a worn item before it asks to bank it, so the request
 * does that through the unequip delegate, after the rules agreed and only when a backpack slot is free.
 * The client sends one request per selected row without waiting, each is checked against the state the
 * request before it left.
 *
 * TODO:
 * Does the client list worn items in the Backpack tab for a delete? They are unequipped first, as for a move.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Linq;
using Akka.Actor;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Bank;

/// <summary>
/// The bank requests of one wizard.
/// </summary>
/// <param name="wizard">The wizard that asks.</param>
/// <param name="session">The SessionActor reference of the wizard's session.</param>
/// <param name="bankIsOpen">Whether the wizard has the bank open at a bank NPC in range.</param>
/// <param name="unequip">Takes an equipped item off the wizard and returns whether it is no longer equipped.</param>
internal sealed class BankRequests(Wizard wizard, IActorRef session, Func<bool> bankIsOpen, Func<ulong, bool> unequip) {

    /// <summary>
    /// Opens the bank window: reads the shared bank again, streams the bank and the shared bank, then opens the
    /// window. The window fills its lists from what the client holds when it opens, so the stream goes first.
    /// The open message names the bank NPC: the client drops it, and tells the server it is done shopping, when
    /// that id is not its open interaction in range.
    /// </summary>
    /// <param name="bankNpcId">The global id of the bank NPC the player picked the bank at.</param>
    internal void Open(ulong bankNpcId) {
        if (!AccountCollection.ReloadSharedBank(wizard.Account)) {
            Logger.Error("The shared bank of account {AccountId} could not be read again, the window opens with what the session holds.",
                Logger.Args(wizard.Account.AccountId));
        }

        var streamed = BankSync.StreamBank(session, wizard, includeShared: true);
        session.Tell(new WIZARD_12_PROTOCOL.MSG_OPENBANK { GlobalID = bankNpcId });
        Logger.Debug("Sent MSG_OPENBANK for NPC {0} to {1} after streaming the bank (complete: {2}).",
            Logger.Args(bankNpcId, wizard.GameObjectID, streamed));
    }

    /// <summary>
    /// Moves an item or part of a stack from the backpack into the bank or the shared bank.
    /// </summary>
    /// <param name="message">The client's request. UseShared names the destination.</param>
    internal void InventoryToBank(WIZARD_12_PROTOCOL.MSG_MOVEINVTOBANK message) {
        var useShared = message.UseShared != 0;
        var itemId = message.GlobalID;
        var to = useShared ? BankContainer.SharedBank : BankContainer.Bank;
        var request = $"move of item {itemId} to the {(useShared ? "shared bank" : "bank")}";
        var item = CheckMove(request, itemId, BankContainer.Backpack, to, message.Quantity);
        if (item is null) {
            session.Tell(BankSync.InvToBankFailed(message.ClientRequestID, itemId, useShared));

            return;
        }

        if (!EnsureUnequipped(request, itemId)) {
            session.Tell(BankSync.InvToBankFailed(message.ClientRequestID, itemId, useShared));

            return;
        }

        var moved = useShared
            ? MoveBackpackToShared(request, message, to)
            : MoveBackpackToBank(request, message, item);
        if (!moved) {
            session.Tell(BankSync.InvToBankFailed(message.ClientRequestID, itemId, useShared));
        }
    }

    private bool MoveBackpackToBank(string request, WIZARD_12_PROTOCOL.MSG_MOVEINVTOBANK message, WizClientObjectItem item) {
        var itemId = message.GlobalID;
        if (!wizard.InventoryToBankTransfer(itemId)) {
            Deny(request, itemId, "the character could not be saved");

            return false;
        }

        Send(BankSync.StorageAdd(wizard, item, shared: false));
        ItemSync.SendRemoved(session, wizard, itemId);
        session.Tell(BankSync.InvToBankConfirm(message.ClientRequestID, itemId, itemId, useShared: false));

        return true;
    }

    private bool MoveBackpackToShared(string request, WIZARD_12_PROTOCOL.MSG_MOVEINVTOBANK message, BankContainer to) {
        var itemId = message.GlobalID;
        var result = BankCollection.TryMoveShared(wizard, new SharedMove(BankContainer.Backpack, to, itemId, message.Quantity));
        if (!result.Success) {
            Deny(request, itemId, $"{result.Denial}: {result.Reason}");

            return false;
        }

        Send(BankSync.StorageAdd(wizard, result.Destination, shared: true));
        SendBackpackLeft(result, itemId);
        session.Tell(BankSync.InvToBankConfirm(message.ClientRequestID, result.OriginalId, result.NewId, useShared: true));

        return true;
    }

    private void SendBackpackLeft(SharedMoveResult result, ulong itemId) {
        if (result.Kind is not (BankItemKind.Reagent or BankItemKind.PetSnack)) {
            ItemSync.SendRemoved(session, wizard, itemId);

            return;
        }

        if (result.Source is null) {
            BankSync.SendBackpackStackRemoved(session, wizard, result.OriginalId, result.Kind);

            return;
        }

        BankSync.SendBackpackStackChanged(session, wizard, result.Source);
    }

    /// <summary>
    /// Moves an item or part of a stack from the bank or the shared bank into the backpack.
    /// </summary>
    /// <param name="message">The client's request. UseShared names the tab the client is on, which is the source.</param>
    internal void BankToInventory(WIZARD_12_PROTOCOL.MSG_MOVEBANKTOINV message) {
        var useShared = message.UseShared != 0;
        var itemId = message.GlobalID;
        var from = useShared ? BankContainer.SharedBank : BankContainer.Bank;
        var request = $"move of item {itemId} from the {(useShared ? "shared bank" : "bank")} to the backpack";
        var item = CheckMove(request, itemId, from, BankContainer.Backpack, message.Quantity);
        if (item is null) {
            session.Tell(BankSync.BankToInvFailed(message.ClientRequestID, itemId));

            return;
        }

        var moved = useShared
            ? MoveSharedToBackpack(request, message, from)
            : MoveBankToBackpack(request, message, item);
        if (!moved) {
            session.Tell(BankSync.BankToInvFailed(message.ClientRequestID, itemId));
        }
    }

    private bool MoveBankToBackpack(string request, WIZARD_12_PROTOCOL.MSG_MOVEBANKTOINV message, WizClientObjectItem item) {
        var itemId = message.GlobalID;
        if (!wizard.BankToInventoryTransfer(itemId)) {
            Deny(request, itemId, "the character could not be saved");

            return false;
        }

        ItemSync.SendAdded(session, wizard, item);
        session.Tell(BankSync.StorageRemove(wizard, itemId, shared: false));
        session.Tell(BankSync.BankToInvConfirm(message.ClientRequestID, itemId, itemId));

        return true;
    }

    private bool MoveSharedToBackpack(string request, WIZARD_12_PROTOCOL.MSG_MOVEBANKTOINV message, BankContainer from) {
        var itemId = message.GlobalID;
        var result = BankCollection.TryMoveShared(wizard, new SharedMove(from, BankContainer.Backpack, itemId, message.Quantity));
        if (!result.Success) {
            Deny(request, itemId, $"{result.Denial}: {result.Reason}");

            return false;
        }

        SendBackpackArrived(result, itemId);
        session.Tell(BankSync.BankToInvConfirm(message.ClientRequestID, result.OriginalId, result.NewId));

        return true;
    }

    private void SendBackpackArrived(SharedMoveResult result, ulong itemId) {
        if (result.Kind is not (BankItemKind.Reagent or BankItemKind.PetSnack)) {
            ItemSync.SendAdded(session, wizard, result.Destination);
            session.Tell(BankSync.StorageRemove(wizard, itemId, shared: true));

            return;
        }

        BankSync.SendBackpackStackAdded(session, wizard, result.Destination);
        if (result.Source is null) {
            session.Tell(BankSync.StorageRemove(wizard, result.OriginalId, shared: true));

            return;
        }

        Send(BankSync.StorageAdd(wizard, result.Source, shared: true));
    }

    /// <summary>
    /// Moves an item between the bank and the shared bank.
    /// </summary>
    /// <param name="message">The client's request. BankID is set for a move into the shared bank, BankSharedID for a move out of it.</param>
    internal void BankToBank(WIZARD_12_PROTOCOL.MSG_MOVEBANKTOBANK message) {
        var toShared = message.BankID != 0;
        var itemId = toShared ? message.BankID : message.BankSharedID;
        var request = $"move of item {itemId} between the bank and the shared bank";
        if ((message.BankID != 0) == (message.BankSharedID != 0)) {
            Deny(request, itemId, "exactly one of BankID and BankSharedID has to be set");
            session.Tell(BankSync.BankToBankFailed(message.ClientRequestID, itemId, toShared));

            return;
        }

        var from = toShared ? BankContainer.Bank : BankContainer.SharedBank;
        var to = toShared ? BankContainer.SharedBank : BankContainer.Bank;
        if (CheckMove(request, itemId, from, to, 1) is null) {
            session.Tell(BankSync.BankToBankFailed(message.ClientRequestID, itemId, toShared));

            return;
        }

        var result = BankCollection.TryMoveShared(wizard, new SharedMove(from, to, itemId, 1));
        if (!result.Success) {
            Deny(request, itemId, $"{result.Denial}: {result.Reason}");
            session.Tell(BankSync.BankToBankFailed(message.ClientRequestID, itemId, toShared));

            return;
        }

        Send(BankSync.StorageAdd(wizard, result.Destination, shared: toShared));
        session.Tell(BankSync.StorageRemove(wizard, itemId, shared: !toShared));
        session.Tell(BankSync.BankToBankConfirm(message.ClientRequestID, itemId, result.NewId, toShared));
    }

    /// <summary>
    /// Deletes an item for good from the backpack, the bank or the shared bank.
    /// </summary>
    /// <param name="message">The client's request. UseShared and FromInventory name the tab, neither set is the Bank tab.</param>
    internal void Delete(WIZARD_12_PROTOCOL.MSG_BANKDELETE message) {
        var useShared = message.UseShared != 0;
        var fromBackpack = !useShared && message.FromInventory != 0;
        var itemId = message.GlobalID;
        var from = useShared ? BankContainer.SharedBank : fromBackpack ? BankContainer.Backpack : BankContainer.Bank;
        var request = $"delete of item {itemId} from the {from}";
        if (!TryDelete(request, message, from, useShared)) {
            session.Tell(BankSync.BankDeleteFailed(message.ClientRequestID, useShared));
        }
    }

    private bool TryDelete(string request, WIZARD_12_PROTOCOL.MSG_BANKDELETE message, BankContainer from, bool useShared) {
        var itemId = message.GlobalID;
        var item = FindItem(from, itemId);
        if (!bankIsOpen()) {
            Deny(request, itemId, "the bank is not open");

            return false;
        }

        if (item is null) {
            ReportUnknown(request, itemId);

            return false;
        }

        if (item is ClientReagentItem or ClientPetSnackItem) {
            Deny(request, itemId, "stacks are not deleted with this request");

            return false;
        }

        var denial = BankRules.CanDelete(item);
        if (denial != BankDenial.None) {
            Deny(request, itemId, denial.ToString());

            return false;
        }

        if (!EnsureUnequipped(request, itemId)) {
            return false;
        }

        var result = from == BankContainer.SharedBank
            ? BankCollection.TryDeleteShared(wizard, itemId, 1)
            : BankCollection.TryDeleteOwn(wizard, from, itemId);
        if (!result.Success) {
            Deny(request, itemId, $"{result.Denial}: {result.Reason}");

            return false;
        }

        if (from == BankContainer.Backpack) {
            ItemSync.SendRemoved(session, wizard, itemId);
        } else {
            session.Tell(BankSync.StorageRemove(wizard, itemId, shared: useShared));
        }

        session.Tell(BankSync.BankDeleteConfirm(message.ClientRequestID, itemId, useShared));

        return true;
    }

    /// <summary>
    /// Deletes units of a reagent or pet snack stack of the shared bank.
    /// </summary>
    /// <param name="message">The client's request.</param>
    internal void DeleteSharedStack(WIZARD_12_PROTOCOL.MSG_SHAREDBANKDELETEREAGENTORPETSNACK message) {
        var stackId = message.GlobalID;
        var request = $"delete of {message.Quantity} units of shared stack {stackId}";
        var deleted = TryDeleteStack(request, stackId, message.Quantity);
        session.Tell(BankSync.SharedStackDeleteConfirm(message.ClientRequestID, stackId, failed: !deleted));
    }

    private bool TryDeleteStack(string request, ulong stackId, int quantity) {
        var stack = FindItem(BankContainer.SharedBank, stackId);
        if (!bankIsOpen()) {
            Deny(request, stackId, "the bank is not open");

            return false;
        }

        if (stack is null) {
            ReportUnknown(request, stackId);

            return false;
        }

        if (stack is not (ClientReagentItem or ClientPetSnackItem)) {
            Deny(request, stackId, "the item is not a stack");

            return false;
        }

        var result = BankCollection.TryDeleteShared(wizard, stackId, quantity);
        if (!result.Success) {
            Deny(request, stackId, $"{result.Denial}: {result.Reason}");

            return false;
        }

        if (result.Source is null) {
            session.Tell(BankSync.StorageRemove(wizard, stackId, shared: true));

            return true;
        }

        Send(BankSync.StorageAdd(wizard, result.Source, shared: true));

        return true;
    }

    /// <summary>
    /// Sends the bank items to a window that asked for them, such as the bazaar, and then the end marker.
    /// </summary>
    /// <param name="endMarker">The message the window asked with, now empty, sent after the stream.</param>
    internal void StreamBankFor(IMessage endMarker) {
        BankSync.StreamBank(session, wizard, includeShared: false);
        session.Tell(endMarker);
    }

    /// <summary>
    /// Answers a bank count request with the number of items that count against the bank capacity.
    /// </summary>
    internal void SendBankCount()
        => session.Tell(new WIZARD2_53_PROTOCOL.MSG_BANKCOUNT { BankCount = (uint) wizard.StorageBehavior.CountRegularItems() });

    private WizClientObjectItem CheckMove(string request, ulong itemId, BankContainer from, BankContainer to, int quantity) {
        if (!bankIsOpen()) {
            Deny(request, itemId, "the bank is not open");

            return null;
        }

        var item = FindItem(from, itemId);
        if (item is null) {
            ReportUnknown(request, itemId);

            return null;
        }

        // The result of a move is told to the client after it happened, so an item the client could not be told about stops here.
        if (!ItemSync.TrySerialize(item, out _)) {
            Deny(request, itemId, "the item could not be serialized for the client");

            return null;
        }

        var denial = BankRules.CanMove(from, to, item, quantity, wizard, wizard.Account);
        if (denial != BankDenial.None) {
            Deny(request, itemId, denial.ToString());

            return null;
        }

        return item;
    }

    private bool EnsureUnequipped(string request, ulong itemId) {
        if (!wizard.EquipmentBehavior.HasItemEquipped(itemId)) {
            return true;
        }

        // The worn item takes a backpack slot when it comes off.
        if (wizard.InventoryBehavior.FreeSlots < 1) {
            Deny(request, itemId, $"{BankDenial.BackpackFull}: a worn item needs a free backpack slot to be taken off");

            return false;
        }

        if (!unequip(itemId) || wizard.EquipmentBehavior.HasItemEquipped(itemId)) {
            Deny(request, itemId, "the item could not be taken off");

            return false;
        }

        return true;
    }

    private WizClientObjectItem FindItem(BankContainer container, ulong itemId) {
        switch (container) {
            case BankContainer.Backpack:
                return wizard.InventoryBehavior.GetItem(itemId)
                    ?? wizard.EquipmentBehavior.GetItem(itemId)
                    ?? wizard.AlchemyBehavior?.Reagents?.FirstOrDefault(stack => stack.m_globalID == itemId)
                    ?? (WizClientObjectItem) wizard.PetSnackBehavior?.Snacks?.FirstOrDefault(stack => stack.m_globalID == itemId);
            case BankContainer.Bank:
                return wizard.StorageBehavior.GetItem(itemId);
            default:
                var shared = wizard.Account?.SharedBank;
                if (shared is null) {
                    return null;
                }

                return shared.Items.FirstOrDefault(item => item.m_globalID == itemId)
                    ?? shared.Reagents.FirstOrDefault(stack => stack.m_globalID == itemId)
                    ?? (WizClientObjectItem) shared.Snacks.FirstOrDefault(stack => stack.m_globalID == itemId);
        }
    }

    private bool IsKnown(ulong itemId)
        => FindItem(BankContainer.Backpack, itemId) is not null
            || FindItem(BankContainer.Bank, itemId) is not null
            || FindItem(BankContainer.SharedBank, itemId) is not null;

    private void ReportUnknown(string request, ulong itemId) {
        // An item in another container is a stale window or a repeated click. One that is nowhere cannot come from a client that is in step.
        if (IsKnown(itemId)) {
            Deny(request, itemId, "the item is not in the container the request names");

            return;
        }

        var infractionText = $"Player sent the bank request '{request}' for item {itemId}, which they do not have!";
        wizard.Account.AddInfraction(InfractionType.SuspiciousBehavior, infractionText);
        Logger.Warning("[Bank] {Infraction} This has been logged as a suspicious behavior infraction.", Logger.Args(infractionText));
    }

    private void Send(IMessage message) {
        // A storage add the item could not be built for is already logged.
        if (message is not null) {
            session.Tell(message);
        }
    }

    private void Deny(string request, ulong itemId, string reason)
        => Logger.Warning("[Bank] Refused the {Request} for {CharId}: {Reason} (item {ItemId})", Logger.Args(request, wizard.CharId, reason, itemId));

}
