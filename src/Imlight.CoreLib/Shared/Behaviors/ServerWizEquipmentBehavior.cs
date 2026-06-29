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
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Imlight.Common;
using Imlight.CoreLib.Shared.Character;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.WizardData.Models.Player;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerWizEquipmentBehavior : IClientBehaviorProvider<ClientWizEquipmentBehavior> {

    [JsonIgnore] public bool NoTransfer { get; set; } = false;

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

    public WizClientObjectItem GetItem(ulong globalId) 
        => EquippedItems.FirstOrDefault(item => item.m_globalID == globalId);

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

    public ulong GetEquippedPetId() {
        var petSlot = SlotList?.FirstOrDefault(s => s.SlotType == EquipmentSlotType.Pet);
        return petSlot?.ItemId ?? 0;
    }

    public ClientWizEquipmentBehavior GetClientBehaviorInstance() => new() {
        m_equipmentSets = new List<EquipmentSet>(),
        m_slotList = SlotList?.Select(slot => slot.GetClientTypeAlternative()).ToList(),
        m_itemList = EquippedItems?.ConvertAll(item => item as CoreObject),
        m_publicItemList = CharacterHelper.GetEquipmentList(this).m_infoList,
    };
    
}
