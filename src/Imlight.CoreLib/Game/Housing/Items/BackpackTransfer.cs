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
 * HOUSING SYSTEM
 * ========================================================================
 *
 * PURPOSE:
 * Moves one item between a character's backpack and a house or an attic, so the four transfers (place, pick up, store in
 * the attic, take out of the attic) share their locking, their checks and their undo.
 *
 * USAGE EXAMPLE:
 * var result = BackpackTransfer.Store(wizard, item, () => (change = house.TryAdd(placed, subType, save)).Done, "be placed");
 * if (result == BackpackTransferResult.Done) {
 *     // tell the client
 * }
 *
 * NOTE:
 * - The backpack changes in memory only, and the container's save writes it together with the container, so a crash
 *   between the two cannot lose the item or leave it in both places. The item's document stays where it is.
 * - Both transfers hold the character's backpack lock for the whole change, and put the backpack back when the save fails.
 * - The inventory is changed by other services without this lock, so a write of the whole inventory list can overwrite a
 *   concurrent change there. Closing that gap takes one inventory lock for every mutator, which is outside housing.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/21/2026
 */

using System;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Housing.Items;

/// <summary>
/// How a transfer between the backpack and a container ended.
/// </summary>
internal enum BackpackTransferResult {
    Done,
    NotInBackpack,
    BackpackCannotHold,
    Failed,
}

internal static class BackpackTransfer {

    /// <summary>
    /// Moves an item out of the backpack into a container.
    /// </summary>
    /// <param name="wizard">The character whose backpack the item leaves.</param>
    /// <param name="item">The item, from the character's own inventory.</param>
    /// <param name="addToContainer">Adds the object to the container and saves it with the backpack; returns <c>true</c> when that was written.</param>
    /// <param name="failedTo">What the item failed to do, for the log when it cannot be put back.</param>
    /// <returns>
    /// <see cref="BackpackTransferResult.NotInBackpack"/> when the item was not in the backpack, <see cref="BackpackTransferResult.Failed"/>
    /// when the container refused it (the item is back in the backpack), otherwise <see cref="BackpackTransferResult.Done"/>.
    /// </returns>
    internal static BackpackTransferResult Store(Wizard wizard, WizClientObjectItem item, Func<bool> addToContainer, string failedTo) {
        lock (HousingItems.BackpackLock(wizard)) {
            if (!wizard.InventoryBehavior.RemoveItem(item)) {
                return BackpackTransferResult.NotInBackpack;
            }

            if (addToContainer()) {
                return BackpackTransferResult.Done;
            }

            if (!wizard.InventoryBehavior.AddItem(item)) {
                Logger.Error("Could not return item {ItemId} to the backpack of character {CharId} after it failed to {FailedTo}.",
                    Logger.Args(item.m_globalID, wizard.CharId, failedTo));
            }

            return BackpackTransferResult.Failed;
        }
    }

    /// <summary>
    /// Moves an object out of a container into the backpack.
    /// </summary>
    /// <param name="wizard">The character whose backpack receives the item.</param>
    /// <param name="templateId">The object's template id.</param>
    /// <param name="itemGid">The item document the object was stored from, or 0 for an object that has none.</param>
    /// <param name="removeFromContainer">
    /// Removes the object from the container and saves it with the backpack. It gets the item when its document is new, or
    /// <c>null</c> when the stored document is used; it returns <c>true</c> when that was written.
    /// </param>
    /// <param name="item">The item in the backpack when this returns <see cref="BackpackTransferResult.Done"/>.</param>
    /// <returns>
    /// <see cref="BackpackTransferResult.BackpackCannotHold"/> when there is no room or the template's copy limit is reached,
    /// <see cref="BackpackTransferResult.Failed"/> when the item could not be made or the container could not save (the backpack is as it was),
    /// otherwise <see cref="BackpackTransferResult.Done"/>.
    /// </returns>
    internal static BackpackTransferResult Take(Wizard wizard, uint templateId, ulong itemGid, Func<WizClientObjectItem, bool> removeFromContainer,
                                                out WizClientObjectItem item) {
        item = null;
        lock (HousingItems.BackpackLock(wizard)) {
            if (CoreObjectFactory.GetCoreTemplate(templateId) is not WizItemTemplate template
                || wizard.InventoryBehavior.FreeSlots <= 0
                || HousingItems.TemplateCopyLimitReached(template, HousingItems.CountInBackpack(wizard, templateId))) {
                return BackpackTransferResult.BackpackCannotHold;
            }

            item = HousingItems.TakeItem(wizard, templateId, itemGid, out var created);
            if (item is null || !wizard.InventoryBehavior.AddItem(item)) {
                return BackpackTransferResult.Failed;
            }

            if (removeFromContainer(created ? item : null)) {
                return BackpackTransferResult.Done;
            }

            wizard.InventoryBehavior.RemoveItem(item);

            return BackpackTransferResult.Failed;
        }
    }

}
