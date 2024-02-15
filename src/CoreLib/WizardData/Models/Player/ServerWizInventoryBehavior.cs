/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.Common.Configuration;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.Common.Utilities;
using Imlight.CoreLib.WizardData.Implementations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Models.Player;

[Serializable]
public class ServerWizInventoryBehavior : BehaviorInstance, IClientBehaviorProvider<ClientWizInventoryBehavior> {
    private static readonly int s_maxItemsAllowed = ConfigurationManager.Settings.MaxInventoryItems;
    private static readonly int s_maxJewelsAllowed = ConfigurationManager.Settings.MaxJewelsAllowed;

    public List<ulong> InventoryItemIds { get; set; }

    [JsonIgnore] public List<WizClientObjectItem> InventoryItems { get; set; }

    /// <summary>
    /// Adds an item to the player's inventory.
    /// </summary>
    /// <param name="item">The item to be added.</param>
    /// <returns>True if the item was successfully added, false otherwise.</returns>
    public bool AddItem(WizClientObjectItem item) {
        if (InventoryItems.Count >= s_maxItemsAllowed) {
            Logger.Debug("Player inventory is full. Cannot add item with global id {0}.", Logger.Args(item.m_globalID));
            return false;
        }

        if (HasItem(item.m_globalID)) {
            Logger.Error("Item with same global id {0} already exists in player inventory.", Logger.Args(item.m_globalID));
            return false;
        }

        InventoryItems.Add(item);
        return true;
    }

    /// <summary>
    /// Removes an item from the player's inventory based on its unique identifier.
    /// </summary>
    /// <param name="itemId">The unique identifier of the item to be removed.</param>
    /// <returns><c>true</c> if the item was successfully removed; otherwise, <c>false</c>.</returns>
    public bool RemoveItem(ulong itemId) {
        // Get the actual item from the inventory.
        var item = InventoryItems.Find(i => i.m_globalID == itemId);
        if (item is null) {
            Logger.Debug("Tried to remove item with global id {0} that does not exist in player inventory.",
                Logger.Args(itemId));
            return false;
        }

        return RemoveItem(item);
    }

    /// <summary>
    /// Removes an item from the player's inventory.
    /// </summary>
    /// <param name="item">The item to be removed.</param>
    /// <returns><c>true</c> if the item was successfully removed; otherwise, <c>false</c>.</returns>
    public bool RemoveItem(WizClientObjectItem item) {
        if (item is null) {
            throw new NullReferenceException("Item cannot be null.");
        }
        if (!InventoryItems.Remove(item)) {
            Logger.Debug("Tried to remove item with global id {0} that does not exist in player inventory.",
                Logger.Args(item.m_globalID));
            return false;
        }

        if (!InventoryItemIds.Remove(item.m_globalID)) {
            Logger.Debug("Tried to remove item with global id {0} that does not exist in player inventory.",
                Logger.Args(item.m_globalID));
            return false;
        }

        // Persistent save.
        var persistentSaveSucceeded = WizardItemCollection.RemoveItem(item);
        if (!persistentSaveSucceeded) {
            Logger.Error("Could not remove item with global id {0} from database.", Logger.Args(item.m_globalID));
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if the inventory contains an item with the specified global ID.
    /// </summary>
    /// <param name="globalId">The global ID of the item to check.</param>
    /// <returns>True if the inventory contains an item with the specified global ID, otherwise false.</returns>
    public bool HasItem(ulong globalId) => InventoryItems.Any(item => item.m_globalID == globalId);

    /// <summary>
    /// Represents an item in the wizard's inventory.
    /// </summary>
    public WizClientObjectItem GetItem(ulong globalId) => InventoryItems.FirstOrDefault(item => item.m_globalID == globalId);

    ClientWizInventoryBehavior IClientBehaviorProvider<ClientWizInventoryBehavior>.GetClientBehaviorInstance(){
        return new ClientWizInventoryBehavior {
            m_numItemsAllowed = s_maxItemsAllowed,
            m_numJewelsAllowed = s_maxJewelsAllowed,
            m_itemList = InventoryItems.ConvertAll(item => (CoreObject) item)
        };
    }
}
