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
using Imlight.CoreLib.Game;
using Newtonsoft.Json;
using SharpDX;
using static Imlight.Common.Caches.TypeCache;
using Imlight.CoreLib.Game.Effects;

namespace Imlight.CoreLib.WizardData.Models.Player;

public enum MagicSchool {
    Ice = 72777,
    Life = 2330892,
    Fire = 2343174,
    Myth = 2448141,
    Death = 78318724,
    Storm = 83375795,
    Balance = 1027491821,
}

[Serializable]
public class Wizard : IDisposable {
    private const float OrientationCompressionFactor = CharacterHelper.OrientationCompressionFactor;

    public ulong AccountId { get; set; }               // <
    public ulong CharId { get; set; }                  //  | These values are never subject to change.
    public uint NameIndices { get; set; }              //  |
    public WideByteString NameOverride { get; set; }   // <
    public MagicSchool WizardSchool { get; set; }
    public byte Level { get; set; }
    public int TrainingPoints { get; set; }
    public int XpToNextLevel { get; set; }
    public string Zone { get; set; }
    public string ZoneDisplayName { get; set; }
    public byte World { get; set; }
    public Vector3 Location {
        get => GameObject?.m_location ?? _location;
        set {
            if (GameObject is not null) {
                GameObject.m_location = value;
            }
            else {
                _location = value;
            }
        }
    }
    public Vector3 Orientation {
        get => GameObject?.m_orientation ?? _orientation;
        set {
            if (GameObject is not null) {
                GameObject.m_orientation = value;
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
    [JsonIgnore] public List<GameEffectBase> GameEffects = new();
    [JsonIgnore] public string GameServerIp;
    [JsonIgnore] public ushort GameServerPort;
    [JsonIgnore] public string QueuedZoneName;
    [JsonIgnore] public string QueuedZoneLocation;

    [JsonIgnore] private Vector3 _location;
    [JsonIgnore] private Vector3 _orientation;
    [JsonIgnore] private List<ulong> _defaultItems = new() {
        // Quality assurance hats, 05-10-25-50-100
        1317127, 1317128, 1317125, 1317124, 1317126,

        // Quality assurance robes, 05-10-25-50-100
        1317129, 1317130, 1317131, 1317132, 1317133,

        // Quality assurance boots, 100% speed bost
        1317234,

        // Weapons, each of different animation
        87256,   // Antiquated Wand (starting wand)
        1456120, // Celebration Staff

        180047, // Conumdrum Blade
        4672,   // Storm Slitherer Gem
        4757,   // Flawed Opal Band
        126412, // Black Cat Pet
        284071, // Swift Gryphon (PERM)
        126983, // Starter Deck
    };

    // Constructor: Used for deserialization. If this is not present, the default constructor will be used.
    [JsonConstructor]
    public Wizard() { }

    // Constructor: Used for character creation.
    public Wizard(MagicSchool wizardSchoolType, WizardCharacterBehavior avatar, uint nameIndices, byte level = 1) {
        CharId = RandomGen.GenerateGUID();
        WizardSchool = wizardSchoolType;
        WizardAvatar = avatar;
        NameIndices = nameIndices;
        Level = level;
        Zone = ConfigurationManager.Settings.StartingZone;
        World = ConfigurationManager.Settings.StartingWorld;
        GameStats = new WizGameStats();

        InitializeDefaultInventory();
        InitializeDefaultEquipmentSlots();
    }

    public void SetCachedLocation(Vector3 loc) => Location = loc;

    public void SetCachedOrientation(byte direction) => Orientation = new Vector3(0, 0, direction * OrientationCompressionFactor);

    public void SetPersistentLocation(Vector3 loc) {
        Location = loc;

        // Persistent save.
        WizardCollection.UpdateCharacterLocation(this, loc, Orientation.Z);
    }

    public void SetPersistentOrientation(byte direction) {
        Orientation = new Vector3(0, 0, direction * OrientationCompressionFactor);

        // Persistent save.
        WizardCollection.UpdateCharacterLocation(this, Location, Orientation.Z);
    }

    public void SetZone(string zone, string zoneDisplayName) {
        Zone = zone;
        ZoneDisplayName = zoneDisplayName;

        // Persistent save.
        WizardCollection.UpdateCharacterZone(this, zone, zoneDisplayName);
    }

    public bool SetLevel(byte level) {
        if (level > ConfigurationManager.Settings.MaxLevel) {
            Logger.Warning("Tried to set character {0} level to {1}, which is past max level.", Logger.Args(CharId, level));
            return false;
        }

        Level = level;

        // Persistent save.
        WizardCollection.UpdateCharacterLevel(this);

        return true;
    }

    // todo: Regions are a sign of a class becoming monolithic.
    #region Inventory

    public bool InventoryAddItem(WizClientObjectItem item) {
        if (item is null) {
            throw new NullReferenceException("Item cannot be null.");
        }
        if (InventoryHasItem(item.m_globalID)) {
            Logger.Error("Item with same global id {0} already exists in player inventory.", Logger.Args(item.m_globalID));
            return false;
        }

        item.m_characterId = (GID) CharId;
        InventoryItems.Add(item);
        InventoryItemIds.Add(item.m_globalID);

        // Persistent save.
        var persistentSaveSucceeded = WizardItemCollection.AddItem(item);
        if (!persistentSaveSucceeded) {
            Logger.Error("Could not save item with global id {0} to database.", Logger.Args(item.m_globalID));
            return false;
        }

        return true;
    }

    public bool InventoryRemoveItem(ulong itemId) {
        // Get the actual item from the inventory.
        var item = InventoryItems.Find(i => i.m_globalID == itemId);
        if (item is null) {
            Logger.Debug("Tried to remove item with global id {0} that does not exist in player inventory.",
                Logger.Args(itemId));
            return false;
        }

        return InventoryRemoveItem(item);
    }

    public bool InventoryRemoveItem(WizClientObjectItem item) {
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

    public bool InventoryHasItem(ulong itemId) => InventoryItems.Any(i => i.m_globalID == itemId);

    public WizClientObjectItem InventoryGetItem(ulong itemId) => InventoryItems.Find(i => i.m_globalID == itemId);

    private void InitializeDefaultInventory() {
        InventoryItems = new List<WizClientObjectItem>();
        InventoryItemIds = new List<ulong>();

        // Add default items to the inventory.
        var itemsToAdd = new List<WizClientObjectItem>();
        _defaultItems.ForEach(templateId => {
            var coreObject = new WizClientObjectItem {
                m_globalID = RandomGen.GenerateGUID(),
                m_templateID = (GID) templateId,
                m_characterId = (GID) CharId
            };

            itemsToAdd.Add(coreObject);
            InventoryItemIds.Add(coreObject.m_globalID);
        });

        // This is a different method that bulk uploads items to the database.
        var success = WizardItemCollection.AddDefaultItems(itemsToAdd);
        if (!success) {
            Logger.Error("Could not add default items for Wizard {0} to database.", Logger.Args(CharId));
        }
    }

    #endregion

    #region Equipment

    public List<GameEffectBase> EquipmentEquipItem(ulong itemId) {
        // These validations also occur in the EquipmentService, where they are properly dealt with.
        // They must also happen here, as the Wizard does not keep track of the equipment items, only their IDs.
        if (EquipmentHasEquippedItem(itemId)) {
            Logger.Warning("Tried to equip item with global id {0} that is already equipped.", Logger.Args(itemId));
            return null;
        }

        // We're still dealing with just an item ID, so we need to get the actual item from the inventory.
        var item = InventoryGetItem(itemId);
        if (item is null) {
            Logger.Warning("Tried to equip item with global id {0} that does not exist in player inventory.", Logger.Args(itemId));
            return null;
        }

        // Get the slot name hash of the item.
        var slotNameHash = ItemHelper.GetItemSlotHash(item);
        if (slotNameHash == 0) {
            Logger.Warning("Tried to equip item with global id {0} that does not have a slot name adjective.", Logger.Args(itemId));
            return null;
        }

        // Get the slot info for the slot we want to place this item in.
        var slot = GetEquipmentSlotInfo(slotNameHash);
        if (slot is null) {
            Logger.Warning("Could not get slot info for item {0}.", Logger.Args(itemId));
            return null;
        }

        // Clear the slot and set.
        ClearEquipmentSlot(slotNameHash);
        SetEquipmentSlot(slotNameHash, itemId);

        // Persistent save.
        // The equipped items array is a fairly small binary, so we can just save the whole thing.
        WizardCollection.UpdateCharacterEquipment(this);

        // Update the actual behavior.
        WizardObjectLoader.SetEquipmentBehavior(GameObject, this);

        // Apply effects.
        var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(item.m_templateID);
        var effects = AddEffectsFromTemplate(template);

        // Debug log.
        var actualName = WizardNameBank.GetEnglishName(NameIndices, WizardAvatar.m_eGender);
        Logger.Debug("{0} equips item in slot {1}.", Logger.Args(actualName, slot.m_itemSlotNameID));

        return effects;
    }

    public List<GameEffectBase> EquipmentUnequipItem(ulong itemId) {
        // These validations also occur in the EquipmentService, where they are properly dealt with.
        // They must also happen here, as the Wizard does not keep track of the equipment items, only their IDs.
        if (!EquipmentHasEquippedItem(itemId)) {
            Logger.Warning("Tried to unequip item with global id {0} that is not equipped.", Logger.Args(itemId));
            return null;
        }

        // We're still dealing with just an item ID, so we need to get the actual item from the inventory.
        var item = InventoryGetItem(itemId);
        if (item is null) {
            Logger.Warning("Tried to equip item with global id {0} that does not exist in player inventory.", Logger.Args(itemId));
            return null;
        }

        // Get the slot name hash of the item.
        var slotNameHash = ItemHelper.GetItemSlotHash(item);
        if (slotNameHash == 0) {
            Logger.Warning("Tried to equip item with global id {0} that does not have a slot name adjective.", Logger.Args(itemId));
            return null;
        }

        // Get the slot info for the slot we want to remove from.
        var slot = GetEquipmentSlotInfo(slotNameHash);
        if (slot is null) {
            Logger.Warning("Could not get slot info for item {0}.", Logger.Args(itemId));
            return null;
        }

        ClearEquipmentSlot(slotNameHash);

        // Persistent save.
        // The equipped items array is a fairly small binary, so we can just save the whole thing.
        WizardCollection.UpdateCharacterEquipment(this);

        // Update the actual behavior.
        WizardObjectLoader.SetEquipmentBehavior(GameObject, this);

        // Remove effects.
        var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(item.m_templateID);
        var effects = RemoveEffectsFromTemplate(template);

        // Debug log.
        var actualName = WizardNameBank.GetEnglishName(NameIndices, WizardAvatar.m_eGender);
        Logger.Debug("{0} unequips item in slot {1}.", Logger.Args(actualName, slot.m_itemSlotNameID));

        return effects;
    }

    public bool EquipmentHasEquippedItem(ulong itemId) => EquippedItems.Any(i => i.m_itemID == itemId);

    public IEnumerable<(WizClientObjectItem, WizItemTemplate)> EquipmentGetAllItems() {
        var items = new List<(WizClientObjectItem, WizItemTemplate)>();
        foreach (var slot in EquippedItems) {
            if (slot.m_itemID != 0) {
                var item = InventoryGetItem(slot.m_itemID);
                if (item is not null) {
                    var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(item.m_templateID);
                    items.Add((item, template));
                }
            }
        }

        return items;
    }

    public byte EquipmentGetItemSlotIndex(ulong itemId) {
        var equippedItemsWithIds = EquippedItems
            .Where(i => i.m_itemID != 0)
            .ToList();

        int index = equippedItemsWithIds.FindIndex(i => i.m_itemID == (GID) itemId);

        return (byte) index;
    }

    public void ApplyEffectsForAllEquipment() {
        var actualWizardName = WizardNameBank.GetEnglishName(NameIndices, WizardAvatar.m_eGender);
        Logger.Debug("{0} is applying effects for all equipment.", Logger.Args(actualWizardName));

        foreach (var item in EquipmentGetAllItems()) {
            var template = item.Item2;

            var activatedEffects = AddEffectsFromTemplate(template);
            Logger.Debug("{0} Applied {1} effects for item {2}.",
                Logger.Args(actualWizardName, activatedEffects.Count, template.m_objectName));
        }
    }

    private void InitializeDefaultEquipmentSlots() {
        // Initialize the equipment slots.
        // There is a slot for every EquipmentSlot enum value.
        var slotList = new List<EquippedSlotInfo>();
        for (uint i = 0; i < Enum.GetValues(typeof(EquipmentSlot)).Length; i++) {
            // Get the name of the slot.
            var slotName = Enum.GetName(typeof(EquipmentSlot), i);

            slotList.Add(new EquippedSlotInfo() {
                m_itemID = (GID) 0,
                m_itemSlotNameID = StringHash.Compute(slotName)
            });
        }

        EquippedItems = slotList.ToArray();
    }

    private EquippedSlotInfo GetEquipmentSlotInfo(uint slotNameHash)
        => EquippedItems.FirstOrDefault(i => i.m_itemSlotNameID == slotNameHash);

    private void ClearEquipmentSlot(uint slotNameHash) {
        var slot = GetEquipmentSlotInfo(slotNameHash);
        if (slot is null) {
            throw new Exception($"Could not get slot info for slot name hash {slotNameHash}.");
        }

        slot.m_itemID = (GID) 0;
    }

    private void SetEquipmentSlot(uint slotNameHash, ulong itemId) {
        var slot = GetEquipmentSlotInfo(slotNameHash);
        if (slot is null) {
            throw new Exception($"Could not get slot info for slot name hash {slotNameHash}.");
        }

        slot.m_itemID = (GID) itemId;
    }

    #endregion

    #region Game Effects

    private List<GameEffectBase> AddEffectsFromTemplate(WizItemTemplate template) {
        var addedEffects = new List<GameEffectBase>();
        var slotHash = ItemHelper.GetItemSlotHash(template);

        // Apply the effects from the template.
        foreach (var effectInfo in template.m_equipEffects) {
            var gameEffect = GameEffectFactory.CreateEffectFromInfo(effectInfo, slotHash);
            gameEffect.m_internalID = GameEffects.Count;

            if (gameEffect is WizStatisticEffect canonicalEffect) {
                var canonicalEffectName = CanonicalStatEffects.GetEffectTemplate(effectInfo.m_effectName).m_effectName;
                CharacterEffectHelper.AddGameEffectToStats(this.GameStats, canonicalEffectName, canonicalEffect);
            }

            addedEffects.Add(gameEffect);
            GameEffects.Add(gameEffect);
        }

        return addedEffects;
    }

    private List<GameEffectBase> RemoveEffectsFromTemplate(WizItemTemplate template) {
        var removedEffects = new List<GameEffectBase>();
        var slotHash = ItemHelper.GetItemSlotHash(template);

        // Apply the effects from the template.
        foreach (var effectInfo in template.m_equipEffects) {
            // Find the effect in the player's list of effects.
            var nameHash = StringHash.Compute(effectInfo.m_effectName);
            var gameEffect = GameEffects.Find(e => e.m_effectNameID == nameHash && e.m_itemSlotID == slotHash);
            if (gameEffect is null) {
                Logger.Warning("Could not find effect {0} in player's list of effects.", Logger.Args(effectInfo.m_effectName));
                continue;
            }

            removedEffects.Add(gameEffect);

            // Remove the effect.
            GameEffects.Remove(gameEffect);

            if (gameEffect is WizStatisticEffect canonicalEffect) {
                var canonicalEffectName = CanonicalStatEffects.GetEffectTemplate(effectInfo.m_effectName).m_effectName;
                CharacterEffectHelper.RemoveGameEffectFromStats(this.GameStats, canonicalEffectName, canonicalEffect);
            }
        }

        return removedEffects;
    }

    #endregion

    public void Dispose() =>
        // If this object is being disposed, the player probably left the server.
        // Save the character's location to the database.
        WizardCollection.UpdateCharacterLocation(this, Location, Orientation.Z);
}
