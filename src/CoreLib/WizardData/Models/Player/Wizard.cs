/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imlight.Common;
using Imlight.Common.Configuration;
using Imlight.Common.Cryptography;
using Imlight.Common.IO;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.Common.Utilities;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Implementations;
using Newtonsoft.Json;
using SharpDX;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Models.Player;

public enum MagicSchoolEnum {
    Fire = 2343174,
    Ice = 72777,
    Storm = 83375795,
    Life = 2330892,
    Myth = 2448141,
    Death = 78318724,
    Balance = 1027491821,
}

[Serializable]
public class Wizard : IDisposable {
    private const float OrientationCompressionFactor = CharacterHelper.OrientationCompressionFactor;

    public ulong AccountId { get; set; }               // <
    public ulong CharId { get; set; }                  //  | These values are never subject to change.
    public uint NameIndices { get; set; }              //  |
    public WideByteString NameOverride { get; set; }   // <
    public MagicSchoolEnum WizardSchool { get; set; }
    public byte Level { get; set; }
    public int TrainingPoints { get; set; }
    public int XpToNextLevel { get; set; }
    public string Zone { get; set; }
    public string ZoneDisplayName { get; set; }
    public byte World { get; set; }
    public Vector3 Location {
        get => this.GameObject?.m_location ?? _location;
        set {
            if (this.GameObject is not null) {
                this.GameObject.m_location = value;
            }
            else {
                _location = value;
            }
        }
    }
    public Vector3 Orientation {
        get => this.GameObject?.m_orientation ?? _orientation;
        set {
            if (this.GameObject is not null) {
                this.GameObject.m_orientation = value;
            }
            else {
                _orientation = value;
            }
        }
    }
    public WizardCharacterBehavior WizardAvatar { get; set; }
    public WizGameStats GameStats { get; set; }
    public List<ulong> InventoryItemIds { get; set; }
    public EquippedSlotInfo[] EquippedItems { get; set; }

    // Imlight doesn't disassociate the inventory from equipment. An equipped item is still in the inventory.
    [JsonIgnore] public List<WizClientObjectItem> InventoryItems { get; set; }
    [JsonIgnore] public WizClientObject GameObject;
    [JsonIgnore] public string GameServerIp;
    [JsonIgnore] public ushort GameServerPort;
    [JsonIgnore] public string QueuedZoneName;
    [JsonIgnore] public string QueuedZoneLocation;

    [JsonIgnore] private Vector3 _location;
    [JsonIgnore] private Vector3 _orientation;

    [JsonConstructor] public Wizard() {  }

    public Wizard(MagicSchoolEnum wizardSchoolType, WizardCharacterBehavior avatar, uint nameIndices, byte level = 1) {
        this.CharId = RandomGen.GenerateGUID();
        this.WizardSchool = wizardSchoolType;
        this.WizardAvatar = avatar;
        this.NameIndices = nameIndices;
        this.Level = level;
        this.Zone = ConfigurationManager.Settings.StartingZone;
        this.World = ConfigurationManager.Settings.StartingWorld;
        this.GameStats = new WizGameStats();

        InitializeDefaultInventory();
        InitializeEquipmentSlots();
    }

    public void SetLocation(Vector3 loc) {
        this.Location = loc;

        // Persistent save.
        CharacterCollection.UpdateCharacterLocation(this, loc, Orientation.Z);
    }

    public void SetOrientation(byte direction) {
        this.Orientation = new Vector3(0, 0, direction * OrientationCompressionFactor);

        // Persistent save.
        CharacterCollection.UpdateCharacterLocation(this, Location, Orientation.Z);
    }

    public void SetZone(string zone, string zoneDisplayName) {
        this.Zone = zone;
        this.ZoneDisplayName = zoneDisplayName;

        // Persistent save.
        CharacterCollection.UpdateCharacterZone(this, zone, zoneDisplayName);
    }

    public bool InventoryAddItem(WizClientObjectItem item) {
        if (item is null) {
            return false;
        }
        if (InventoryItems.Any(i => i.m_globalID == item.m_globalID)) {
            Logger.Error("Item with same global id {0} already exists in player inventory.", Logger.Args(item.m_globalID));
            return false;
        }

        InventoryItems.Add(item);

        // Persistent save.
        WorldItemCollection.AddItem(CharId, item);

        return true;
    }

    public bool InventoryRemoveItem(WizClientObjectItem item) {
        if (item is null) {
            return false;
        }
        if (!InventoryItems.Remove(item)) {
            Logger.Debug("Tried to remove item with global id {0} that does not exist in player inventory.", Logger.Args(item.m_globalID));
            return false;
        }

        // Persistent save.
        WorldItemCollection.RemoveItem(CharId, item);

        return true;
    }

    public bool InventoryRemoveItem(ulong itemId) {
        var item = InventoryItems.Find(i => i.m_globalID == itemId);
        if (item is null) {
            return false;
        }

        InventoryItems.Remove(item);

        // Persistent save.
        WorldItemCollection.RemoveItem(CharId, item);

        return true;
    }

    public bool InventoryHasItem(ulong itemId) {
        return InventoryItems.Any(i => i.m_globalID == itemId);
    }

    public WizClientObjectItem InventoryGetItem(ulong itemId) {
        return InventoryItems.Find(i => i.m_globalID == itemId);
    }

    public bool EquipmentHasEquippedItem(ulong itemId) {
        return EquippedItems.Any(i => i.m_itemID == itemId);
    }

    public int EquipmentGetItemSlotIndex(ulong itemId) {
        return EquippedItems.ToList().FindIndex(i => i.m_itemID == itemId);
    }

    public bool EquipmentEquipItem(ulong itemId) {
        if (!InventoryHasItem(itemId)) {
            Logger.Debug("Tried to equip item with global id {0} that does not exist in player inventory.", Logger.Args(itemId));
            return false;
        }

        var slot = GetAppropriateEquipmentSlotForItem(itemId);
        if (slot == -1) {
            Logger.Debug("Tried to equip item with global id {0} that does not have a slot name adjective.", Logger.Args(itemId));
            return false;
        }

        // Unequip the item currently in that slot.
        if (EquippedItems[slot].m_itemID != 0) {
            EquipmentUnequipItem(itemId);
        }

        // Set the item in the equipment list.
        EquippedItems[slot].m_itemID = (GID) itemId;

        var actualName = CharacterNameBank.GetEnglishName(NameIndices, WizardAvatar.m_eGender);
        Logger.Debug("{0} equips item in slot {1}.", Logger.Args(actualName, slot));

        return true;
    }

    public bool EquipmentUnequipItem(ulong itemId) {
        // Get the index of the item in the equipment list.
        var slot = GetAppropriateEquipmentSlotForItem(itemId);
        if (slot == -1) {
            Logger.Debug("Tried to unequip item with global id {0} that does not have a slot name adjective.", Logger.Args(itemId));
            return false;
        }

        RemoveSlotFromEquipmentSlotList(slot);

        var actualName = CharacterNameBank.GetEnglishName(NameIndices, WizardAvatar.m_eGender);
        Logger.Debug("{0} unequips item in slot {1}.", Logger.Args(actualName, slot));

        return true;
    }

    public void Dispose() {
        // If this object is being disposed, the player probably left the server.
        // Save the character's location to the database.
        CharacterCollection.UpdateCharacterLocation(this, Location, Orientation.Z);
    }

    private void InitializeDefaultInventory() {
        this.InventoryItems = new List<WizClientObjectItem>();
        this.InventoryItemIds = new List<ulong>();

        // Add default items to the inventory.
        var defaultItems = new List<WizClientObjectItem>();
        new List<ulong>() { 4740, 4705, 5030, 39068, 1363076, 1475149,
                            1472644, 1317133, 1317126, 1317234, 1359455,
                            1392077, 1352341, 87158, 87159, 87160, 1540397 }.ForEach(templateId => {
            var template = CoreObjectFactory.GetCoreTemplate(templateId);
            var coreObject = new WizClientObjectItem {
                m_globalID = RandomGen.GenerateGUID(),
                m_templateID = (GID) templateId
            };

            defaultItems.Add(coreObject);
            this.InventoryItemIds.Add(coreObject.m_globalID);
        });

        WorldItemCollection.AddDefaultItems(CharId, defaultItems);
    }

    private void InitializeEquipmentSlots() {
        // Initialize the equipment slots.
        // There is a slot for every EquipmentSlot enum value.
        var slotList = new List<EquippedSlotInfo>();
        foreach (var slot in Enum.GetValues(typeof(EquipmentSlot))) {
            // Get the name of the slot.
            var slotName = Enum.GetName(typeof(EquipmentSlot), slot);

            slotList.Add(new EquippedSlotInfo() {
                m_itemID = (GID) 0,
                m_itemSlotNameID = StringHash.Compute(slotName)
            });
        }

        this.EquippedItems = slotList.ToArray();
    }

    private int GetAppropriateEquipmentSlotForItem(ulong itemId) {
        // Get the template for the item. It will contain an adjective list that contains the slot name.
        // We can hash the slot name to get the slot index.
        var item = InventoryGetItem(itemId);
        var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(item.m_templateID);
        if (template.m_adjectiveList.Count < 2) {
            Logger.Debug("Tried to equip item with global id {0} that does not have a slot name adjective.", Logger.Args(itemId));
            return -1;
        }

        var slotName = template.m_adjectiveList[1]; // The second adjective is the slot name.
        var slotHash = StringHash.Compute(slotName);
        var slot = EquippedItems.ToList().FindIndex(i => i.m_itemSlotNameID == slotHash);
        return slot;
    }

    private void RemoveSlotFromEquipmentSlotList(int slot) {
        // Zero-out item from slot list and move all items down to fill "empty" zero slots, should they exist.
        var numEquippedItemsInSlots = EquippedItems.Count(slot => slot.m_itemID != 0);
        EquippedItems[slot].m_itemID = (GID) 0;

        if (slot < numEquippedItemsInSlots - 1) {
            for (int i = slot; i < numEquippedItemsInSlots; i++) {
                if (EquippedItems[i].m_itemID != 0) {
                    EquippedItems[i - 1].m_itemID = EquippedItems[i].m_itemID;
                    EquippedItems[i].m_itemID = (GID) 0;
                }
            }
        }
    }
}
