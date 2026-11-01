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
 * BANK COLLECTION
 * ========================================================================
 *
 * PURPOSE:
 * Moves, adds and deletes the items of the shared bank, and deletes the items of a character's own backpack and bank.
 * Each call is one Raven transaction over the account, the character and the item documents involved, so an item
 * or a stack is in exactly one place afterwards.
 *
 * USAGE EXAMPLE:
 * var result = BankCollection.TryMoveShared(wizard, new SharedMove(BankContainer.Backpack, BankContainer.SharedBank, itemId, 1));
 * if (result.Success) { ... tell the client ... }
 *
 * NOTE:
 * The shared bank belongs to the account, so two characters of it can be online at once. A call takes the
 * account lane, then the character lane, and checks the request against the stored documents, not against
 * the memory of its own session. After the commit it brings the wizard and the shared bank of its own session
 * in line with what it stored; the caller only tells the client. A move that crosses owners changes the owner
 * of the item document and keeps its id, a stack move adds to the stack of that template at the destination.
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
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.Bank;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace Imlight.CoreLib.WizardData.Collections;

/// <summary>
/// A move between the shared bank and the backpack or the bank of a character.
/// </summary>
/// <param name="From">Where the item is.</param>
/// <param name="To">Where it should go.</param>
/// <param name="ItemId">The global id of the item or the stack in the source container.</param>
/// <param name="Quantity">1 for an item, 1 up to the stack size for a reagent or pet snack stack.</param>
internal readonly record struct SharedMove(BankContainer From, BankContainer To, ulong ItemId, int Quantity);

/// <summary>
/// What a shared bank call did.
/// </summary>
/// <param name="Denial">None when it was done, otherwise why it was not.</param>
/// <param name="Reason">What the log says for a denial. Empty on success.</param>
/// <param name="Kind">Whether the call was about an item, a jewel, a reagent stack or a pet snack stack.</param>
/// <param name="OriginalId">The id the client knows the source by.</param>
/// <param name="NewId">The id of the item or stack at the destination, 0 for a delete.</param>
/// <param name="Source">The stack left in the source container, null when it is gone and for items.</param>
/// <param name="Destination">The item or stack as it is now at the destination, null for a delete.</param>
internal sealed record SharedMoveResult(BankDenial Denial, string Reason, BankItemKind Kind, ulong OriginalId, ulong NewId,
                                        WizClientObjectItem Source, WizClientObjectItem Destination) {

    internal bool Success => Denial == BankDenial.None;

}

internal static class BankCollection {

    private static readonly StackAccess<ClientReagentItem> s_reagents = new() {
        CollectionName = WizardReagentCollection.CollectionName,
        MaxStack = ServerAlchemyBehavior.MaxStack,
        SharedIds = bank => bank.ReagentIds,
        GetShared = bank => bank.Reagents,
        SetShared = (bank, stacks) => bank.Reagents = stacks,
        BackpackIds = wizard => (wizard.AlchemyBehavior ??= new ServerAlchemyBehavior()).ReagentItemIds ??= [],
        BackpackStacks = wizard => (wizard.AlchemyBehavior ??= new ServerAlchemyBehavior()).Reagents ??= [],
        GetQuantity = stack => stack.m_quantity,
        SetQuantity = (stack, quantity) => stack.m_quantity = quantity,
        CreateLike = source => {
            var created = CoreObjectFactory.FinalizeCoreObject(source.m_templateID) as ClientReagentItem;
            if (created is not null) {
                created.m_displayKey = source.m_displayKey;
            }

            return created;
        },
    };

    private static readonly StackAccess<ClientPetSnackItem> s_snacks = new() {
        CollectionName = WizardPetSnackCollection.CollectionName,
        MaxStack = ServerPetSnackBehavior.MaxStack,
        SharedIds = bank => bank.SnackIds,
        GetShared = bank => bank.Snacks,
        SetShared = (bank, stacks) => bank.Snacks = stacks,
        BackpackIds = wizard => (wizard.PetSnackBehavior ??= new ServerPetSnackBehavior()).SnackItemIds ??= [],
        BackpackStacks = wizard => (wizard.PetSnackBehavior ??= new ServerPetSnackBehavior()).Snacks ??= [],
        GetQuantity = stack => stack.m_quantity,
        SetQuantity = (stack, quantity) => stack.m_quantity = quantity,
        CreateLike = source => {
            var created = CoreObjectFactory.FinalizeCoreObject(source.m_templateID) as ClientPetSnackItem;
            if (created is not null) {
                CoreObjectFactory.InitializeCoreObjectBehaviors(created, created.m_templateID);
            }

            return created;
        },
    };

    /// <summary>
    /// Moves an item, or part of a stack, between the shared bank and the backpack or the bank of the wizard.
    /// </summary>
    /// <param name="wizard">The wizard that moves. Its account owns the shared bank.</param>
    /// <param name="move">What to move and where.</param>
    /// <returns>The result. On success the wizard and its shared bank are updated; on a denial nothing changed.</returns>
    internal static SharedMoveResult TryMoveShared(Wizard wizard, SharedMove move) {
        var account = wizard.Account;
        if (account is null || move.From == move.To
            || (move.From != BankContainer.SharedBank && move.To != BankContainer.SharedBank)) {
            return Denied(move, BankDenial.WrongContainer, "a shared bank move needs an account and the shared bank on exactly one side");
        }

        account.SharedBank ??= new AccountSharedBank();
        var failure = RunPlan(account.AccountId, wizard.CharId, move,
            (session, storedAccount, storedWizard) => PlanMove(session, wizard, storedAccount, storedWizard, move), out var state);
        if (failure is not null) {
            return failure;
        }

        return MirrorMove(wizard, state);
    }

    /// <summary>
    /// Deletes an item or a quantity of a stack from the shared bank for good.
    /// </summary>
    /// <param name="wizard">The wizard that deletes. Its account owns the shared bank.</param>
    /// <param name="itemId">The global id of the item or the stack.</param>
    /// <param name="quantity">1 for an item, 1 up to the stack size for a stack.</param>
    /// <returns>The result. On success the shared bank of the wizard's account is updated.</returns>
    internal static SharedMoveResult TryDeleteShared(Wizard wizard, ulong itemId, int quantity) {
        var move = new SharedMove(BankContainer.SharedBank, BankContainer.SharedBank, itemId, quantity);
        var account = wizard.Account;
        if (account is null) {
            return Denied(move, BankDenial.WrongContainer, "the wizard has no account");
        }

        account.SharedBank ??= new AccountSharedBank();
        var failure = RunPlan(account.AccountId, wizard.CharId, move,
            (session, storedAccount, _) => PlanDelete(session, storedAccount, move), out var state);
        if (failure is not null) {
            return failure;
        }

        AdoptSharedBank(account.SharedBank, state);
        var remaining = FindShared(account.SharedBank, state.Kind, itemId);

        return new SharedMoveResult(BankDenial.None, string.Empty, state.Kind, itemId, 0, remaining, null);
    }

    /// <summary>
    /// Puts a new item, one that is stored nowhere yet, into the shared bank. The item document and the shared
    /// bank's id list are written in one Raven transaction, checked against the stored shared bank like a move.
    /// </summary>
    /// <param name="wizard">The wizard that received the item. Its account owns the shared bank.</param>
    /// <param name="item">The new item, not a stack. Its owner becomes the account.</param>
    /// <returns>The result. On success the shared bank of the wizard's account is updated; on a denial nothing was stored.</returns>
    internal static SharedMoveResult TryAddShared(Wizard wizard, WizClientObjectItem item) {
        var move = new SharedMove(BankContainer.Backpack, BankContainer.SharedBank, item.m_globalID, 1);
        var account = wizard.Account;
        if (account is null) {
            return Denied(move, BankDenial.WrongContainer, "the wizard has no account");
        }

        account.SharedBank ??= new AccountSharedBank();
        var owner = item.m_characterId;
        var failure = RunPlan(account.AccountId, wizard.CharId, move,
            (session, storedAccount, _) => PlanAdd(session, wizard, storedAccount, item, move), out var state);
        if (failure is not null) {
            item.m_characterId = owner;

            return failure;
        }

        AdoptSharedBank(account.SharedBank, state);

        return new SharedMoveResult(BankDenial.None, string.Empty, state.Kind, item.m_globalID, item.m_globalID, null, item);
    }

    /// <summary>
    /// Deletes an item of the wizard's own backpack or bank for good. The id leaves its list and the item document
    /// goes in one Raven transaction. Reagent and pet snack stacks are not deleted here.
    /// </summary>
    /// <param name="wizard">The wizard that deletes.</param>
    /// <param name="container">Backpack or Bank.</param>
    /// <param name="itemId">The global id of the item.</param>
    /// <returns>The result. On success the wizard's list is updated; on a denial nothing changed.</returns>
    internal static SharedMoveResult TryDeleteOwn(Wizard wizard, BankContainer container, ulong itemId) {
        var move = new SharedMove(container, container, itemId, 1);
        if (container == BankContainer.SharedBank) {
            return Denied(move, BankDenial.WrongContainer, "the shared bank is deleted from with TryDeleteShared");
        }

        var denial = BankDenial.None;
        var reason = string.Empty;
        try {
            var stored = WizardCollection.UpdateCharacterWithSession(wizard.CharId, (session, storedWizard) => {
                var ids = container == BankContainer.Backpack ? storedWizard.InventoryBehavior?.InventoryItemIds : storedWizard.StorageBehavior?.BankItemIds;
                if (ids is null || !ids.Contains(itemId)) {
                    denial = BankDenial.UnknownItem;
                    reason = $"item {itemId} is not in {container}";

                    return false;
                }

                var item = FindDocument<WizClientObjectItem>(session, WizardItemCollection.CollectionName, wizard.CharId, itemId);
                if (item is null) {
                    denial = BankDenial.UnknownItem;
                    reason = $"item {itemId} is listed in {container} but has no document";

                    return false;
                }

                denial = BankRules.CanDelete(item);
                if (denial != BankDenial.None) {
                    reason = $"item {itemId} may not be deleted: {denial}";

                    return false;
                }

                session.Delete(item);
                ids.Remove(itemId);

                return true;
            });
            if (!stored) {
                return denial != BankDenial.None
                    ? Denied(move, denial, reason)
                    : Denied(move, BankDenial.SaveFailed, "the character is not stored");
            }
        } catch (Exception ex) {
            Logger.Error("Failed to delete item {ItemId} of character {CharId}: {Reason}", Logger.Args(itemId, wizard.CharId, ex.Message));

            return Denied(move, BankDenial.SaveFailed, ex.Message);
        }

        if (container == BankContainer.Backpack) {
            wizard.InventoryBehavior.RemoveItem(itemId, out _);
        } else {
            wizard.StorageBehavior.RemoveItem(itemId, out _);
        }

        return new SharedMoveResult(BankDenial.None, string.Empty, BankItemKind.Item, itemId, 0, null, null);
    }

    private static SharedMoveResult RunPlan(ulong accountId, ulong charId, SharedMove move,
                                            Func<IDocumentSession, Account, Wizard, MoveState> plan, out MoveState state) {
        MoveState planned = null;
        try {
            var stored = AccountCollection.UpdateAccountAndCharacterWithSession(accountId, charId, (session, storedAccount, storedWizard) => {
                planned = plan(session, storedAccount, storedWizard);

                return planned.Denial == BankDenial.None;
            });
            state = planned;
            if (stored) {
                return null;
            }

            return planned is { Denial: not BankDenial.None }
                ? Denied(move, planned.Denial, planned.Reason)
                : Denied(move, BankDenial.SaveFailed, "the account or the character is not stored");
        } catch (Exception ex) {
            state = planned;
            Logger.Error("Failed to store the shared bank request of item {ItemId} for character {CharId}: {Reason}",
                Logger.Args(move.ItemId, charId, ex.Message));

            return Denied(move, BankDenial.SaveFailed, ex.Message);
        }
    }

    private static MoveState PlanMove(IDocumentSession session, Wizard wizard, Account storedAccount, Wizard storedWizard, SharedMove move) {
        var state = new MoveState(move);
        var shared = storedAccount.SharedBank ??= new AccountSharedBank();
        state.StoredShared = shared;
        var kind = FindKind(move.From, move.ItemId, shared, storedWizard);
        if (kind is null) {
            return state.Deny(BankDenial.UnknownItem, $"item {move.ItemId} is not in {move.From}");
        }

        state.Kind = kind.Value;

        return kind.Value switch {
            BankItemKind.Reagent => PlanStackMove(session, wizard, storedAccount, storedWizard, state, s_reagents),
            BankItemKind.PetSnack => PlanStackMove(session, wizard, storedAccount, storedWizard, state, s_snacks),
            _ => PlanItemMove(session, wizard, storedAccount, storedWizard, state),
        };
    }

    private static MoveState PlanAdd(IDocumentSession session, Wizard wizard, Account storedAccount, WizClientObjectItem item, SharedMove move) {
        var state = new MoveState(move);
        var shared = storedAccount.SharedBank ??= new AccountSharedBank();
        state.StoredShared = shared;
        state.Kind = BankRules.Classify(item, CoreObjectFactory.GetCoreTemplate(item.m_templateID) as WizItemTemplate);
        if (state.Kind is BankItemKind.Reagent or BankItemKind.PetSnack) {
            return state.Deny(BankDenial.WrongContainer, $"stack {move.ItemId} is not added to the shared bank from nothing");
        }

        if (shared.ItemIds.Contains(move.ItemId)) {
            return state.Deny(BankDenial.UnknownItem, $"item {move.ItemId} is in the shared bank already");
        }

        shared.Items = AccountCollection.LoadSharedDocuments<WizClientObjectItem>(session, WizardItemCollection.CollectionName, storedAccount.AccountId, shared.ItemIds);
        var denial = BankRules.CanMove(move.From, move.To, item, 1, wizard, storedAccount);
        if (denial != BankDenial.None) {
            return state.Deny(denial, $"item {move.ItemId} may not be added to the shared bank: {denial}");
        }

        item.m_characterId = (GID) storedAccount.AccountId;
        WizardItemCollection.StoreInSession(session, item);
        shared.ItemIds.Add(move.ItemId);
        shared.Items.Add(item);
        state.DestinationDoc = item;
        state.OriginalId = move.ItemId;
        state.NewId = move.ItemId;
        state.DestinationIsNew = true;

        return state;
    }

    private static MoveState PlanItemMove(IDocumentSession session, Wizard wizard, Account storedAccount, Wizard storedWizard, MoveState state) {
        var move = state.Move;
        var shared = storedAccount.SharedBank;
        var sourceOwner = OwnerOf(move.From, wizard, storedAccount);
        var destinationOwner = OwnerOf(move.To, wizard, storedAccount);

        var item = FindDocument<WizClientObjectItem>(session, WizardItemCollection.CollectionName, sourceOwner, move.ItemId);
        if (item is null) {
            return state.Deny(BankDenial.UnknownItem, $"item {move.ItemId} is listed in {move.From} but has no document");
        }

        shared.Items = AccountCollection.LoadSharedDocuments<WizClientObjectItem>(session, WizardItemCollection.CollectionName, storedAccount.AccountId, shared.ItemIds);
        var denial = BankRules.CanMove(move.From, move.To, item, move.Quantity, wizard, storedAccount);
        if (denial != BankDenial.None) {
            return state.Deny(denial, $"item {move.ItemId} may not move from {move.From} to {move.To}: {denial}");
        }

        state.Kind = BankRules.Classify(item, CoreObjectFactory.GetCoreTemplate(item.m_templateID) as WizItemTemplate);
        var sourceIds = IdsOf(move.From, BankItemKind.Item, storedAccount, storedWizard);
        var destinationIds = IdsOf(move.To, BankItemKind.Item, storedAccount, storedWizard);
        if (destinationIds.Contains(move.ItemId) || !sourceIds.Remove(move.ItemId)) {
            return state.Deny(BankDenial.UnknownItem, $"the lists of {move.From} and {move.To} disagree about item {move.ItemId}");
        }

        destinationIds.Add(move.ItemId);
        item.m_characterId = (GID) destinationOwner;
        if (move.To == BankContainer.SharedBank) {
            shared.Items.Add(item);
        } else {
            shared.Items.Remove(item);
        }

        state.SourceDoc = null;
        state.DestinationDoc = item;
        state.OriginalId = move.ItemId;
        state.NewId = move.ItemId;
        state.DestinationIsNew = true;

        return state;
    }

    private static MoveState PlanStackMove<T>(IDocumentSession session, Wizard wizard, Account storedAccount, Wizard storedWizard,
                                              MoveState state, StackAccess<T> access) where T : WizClientObjectItem {
        var move = state.Move;
        var shared = storedAccount.SharedBank;
        var sourceOwner = OwnerOf(move.From, wizard, storedAccount);
        var destinationOwner = OwnerOf(move.To, wizard, storedAccount);

        // Every query comes first: the documents are changed only when the rules agree.
        var source = FindDocument<T>(session, access.CollectionName, sourceOwner, move.ItemId);
        if (source is null) {
            return state.Deny(BankDenial.UnknownItem, $"stack {move.ItemId} is listed in {move.From} but has no document");
        }

        var sharedStacks = AccountCollection.LoadSharedDocuments<T>(session, access.CollectionName, storedAccount.AccountId, access.SharedIds(shared));
        access.SetShared(shared, sharedStacks);
        var backpackIds = access.BackpackIds(storedWizard);
        T destination = move.To == BankContainer.SharedBank
            ? sharedStacks.FirstOrDefault(stack => stack.m_templateID == source.m_templateID)
            : FindStackByTemplate<T>(session, access.CollectionName, wizard.CharId, source.m_templateID, backpackIds);

        var denial = BankRules.CanMove(move.From, move.To, source, move.Quantity, wizard, storedAccount);
        if (denial == BankDenial.None && destination is not null
            && access.GetQuantity(destination) + move.Quantity > access.MaxStack) {
            denial = move.To == BankContainer.SharedBank ? BankDenial.SharedBankFull : BankDenial.BackpackFull;
        }

        if (denial != BankDenial.None) {
            return state.Deny(denial, $"stack {move.ItemId} may not move from {move.From} to {move.To}: {denial}");
        }

        var sourceIds = IdsOf(move.From, state.Kind, storedAccount, storedWizard);
        var destinationIds = IdsOf(move.To, state.Kind, storedAccount, storedWizard);
        var sourceQuantity = access.GetQuantity(source);
        var wholeStack = move.Quantity == sourceQuantity;
        state.OriginalId = move.ItemId;
        state.SourceDoc = source;

        if (destination is not null) {
            MergeIntoStack(session, state, access, source, destination, sourceIds, sharedStacks);
        } else if (wholeStack) {
            if (!HandOverStack(state, source, sourceIds, destinationIds, sharedStacks, destinationOwner)) {
                return state.Deny(BankDenial.UnknownItem, $"the lists of {move.From} and {move.To} disagree about stack {move.ItemId}");
            }

            destination = source;
        } else {
            destination = SplitStack(session, state, access, source, destinationIds, sharedStacks, destinationOwner);
            if (destination is null) {
                return state.Deny(BankDenial.SaveFailed, $"a stack of template {source.m_templateID.Full} could not be created");
            }
        }

        state.DestinationDoc = destination;
        state.NewId = destination.m_globalID;

        return state;
    }

    private static void MergeIntoStack<T>(IDocumentSession session, MoveState state, StackAccess<T> access, T source, T destination,
                                          List<ulong> sourceIds, List<T> sharedStacks) where T : WizClientObjectItem {
        var move = state.Move;
        var sourceQuantity = access.GetQuantity(source);
        access.SetQuantity(destination, access.GetQuantity(destination) + move.Quantity);
        if (move.Quantity < sourceQuantity) {
            access.SetQuantity(source, sourceQuantity - move.Quantity);

            return;
        }

        DeleteStack(session, source, sourceIds, move.From == BankContainer.SharedBank ? sharedStacks : null);
        state.SourceGone = true;
    }

    // Nothing of the template is at the destination, so the stack itself changes owner and keeps its id.
    private static bool HandOverStack<T>(MoveState state, T source, List<ulong> sourceIds, List<ulong> destinationIds,
                                         List<T> sharedStacks, ulong destinationOwner) where T : WizClientObjectItem {
        if (destinationIds.Contains(source.m_globalID) || !sourceIds.Remove(source.m_globalID)) {
            return false;
        }

        destinationIds.Add(source.m_globalID);
        source.m_characterId = (GID) destinationOwner;
        if (state.Move.To == BankContainer.SharedBank) {
            sharedStacks.Add(source);
        } else {
            sharedStacks.Remove(source);
        }

        state.SourceGone = true;
        state.DestinationIsNew = true;

        return true;
    }

    private static T SplitStack<T>(IDocumentSession session, MoveState state, StackAccess<T> access, T source, List<ulong> destinationIds,
                                   List<T> sharedStacks, ulong destinationOwner) where T : WizClientObjectItem {
        var created = access.CreateLike(source);
        if (created is null) {
            return null;
        }

        created.m_characterId = (GID) destinationOwner;
        access.SetQuantity(created, state.Move.Quantity);
        access.SetQuantity(source, access.GetQuantity(source) - state.Move.Quantity);
        StoreStack(session, created, access.CollectionName);
        destinationIds.Add(created.m_globalID);
        if (state.Move.To == BankContainer.SharedBank) {
            sharedStacks.Add(created);
        }

        state.DestinationIsNew = true;

        return created;
    }

    private static MoveState PlanDelete(IDocumentSession session, Account storedAccount, SharedMove move) {
        var state = new MoveState(move);
        var shared = storedAccount.SharedBank ??= new AccountSharedBank();
        state.StoredShared = shared;
        BankItemKind? kind = shared.ItemIds.Contains(move.ItemId) ? BankItemKind.Item
            : shared.ReagentIds.Contains(move.ItemId) ? BankItemKind.Reagent
            : shared.SnackIds.Contains(move.ItemId) ? BankItemKind.PetSnack
            : null;
        if (kind is null) {
            return state.Deny(BankDenial.UnknownItem, $"item {move.ItemId} is not in the shared bank");
        }

        state.Kind = kind.Value;

        return kind.Value switch {
            BankItemKind.Reagent => PlanStackDelete(session, storedAccount, state, s_reagents),
            BankItemKind.PetSnack => PlanStackDelete(session, storedAccount, state, s_snacks),
            _ => PlanItemDelete(session, storedAccount, state),
        };
    }

    private static MoveState PlanItemDelete(IDocumentSession session, Account storedAccount, MoveState state) {
        var shared = storedAccount.SharedBank;
        var itemId = state.Move.ItemId;
        shared.Items = AccountCollection.LoadSharedDocuments<WizClientObjectItem>(session, WizardItemCollection.CollectionName, storedAccount.AccountId, shared.ItemIds);
        var item = shared.Items.FirstOrDefault(stored => stored.m_globalID == itemId);
        if (item is null) {
            return state.Deny(BankDenial.UnknownItem, $"item {itemId} is listed in the shared bank but has no document");
        }

        var denial = state.Move.Quantity == 1 ? BankRules.CanDelete(item) : BankDenial.BadQuantity;
        if (denial != BankDenial.None) {
            return state.Deny(denial, $"item {itemId} may not be deleted: {denial}");
        }

        session.Delete(item);
        shared.ItemIds.Remove(itemId);
        shared.Items.Remove(item);
        state.OriginalId = itemId;
        state.SourceGone = true;

        return state;
    }

    private static MoveState PlanStackDelete<T>(IDocumentSession session, Account storedAccount, MoveState state, StackAccess<T> access)
        where T : WizClientObjectItem {
        var shared = storedAccount.SharedBank;
        var itemId = state.Move.ItemId;
        var stacks = AccountCollection.LoadSharedDocuments<T>(session, access.CollectionName, storedAccount.AccountId, access.SharedIds(shared));
        access.SetShared(shared, stacks);
        var stack = stacks.FirstOrDefault(stored => stored.m_globalID == itemId);
        if (stack is null) {
            return state.Deny(BankDenial.UnknownItem, $"stack {itemId} is listed in the shared bank but has no document");
        }

        var quantity = state.Move.Quantity;
        var held = access.GetQuantity(stack);
        if (quantity < 1 || quantity > held) {
            return state.Deny(BankDenial.BadQuantity, $"{quantity} units of stack {itemId} may not be deleted, it holds {held}");
        }

        state.OriginalId = itemId;
        if (quantity == held) {
            DeleteStack(session, stack, access.SharedIds(shared), stacks);
            state.SourceGone = true;
        } else {
            access.SetQuantity(stack, held - quantity);
            state.SourceDoc = stack;
        }

        return state;
    }

    private static SharedMoveResult MirrorMove(Wizard wizard, MoveState state) {
        var move = state.Move;
        AdoptSharedBank(wizard.Account.SharedBank, state);

        switch (state.Kind) {
            case BankItemKind.Reagent:
                return MirrorStackMove(wizard, state, s_reagents);
            case BankItemKind.PetSnack:
                return MirrorStackMove(wizard, state, s_snacks);
        }

        var item = state.DestinationDoc;
        if (move.From == BankContainer.Backpack) {
            wizard.InventoryBehavior.RemoveItem(move.ItemId, out _);
        } else if (move.From == BankContainer.Bank) {
            wizard.StorageBehavior.RemoveItem(move.ItemId, out _);
        }

        if (move.To == BankContainer.Backpack) {
            wizard.InventoryBehavior.Items.Add(item);
            wizard.InventoryBehavior.InventoryItemIds.Add(move.ItemId);
        } else if (move.To == BankContainer.Bank) {
            wizard.StorageBehavior.Items.Add(item);
            wizard.StorageBehavior.BankItemIds.Add(move.ItemId);
        }

        return new SharedMoveResult(BankDenial.None, string.Empty, state.Kind, move.ItemId, move.ItemId, null, item);
    }

    private static SharedMoveResult MirrorStackMove<T>(Wizard wizard, MoveState state, StackAccess<T> access) where T : WizClientObjectItem {
        var move = state.Move;
        var sharedStacks = access.GetShared(wizard.Account.SharedBank);
        var (remaining, arrived) = move.From == BankContainer.Backpack
            ? MirrorStackFromBackpack(wizard, state, access, sharedStacks)
            : MirrorStackToBackpack(wizard, state, access, sharedStacks);

        return new SharedMoveResult(BankDenial.None, string.Empty, state.Kind, move.ItemId, state.NewId, remaining, arrived);
    }

    private static (WizClientObjectItem Remaining, WizClientObjectItem Arrived) MirrorStackFromBackpack<T>(
        Wizard wizard, MoveState state, StackAccess<T> access, List<T> sharedStacks) where T : WizClientObjectItem {
        var itemId = state.Move.ItemId;
        var backpackStacks = access.BackpackStacks(wizard);
        var held = backpackStacks.FirstOrDefault(stack => stack.m_globalID == itemId);
        var arrived = sharedStacks.FirstOrDefault(stack => stack.m_globalID == state.NewId);
        if (!state.SourceGone) {
            if (held is not null) {
                access.SetQuantity(held, access.GetQuantity((T) state.SourceDoc));
            }

            return (held ?? state.SourceDoc, arrived);
        }

        if (held is not null) {
            backpackStacks.Remove(held);
        }

        access.BackpackIds(wizard).Remove(itemId);

        return (null, arrived);
    }

    private static (WizClientObjectItem Remaining, WizClientObjectItem Arrived) MirrorStackToBackpack<T>(
        Wizard wizard, MoveState state, StackAccess<T> access, List<T> sharedStacks) where T : WizClientObjectItem {
        var backpackStacks = access.BackpackStacks(wizard);
        var backpackIds = access.BackpackIds(wizard);
        var destination = (T) state.DestinationDoc;
        var held = backpackStacks.FirstOrDefault(stack => stack.m_globalID == state.NewId);
        if (held is not null) {
            access.SetQuantity(held, access.GetQuantity(destination));
        } else {
            backpackStacks.Add(destination);
            if (!backpackIds.Contains(state.NewId)) {
                backpackIds.Add(state.NewId);
            }
        }

        var remaining = state.SourceGone ? null : sharedStacks.FirstOrDefault(stack => stack.m_globalID == state.Move.ItemId);

        return (remaining, held ?? destination);
    }

    private static void AdoptSharedBank(AccountSharedBank memory, MoveState state) {
        // Only the lists of the kind the call touched are replaced, with what the call stored.
        var stored = state.StoredShared;
        switch (state.Kind) {
            case BankItemKind.Reagent:
                memory.ReagentIds = [.. stored.ReagentIds];
                memory.Reagents = [.. stored.Reagents];

                break;
            case BankItemKind.PetSnack:
                memory.SnackIds = [.. stored.SnackIds];
                memory.Snacks = [.. stored.Snacks];

                break;
            default:
                memory.ItemIds = [.. stored.ItemIds];
                memory.Items = [.. stored.Items];

                break;
        }
    }

    private static WizClientObjectItem FindShared(AccountSharedBank shared, BankItemKind kind, ulong itemId) => kind switch {
        BankItemKind.Reagent => shared.Reagents.FirstOrDefault(stack => stack.m_globalID == itemId),
        BankItemKind.PetSnack => shared.Snacks.FirstOrDefault(stack => stack.m_globalID == itemId),
        _ => null,
    };

    private static BankItemKind? FindKind(BankContainer container, ulong itemId, AccountSharedBank shared, Wizard storedWizard) {
        switch (container) {
            case BankContainer.SharedBank:
                if (shared.ItemIds.Contains(itemId)) {
                    return BankItemKind.Item;
                }

                if (shared.ReagentIds.Contains(itemId)) {
                    return BankItemKind.Reagent;
                }

                return shared.SnackIds.Contains(itemId) ? BankItemKind.PetSnack : null;
            case BankContainer.Backpack:
                if (storedWizard.InventoryBehavior?.InventoryItemIds?.Contains(itemId) == true) {
                    return BankItemKind.Item;
                }

                if (storedWizard.AlchemyBehavior?.ReagentItemIds?.Contains(itemId) == true) {
                    return BankItemKind.Reagent;
                }

                return storedWizard.PetSnackBehavior?.SnackItemIds?.Contains(itemId) == true ? BankItemKind.PetSnack : null;
            default:
                return storedWizard.StorageBehavior?.BankItemIds?.Contains(itemId) == true ? BankItemKind.Item : null;
        }
    }

    private static List<ulong> IdsOf(BankContainer container, BankItemKind kind, Account storedAccount, Wizard storedWizard) {
        var shared = storedAccount.SharedBank;
        var isStack = kind is BankItemKind.Reagent or BankItemKind.PetSnack;

        return container switch {
            BankContainer.SharedBank => kind switch {
                BankItemKind.Reagent => shared.ReagentIds,
                BankItemKind.PetSnack => shared.SnackIds,
                _ => shared.ItemIds,
            },
            BankContainer.Backpack => kind switch {
                BankItemKind.Reagent => s_reagents.BackpackIds(storedWizard),
                BankItemKind.PetSnack => s_snacks.BackpackIds(storedWizard),
                _ => storedWizard.InventoryBehavior.InventoryItemIds ??= [],
            },
            _ when !isStack => (storedWizard.StorageBehavior ??= new ServerWizStorageBehavior()).BankItemIds ??= [],
            _ => throw new InvalidOperationException("The bank holds no stacks."),
        };
    }

    private static ulong OwnerOf(BankContainer container, Wizard wizard, Account storedAccount)
        => container == BankContainer.SharedBank ? storedAccount.AccountId : wizard.CharId;

    private static T FindDocument<T>(IDocumentSession session, string collectionName, ulong owner, ulong itemId) where T : WizClientObjectItem
        => session.Query<T>(collectionName: collectionName)
            .Customize(query => query.WaitForNonStaleResults())
            .FirstOrDefault(stored => stored.m_characterId == owner && stored.m_globalID == itemId);

    private static T FindStackByTemplate<T>(IDocumentSession session, string collectionName, ulong owner, GID templateId, List<ulong> ownerIds)
        where T : WizClientObjectItem {
        var stack = session.Query<T>(collectionName: collectionName)
            .Customize(query => query.WaitForNonStaleResults())
            .FirstOrDefault(stored => stored.m_characterId == owner && stored.m_templateID == templateId);

        return stack is not null && ownerIds.Contains(stack.m_globalID) ? stack : null;
    }

    private static void StoreStack<T>(IDocumentSession session, T stack, string collectionName) where T : WizClientObjectItem {
        session.Store(stack);
        session.Advanced.GetMetadataFor(stack)[Raven.Client.Constants.Documents.Metadata.Collection] = collectionName;
    }

    private static void DeleteStack<T>(IDocumentSession session, T stack, List<ulong> ownerIds, List<T> loaded) where T : WizClientObjectItem {
        session.Delete(stack);
        ownerIds.Remove(stack.m_globalID);
        loaded?.Remove(stack);
    }

    private static SharedMoveResult Denied(SharedMove move, BankDenial denial, string reason) {
        Logger.Warning("Shared bank request of item {ItemId} refused: {Reason}", Logger.Args(move.ItemId, reason));

        return new SharedMoveResult(denial, reason, BankItemKind.Item, move.ItemId, 0, null, null);
    }

    private sealed class MoveState(SharedMove move) {

        internal SharedMove Move { get; } = move;
        internal BankDenial Denial { get; private set; }
        internal string Reason { get; private set; } = string.Empty;
        internal BankItemKind Kind { get; set; }
        internal AccountSharedBank StoredShared { get; set; }
        internal ulong OriginalId { get; set; }
        internal ulong NewId { get; set; }
        internal WizClientObjectItem SourceDoc { get; set; }
        internal WizClientObjectItem DestinationDoc { get; set; }
        internal bool SourceGone { get; set; }
        internal bool DestinationIsNew { get; set; }

        internal MoveState Deny(BankDenial denial, string reason) {
            Denial = denial;
            Reason = reason;

            return this;
        }

    }

    private sealed class StackAccess<T> where T : WizClientObjectItem {

        internal required string CollectionName { get; init; }
        internal required int MaxStack { get; init; }
        internal required Func<AccountSharedBank, List<ulong>> SharedIds { get; init; }
        internal required Func<AccountSharedBank, List<T>> GetShared { get; init; }
        internal required Action<AccountSharedBank, List<T>> SetShared { get; init; }
        internal required Func<Wizard, List<ulong>> BackpackIds { get; init; }
        internal required Func<Wizard, List<T>> BackpackStacks { get; init; }
        internal required Func<T, int> GetQuantity { get; init; }
        internal required Action<T, int> SetQuantity { get; init; }
        internal required Func<T, T> CreateLike { get; init; }

    }

}
