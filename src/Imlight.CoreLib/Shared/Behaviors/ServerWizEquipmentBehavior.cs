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

using Imcodec.CoreObject;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Shared.Character;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerWizEquipmentBehavior : IClientBehaviorProvider<ClientWizEquipmentBehavior> {
    private static readonly int s_maxActiveElixirs
        = ConfigurationManager.Settings["Character.MaxActiveElixirs"].AsInt();

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

        if (slot is not null) {
            SlotList.Remove(slot);
        }
         
        if (item is not null) {
            EquippedItems.Remove(item);
        }

        EquippedItemIds.Remove(itemId);

        return true;
    }

    public bool HasItemEquipped(ulong itemId) => 
        (EquippedItems is not null && EquippedItems.Any(item => item.m_globalID == itemId))
        || (SlotList is not null && SlotList.Any(item => item.ItemId.Value == itemId))
        || (EquippedItemIds is not null && EquippedItemIds.Contains(itemId));

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
        => EquippedItems?.FirstOrDefault(item => item.m_globalID == globalId);

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

    /// <summary>
    /// Enumerates all active elixir slots and their associated item instances.
    /// </summary>
    public IEnumerable<(EquipmentSlot Slot, WizClientObjectItem Item)> GetActiveElixirs() {
        if (SlotList is null) {
            yield break;
        }

        foreach (var slot in SlotList.Where(s => s.SlotType == EquipmentSlotType.Elixir)) {
            var item = GetItem(slot.ItemId.Value);
            if (item is not null) {
                yield return (slot, item);
            }
        }
    }

    /// <summary>
    /// Synchronizes the runtime ClientElixirBehavior expire time with each elixir's persisted remaining seconds.
    /// </summary>
    public void SyncActiveElixirTimes() {
        foreach (var (slot, item) in GetActiveElixirs()) {
            if (CoreObjectFactory.FindBehaviorInstance<ClientElixirBehavior>(item, out var clientElixir)) {
                clientElixir.m_expireTime = slot.RemainingSeconds;
            }
        }
    }

    public bool CanEquipElixir(ElixirBehaviorTemplate template) {
        var activeCount = SlotList?.Count(s => s.SlotType == EquipmentSlotType.Elixir) ?? 0;
        if (activeCount >= s_maxActiveElixirs) {
            return false;
        }

        if (template?.m_typeList is { Count: > 0 }) {
            foreach (var (_, activeItem) in GetActiveElixirs()) {
                var activeTemplate = ItemHelper.GetItemTemplate(activeItem);
                var activeElixirTemplate = activeTemplate?.m_behaviors?.OfType<ElixirBehaviorTemplate>().FirstOrDefault();
                if (activeElixirTemplate?.m_typeList is not null
                    && activeElixirTemplate.m_typeList.Any(template.m_typeList.Contains)) {
                    return false;
                }
            }
        }

        return true;
    }

    public bool EquipElixir(WizClientObjectItem item, uint durationSeconds) {
        var itemId = item.m_globalID;

        if (HasItemEquipped(itemId)) {
            return false;
        }

        var newSlot = new EquipmentSlot {
            SlotType = EquipmentSlotType.Elixir,
            ItemName = item.m_debugName,
            ItemId = (GID) itemId,
            EquippedSince = DateTime.UtcNow,
            RemainingSeconds = durationSeconds
        };

        SlotList ??= [];
        EquippedItemIds ??= [];
        EquippedItems ??= [];

        SlotList.Add(newSlot);
        EquippedItemIds.Add(itemId);
        EquippedItems.Add(item);

        return true;
    }

    public ClientWizEquipmentBehavior GetClientBehaviorInstance() => new() {
        m_equipmentSets = new List<EquipmentSet>(),
        m_slotList = SlotList?.Select(slot => slot.GetClientTypeAlternative()).ToList(),
        m_itemList = EquippedItems?.ConvertAll(item => item as CoreObject),
        m_publicItemList = CharacterHelper.GetEquipmentList(this).m_infoList,
    };
    
}
