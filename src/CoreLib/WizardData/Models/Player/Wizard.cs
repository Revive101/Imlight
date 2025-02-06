/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using SharpDX;
using Newtonsoft.Json;
using Imlight.Common;
using Imlight.Common.Utilities;
using Imlight.Common.Configuration;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Game.Effects;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Character;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Implementations;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Models.Player;

[Serializable]
public class Wizard : IDisposable {
    public ulong AccountId { get; set; }
    public ulong CharId { get; set; }
    public string Zone { get; set; }
    public string ZoneDisplayName { get; set; }
    public string MarkedZone { get; set; }
    public long TimeHomeLastClicked { get; set; }
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
    public ServerWizSpellbookBehavior SpellbookBehavior { get; set; }
    public ServerMountOwnerBehavior MountOwnerBehavior { get; set; }
    public ServerPetSnackBehavior PetSnackBehavior { get; set; }
    public ServerAlchemyBehavior AlchemyBehavior { get; set; }
    [JsonIgnore] public ServerObjectStateBehavior ObjectStateBehavior { get; set; }
    public ServerWizGameStats GameStats { get; set; }
    public ServerPetOwnerBehavior PetOwnerBehavior { get; set; }

    [JsonIgnore] public Account Account;
    [JsonIgnore] public WizClientObject GameObject;
    [JsonIgnore] public List<GameEffectBase> GameEffects = new();
    [JsonIgnore] public string GameServerIp;
    [JsonIgnore] public ushort GameServerPort;
    [JsonIgnore] public string QueuedZoneName;
    [JsonIgnore] public string QueuedZoneLocation;
    [JsonIgnore] internal DynamodSet DynamodSet { get; set; }

    [JsonIgnore] private Vector3 _location;
    [JsonIgnore] private Vector3 _orientation;
    [JsonIgnore] private readonly List<ulong> _defaultItems = new() {
        // warning: do not exceed 16 items! RavenDB has a batch limit of 16 items.
        // Quality assurance hats, 05-10-25-50-100
        1317127, 1317128, 1317125, 1317124, 1317126,

        // Quality assurance robes, 05-10-25-50-100
        1317129, 1317130, 1317131, 1317132, 1317133,

        // Quality assurance boots, 100% speed bost
        1317234,

        // Weapons, each of different animation
        87256,   // Antiquated Wand (starting wand)
        1456120, // Celebration Staff

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
        InitializeDefaultPetSnackBehavior();
        InitializePetOwnerBehavior();
        InitializeAlchemyBehavior();

        ObjectStateBehavior = new ServerObjectStateBehavior("PlayerMobileStates");

        DynamodSet = new DynamodSet(CharId);
        DynamodCollection.AddDynamodSet(DynamodSet);
    }

    public void SetCachedLocation(Vector3 loc) => Location = loc;

    public void SetCachedOrientation(float direction) => Orientation = new Vector3(0, 0, direction);

    public void SetPersistentLocation(Vector3 loc) {
        Location = loc;

        // Persistent save.
        WizardCollection.UpdateCharacterLocation(this, loc, Orientation.Z);
    }

    public void SetPersistentOrientation(float orientation) {
        Orientation = new Vector3(0, 0, orientation);

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
        var school = MagicSchoolBehavior.MagicSchool;
        var currentLevel = MagicSchoolBehavior.Level;
        var oldBaseStats = MagicLevelsConfig.GetPlayerLevelInfo(school, currentLevel);

        MagicSchoolBehavior.Level = level;
        GameStats.Level = level;

        var newBaseStats = MagicLevelsConfig.GetPlayerLevelInfo(school, level);
        var healthDifference = newBaseStats.m_hitpoints - oldBaseStats.m_hitpoints;
        var manaDifference = newBaseStats.m_mana - oldBaseStats.m_mana;
        var powerPipDifference = newBaseStats.m_pipChance - oldBaseStats.m_pipChance;

        GameStats.m_baseHitpoints += healthDifference;
        GameStats.m_baseMana += manaDifference;
        GameStats.m_powerPipBase += powerPipDifference;

        // Persistent save.
        WizardCollection.UpdateCharacterLevel(this);

        return true;
    }

    public void SetMarkedLocation(Vector3 loc, Vector3 orientation, string zone) {
        MarkedLocation = loc;
        MarkedOrientation = orientation;
        MarkedZone = zone;

        // Persistent save.
        WizardCollection.UpdateCharacterMarkedLocation(this, loc, orientation, zone);
    }

    public void SetTimeHomeLastClicked(long time) {
        TimeHomeLastClicked = time;

        WizardCollection.UpdateCharacterTimeWentHome(this, time);
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

    public void RemoveGold(int gold) {
        GameStats.m_currentGold -= gold;

        // Persistent save.
        WizardCollection.UpdateCharacterGameStats(this);
    }

    public void UpdateHealth(int newHealth) {
        GameStats.m_currentHitpoints = newHealth;

        // Persistent save.
        WizardCollection.UpdateCharacterGameStats(this);
    }

    public void UpdateMaxHealth(int newMaxHealth) {
        GameStats.m_baseHitpoints = newMaxHealth;

        // Persistent save.
        WizardCollection.UpdateCharacterGameStats(this);
    }

    public void UpdateMana(int newMana) {
        GameStats.m_currentMana = newMana;

        // Persistent save.
        WizardCollection.UpdateCharacterGameStats(this);
    }

    public void UpdateEnergy(int newEnergy) {
        PetOwnerBehavior.SetEnergy(newEnergy);

        // Persistent save.
        WizardCollection.UpdateCharacterPetOwnerBehavior(this);
    }

    public void UpdateMaxMana(int newMaxMana) {
        GameStats.m_baseMana = newMaxMana;

        // Persistent save.
        WizardCollection.UpdateCharacterGameStats(this);
    }

    public void UpdateCantripLevel(byte newCantripLevel) {
        GameStats.m_cantripLevel = newCantripLevel;

        // Persistent save.
        WizardCollection.UpdateCharacterGameStats(this);
    }

    public void UpdateTrainingPoints(int newTrainingPoints) {
        MagicSchoolBehavior.TrainingPoints = newTrainingPoints;

        // Persistent save.
        WizardCollection.UpdateCharacterTrainingPoints(this);
    }

    public bool AddItemToInventory(ulong itemId, out WizClientObjectItem item) {
        item = (WizClientObjectItem) CoreObjectFactory.FinalizeCoreObject(itemId);
        item.m_characterId = (GID) CharId;

        return AddItemToInventory(item);
    }

    public bool AddItemToInventory(WizClientObjectItem item) {
        if (item is null) {
            Logger.Warning("Cannot add item to inventory because that item does not exist.");
            return false;
        }

        CoreObjectFactory.InitializeCoreObjectBehaviors(item, item.m_templateID);

        // Ensure that the item is associated with this Wizard.
        item.m_characterId = (GID) CharId;

        var success = InventoryBehavior.AddItem(item);
        if (!success) {
            Logger.Warning("Could not add item {0} to player {1}'s inventory.",
                Logger.Args(item.m_globalID, PlayerNameBehavior.GetWizardName()));
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
            EquipMount(template, inventoryItem);
        }
        if (slot.SlotType == EquipmentSlotType.Deck) {
            InformSpellbookOfNewDeck(template, inventoryItem.m_globalID);
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
            UnequipMount();
        }

        // Persistent save.
        WizardCollection.UpdateCharacterItems(this);

        // Debug log.
        Logger.Debug("{0} unequips item {1}", Logger.Args(PlayerNameBehavior.GetWizardName(), itemId));

        unequipEffects = CharacterEffectHelper.RemoveEffectsFromWizard(this, template);
        return true;
    }

    public bool AddSnack(ulong snackTemplateId, out ClientPetSnackItem snackObj) {
        if (PetSnackBehavior.HasSnack(snackTemplateId)) {
            snackObj = PetSnackBehavior.GetSnack(snackTemplateId);
        } else {
            snackObj = (ClientPetSnackItem) CoreObjectFactory.FinalizeCoreObject(snackTemplateId);
            snackObj.m_characterId = (GID) CharId;
            snackObj.m_quantity = 1;
        }

        return AddSnack(snackObj);
    }

    public bool AddSnack(ClientPetSnackItem snack) {
        if (snack is null) {
            Logger.Warning("Cannot add snack to snack bag because that snack does not exist.");
            return false;
        }

        CoreObjectFactory.InitializeCoreObjectBehaviors(snack, snack.m_templateID);

        // Ensure that the item is associated with this Wizard.
        snack.m_characterId = (GID) CharId;

        var success = PetSnackBehavior.AddSnack(snack);
        if (!success) {
            Logger.Warning("Could not add snack {0} to player {1}'s snackbag.",
                Logger.Args(snack.m_globalID, PlayerNameBehavior.GetWizardName()));
            return false;
        }

        if (snack.m_quantity > 1) {
            // Persistent save.
            WizardPetSnackCollection.UpdateSnack(snack);
            WizardCollection.UpdateCharacterItems(this);
            return true;
        }

        // Persistent save.
        WizardPetSnackCollection.AddSnack(snack);
        WizardCollection.UpdateCharacterItems(this);
        return true;
    }

    public bool RemoveSnack(ulong globalId, out ClientPetSnackItem snack) {
        PetSnackBehavior.RemoveSnack(globalId, out snack);

        if (snack.m_quantity <= 0) {
            // Persistent save.
            WizardPetSnackCollection.RemoveSnack(snack);
            WizardCollection.UpdateCharacterItems(this);
            return true;
        }

        // Persistent save.
        WizardPetSnackCollection.UpdateSnack(snack);
        WizardCollection.UpdateCharacterItems(this);
        return true;
    }

    public bool AddReagent(ulong reagentTemplateId, out ClientReagentItem reagentObj) {
        if (AlchemyBehavior.HasReageant(reagentTemplateId)) {
            reagentObj = AlchemyBehavior.GetReagent(reagentTemplateId);
        }
        else {
            reagentObj = (ClientReagentItem) CoreObjectFactory.FinalizeCoreObject(reagentTemplateId);
            reagentObj.m_characterId = (GID) CharId;
            reagentObj.m_quantity = 1;
        }

        return AddReagent(reagentObj);
    }

    public bool AddReagent(ClientReagentItem reagent) {
        if (reagent is null) {
            Logger.Warning("Cannot add reagent to reagent bag because that reagent does not exist.");
            return false;
        }

        // Ensure that the item is associated with this Wizard.
        reagent.m_characterId = (GID) CharId;

        var success = AlchemyBehavior.AddReagent(reagent);
        if (!success) {
            Logger.Warning("Could not add reagent {0} to player {1}'s reagent bag.",
                Logger.Args(reagent.m_globalID, PlayerNameBehavior.GetWizardName()));
                
            return false;
        }

        // Persistent save.
        WizardReagentCollection.AddReagent(reagent);
        WizardCollection.UpdateCharacterItems(this);

        return true;
    }

    public void SetNameOverride(string newName) {
        PlayerNameBehavior.NameOverride = newName;

        // Persistent save.
        WizardCollection.UpdateCharacterNameOverride(this);
    }

    public void SetBadgeOverride(string newBadge) {
        PlayerNameBehavior.BadgeTitle = newBadge;

        // Persistent save.
        WizardCollection.UpdateCharacterBadgeOverride(this);
    }

    public bool LearnSpell(Spell spell) {
        if (SpellbookBehavior.LearnedSpellTemplateIds.Contains(spell.m_templateID)) {
            Logger.Warning("{0} Tried to learn spell with template ID {1} that is already known.",
                Logger.Args(PlayerNameBehavior.GetWizardName(), spell.m_templateID));
            return false;
        }

        SpellbookBehavior.AddSpellToBook(spell);

        // Persistent save.
        WizardCollection.LearnSpell(this, spell.m_templateID);

        return true;
    }

    public bool UnlearnSpell(uint spellTemplateId) {
        if (!SpellbookBehavior.LearnedSpellTemplateIds.Contains(spellTemplateId)) {
            Logger.Warning("{0} Tried to unlearn spell with template ID {1} that is not known.",
                Logger.Args(PlayerNameBehavior.GetWizardName(), spellTemplateId));
            return false;
        }

        SpellbookBehavior.RemoveSpellFromBook(spellTemplateId);

        // Persistent save.
        WizardCollection.UnlearnSpell(this, spellTemplateId);

        return true;
    }

    public void AddTemporarySpell(Spell spell) {
        SpellbookBehavior.AddTemporarySpellToBook(spell);
    }

    public void RemoveTemporarySpell(uint spellTemplateId) {
        SpellbookBehavior.RemoveTemporarySpellFromBook(spellTemplateId);
    }

    public bool AddSpellToDeck(uint spellTemplateId, ulong deckId) {
        // Find the actual item in the inventory.
        var item = InventoryBehavior.Items.FirstOrDefault(i => i.m_globalID == deckId);
        if (item is null) {
            // The item may be equipped instead.
            item = EquipmentBehavior.EquippedItems.FirstOrDefault(i => i.m_globalID == deckId);
            if (item is null) {
                Logger.Warning("Could not find item with global ID {0} in player {1}'s inventory or equipment.",
                    Logger.Args(deckId, PlayerNameBehavior.GetWizardName()));
                return false;
            }

            // If the item is equipped, we'll also want to update the spellbook behavior.
            var addedSuccess = SpellbookBehavior.AddSpellToDeck(spellTemplateId);
            if (!addedSuccess) {
                Logger.Debug("Could not add spell with template ID {0} to player {1}'s deck.",
                    Logger.Args(spellTemplateId, PlayerNameBehavior.GetWizardName()));
                return false;
            }

            WizardItemCollection.AddSpellToDeck(deckId, spellTemplateId);
            return true;
        }

        // Regardless, we'll want to add this spell to the deck item's DeckBehavior.
        if (!CoreObjectFactory.FindBehaviorInstance<DeckBehavior>(item, out var deckBehavior)) {
            Logger.Error("Could not find deck behavior for item with global ID {0}.", Logger.Args(spellTemplateId));
            return false;
        }

        var spellList = deckBehavior.m_spellList ?? new List<SpellData>();
        var spellDeckData = spellList.FirstOrDefault(s => s.m_templateID == spellTemplateId);
        if (spellDeckData is null) {
            // It may not be included yet. We'll add another entry.
            var newSpellDeckData = new SpellData {
                m_templateID = spellTemplateId,
                m_quantity = 1
            };
            spellList.Add(newSpellDeckData);
        }
        else {
            // Otherwise, we'll just increment the quantity.
            spellDeckData.m_quantity++;
        }

        // Persistent save.
        WizardItemCollection.AddSpellToDeck(deckId, spellTemplateId);

        return true;
    }

    public bool RemoveSpellFromDeck(uint spellTemplateId, ulong deckId) {
        // Find the actual item in the inventory.
        var item = InventoryBehavior.Items.FirstOrDefault(i => i.m_globalID == deckId);
        if (item is null) {
            // The item may be equipped instead.
            item = EquipmentBehavior.EquippedItems.FirstOrDefault(i => i.m_globalID == deckId);
            if (item is null) {
                Logger.Warning("Could not find item with global ID {0} in player {1}'s inventory or equipment.",
                    Logger.Args(deckId, PlayerNameBehavior.GetWizardName()));
                return false;
            }

            // If the item is equipped, we'll also want to update the spellbook behavior.
            var removedSuccess = SpellbookBehavior.RemoveSpellFromDeck(spellTemplateId);
            if (!removedSuccess) {
                Logger.Warning("Could not remove spell with template ID {0} from player {1}'s deck.",
                    Logger.Args(spellTemplateId, PlayerNameBehavior.GetWizardName()));
                return false;
            }

            WizardItemCollection.RemoveSpellFromDeck(deckId, spellTemplateId);
            return true;
        }

        // Regardless, we'll want to remove this spell from the deck item's DeckBehavior.
        if (!CoreObjectFactory.FindBehaviorInstance<DeckBehavior>(item, out var deckBehavior)) {
            Logger.Error("Could not find deck behavior for item with global ID {0}.", Logger.Args(spellTemplateId));
            return false;
        }

        var spellList = deckBehavior.m_spellList ?? new List<SpellData>();
        var spellDeckData = spellList.FirstOrDefault(s => s.m_templateID == spellTemplateId);
        if (spellDeckData is null) {
            Logger.Warning("Could not find spell with template ID {0} in player {1}'s deck.",
                Logger.Args(spellTemplateId, PlayerNameBehavior.GetWizardName()));
            return false;
        }

        if (spellDeckData.m_quantity > 1) {
            spellDeckData.m_quantity--;
        }
        else {
            spellList.Remove(spellDeckData);
        }

        // Persistent save.
        WizardItemCollection.RemoveSpellFromDeck(deckId, spellTemplateId);

        return true;
    }

    public ObjState EnterState(string stateName) => ObjectStateBehavior.SetState(stateName);

    public bool AddDynamod(string zoneName, string clientTag, string modState) {
        DynamodSet ??= new DynamodSet(CharId);

        var dynamod = new Dynamod {
            ZoneName = zoneName,
            ClientTag = clientTag,
            ModState = modState
        };

        var addSuccess = DynamodSet.AddDynamod(dynamod);

        if (!addSuccess) {
            Logger.Warning("Could not add Dynamod to player {0}'s DynamodSet.", Logger.Args(PlayerNameBehavior.GetWizardName()));
            return false;
        }

        // Persistent save.
        DynamodCollection.UpdateDynamodSet(DynamodSet);

        return true;
    }

    public bool RemoveDynamod(string clientTag) {
        if (DynamodSet is null) {
            return false;
        }

        var removeSuccess = DynamodSet.RemoveDynamod(clientTag);

        if (!removeSuccess) {
            return false;
        }

        // Persistent save.
        DynamodCollection.UpdateDynamodSet(DynamodSet);

        return true;
    }

    internal void AfterDatabaseLoad() {
        AfterDatabaseLoadWizardGameStats();
        AfterDatabaseLoadSpellbookBehavior();
        AfterDatabaseloadMountOwnerBehavior();
        AfterDatabaseLoadPetOwnerBehavior();
        AfterDatabaseLoadAlchemyBehavior();

        ObjectStateBehavior ??= new ServerObjectStateBehavior("PlayerMobileStates");
    }

    private void EquipMount(WizItemTemplate template, WizClientObjectItem item) {
        var mountEquipSuccess = MountOwnerBehavior.EquipMount(template, item);
        if (!mountEquipSuccess) {
            Logger.Warning("Could not equip mount {0} to player {1}.", Logger.Args(template.m_objectName, PlayerNameBehavior.GetWizardName()));
            return;
        }

        // Persistent save.
        WizardCollection.UpdateCharacterMount(this);
    }

    private void UnequipMount() {
        MountOwnerBehavior.UnequipMount();

        // Persistent save.
        WizardCollection.UpdateCharacterMount(this);
    }

    private void InformSpellbookOfNewDeck(WizItemTemplate template, ulong deckGlobalId) {
        // The caller of this method has already equipped the deck to the player.
        // This method just updates the spellbook behavior to reflect the new deck.

        // Get the actual item from equipment.
        var deckItem = EquipmentBehavior.EquippedItems.FirstOrDefault(i => i.m_globalID == deckGlobalId);
        if (deckItem is null) {
            Logger.Error("Could not find deck item with global ID {0}.", Logger.Args(deckGlobalId));
            return;
        }

        // Get the deck behavior.
        if (!CoreObjectFactory.FindBehaviorInstance<DeckBehavior>(deckItem, out var deckBehavior)) {
            Logger.Error("Could not find deck behavior for item with global ID {0}.", Logger.Args(deckGlobalId));
            return;
        }

        var deckEquipSuccess = SpellbookBehavior.EquipDeck(template, deckBehavior);
        if (!deckEquipSuccess) {
            Logger.Warning("Could not equip deck {0} to player {1}.", Logger.Args(template.m_objectName, PlayerNameBehavior.GetWizardName()));
            return;
        }
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
            CoreObjectFactory.InitializeCoreObjectBehaviors(cObj, templateId);
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
            FriendlyPlayer = false,
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
        SpellbookBehavior = new ServerWizSpellbookBehavior();
    }

    private void InitializeDefaultPetSnackBehavior() {
        PetSnackBehavior = new ServerPetSnackBehavior() {
            Snacks = new List<ClientPetSnackItem>(),
            SnackItemIds = new List<ulong>()
        };
    }

    private void AfterDatabaseLoadSpellbookBehavior() {
        // Find the deck in our equipment.
        var idInSlot = EquipmentBehavior.SlotList.FirstOrDefault(s => s.SlotType == EquipmentSlotType.Deck)?.ItemId;
        if (idInSlot is null) {
            // Normal behavior; we just don't have a deck equipped.
            return;
        }

        // Get the actual item.
        var deckItem = EquipmentBehavior.EquippedItems.FirstOrDefault(i => i.m_globalID == idInSlot);
        if (deckItem is null) {
            Logger.Error("Could not find deck item with global ID {0}.", Logger.Args(idInSlot));
            return;
        }

        // Get the deck behavior.
        if (!CoreObjectFactory.FindBehaviorInstance<DeckBehavior>(deckItem, out var deckBehavior)) {
            Logger.Error("Could not find deck behavior for item with global ID {0}.", Logger.Args(idInSlot));
            return;
        }

        // Get the template. This gives us information like the max instance count, what school the deck is, etc.
        var deckTemplate = CoreObjectFactory.GetCoreTemplate(deckItem.m_templateID);
        if (deckTemplate is null) {
            Logger.Error("Could not find deck template with global ID {0}.", Logger.Args(idInSlot));
            return;
        }

        // Get the DeckBehaviorTemplate within the deck template.
        if (deckTemplate.m_behaviors.FirstOrDefault(b => b is DeckBehaviorTemplate) is not DeckBehaviorTemplate deckBehaviorTemplate) {
            Logger.Error("Could not find deck behavior template within deck template with global ID {0}.", Logger.Args(idInSlot));
            return;
        }

        // Finally, initialize the spellbook behavior with the deck behavior.
        SpellbookBehavior.InitializeProperties(deckBehaviorTemplate);
        SpellbookBehavior.InitializeSpells(deckBehavior);
    }

    private void InitializeMountOwnerBehavior() {
        MountOwnerBehavior = new ServerMountOwnerBehavior();
    }

    private void AfterDatabaseloadMountOwnerBehavior() {
        // FOund our mount in the equipment.
        var idInSlot = EquipmentBehavior.SlotList.FirstOrDefault(s => s.SlotType == EquipmentSlotType.Mount)?.ItemId;
        if (idInSlot is null) {
            // Normal behavior; we just don't have a mount equipped.
            return;
        }

        // Get the actual item.
        var mountItem = EquipmentBehavior.EquippedItems.FirstOrDefault(i => i.m_globalID == idInSlot);
        if (mountItem is null) {
            Logger.Error("Could not find mount item with global ID {0}.", Logger.Args(idInSlot));
            return;
        }

        // Get the template for this item.
        var mountTemplate = ItemHelper.GetItemTemplate(mountItem);

        MountOwnerBehavior.EquipMount(mountTemplate, mountItem);
    }

    private void InitializeWizardGameStats(MagicSchool school, byte level) {
        GameStats = new ServerWizGameStats(school, level);
        CharacterHelper.RecalculateGameStats(this);

        GameStats.m_currentHitpoints = GameStats.m_baseHitpoints;
        GameStats.m_currentMana = GameStats.m_baseMana;
    }

    private void InitializePetOwnerBehavior() {
        PetOwnerBehavior = new ServerPetOwnerBehavior();
        PetOwnerBehavior.SetEnergy(GameStats.m_energyMax);
    }

    private void InitializeAlchemyBehavior() {
        AlchemyBehavior = new ServerAlchemyBehavior() {
            Reagents = new List<ClientReagentItem>(),
            Recipes = new List<Recipe>(),
            CraftingSlots = new List<CraftingSlot>(),
            ReagentItemIds = new List<ulong>()
        };
    }

    private void AfterDatabaseLoadPetOwnerBehavior() {
        var magicSchool = MagicSchoolBehavior.MagicSchool;
        var level = MagicSchoolBehavior.Level;
        var baseStats = MagicLevelsConfig.GetPlayerLevelInfo(magicSchool, level);
        var normMaxEnergy = baseStats.m_petEnergy;
        GameStats.m_energyMax = normMaxEnergy;

        if (PetOwnerBehavior is null) {
            PetOwnerBehavior = new ServerPetOwnerBehavior();
            PetOwnerBehavior.SetEnergy(GameStats.m_energyMax);

            return;
        }

        // If the last energy tick is in the future, don't bother.
        if (PetOwnerBehavior.LastEnergyTickEpoch > DateTimeOffset.UtcNow.ToUnixTimeSeconds()) {
            return;
        }

        // Deduce how much energy has been regained since the last tick.
        var energyTickIntervalInSeconds = PetOwnerBehavior.EnergyTickIntervalInSeconds;
        var lastEnergyTickEpoch = PetOwnerBehavior.LastEnergyTickEpoch;
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var timeDifference = currentTime - lastEnergyTickEpoch;
        var energyRegained = timeDifference / energyTickIntervalInSeconds;

        // If the player has regained more energy than their max, set it to the max.
        if (PetOwnerBehavior.Energy + energyRegained > normMaxEnergy) {
            UpdateEnergy(normMaxEnergy);
        }
        else {
            UpdateEnergy((int) (PetOwnerBehavior.Energy + energyRegained));
        }
    }

    private void AfterDatabaseLoadWizardGameStats() {
        var highestLevelOnAcc = Account.GetHighestLevelWizard().MagicSchoolBehavior.Level;

        GameStats.Level = MagicSchoolBehavior.Level;
        GameStats.MagicSchool = MagicSchoolBehavior.MagicSchool;
        GameStats.m_schoolID = (uint) MagicSchoolBehavior.MagicSchool;
        GameStats.m_highestCharacterLevelOnAccount = highestLevelOnAcc;
    }

    private void AfterDatabaseLoadAlchemyBehavior() 
        => AlchemyBehavior ??= new ServerAlchemyBehavior() {
        Reagents = [],
        Recipes = [],
        CraftingSlots = [],
        ReagentItemIds = []
    };

    public void Dispose() =>
        // If this object is being disposed, the player probably left the server.
        // Save the character's location to the database.
        WizardCollection.UpdateCharacterLocation(this, Location, Orientation.Z);
}
