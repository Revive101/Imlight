/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.Common.Configuration;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.Common.Utilities;
using Imlight.CoreLib.Game.Effects;
using Imlight.CoreLib.WizardData.Implementations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Models.Player;

[Serializable]
public class ServerWizEquipmentBehavior : BehaviorInstance, IClientBehaviorProvider<ClientWizEquipmentBehavior> {
    public List<EquipmentSlot> SlotList;
    public List<ulong> EquippedItemIds;

    [JsonIgnore] public List<WizClientObjectItem> EquippedItems;

    public bool EquipItem(WizClientObjectItem item, out WizItemTemplate template, out WizItemTemplate removedTemplate) {
        template = default;
        removedTemplate = default;
        var itemId = item.m_globalID;

        // Prerequisite checks.
        if (HasItemEquipped(itemId)) {
            return false;
        }

        // Get the template for this item.
        template = ItemHelper.GetItemTemplate(item);
        if (template == null) {
            Logger.Warning("Tried to equip item with global id {0} that does not have a template.", Logger.Args(itemId));
            return false;
        }

        // The slot name is in the adjectives of the item.
        var slot = ItemHelper.GetItemSlot(template);
        if (slot is null) {
            Logger.Warning("Tried to equip item with global id {0} that does not have a slot name adjective.", Logger.Args(itemId));
            return false;
        }

        var existingItem = GetItemInSlot(slot.SlotType);
        if (existingItem is not null) {
            EquippedItemIds.Remove(existingItem.m_globalID);
            EquippedItems.Remove(existingItem);
            removedTemplate = ItemHelper.GetItemTemplate(existingItem);
        }

        // Finally, update the slot.
        UpdateEquipmentSlot(slot, item.m_debugName, itemId);
        EquippedItemIds.Add(itemId);
        EquippedItems.Add(item);
        return true;
    }

    public bool UnequipItem(ulong itemId, out WizItemTemplate template) {
        template = default;

        // Prerequisite checks.
        if (!HasItemEquipped(itemId)) {
            return false;
        }

        // Get the actual item from this ID.
        var item = EquippedItems.FirstOrDefault(item => item.m_globalID == itemId);
        if (item == null) {
            Logger.Warning("Tried to unequip item with global id {0} that does not exist.", Logger.Args(itemId));
            return false;
        }

        // Get the template for this item.
        template = ItemHelper.GetItemTemplate(item);
        if (template == null) {
            Logger.Warning("Tried to unequip item with global id {0} that does not have a template.", Logger.Args(itemId));
            return false;
        }

        // The slot name is in the adjectives of the item.
        var slot = ItemHelper.GetItemSlot(template);
        if (slot is null) {
            Logger.Warning("Tried to unequip item with global id {0} that does not have a slot name adjective.", Logger.Args(itemId));
            return false;
        }

        // Finally, update the slot.
        ClearEquipmentSlot(slot);
        EquippedItemIds.Remove(itemId);
        EquippedItems.Remove(item);
        return true;
    }

    public bool HasItemEquipped(ulong itemId) => EquippedItems.Any(item => item.m_globalID == itemId);

    public bool SlotInUse(string slotName, out byte index) {
        index = 255;

        // Cast the slot name to an enum value.
        if (!Enum.TryParse(typeof(EquipmentSlotType), slotName, true, out var slot)) {
            Logger.Error("Could not parse slot name {0} to an enum value.", Logger.Args(slotName));
            return false;
        }

        return SlotInUse((EquipmentSlotType) slot, out index);
    }

    public bool SlotInUse(EquipmentSlotType slotType, out byte index) {
        index = 255;

        var slotIndex = SlotList.FindIndex(item => item.SlotType == slotType);
        if (slotIndex == -1) {
            return false;
        }

        index = (byte) slotIndex;
        return true;
    }

    public WizClientObjectItem GetItemInSlot(string slotName) {
        // Cast the slot name to an enum value.
        if (!Enum.TryParse(typeof(EquipmentSlotType), slotName, true, out var slot)) {
            Logger.Error("Could not parse slot name {0} to an enum value.", Logger.Args(slotName));
            return null;
        }

        return GetItemInSlot((EquipmentSlotType) slot);
    }

    public WizClientObjectItem GetItemInSlot(EquipmentSlotType slotType) {
        var slotIndex = SlotList.FindIndex(item => item.SlotType == slotType);
        if (slotIndex == -1) {
            return null;
        }

        return EquippedItems.FirstOrDefault(item => item.m_globalID == SlotList[slotIndex].ItemId);
    }

    public byte GetSlotOfItem(ulong itemId) {
        var slotIndex = SlotList.FindIndex(item => item.ItemId == itemId);
        if (slotIndex == -1) {
            return 255;
        }

        return (byte) slotIndex;
    }

    private void UpdateEquipmentSlot(EquipmentSlot slot, string itemName,  ulong newItemId) {
        // Find the slot in the list. If it does, remove it.
        ClearEquipmentSlot(slot);

        // Create a new slot and add it to the list.
        var newSlot = new EquipmentSlot {
            SlotType = slot.SlotType,
            ItemName = itemName,
            ItemId = (GID) newItemId,
            EquippedSince = DateTime.Now,
        };
        SlotList.Add(newSlot);
    }

    private void ClearEquipmentSlot(EquipmentSlot slot) {
        // Find the slot in the list. If it does, remove it.
        var slotIndex = SlotList.FindIndex(eSlot => eSlot.SlotType == slot.SlotType);
        if (slotIndex != -1) {
            SlotList.RemoveAt(slotIndex);
        }
    }

    public ClientWizEquipmentBehavior GetClientBehaviorInstance() {
        return new ClientWizEquipmentBehavior {
            m_equipmentSets = new List<EquipmentSet>(),
            m_slotList = SlotList.Select(slot => slot.GetClientTypeAlternative()).ToList(),
            m_itemList = EquippedItems.ConvertAll(item => item as CoreObject),
            m_publicItemList = CharacterHelper.GetEquipmentList(this).m_infoList,
        };
    }
}
