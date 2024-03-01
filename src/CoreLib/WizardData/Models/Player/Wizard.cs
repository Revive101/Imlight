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

[Serializable]
public class Wizard : IDisposable {
    private const float OrientationCompressionFactor = CharacterHelper.OrientationCompressionFactor;

    public ulong AccountId { get; set; }
    public ulong CharId { get; set; }
    public string Zone { get; set; }
    public string ZoneDisplayName { get; set; }
    public string MarkedZone { get; set; }
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

    public Vector3 MarkedLocation { get; set; }
    public Vector3 MarkedOrientation { get; set; }

    public WizardCharacterBehavior WizardAvatar { get; set; }
    public ServerWizPlayerNameBehavior PlayerNameBehavior { get; set; }
    public ServerWizInventoryBehavior InventoryBehavior { get; set; }
    public ServerWizEquipmentBehavior EquipmentBehavior { get; set; }
    public ServerMagicSchoolBehavior MagicSchoolBehavior { get; set; }
    public ServerSpellbookBehavior SpellbookBehavior { get; set; }
    public ServerMountOwnerBehavior MountOwnerBehavior { get; set; }
    public ServerWizGameStats GameStats { get; set; }

    [JsonIgnore] public Account Account;
    [JsonIgnore] public WizClientObject GameObject;
    [JsonIgnore] public List<GameEffectBase> GameEffects = new();
    [JsonIgnore] public string GameServerIp;
    [JsonIgnore] public ushort GameServerPort;
    [JsonIgnore] public string QueuedZoneName;
    [JsonIgnore] public string QueuedZoneLocation;

    [JsonIgnore] private Vector3 _location;
    [JsonIgnore] private Vector3 _orientation;
    [JsonIgnore] private readonly List<ulong> _defaultItems = new() {
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
        Zone = ConfigurationManager.Settings.StartingZone;
        World = ConfigurationManager.Settings.StartingWorld;

        // Do behaviors.
        WizardAvatar = avatar;
        InitializeDefaultInventory();
        InitializeDefaultEquipment();
        InitializePlayerName(nameIndices);
        InitializeMagicSchoolBehavior(wizardSchoolType, level);
        InitializeSpellbookBehavior();
        InitializeMountOwnerBehavior();
        InitializeWizardGameStats(wizardSchoolType, level);
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
        if (!MagicSchoolBehavior.SetLevel(level)) {
            return false;
        }

        GameStats.Level = level;

        // todo: fixme. We want to add/subtract rather than totally resetting the base. If we reset the base,
        // equipped items will not be recalculated.
        //CharacterHelper.SetBaseStats(level, MagicSchoolBehavior.MagicSchool);

        // Persistent save.
        WizardCollection.UpdateCharacterLevel(this);

        return true;
    }

    public void SetMarkedLocation(Vector3 loc, Vector3 orientation, string zone) {
        MarkedLocation = loc;
        MarkedOrientation = orientation;
        MarkedZone = zone;

        // Persistent save.
        WizardCollection.UpdateCharcterMarkedLocation(this, loc, orientation, zone);
    }

    public void SetMaxGold(int maxGold) {
        GameStats.m_baseGoldPouch = maxGold;

        // Persistent save.
        WizardCollection.UpdateCharacterGameStats(this);
    }

    public void AddGold(int gold) {
        if (GameStats.m_currentGold + gold > GameStats.m_baseGoldPouch) {
            GameStats.m_currentGold = GameStats.m_baseGoldPouch; // Do not exceed gold pouch.
        } else {
            GameStats.m_currentGold += gold;
        }

        // Persistent save.
        WizardCollection.UpdateCharacterGameStats(this);
    }

    public void RemovedGold(int gold) {
        GameStats.m_currentGold -= gold;

        // Persistent save.
        WizardCollection.UpdateCharacterGameStats(this);
    }

    public bool AddItemToInventory(ulong itemId, out WizClientObjectItem item) {
        item = (WizClientObjectItem) CoreObjectFactory.FinalizeCoreObject(itemId);
        item.m_characterId = (GID) CharId;

        if (item is null) {
            Logger.Warning("Cannot add item to inventory with ID {0} because that item does not exist.", Logger.Args(itemId));
            return false;
        }

        var success = InventoryBehavior.AddItem(item);
        if (!success) {
            Logger.Warning("Could not add item {0} to player {1}'s inventory.", Logger.Args(itemId, PlayerNameBehavior.GetWizardName()));
            item = null;
            return false;
        }

        // Persistent save.
        WizardItemCollection.AddItem(item);
        WizardCollection.UpdateCharacterItems(this);

        return true;
    }

    public bool RemoveItemFromInventory(ulong itemId) {
        var success = InventoryBehavior.RemoveItem(itemId, out var item);
        if (!success) {
            Logger.Warning("Could not remove item {0} from player {1}'s inventory.", Logger.Args(itemId, PlayerNameBehavior.GetWizardName()));
            return false;
        }

        // Persistent save.
        WizardCollection.UpdateCharacterItems(this);

        return true;
    }

    public bool InventoryToEquipmentTransfer(ulong itemId, out List<GameEffectBase> equipEffects, out List<GameEffectBase> unequipEffects) {
        equipEffects = null;
        unequipEffects = null;

        // Remove the item from the inventory.
        if (!InventoryBehavior.RemoveItem(itemId, out var inventoryItem)) {
            Logger.Warning("Tried to equip item with global id {0} that does not exist in player inventory.", Logger.Args(itemId));
            return false;
        }

        // Get the template for this item. Using this template we can get the slot this object should be on.
        var template = ItemHelper.GetItemTemplate(inventoryItem);
        var slot = ItemHelper.GetItemSlot(template);

        // Get the item that is currently in the slot, if there is one. We want to remove its effects.
        var replacedItem = EquipmentBehavior.GetItemInSlot(slot.SlotType);
        if (replacedItem != null) {
            if (!EquipmentToInventoryTransfer(replacedItem.m_globalID, out unequipEffects)) {
                Logger.Warning("Could not replace item {0} from slot {1}.", Logger.Args(replacedItem.m_globalID, slot.SlotType));
                return false;
            }
        }

        // Add the item to the equipment.
        var equipResult = EquipmentBehavior.EquipItem(inventoryItem, slot.SlotType);
        if (!equipResult) {
            Logger.Warning("Tried to equip item with global id {0} that is already equipped.", Logger.Args(itemId));
            return false;
        }

        // If this object is a mount, we'll also want to update the mount owner behavior.
        if (slot.SlotType == EquipmentSlotType.Mount) {
            var mountEquipSuccess = MountOwnerBehavior.EquipMount(template, inventoryItem);
            if (!mountEquipSuccess) {
                Logger.Warning("Could not equip mount {0} to player {1}.", Logger.Args(template.m_objectName, PlayerNameBehavior.GetWizardName()));
                return false;
            }

            // Persistent save.
            WizardCollection.UpdateCharacterMount(this);
        }

        // Persistent save.
        WizardCollection.UpdateCharacterItems(this);

        // Debug log.
        Logger.Debug("{0} equips item {1}", Logger.Args(PlayerNameBehavior.GetWizardName(), itemId));

        equipEffects = CharacterEffectHelper.AddEffectsToWizard(this, template);
        return true;
    }

    public bool EquipmentToInventoryTransfer(ulong itemId, out List<GameEffectBase> unequipEffects) {
        unequipEffects = null;

        // Get the actual item. We'll also grab the template to remove the effects from the wizard.
        var item = EquipmentBehavior.EquippedItems.FirstOrDefault(i => i.m_globalID == itemId);
        var template = ItemHelper.GetItemTemplate(item);
        var slot = ItemHelper.GetItemSlot(template);

        // Remove the item from the equipment.
        var unequipResult = EquipmentBehavior.UnequipItem(itemId);
        if (!unequipResult) {
            Logger.Warning("Tried to unequip item with global id {0} that is not equipped.", Logger.Args(itemId));
            return false;
        }

        // Add the item to the inventory.
        var invAddResult = InventoryBehavior.AddItem(item);
        if (!invAddResult) {
            Logger.Warning("Tried to add item with global id {0} to inventory, but it already exists.", Logger.Args(itemId));
            return false;
        }

        // If this object is a mount, we'll also want to update the mount owner behavior.
        if (slot.SlotType == EquipmentSlotType.Mount) {
            MountOwnerBehavior.UnequipMount();

            // Persistent save.
            WizardCollection.UpdateCharacterMount(this);
        }

        // Persistent save.
        WizardCollection.UpdateCharacterItems(this);

        // Debug log.
        Logger.Debug("{0} unequips item {1}", Logger.Args(PlayerNameBehavior.GetWizardName(), itemId));

        unequipEffects = CharacterEffectHelper.RemoveEffectsFromWizard(this, template);
        return true;
    }

    public void SetNameOverride(string newName) {
        PlayerNameBehavior.NameOverride = newName;

        // Persistent save.
        WizardCollection.UpdateCharacterNameOverride(this);
    }

    internal void RefurbishReferences() {
        GameStats.Level = MagicSchoolBehavior.Level;
        GameStats.MagicSchool = MagicSchoolBehavior.MagicSchool;
    }

    private void InitializeDefaultInventory() {
        InventoryBehavior = new ServerWizInventoryBehavior {
            Items = new List<WizClientObjectItem>(),
            InventoryItemIds = new List<ulong>()
        };

        // Add default items to the inventory.
        var itemsToAdd = new List<WizClientObjectItem>();
        _defaultItems.ForEach(templateId => {
            var cObj = (WizClientObjectItem) CoreObjectFactory.FinalizeCoreObject(templateId);
            cObj.m_characterId = (GID) CharId;

            itemsToAdd.Add(cObj);
            InventoryBehavior.InventoryItemIds.Add(cObj.m_globalID);
        });

        // This is a different method that bulk uploads items to the database.
        var success = WizardItemCollection.AddDefaultItems(itemsToAdd);
        if (!success) {
            Logger.Error("Could not add default items for Wizard {0} to database.", Logger.Args(CharId));
        }
    }

    private void InitializeDefaultEquipment() {
        EquipmentBehavior = new ServerWizEquipmentBehavior {
            SlotList = new List<EquipmentSlot>(),
            EquippedItemIds = new List<ulong>(),
            EquippedItems = new List<WizClientObjectItem>(),
        };
    }

    private void InitializePlayerName(uint nameIndices) {
        PlayerNameBehavior = new ServerWizPlayerNameBehavior {
            NameIndices = nameIndices,
            UseRank = false,
            Gender = WizardAvatar.m_eGender,
            Race = WizardAvatar.m_eRace,
            ChatPermissions = 0,
            PvpIconId = 0,
            LocaleId = 0,
            FriendlyPlayer = true,
            Volunteer = false,
            GuildName = 0,
        };
    }

    private void InitializeMagicSchoolBehavior(MagicSchool school, byte level) {
        MagicSchoolBehavior = new ServerMagicSchoolBehavior {
            MagicSchool = school,
            ExperiencePoints = 0,
            Level = level,
            TrainingPoints = 0,
            OverflowXp = 0,
            LevelIsLocked = 0,
            EquippedTeleportEffect = 0,
        };
    }

    private void InitializeSpellbookBehavior() {
        SpellbookBehavior = new ServerSpellbookBehavior {
            SpellIdList = new List<SpellIDTracker>()
        };
    }

    private void InitializeMountOwnerBehavior() {
        MountOwnerBehavior = new ServerMountOwnerBehavior();
    }

    private void InitializeWizardGameStats(MagicSchool school, byte level) {
        GameStats = new ServerWizGameStats(school, level);
        CharacterHelper.RecalculateGameStats(this);

        GameStats.m_currentHitpoints = GameStats.m_baseHitpoints;
        GameStats.m_currentMana = GameStats.m_baseMana;
    }

    public void Dispose() =>
        // If this object is being disposed, the player probably left the server.
        // Save the character's location to the database.
        WizardCollection.UpdateCharacterLocation(this, Location, Orientation.Z);
}
