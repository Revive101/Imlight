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
 * WIZARD STORAGE BEHAVIOR
 * ========================================================================
 *
 * PURPOSE:
 * The items in a character's bank and the bank's capacity. The shared bank of the account is not here, it
 * lives on the account.
 *
 * USAGE EXAMPLE:
 * if (!wizard.StorageBehavior.AddItem(item)) { ... }
 *
 * NOTE:
 * Like the backpack, the wizard document keeps only the item ids. The items are documents of the WizardItems
 * collection owned by the character and are loaded next to the backpack items.
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
using System.Text.Json.Serialization;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Bank;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerWizStorageBehavior {

    public List<ulong> BankItemIds { get; set; } = [];

    [JsonIgnore] public List<WizClientObjectItem> Items { get; set; } = [];

    /// <summary>
    /// The capacity this character has of its own, from an expansion or a GM command. Zero follows the ini
    /// default, see <see cref="Limit"/>.
    /// </summary>
    public int BankLimit { get; set; }

    /// <summary>
    /// The most regular items the bank holds. Reagents, pet snacks and emotes do not count, see <see cref="BankRules"/>.
    /// </summary>
    [JsonIgnore] public int Limit => BankLimit > 0 ? BankLimit : BankConfig.MaxBankItems;

    /// <summary>
    /// Raises the capacity of the bank, up to [Bank] MaxBankItems when that is set. A capacity that is above the
    /// ceiling already stays as it is.
    /// </summary>
    /// <param name="amount">How much to add. Zero or less adds nothing.</param>
    /// <returns>The capacity after the expansion.</returns>
    internal int Expand(int amount) {
        var current = Limit;
        var expanded = (int) Math.Min((long) current + Math.Max(0, amount), int.MaxValue);
        var ceiling = BankConfig.MaxBankLimit;
        BankLimit = ceiling > 0 ? Math.Max(current, Math.Min(expanded, ceiling)) : expanded;

        return BankLimit;
    }

    /// <summary>
    /// The number of bank items that count against <see cref="Limit"/>.
    /// </summary>
    internal int CountRegularItems() => Items.Count(BankRules.CountsTowardBank);

    /// <summary>
    /// Whether the bank has room for one more item.
    /// </summary>
    /// <param name="item">The item that would be added.</param>
    /// <returns>True when the item takes no space or a slot is free.</returns>
    internal bool HasRoomFor(WizClientObjectItem item)
        => !BankRules.CountsTowardBank(item) || CountRegularItems() < Limit;

    /// <summary>
    /// Adds an item to the bank.
    /// </summary>
    /// <param name="item">The item to be added.</param>
    /// <returns>True if the item was added, false if the bank is full or already holds the item.</returns>
    public bool AddItem(WizClientObjectItem item) {
        if (BankRules.IsRental(item)) {
            Logger.Warning("The bank does not hold rentals. Cannot add item with global id {ItemId}.", Logger.Args(item.m_globalID.Full));

            return false;
        }

        if (!HasRoomFor(item)) {
            Logger.Debug("The bank is full. Cannot add item with global id {ItemId}.", Logger.Args(item.m_globalID.Full));

            return false;
        }

        if (HasItem(item.m_globalID)) {
            Logger.Error("Item with same global id {ItemId} already exists in the bank.", Logger.Args(item.m_globalID.Full));

            return false;
        }

        BankItemIds.Add(item.m_globalID);
        Items.Add(item);

        return true;
    }

    /// <summary>
    /// Removes an item from the bank based on its unique identifier.
    /// </summary>
    /// <param name="itemId">The unique identifier of the item to be removed.</param>
    /// <param name="removedItem">The item that was removed, or null.</param>
    /// <returns><c>true</c> if the item was removed; otherwise, <c>false</c>.</returns>
    public bool RemoveItem(ulong itemId, out WizClientObjectItem removedItem) {
        removedItem = GetItem(itemId);
        if (removedItem is null) {
            Logger.Debug("Tried to remove item with global id {ItemId} that does not exist in the bank.", Logger.Args(itemId));

            return false;
        }

        return RemoveItem(removedItem);
    }

    /// <summary>
    /// Removes an item from the bank.
    /// </summary>
    /// <param name="item">The item to be removed.</param>
    /// <returns><c>true</c> if the item was removed; otherwise, <c>false</c>.</returns>
    public bool RemoveItem(WizClientObjectItem item) {
        ArgumentNullException.ThrowIfNull(item);

        if (!Items.Remove(item)) {
            Logger.Debug("Tried to remove item with global id {ItemId} that does not exist in the bank.", Logger.Args(item.m_globalID.Full));

            return false;
        }

        BankItemIds.Remove(item.m_globalID);

        return true;
    }

    /// <summary>
    /// Checks if the bank contains an item with the specified global ID.
    /// </summary>
    /// <param name="globalId">The global ID of the item to check.</param>
    /// <returns>True if the bank contains the item.</returns>
    public bool HasItem(ulong globalId) => Items.Any(item => item.m_globalID == globalId);

    /// <summary>
    /// Gets a bank item by its global ID.
    /// </summary>
    /// <param name="globalId">The global ID of the item.</param>
    /// <returns>The item, or null when the bank does not hold it.</returns>
    public WizClientObjectItem GetItem(ulong globalId) => Items.FirstOrDefault(item => item.m_globalID == globalId);

    /// <summary>
    /// Builds the storage behavior of the player object. The item list stays empty: the client gets the bank items
    /// from the stream the server sends when the bank opens.
    /// </summary>
    /// <param name="sharedBankLimit">The capacity of the account's shared bank.</param>
    /// <returns>The client behavior with both capacities.</returns>
    public ClientWizStorageBehavior ToClientBehavior(int sharedBankLimit) => new() {
        m_bankLimit = Limit,
        m_sharedBankLimit = sharedBankLimit,
        m_itemList = [],
    };

}
