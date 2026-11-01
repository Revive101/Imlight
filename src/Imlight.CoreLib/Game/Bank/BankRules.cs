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
 * BANK RULES
 * ========================================================================
 *
 * PURPOSE:
 * The rules of the bank that need no database: which items count against a capacity, which items the shared
 * bank refuses and whether a move or a delete is allowed.
 *
 * USAGE EXAMPLE:
 * var denial = BankRules.CanMove(BankContainer.Backpack, BankContainer.SharedBank, item, 1, wizard, account);
 *
 * NOTE:
 * The counts follow the client's: the bank does not count reagents, pet snacks and emotes, the shared bank
 * does not count jewels either and holds them against SharedJewelCap. The backpack check is the server's own
 * (ServerWizInventoryBehavior.FreeSlots), the client also counts equipped items. The caller unequips an
 * equipped item before it asks about it. A rental never goes into the bank or the shared bank, because the
 * rental expiry only looks at the backpack and what is worn.
 *
 * TODO:
 * Are treasure card posters and vaults that hold no trade cards no trade items? They need housing first.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Bank;

/// <summary>
/// Pure rules of the bank.
/// </summary>
internal static class BankRules {

    private const string EMOTE_ADJECTIVE = "Emote";
    private const string JEWEL_ADJECTIVE = "Jewel";
    private const string NO_TRADE_ADJECTIVE = "FLAG_NoTrade";
    private const string HOUSE_ADJECTIVE = "Islands";
    private const uint NO_TRADE_ITEM_FLAG = 1;
    private const int LOCK_BIT = unchecked((int) 0x80000000);

    /// <summary>
    /// Tells what kind of item a bank request is about.
    /// </summary>
    /// <param name="item">The item in question.</param>
    /// <param name="template">The template of the item, or null when it has none.</param>
    /// <returns>Reagents and pet snacks by their class, jewels by their adjective, anything else as an item.</returns>
    internal static BankItemKind Classify(WizClientObjectItem item, WizItemTemplate template) {
        if (item is ClientReagentItem) {
            return BankItemKind.Reagent;
        }

        if (item is ClientPetSnackItem) {
            return BankItemKind.PetSnack;
        }

        return HasAdjective(template, JEWEL_ADJECTIVE) ? BankItemKind.Jewel : BankItemKind.Item;
    }

    /// <summary>
    /// Whether an item takes a slot of the regular bank.
    /// </summary>
    /// <param name="item">The item in question.</param>
    /// <returns>False for reagents, pet snacks and emote items, which the bank does not count.</returns>
    internal static bool CountsTowardBank(WizClientObjectItem item) {
        if (item is ClientReagentItem or ClientPetSnackItem) {
            return false;
        }

        return !HasAdjective(GetTemplate(item), EMOTE_ADJECTIVE);
    }

    /// <summary>
    /// Whether an item takes a slot of the regular part of the shared bank.
    /// </summary>
    /// <param name="item">The item in question.</param>
    /// <returns>False for what the bank does not count and for jewels, which have their own cap.</returns>
    internal static bool CountsTowardSharedItems(WizClientObjectItem item)
        => CountsTowardBank(item) && !IsJewel(item);

    /// <summary>
    /// Whether an item is a jewel.
    /// </summary>
    /// <param name="item">The item in question.</param>
    /// <returns>True when the template has the Jewel adjective and the item is not a stack.</returns>
    internal static bool IsJewel(WizClientObjectItem item)
        => Classify(item, GetTemplate(item)) == BankItemKind.Jewel;

    /// <summary>
    /// Whether the item cannot be traded, so the shared bank refuses it.
    /// </summary>
    /// <param name="item">The item in question.</param>
    /// <param name="template">The template of the item, or null when it has none.</param>
    /// <returns>True for the FLAG_NoTrade adjective or the no trade bit of the item flags.</returns>
    internal static bool IsNoTrade(WizClientObjectItem item, WizItemTemplate template)
        => (item.m_itemFlags & NO_TRADE_ITEM_FLAG) != 0 || HasAdjective(template, NO_TRADE_ADJECTIVE);

    /// <summary>
    /// Whether jewels are socketed in the item, which the shared bank refuses.
    /// </summary>
    /// <param name="item">The item in question.</param>
    /// <returns>True when its jewel socket behavior lists at least one socketed jewel.</returns>
    internal static bool HasSocketedJewels(WizClientObjectItem item)
        => item.m_inactiveBehaviors?.OfType<ClientJewelSocketBehavior>()
            .Any(sockets => sockets.m_publicJewelInfo?.m_infoPairs is { Count: > 0 }) == true;

    /// <summary>
    /// Whether the item is a house, which the shared bank refuses until housing can move one.
    /// </summary>
    /// <param name="template">The template of the item, or null when it has none.</param>
    /// <returns>True for the Islands adjective.</returns>
    internal static bool IsHouse(WizItemTemplate template) => HasAdjective(template, HOUSE_ADJECTIVE);

    /// <summary>
    /// Whether the item is a rental, which the bank and the shared bank refuse.
    /// </summary>
    /// <param name="item">The item in question.</param>
    /// <returns>True when its template is a rental.</returns>
    internal static bool IsRental(WizClientObjectItem item) => ItemHelper.IsRental(GetTemplate(item));

    /// <summary>
    /// Whether the client has locked the item. The bank window lets a locked item move, it only keeps the item
    /// from being sold, so a locked item is only kept from being deleted here.
    /// </summary>
    /// <param name="item">The item in question.</param>
    /// <returns>True when bit 31 of the pattern is set.</returns>
    internal static bool IsLocked(WizClientObjectItem item) => (item.m_pattern & LOCK_BIT) != 0;

    /// <summary>
    /// Whether other items are stored in the item, as in a vault, which stops it from being deleted.
    /// </summary>
    /// <param name="item">The item in question.</param>
    /// <returns>True when its equipment behavior holds an item.</returns>
    internal static bool HasContents(WizClientObjectItem item)
        => item.m_inactiveBehaviors?.OfType<ClientEquipmentBehavior>()
            .Any(contents => contents.m_itemList is { Count: > 0 }) == true;

    /// <summary>
    /// Counts the items of the regular part of the shared bank.
    /// </summary>
    /// <param name="items">The items of the shared bank.</param>
    /// <returns>The number of items that count against the shared bank limit.</returns>
    internal static int CountSharedItems(IEnumerable<WizClientObjectItem> items) => items.Count(CountsTowardSharedItems);

    /// <summary>
    /// Counts the jewels of the shared bank.
    /// </summary>
    /// <param name="items">The items of the shared bank.</param>
    /// <returns>The number of jewels, which count against the shared jewel cap.</returns>
    internal static int CountSharedJewels(IEnumerable<WizClientObjectItem> items) => items.Count(IsJewel);

    /// <summary>
    /// Decides whether a move may happen.
    /// </summary>
    /// <param name="from">The container the item is in.</param>
    /// <param name="to">The container it should go to.</param>
    /// <param name="item">The item, or the stack, found in the source container.</param>
    /// <param name="quantity">1 for an item, 1 up to the stack size for a stack.</param>
    /// <param name="wizard">The wizard that moves.</param>
    /// <param name="account">The account of the wizard, which owns the shared bank.</param>
    /// <returns>None when the move is allowed, otherwise the first reason it is not.</returns>
    internal static BankDenial CanMove(BankContainer from, BankContainer to, WizClientObjectItem item, int quantity,
                                       Wizard wizard, Account account) {
        if (item is null) {
            return BankDenial.UnknownItem;
        }

        if (from == to) {
            return BankDenial.WrongContainer;
        }

        var template = GetTemplate(item);
        var kind = Classify(item, template);
        var isStack = kind is BankItemKind.Reagent or BankItemKind.PetSnack;
        if (isStack) {
            if (from == BankContainer.Bank || to == BankContainer.Bank) {
                return BankDenial.WrongContainer;
            }

            if (quantity < 1 || quantity > GetQuantity(item)) {
                return BankDenial.BadQuantity;
            }
        } else if (quantity != 1) {
            return BankDenial.BadQuantity;
        }

        if (to != BankContainer.Backpack && IsRental(item)) {
            return BankDenial.Rental;
        }

        if (to == BankContainer.SharedBank) {
            if (IsNoTrade(item, template)) {
                return BankDenial.NoTrade;
            }

            if (HasSocketedJewels(item)) {
                return BankDenial.SocketedJewels;
            }

            if (IsHouse(template)) {
                return BankDenial.House;
            }
        }

        return to switch {
            BankContainer.Backpack => HasBackpackRoom(item, kind, quantity, wizard) ? BankDenial.None : BankDenial.BackpackFull,
            BankContainer.Bank => wizard.StorageBehavior.HasRoomFor(item) ? BankDenial.None : BankDenial.BankFull,
            _ => SharedRoomDenial(item, kind, quantity, account),
        };
    }

    /// <summary>
    /// Decides whether an item may be deleted for good.
    /// </summary>
    /// <param name="item">The item in the bank, the backpack or the shared bank.</param>
    /// <returns>None when it may be deleted, Locked for a locked item and HasContents for a container with items in it.</returns>
    internal static BankDenial CanDelete(WizClientObjectItem item) {
        if (item is null) {
            return BankDenial.UnknownItem;
        }

        if (IsLocked(item)) {
            return BankDenial.Locked;
        }

        return HasContents(item) ? BankDenial.HasContents : BankDenial.None;
    }

    /// <summary>
    /// The number of units of a stack, 1 for any other item.
    /// </summary>
    /// <param name="item">The item or stack.</param>
    /// <returns>The quantity.</returns>
    internal static int GetQuantity(WizClientObjectItem item) => item switch {
        ClientReagentItem reagent => reagent.m_quantity,
        ClientPetSnackItem snack => snack.m_quantity,
        _ => 1,
    };

    private static bool HasBackpackRoom(WizClientObjectItem item, BankItemKind kind, int quantity, Wizard wizard) {
        switch (kind) {
            case BankItemKind.Reagent:
                var reagent = wizard.AlchemyBehavior?.GetReagent(item.m_templateID);

                return (reagent?.m_quantity ?? 0) + quantity <= ServerAlchemyBehavior.MaxStack;
            case BankItemKind.PetSnack:
                var snack = wizard.PetSnackBehavior?.GetSnack(item.m_templateID);

                return (snack?.m_quantity ?? 0) + quantity <= ServerPetSnackBehavior.MaxStack;
            default:
                return wizard.InventoryBehavior.FreeSlots >= 1;
        }
    }

    private static BankDenial SharedRoomDenial(WizClientObjectItem item, BankItemKind kind, int quantity, Account account) {
        var shared = account.SharedBank;
        switch (kind) {
            case BankItemKind.Reagent:
                var reagent = shared.Reagents.FirstOrDefault(stack => stack.m_templateID == item.m_templateID);

                return (reagent?.m_quantity ?? 0) + quantity <= ServerAlchemyBehavior.MaxStack ? BankDenial.None : BankDenial.SharedBankFull;
            case BankItemKind.PetSnack:
                var snack = shared.Snacks.FirstOrDefault(stack => stack.m_templateID == item.m_templateID);

                return (snack?.m_quantity ?? 0) + quantity <= ServerPetSnackBehavior.MaxStack ? BankDenial.None : BankDenial.SharedBankFull;
            case BankItemKind.Jewel:
                return CountSharedJewels(shared.Items) < BankConfig.SharedJewelCap ? BankDenial.None : BankDenial.SharedJewelsFull;
            default:
                return !CountsTowardSharedItems(item) || CountSharedItems(shared.Items) < shared.Limit ? BankDenial.None : BankDenial.SharedBankFull;
        }
    }

    private static WizItemTemplate GetTemplate(WizClientObjectItem item)
        => CoreObjectFactory.GetCoreTemplate(item.m_templateID) as WizItemTemplate;

    private static bool HasAdjective(WizItemTemplate template, string name)
        => template?.m_adjectiveList is not null
            && template.m_adjectiveList.Any(adjective => string.Equals(adjective, name, StringComparison.OrdinalIgnoreCase));

}
