/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models.Player;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerWizEquipmentBehavior : ServerBehaviorInstance {
    [JsonIgnore] public override bool NoTransfer { get; set; } = false;

    public List<EquipmentSlot> SlotList;
    public List<ulong> EquippedItemIds;

    [JsonIgnore] public List<WizClientObjectItem> EquippedItems;

    public bool EquipItem(WizClientObjectItem item, EquipmentSlotType slotType) {
        var itemId = item.m_globalID;

        // Prerequisite checks.
        if (HasItemEquipped(itemId)) {
            return false;
        }

        var existingItem = GetItemInSlot(slotType);
        if (existingItem is not null) {
            EquippedItemIds.Remove(existingItem.m_globalID);
            EquippedItems.Remove(existingItem);
        }

        // Finally, update the slot.
        UpdateEquipmentSlot(slotType, item.m_debugName, itemId);
        EquippedItemIds.Add(itemId);
        EquippedItems.Add(item);
        return true;
    }

    public void ForceEquipItem(WizClientObjectItem item) {
        var itemId = item.m_globalID;
        EquippedItemIds.Add(itemId);
        EquippedItems.Add(item);
    }

    public bool UnequipItem(ulong itemId) {
        // Prerequisite checks.
        if (!HasItemEquipped(itemId)) {
            return false;
        }

        var item = EquippedItems.FirstOrDefault(item => item.m_globalID == itemId);
        var slot = SlotList.Find(eSlot => eSlot.ItemId == itemId);

        // Finally, update the slot.
        ClearEquipmentSlot(slot.SlotType);
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

    public WizClientObjectItem GetItem(ulong globalId) {
        return EquippedItems.FirstOrDefault(item => item.m_globalID == globalId);
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

    public WizItemTemplate GetTemplateInSlot(string slotName) {
        // Cast the slot name to an enum value.
        if (!Enum.TryParse(typeof(EquipmentSlotType), slotName, true, out var slot)) {
            Logger.Error("Could not parse slot name {0} to an enum value.", Logger.Args(slotName));
            return null;
        }

        return GetTemplateInSlot((EquipmentSlotType) slot);
    }

    public WizItemTemplate GetTemplateInSlot(EquipmentSlotType slotType) {
        var slotIndex = SlotList.FindIndex(item => item.SlotType == slotType);
        if (slotIndex == -1) {
            return null;
        }

        return ItemHelper.GetItemTemplate(EquippedItems.FirstOrDefault(item => item.m_globalID == SlotList[slotIndex].ItemId));
    }

    public byte GetSlotOfItem(ulong itemId) {
        var slotIndex = SlotList.FindIndex(item => item.ItemId == itemId);
        if (slotIndex == -1) {
            return 255;
        }

        return (byte) slotIndex;
    }

    private void UpdateEquipmentSlot(EquipmentSlotType slotType, string itemName,  ulong newItemId) {
        // Find the slot in the list. If it does, remove it.
        ClearEquipmentSlot(slotType);

        // Create a new slot and add it to the list.
        var newSlot = new EquipmentSlot {
            SlotType = slotType,
            ItemName = itemName,
            ItemId = (GID) newItemId,
            EquippedSince = DateTime.Now,
        };
        SlotList.Add(newSlot);
    }

    private void ClearEquipmentSlot(EquipmentSlotType slotType) {
        // Find the slot in the list. If it does, remove it.
        var slotIndex = SlotList.FindIndex(eSlot => eSlot.SlotType == slotType);
        if (slotIndex != -1) {
            SlotList.RemoveAt(slotIndex);
        }
    }

    public override ClientWizEquipmentBehavior GetClientBehaviorInstance() => new() {
        m_equipmentSets = new List<EquipmentSet>(),
        m_slotList = SlotList?.Select(slot => slot.GetClientTypeAlternative()).ToList(),
        m_itemList = EquippedItems?.ConvertAll(item => item as CoreObject),
        m_publicItemList = CharacterHelper.GetEquipmentList(this).m_infoList,
    };
}
