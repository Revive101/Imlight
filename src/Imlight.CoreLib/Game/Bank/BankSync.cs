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
 * BANK SYNC
 * ========================================================================
 *
 * PURPOSE:
 * Builds the messages that tell a client what its bank holds: the items of the bank and the shared bank,
 * the confirms of a move or a delete, the overflow notices and the new capacity. It also streams the whole
 * bank to a client.
 *
 * USAGE EXAMPLE:
 * session.Tell(BankSync.StorageAdd(wizard, item, shared: false));
 * session.Tell(BankSync.InvToBankConfirm(requestId, itemId, itemId, useShared: false));
 *
 * NOTE:
 * The client orders matter, see BankRequests: the destination add, the source remove, then the confirm.
 * A failed delete confirm names item 0, because the client removes the row it is given whatever the failure
 * says. The failure of the bank to bank confirm is read as its low byte only, so it is always 1. Items of the
 * backpack are not told here, ItemSync does that for items and the stack senders below do it for reagents and
 * pet snacks.
 *
 * TODO:
 * Does the client read a storage add at the same property mask as an inventory add? Both have the same layout in the captures.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Linq;
using Akka.Actor;
using Imcodec.CoreObject;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Bank;

internal static class BankSync {

    private const uint REAGENT_ADD_SERIALIZATION_FLAGS = 27;
    private const uint SNACK_ADD_SERIALIZATION_FLAGS = 24;
    private const sbyte FAILED = 1;
    private const sbyte SUCCEEDED = 0;
    private const uint FAILED_ERROR = 1;
    private static readonly CoreObjectSerializer s_stackSerializer = new(behaviors: SerializerFlags.None);

    /// <summary>
    /// Builds the message that puts an item into the bank list or the shared bank list of the client.
    /// </summary>
    /// <param name="wizard">The wizard whose client is told.</param>
    /// <param name="item">The item, reagent stack or pet snack stack.</param>
    /// <param name="shared">True for the shared bank list, false for the bank list.</param>
    /// <returns>The message, or null when the item could not be serialized.</returns>
    internal static WIZARD_12_PROTOCOL.MSG_STORAGECLIENTADD StorageAdd(Wizard wizard, WizClientObjectItem item, bool shared) {
        if (!ItemSync.TrySerialize(item, out var serializedItem)) {
            Logger.Error("Failed to serialize item {ItemId} for the storage add message.", Logger.Args(item.m_globalID.Full));

            return null;
        }

        return new WIZARD_12_PROTOCOL.MSG_STORAGECLIENTADD {
            GlobalID = wizard.GameObjectID,
            SerializedItem = serializedItem,
            SharedBank = ListNumber(shared),
        };
    }

    /// <summary>
    /// Builds the message that takes an item out of the bank list or the shared bank list of the client.
    /// </summary>
    /// <param name="wizard">The wizard whose client is told.</param>
    /// <param name="itemId">The global id of the item or stack.</param>
    /// <param name="shared">True for the shared bank list, false for the bank list.</param>
    /// <returns>The message.</returns>
    internal static WIZARD_12_PROTOCOL.MSG_STORAGECLIENTREMOVE StorageRemove(Wizard wizard, ulong itemId, bool shared)
        => new() {
            GlobalID = wizard.GameObjectID,
            SharedBank = ListNumber(shared),
            ItemID = itemId,
        };

    /// <summary>
    /// Builds the reply to a move from the backpack into the bank or the shared bank.
    /// </summary>
    /// <param name="requestId">The request id of the move, which the client does not read.</param>
    /// <param name="originalId">The id of the item or stack in the backpack.</param>
    /// <param name="newId">The id of the item or stack at the destination.</param>
    /// <param name="useShared">True when the destination is the shared bank. The client reads it as the destination.</param>
    /// <returns>The confirm of a move that happened.</returns>
    internal static WIZARD_12_PROTOCOL.MSG_INVTOBANKCONFIRM InvToBankConfirm(int requestId, ulong originalId, ulong newId, bool useShared)
        => new() {
            ClientRequestID = requestId,
            OriginalGlobalID = originalId,
            GlobalID = newId,
            UseShared = ListNumber(useShared),
            Failure = SUCCEEDED,
        };

    /// <summary>
    /// Builds the reply to a move from the backpack that did not happen.
    /// </summary>
    /// <param name="requestId">The request id of the move.</param>
    /// <param name="requestedId">The id the client asked to move.</param>
    /// <param name="useShared">True when the client asked for the shared bank.</param>
    /// <returns>The confirm with the failure set.</returns>
    internal static WIZARD_12_PROTOCOL.MSG_INVTOBANKCONFIRM InvToBankFailed(int requestId, ulong requestedId, bool useShared)
        => new() {
            ClientRequestID = requestId,
            OriginalGlobalID = requestedId,
            GlobalID = 0,
            UseShared = ListNumber(useShared),
            Failure = FAILED,
        };

    /// <summary>
    /// Builds the reply to a move from the bank or the shared bank into the backpack.
    /// </summary>
    /// <param name="requestId">The request id of the move.</param>
    /// <param name="originalId">The id of the item or stack in the bank.</param>
    /// <param name="newId">The id of the item or stack in the backpack.</param>
    /// <returns>The confirm of a move that happened. It has no list number, the client takes the source from the open tab.</returns>
    internal static WIZARD_12_PROTOCOL.MSG_BANKTOINVCONFIRM BankToInvConfirm(int requestId, ulong originalId, ulong newId)
        => new() {
            ClientRequestID = requestId,
            OriginalGlobalID = originalId,
            GlobalID = newId,
            Failure = SUCCEEDED,
        };

    /// <summary>
    /// Builds the reply to a move into the backpack that did not happen.
    /// </summary>
    /// <param name="requestId">The request id of the move.</param>
    /// <param name="requestedId">The id the client asked to move.</param>
    /// <returns>The confirm with the failure set.</returns>
    internal static WIZARD_12_PROTOCOL.MSG_BANKTOINVCONFIRM BankToInvFailed(int requestId, ulong requestedId)
        => new() {
            ClientRequestID = requestId,
            OriginalGlobalID = requestedId,
            GlobalID = 0,
            Failure = FAILED,
        };

    /// <summary>
    /// Builds the reply to a move between the bank and the shared bank.
    /// </summary>
    /// <param name="requestId">The request id of the move.</param>
    /// <param name="originalId">The id of the item in the list it left.</param>
    /// <param name="newId">The id of the item in the list it entered.</param>
    /// <param name="useShared">True when the destination is the shared bank. The client reads it as the destination.</param>
    /// <returns>The confirm of a move that happened.</returns>
    internal static WIZARD_12_PROTOCOL.MSG_BANKTOBANKCONFIRM BankToBankConfirm(int requestId, ulong originalId, ulong newId, bool useShared)
        => new() {
            ClientRequestID = requestId,
            OriginalGlobalID = originalId,
            GlobalID = newId,
            UseShared = ListNumber(useShared),
            Error = 0,
        };

    /// <summary>
    /// Builds the reply to a move between the bank and the shared bank that did not happen.
    /// </summary>
    /// <param name="requestId">The request id of the move.</param>
    /// <param name="requestedId">The id the client asked to move.</param>
    /// <param name="useShared">True when the client asked for the shared bank as the destination.</param>
    /// <returns>The confirm with the error set. The client reads only the low byte of it, so it is 1.</returns>
    internal static WIZARD_12_PROTOCOL.MSG_BANKTOBANKCONFIRM BankToBankFailed(int requestId, ulong requestedId, bool useShared)
        => new() {
            ClientRequestID = requestId,
            OriginalGlobalID = requestedId,
            GlobalID = 0,
            UseShared = ListNumber(useShared),
            Error = FAILED_ERROR,
        };

    /// <summary>
    /// Builds the reply to a delete that happened.
    /// </summary>
    /// <param name="requestId">The request id of the delete.</param>
    /// <param name="itemId">The id of the deleted item.</param>
    /// <param name="useShared">True when the item was in the shared bank.</param>
    /// <returns>The confirm. The client removes the row of the item from the open tab.</returns>
    internal static WIZARD_12_PROTOCOL.MSG_BANKDELETECONFIRM BankDeleteConfirm(int requestId, ulong itemId, bool useShared)
        => new() {
            ClientRequestID = requestId,
            GlobalID = itemId,
            UseShared = ListNumber(useShared),
            Failure = SUCCEEDED,
        };

    /// <summary>
    /// Builds the reply to a delete that did not happen.
    /// </summary>
    /// <param name="requestId">The request id of the delete.</param>
    /// <param name="useShared">True when the client asked for the shared bank.</param>
    /// <returns>The confirm with the failure set and item 0. The client removes the row it is given even for a failure, and skips item 0.</returns>
    internal static WIZARD_12_PROTOCOL.MSG_BANKDELETECONFIRM BankDeleteFailed(int requestId, bool useShared)
        => new() {
            ClientRequestID = requestId,
            GlobalID = 0,
            UseShared = ListNumber(useShared),
            Failure = FAILED,
        };

    /// <summary>
    /// Builds the reply to the delete of a shared reagent or pet snack stack.
    /// </summary>
    /// <param name="requestId">The request id of the delete.</param>
    /// <param name="stackId">The id of the stack, 0 for a delete that did not happen.</param>
    /// <param name="failed">Whether the delete did not happen.</param>
    /// <returns>The confirm.</returns>
    internal static WIZARD_12_PROTOCOL.MSG_SHAREDBANKDELETEREAGENTORPETSNACKCONFIRM SharedStackDeleteConfirm(int requestId, ulong stackId, bool failed)
        => new() {
            ClientRequestID = requestId,
            GlobalID = failed ? 0 : stackId,
            Failure = failed ? FAILED : SUCCEEDED,
        };

    /// <summary>
    /// Builds the notice that an item went to the bank because the backpack was full.
    /// </summary>
    /// <param name="itemId">The global id of the item.</param>
    /// <param name="countInBank">How many items the bank holds now.</param>
    /// <returns>The message, sent right after the storage add of the item.</returns>
    internal static WIZARD_12_PROTOCOL.MSG_ITEMOVERFLOWTOBANK ItemOverflowToBank(ulong itemId, int countInBank)
        => new() {
            ItemID = itemId,
            CountInBank = countInBank,
        };

    /// <summary>
    /// Builds the notice that an item was lost because the backpack and the bank were full.
    /// </summary>
    /// <param name="templateId">The template of the lost item.</param>
    /// <param name="errorId">The error hash. Only the reagent drop error has a meaning to the client.</param>
    /// <returns>The message.</returns>
    internal static WIZARD_12_PROTOCOL.MSG_ITEMDROP ItemDrop(ulong templateId, uint errorId = 0)
        => new() {
            TemplateID = templateId,
            ErrorID = errorId,
        };

    /// <summary>
    /// Builds the message that gives the client the new capacity of the bank.
    /// </summary>
    /// <param name="wizard">The wizard whose client is told.</param>
    /// <returns>The message. No message exists for the capacity of the shared bank.</returns>
    internal static WIZARD2_53_PROTOCOL.MSG_UPDATEBANKLIMIT UpdateBankLimit(Wizard wizard)
        => new() {
            GlobalID = wizard.GameObjectID,
            BankLimit = wizard.StorageBehavior.Limit,
        };

    /// <summary>
    /// Sends every item of the bank to the client, and with it the items and stacks of the shared bank.
    /// </summary>
    /// <param name="session">The SessionActor reference of the wizard's session.</param>
    /// <param name="wizard">The wizard whose bank is sent.</param>
    /// <param name="includeShared">Whether the shared bank follows the bank. The caller reloads it first.</param>
    /// <returns>False when an item could not be serialized and was left out.</returns>
    internal static bool StreamBank(IActorRef session, Wizard wizard, bool includeShared) {
        var complete = true;
        foreach (var item in wizard.StorageBehavior.Items.ToList()) {
            complete &= Send(session, wizard, item, shared: false);
        }

        if (!includeShared || wizard.Account?.SharedBank is not { } sharedBank) {
            return complete;
        }

        foreach (var item in sharedBank.Items.ToList()) {
            complete &= Send(session, wizard, item, shared: true);
        }

        foreach (var stack in sharedBank.Reagents.ToList()) {
            complete &= Send(session, wizard, stack, shared: true);
        }

        foreach (var stack in sharedBank.Snacks.ToList()) {
            complete &= Send(session, wizard, stack, shared: true);
        }

        return complete;
    }

    /// <summary>
    /// Tells the client that a reagent or pet snack stack entered the backpack, with its quantity.
    /// </summary>
    /// <param name="session">The SessionActor reference of the wizard's session.</param>
    /// <param name="wizard">The wizard whose client is told.</param>
    /// <param name="stack">The stack as it is now.</param>
    /// <returns>False if the stack could not be serialized, in which case nothing was sent.</returns>
    internal static bool SendBackpackStackAdded(IActorRef session, Wizard wizard, WizClientObjectItem stack) {
        var flags = stack is ClientReagentItem ? REAGENT_ADD_SERIALIZATION_FLAGS : SNACK_ADD_SERIALIZATION_FLAGS;
        if (!s_stackSerializer.Serialize(stack, flags, out var data)) {
            Logger.Error("Failed to serialize stack {ItemId} for the backpack add message.", Logger.Args(stack.m_globalID.Full));

            return false;
        }

        if (stack is ClientReagentItem reagent) {
            session.Tell(new WIZARD_12_PROTOCOL.MSG_REAGENTADD { GlobalID = wizard.GameObjectID, Data = data });
            session.Tell(new WIZARD_12_PROTOCOL.MSG_REAGENTUPDATE { GlobalID = wizard.GameObjectID, ItemID = reagent.m_globalID, Quantity = reagent.m_quantity });
        } else if (stack is ClientPetSnackItem snack) {
            session.Tell(new PET_9_PROTOCOL.MSG_PETSNACKADD { GlobalID = wizard.GameObjectID, Data = data });
            session.Tell(new PET_9_PROTOCOL.MSG_PETSNACKUPDATE { GlobalID = wizard.GameObjectID, ItemID = snack.m_globalID, Quantity = snack.m_quantity });
        }

        return true;
    }

    /// <summary>
    /// Tells the client how many units a backpack reagent or pet snack stack has left.
    /// </summary>
    /// <param name="session">The SessionActor reference of the wizard's session.</param>
    /// <param name="wizard">The wizard whose client is told.</param>
    /// <param name="stack">The stack as it is now.</param>
    internal static void SendBackpackStackChanged(IActorRef session, Wizard wizard, WizClientObjectItem stack) {
        if (stack is ClientReagentItem reagent) {
            session.Tell(new WIZARD_12_PROTOCOL.MSG_REAGENTUPDATE { GlobalID = wizard.GameObjectID, ItemID = reagent.m_globalID, Quantity = reagent.m_quantity });
        } else if (stack is ClientPetSnackItem snack) {
            session.Tell(new PET_9_PROTOCOL.MSG_PETSNACKUPDATE { GlobalID = wizard.GameObjectID, ItemID = snack.m_globalID, Quantity = snack.m_quantity });
        }
    }

    /// <summary>
    /// Tells the client that a reagent or pet snack stack left the backpack.
    /// </summary>
    /// <param name="session">The SessionActor reference of the wizard's session.</param>
    /// <param name="wizard">The wizard whose client is told.</param>
    /// <param name="stackId">The id of the stack.</param>
    /// <param name="kind">Reagent or PetSnack.</param>
    internal static void SendBackpackStackRemoved(IActorRef session, Wizard wizard, ulong stackId, BankItemKind kind) {
        if (kind == BankItemKind.Reagent) {
            session.Tell(new WIZARD_12_PROTOCOL.MSG_REAGENTREMOVE { GlobalID = wizard.GameObjectID, ItemID = stackId });
        } else if (kind == BankItemKind.PetSnack) {
            session.Tell(new PET_9_PROTOCOL.MSG_PETSNACKREMOVE { GlobalID = wizard.GameObjectID, ItemID = stackId });
        }
    }

    private static sbyte ListNumber(bool shared) => (sbyte) (shared ? 1 : 0);

    private static bool Send(IActorRef session, Wizard wizard, WizClientObjectItem item, bool shared) {
        var message = StorageAdd(wizard, item, shared);
        if (message is null) {
            return false;
        }

        session.Tell(message);

        return true;
    }

}
