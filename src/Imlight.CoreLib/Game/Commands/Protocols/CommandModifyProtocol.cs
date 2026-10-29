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
using System.Globalization;
using System.Linq;
using Akka.Actor;
using Imcodec.CoreObject;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Bank;
using Imlight.CoreLib.Game.Cantrips;
using Imlight.CoreLib.Game.Pet;
using Imlight.CoreLib.Game.Pet.Config;
using Imlight.CoreLib.Game.Pet.Progression;
using Imlight.CoreLib.Shared.Character;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandModifyProtocol : CommandProtocol {

    private const uint SPEED_BUFF_EFFECT_NAME_HASH = 6543894;

    internal override string Group { get; set; } = "mod";

    [Command("levelup")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    [Alias("lvlup")]
    private void LevelUpCommand() {
        // Inform the user of failure if the new level would be above the max level.
        var newLevel = (byte) (Context.Character.MagicSchoolBehavior.Level + 1);
        if (newLevel > MagicLevelsConfig.MaxLevel) {
            InformSenderClient($"You cannot set level higher than the max level ({MagicLevelsConfig.MaxLevel}).");

            return;
        }

        var msg = new CHARACTER_103_PROTOCOL.MSG_LEVELUP() { NewLevel = newLevel };
        Context.SessionActor.Tell(msg, null);
    }

    [Command("level")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetLevelCommand(string level) {
        // Try to parse the level.
        if (!byte.TryParse(level, out var levelByte)) {
            InformSenderClient("Invalid level.");

            return;
        }

        // Inform the user of failure if the new level would be above the max level.
        if (levelByte > MagicLevelsConfig.MaxLevel) {
            InformSenderClient($"You cannot set level higher than the max level ({MagicLevelsConfig.MaxLevel}).");

            return;
        }

        var msg = new CHARACTER_103_PROTOCOL.MSG_LEVELUP() { NewLevel = levelByte };
        Context.SessionActor.Tell(msg, null);
    }

    [Command("speed")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetSpeedCommand(string speedBonus) {
        // Try to parse the speed multiplier.
        if (!int.TryParse(speedBonus, out var speedBonusInt)) {
            InformSenderClient("Invalid speed multiplier.");

            return;
        }

        // Create the speed effect.
        var effect = new SpeedEffect() {
            m_speedMultiplier = speedBonusInt, // Speed is not a multiplier, but a percentage bonus! (+50%, etc.)
            m_effectNameID = SPEED_BUFF_EFFECT_NAME_HASH,
            m_itemSlotID = 0 //  Should be 0 when effects are not bound to an equipped gear slot!
        };

        var coreObjectSerializer = new CoreObjectSerializer(
            behaviors: Imcodec.ObjectProperty.SerializerFlags.None
        );

        var flags = PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit;
        if (!coreObjectSerializer.Serialize(effect, flags, out var serializedEffect)) {
            InformSenderClient("Failed to serialize speed effect.");
            return;
        }

        // Create the network message and send it.
        var networkMessage = new GAME_5_PROTOCOL.MSG_ADDEFFECT() {
            GameObjectID = Context.Character.GameObjectID,
            EffectData = serializedEffect
        };
        Context.SessionActor.Tell(networkMessage, null);

        if (speedBonusInt > 0) {
            InformSenderClient($"Increased speed by {speedBonusInt}%.");
        }
        else {
            InformSenderClient($"Decreased speed by {speedBonusInt}%.");
        }
    }

    [Command("additem")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void AddItemCommand(string templateId) {
        // Try to parse the item id.
        if (!ulong.TryParse(templateId, out var templateIdLong)) {
            InformSenderClient("Invalid item id.");

            return;
        }

        // Check to see if this template exists.
        var template = CoreObjectFactory.GetCoreTemplate(templateIdLong);
        if (template is null) {
            InformSenderClient("Invalid item id.");

            return;
        }

        // We can't add game objects to the inventory.
        if (template is not WizItemTemplate) {
            InformSenderClient($"Cannot add objects of type {template.GetType().Name} to inventory.");

            return;
        }

        var isPetTemplate = PetFactory.IsPetTemplate((uint) templateIdLong);
        if (!isPetTemplate && template.m_behaviors.Any(b => b?.m_behaviorName.ToString() == "PetItemBehavior")) {
            InformSenderClient("Cannot add this pet item because it is not in the pet list.");

            return;
        }

        if (!isPetTemplate) {
            var probe = CoreObjectFactory.FinalizeCoreObject(templateIdLong) as WizClientObjectItem;
            if (probe is not null) {
                CoreObjectFactory.InitializeCoreObjectBehaviors(probe, probe.m_templateID);
            }

            if (probe?.m_inactiveBehaviors?.Contains(null) == true) {
                InformSenderClient("Cannot add this item because one of its behaviors cannot be created.");

                return;
            }
        }

        if (!Context.Character.TryCreateItem(templateIdLong, out var coreObject)) {
            InformSenderClient("Could not create the item.");

            return;
        }

        // Like a reward: the backpack first, then the bank.
        var placement = ItemPlacement.Place(Context.Character, coreObject);
        ItemPlacement.Announce(Context.SessionActor, Context.Character, coreObject, placement);

        InformSenderClient(placement switch {
            Placement.Backpack => $"Added item {coreObject.m_debugName} to inventory.",
            Placement.Bank => $"Added item {coreObject.m_debugName} to the bank, the inventory is full.",
            Placement.SharedBank => $"Added item {coreObject.m_debugName} to the shared bank, the inventory is full.",
            _ => "Could not add the item, the inventory and the bank are full.",
        });
    }

    [Command("addsnack")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void AddSnackCommand(string templateId) {
        // Try to parse the item id.
        if (!ulong.TryParse(templateId, out var templateIdLong)) {
            InformSenderClient("Invalid item id.");

            return;
        }

        // Check to see if this template exists.
        var template = CoreObjectFactory.GetCoreTemplate(templateIdLong);
        if (template is null) {
            InformSenderClient("Invalid item id.");

            return;
        }

        // We can't non-snack objects to the snack bag.
        if (template is not PetSnackItemTemplate) {
            InformSenderClient($"Cannot add objects of type {template.GetType().Name} to snack bag.");

            return;
        }

        var addedSnackSuccess = Context.Character.AddSnack(templateIdLong, out var snackObj);
        if (!addedSnackSuccess) {
            InformSenderClient("Could not add snack to snack bag.");

            return;
        }

        var coSerializer = new CoreObjectSerializer(
            behaviors: Imcodec.ObjectProperty.SerializerFlags.None
        );
        if (!coSerializer.Serialize(snackObj, 24, out var serializedSnackObj)) {
            InformSenderClient("Failed to serialize core object.");

            return;
        }

        var networkMessage = new PET_9_PROTOCOL.MSG_PETSNACKADD {
            GlobalID = Context.Character.GameObjectID,
            Data = serializedSnackObj
        };
        Context.SessionActor.Tell(networkMessage, null);

        var acquireMessage = new WIZARD2_53_PROTOCOL.MSG_ITEMACQUISITION {
            ItemGlobalID = snackObj.m_globalID,
            ItemTemplateID = (uint) snackObj.m_templateID,
            ItemLocation = 1
        };
        Context.SessionActor.Tell(acquireMessage, null);

        var updateMessage = new PET_9_PROTOCOL.MSG_PETSNACKUPDATE {
            GlobalID = Context.Character.GameObjectID,
            ItemID = snackObj.m_globalID,
            Quantity = snackObj.m_quantity
        };
        Context.SessionActor.Tell(updateMessage, null);

        InformSenderClient($"Added snack {snackObj.m_debugName} to snack bag.");
    }

    [Command("petlevel")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetPetLevelCommand(string level) {
        if (!int.TryParse(level, NumberStyles.Integer, CultureInfo.InvariantCulture, out var petLevel)) {
            InformSenderClient("Invalid pet level.");

            return;
        }

        if (!TryGetEquippedPet(out var pet)) {
            return;
        }

        if (!PetProgression.SetLevel(pet, petLevel)) {
            InformSenderClient($"Cannot set the pet to level {petLevel}.");

            return;
        }

        SendPetBehavior(pet);
        InformSenderClient($"Set the pet to level {petLevel}.");
    }

    [Command("petxp")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void AddPetXpCommand(string xp) {
        if (!uint.TryParse(xp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) || amount == 0) {
            InformSenderClient("Invalid XP amount.");

            return;
        }

        if (!TryGetEquippedPet(out var pet)) {
            return;
        }

        var result = PetXpAward.Give(Context.SessionActor, Context.CharacterObject, Context.Character, pet, amount, PetXpSource.Command);
        if (!result.Applied) {
            InformSenderClient("This pet cannot gain XP.");

            return;
        }

        InformSenderClient($"Gave the pet {result.Granted} XP and {result.LevelsGained} levels.");
    }

    [Command("pethappiness")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetPetHappinessCommand(string happiness) {
        if (!uint.TryParse(happiness, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) {
            InformSenderClient("Invalid happiness.");

            return;
        }

        if (!TryGetEquippedPet(out var pet)) {
            return;
        }

        if (!PetProgression.SetHappiness(pet, value)) {
            InformSenderClient("This pet cannot have its happiness set.");

            return;
        }

        SendPetBehavior(pet);
        InformSenderClient($"Set the pet's happiness to {value}, up to its maximum.");
    }

    [Command("pettalent")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void GivePetTalentCommand(string talentName) {
        if (!PetTalentRegistry.TryGetByName(talentName, out var talent)) {
            InformSenderClient($"No pet talent is named {talentName}.");

            return;
        }

        if (!TryGetEquippedPet(out var pet)) {
            return;
        }

        if (!PetProgression.GiveTalent(pet, talent)) {
            InformSenderClient("This pet cannot be given a talent.");

            return;
        }

        SendPetBehavior(pet);
        InformSenderClient($"Gave the pet the talent {talent.Name}.");
    }

    private bool TryGetEquippedPet(out WizClientObjectItem pet) {
        pet = Context.Character.GetEquippedPetItem();
        if (pet is null) {
            InformSenderClient("Equip a pet first.");

            return false;
        }
        return true;
    }

    private void SendPetBehavior(WizClientObjectItem pet) {
        Context.Character.SavePet(pet);

        if (PetBehaviorUpdate.TryCreate(pet, out var update)) {
            Context.SessionActor.Tell(update, null);
        }
    }

    [Command("addreagent")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void AddReagentCommand(string templateId) {
        // Try to parse the item id.
        if (!ulong.TryParse(templateId, out var templateIdLong)) {
            InformSenderClient("Invalid item id.");

            return;
        }

        // Check to see if this template exists.
        var template = CoreObjectFactory.GetCoreTemplate(templateIdLong);
        if (template is null) {
            InformSenderClient("Invalid item id.");

            return;
        }

        // We can't add non-reagent objects to the reagent bag.
        if (template is not ReagentItemTemplate) {
            InformSenderClient($"Cannot add objects of type {template.GetType().Name} to reagent bag.");

            return;
        }

        var addedReagentSuccess = Context.Character.AddReagent(templateIdLong, out var reagentObj);
        if (!addedReagentSuccess) {
            InformSenderClient("Could not add reagent to reagent bag.");

            return;
        }

        var coSerializer = new CoreObjectSerializer(
            behaviors: Imcodec.ObjectProperty.SerializerFlags.None
        );
        if (!coSerializer.Serialize(reagentObj, 27, out var serializedReagentObj)) {
            InformSenderClient("Failed to serialize core object.");

            return;
        }

        var networkMessage = new WIZARD_12_PROTOCOL.MSG_REAGENTADD {
            GlobalID = Context.Character.GameObjectID,
            Data = serializedReagentObj
        };
        Context.SessionActor.Tell(networkMessage, null);

        var acquireMessage = new WIZARD2_53_PROTOCOL.MSG_ITEMACQUISITION {
            ItemGlobalID = reagentObj.m_globalID,
            ItemTemplateID = (uint) reagentObj.m_templateID,
            ItemLocation = 1
        };
        Context.SessionActor.Tell(acquireMessage, null);

        var updateMessage = new WIZARD_12_PROTOCOL.MSG_REAGENTUPDATE {
            GlobalID = Context.Character.GameObjectID,
            ItemID = reagentObj.m_globalID,
            Quantity = reagentObj.m_quantity
        };
        Context.SessionActor.Tell(updateMessage, null);

        InformSenderClient($"Added reagent {reagentObj.m_debugName} to reagent bag.");
    }

    [Command("name")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetNameCommand([Remainder] string name) {
        // Set the name of the character.
        Context.Character.SetNameOverride(name);

        InformSenderClient($"Set name to {name}. Relog to see changes.");
    }

    [Command("badge")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetBadgeCommand([Remainder] string badge) {
        // The badge service sets the title and tells the zone, so nobody has to relog.
        Context.SessionActor.Tell(new CHARACTER_103_PROTOCOL.MSG_BADGECOMMAND {
            Action = "title",
            Argument = badge,
        }, null);
    }

    [Command("maxgold")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetMaxGoldCommand(string gold) {
        // Try to parse the gold.
        if (!int.TryParse(gold, out var goldInt)) {
            InformSenderClient("Invalid maximum gold amount.");

            return;
        }

        // Set the max gold amount.
        Context.Character.SetMaxGold(goldInt);

        var networkMessage = new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD() {
            Gold = Context.Character.GameStats.m_currentGold,
            MaxGold = goldInt
        };
        Context.SessionActor.Tell(networkMessage, null);

        InformSenderClient($"Set max gold to {goldInt}.");
    }

    [Command("maxbank")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetMaxBankCommand(string limit) {
        if (!int.TryParse(limit, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limitInt) || limitInt < 1) {
            InformSenderClient("Invalid bank limit.");

            return;
        }

        if (!Context.Character.SetBankLimit(limitInt)) {
            InformSenderClient("Could not save the bank limit.");

            return;
        }

        Context.SessionActor.Tell(BankSync.UpdateBankLimit(Context.Character), null);

        InformSenderClient($"Set the bank limit to {limitInt}.");
    }

    [Command("expandbank")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void ExpandBankCommand() {
        var amount = BankConfig.ExpandElixirAmount;
        if (amount <= 0) {
            InformSenderClient("[Bank] ExpandElixirAmount is not set, so the bank cannot be expanded.");

            return;
        }

        var before = Context.Character.StorageBehavior.Limit;
        if (!Context.Character.ExpandBank(amount)) {
            InformSenderClient("Could not save the bank limit.");

            return;
        }

        Context.SessionActor.Tell(BankSync.UpdateBankLimit(Context.Character), null);

        InformSenderClient($"Expanded the bank from {before} to {Context.Character.StorageBehavior.Limit}.");
    }

    [Command("addgold")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void AddGoldCommand(string gold) {
        // Try to parse the gold.
        if (!int.TryParse(gold, out var goldInt)) {
            InformSenderClient("Invalid gold amount.");

            return;
        }

        // Set the gold amount.
        Context.Character.AddGold(goldInt);

        var networkMessage = new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD() {
            Gold = Context.Character.GameStats.m_currentGold,
            MaxGold = Context.Character.GameStats.m_baseGoldPouch
        };
        Context.SessionActor.Tell(networkMessage, null);

        InformSenderClient($"Added {goldInt} gold.");
    }

    [Command("maxhealth")]
    [Alias("maxhp")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetMaxHealthCommand(string health) {
        // Try to parse the health.
        if (!int.TryParse(health, out var healthInt)) {
            InformSenderClient("Invalid maximum health amount.");

            return;
        }

        Context.Character.GameStats.m_baseHitpoints = healthInt;

        var networkMessage = new WIZARD_12_PROTOCOL.MSG_UPDATEHEALTH() {
            CharacterID = Context.CharacterObject.m_globalID,
            NewHealth = Context.Character.GameStats.m_currentHitpoints,
            NewHealthMax = healthInt,
            DisplayDiff = 1,
        };
        Context.SessionActor.Tell(networkMessage, null);

        InformSenderClient($"Set max health to {healthInt}.");
    }

    [Command("maxmana")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetMaxManaCommand(string mana) {
        // Try to parse the mana.
        if (!int.TryParse(mana, out var manaInt)) {
            InformSenderClient("Invalid maximum mana amount.");

            return;
        }

        Context.Character.GameStats.m_baseMana = manaInt;

        var networkMessage = new WIZARD_12_PROTOCOL.MSG_UPDATEMANA() {
            Mana = Context.Character.GameStats.m_currentMana,
            MaxMana = manaInt,
            DisplayDiff = 1,
        };
        Context.SessionActor.Tell(networkMessage, null);

        InformSenderClient($"Set max mana to {manaInt}.");
    }

    [Command("maxenergy")]
    [Alias("maxnrg")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetMaxEnergyCommand(string energy) {
        // Try to parse the energy.
        if (!int.TryParse(energy, out var energyInt)) {
            InformSenderClient("Invalid maximum energy amount.");

            return;
        }

        Context.Character.GameStats.m_energyMax = energyInt;
        Context.Character.PetOwnerBehavior.SetEnergy(energyInt);

        var tickMsg = new PET_9_PROTOCOL.MSG_PETENERGYTICK() {
            GlobalID = Context.Character.GameObject.m_globalID,
            Energy = Context.Character.PetOwnerBehavior.Energy,
            MaxEnergy = energyInt,
            TickTime = (int) Context.Character.PetOwnerBehavior.LastEnergyTickEpoch
        };
        var maxMsg = new PET_9_PROTOCOL.MSG_PETENERGYMAX() {
            MaxEnergy = energyInt
        };

        Context.SessionActor.Tell(tickMsg, null);
        Context.SessionActor.Tell(maxMsg, null);

        InformSenderClient($"Set max energy to {energyInt}.");
    }

    [Command("currenthealth")]
    [Alias("currenthp")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetCurrentHealthCommand(string health) {
        // Try to parse the health.
        if (!int.TryParse(health, out var healthInt)) {
            InformSenderClient("Invalid current health amount.");

            return;
        }

        var newHealth = Math.Min(healthInt, Context.Character.GameStats.m_baseHitpoints);
        Context.Character.UpdateHealth(newHealth);

        // The client has a max health increase effect applied, so sending it here would double the health client side.
        var magicSchool = Context.Character.MagicSchoolBehavior.MagicSchool;
        var level = Context.Character.MagicSchoolBehavior.Level;
        var baseStats = MagicLevelsConfig.GetPlayerLevelInfo(magicSchool, level);
        var normMaxHealth = baseStats.m_hitpoints;

        var networkMessage = new WIZARD_12_PROTOCOL.MSG_UPDATEHEALTH() {
            CharacterID = Context.CharacterObject.m_globalID,
            NewHealth = healthInt,
            NewHealthMax = normMaxHealth,
            DisplayDiff = 1,
        };
        Context.SessionActor.Tell(networkMessage, null);

        InformSenderClient($"Set current health to {healthInt}.");
    }

    [Command("currentmana")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetCurrentManaCommand(string mana) {
        // Try to parse the mana.
        if (!int.TryParse(mana, out var manaInt)) {
            InformSenderClient("Invalid current mana amount.");

            return;
        }

        var newMana = Math.Min(manaInt, Context.Character.GameStats.m_baseMana);
        Context.Character.UpdateMana(newMana);

        // The client has a max mana increase effect applied, so sending it here would double the mana client side.
        var magicSchool = Context.Character.MagicSchoolBehavior.MagicSchool;
        var level = Context.Character.MagicSchoolBehavior.Level;
        var baseStats = MagicLevelsConfig.GetPlayerLevelInfo(magicSchool, level);
        var normMaxMana = baseStats.m_mana;

        var networkMessage = new WIZARD_12_PROTOCOL.MSG_UPDATEMANA() {
            Mana = manaInt,
            MaxMana = normMaxMana,
            DisplayDiff = 1,
        };
        Context.SessionActor.Tell(networkMessage, null);

        InformSenderClient($"Set current mana to {manaInt}.");
    }

    [Command("currentenergy")]
    [Alias("currentnrg")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetCurrentEnergyCommand(string energy) {
        // Try to parse the energy.
        if (!int.TryParse(energy, out var energyInt)) {
            InformSenderClient("Invalid current energy amount.");

            return;
        }

        var newEnergy = Math.Min(energyInt, Context.Character.GameStats.m_energyMax);
        Context.Character.UpdateEnergy(newEnergy);

        // The client has a max energy increase effect applied, so sending it here would double the energy client side.
        var magicSchool = Context.Character.MagicSchoolBehavior.MagicSchool;
        var level = Context.Character.MagicSchoolBehavior.Level;
        var baseStats = MagicLevelsConfig.GetPlayerLevelInfo(magicSchool, level);
        var normMaxEnergy = baseStats.m_petEnergy;

        var networkMessage = new PET_9_PROTOCOL.MSG_PETENERGYTICK() {
            GlobalID = Context.Character.GameObject.m_globalID,
            Energy = energyInt,
            MaxEnergy = normMaxEnergy,
            TickTime = (int) Context.Character.PetOwnerBehavior.LastEnergyTickEpoch
        };
        Context.SessionActor.Tell(networkMessage, null);

        InformSenderClient($"Set current energy to {energyInt}.");
    }

    [Command("refillhealth")]
    [Alias("refillhp", "heal")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void MaxHealthCommand() {
        var stats = Context.Character.GameStats;
        var maxHealth = stats.m_baseHitpoints;

        // The client has a max health increase effect applied, so sending it here would double the health client side.
        var magicSchool = Context.Character.MagicSchoolBehavior.MagicSchool;
        var level = Context.Character.MagicSchoolBehavior.Level;
        var baseStats = MagicLevelsConfig.GetPlayerLevelInfo(magicSchool, level);
        var normMaxHealth = baseStats.m_hitpoints;

        Context.Character.UpdateHealth(maxHealth);

        var networkMessage = new WIZARD_12_PROTOCOL.MSG_UPDATEHEALTH() {
            CharacterID = Context.CharacterObject.m_globalID,
            NewHealth = maxHealth,
            NewHealthMax = normMaxHealth,
            DisplayDiff = 1,
        };
        Context.SessionActor.Tell(networkMessage, null);
    }

    [Command("refillmana")]
    [Alias("refillmp", "rejuvenate", "rejuv")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void MaxManaCommand() {
        var stats = Context.Character.GameStats;
        var maxMana = stats.m_baseMana;

        // The client has a max mana increase effect applied, so sending it here would double the mana client side.
        var magicSchool = Context.Character.MagicSchoolBehavior.MagicSchool;
        var level = Context.Character.MagicSchoolBehavior.Level;
        var baseStats = MagicLevelsConfig.GetPlayerLevelInfo(magicSchool, level);
        var normMaxMana = baseStats.m_mana;

        Context.Character.UpdateMana(maxMana);

        var networkMessage = new WIZARD_12_PROTOCOL.MSG_UPDATEMANA() {
            Mana = maxMana,
            MaxMana = normMaxMana,
            DisplayDiff = 1,
        };
        Context.SessionActor.Tell(networkMessage, null);
    }

    [Command("refillenergy")]
    [Alias("refillen", "refillnrg", "refillpet", "energize")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void MaxEnergyCommand() {
        // The client has a max mana increase effect applied, so sending it here would double the mana client side.
        var magicSchool = Context.Character.MagicSchoolBehavior.MagicSchool;
        var level = Context.Character.MagicSchoolBehavior.Level;
        var baseStats = MagicLevelsConfig.GetPlayerLevelInfo(magicSchool, level);
        var normMaxEnergy = baseStats.m_petEnergy;

        Context.Character.UpdateEnergy(normMaxEnergy);

        // Inform the client of the change.
        var networkMessage = new PET_9_PROTOCOL.MSG_PETENERGYTICK() {
            GlobalID = Context.Character.GameObject.m_globalID,
            Energy = normMaxEnergy,
            MaxEnergy = normMaxEnergy,
            TickTime = (int) Context.Character.PetOwnerBehavior.LastEnergyTickEpoch
        };
        Context.SessionActor.Tell(networkMessage, null);
    }

    [Command("cantriplevelup")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    [Alias("clvlup")]
    private void CantripLevelUpCommand() {
        var newLevel = (byte) (Context.Character.GameStats.m_cantripLevel + 1);
        if (newLevel > CantripFactory.GetMaxCantripLevel()) {
            InformSenderClient($"You cannot set level higher than the max level (10).");

            return;
        }

        Context.Character.UpdateCantripLevel(newLevel);

        var msg = new CANTRIPSMESSAGES_57_PROTOCOL.MSG_UPDATECANTRIPXP {
            XP = 0,
            Level = newLevel
        };
        Context.SessionActor.Tell(msg, null);
    }

    [Command("cantriplevel")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    [Alias("clvl")]
    private void SetCantripLevelCommand(string level) {
        // Try to parse the level.
        if (!byte.TryParse(level, out var levelByte)) {
            InformSenderClient("Invalid level.");

            return;
        }

        if (levelByte > CantripFactory.GetMaxCantripLevel()) {
            InformSenderClient($"You cannot set level higher than the max level (10).");

            return;
        }

        Context.Character.UpdateCantripLevel(levelByte);

        var msg = new CANTRIPSMESSAGES_57_PROTOCOL.MSG_UPDATECANTRIPXP {
            XP = 0,
            Level = levelByte
        };
        Context.SessionActor.Tell(msg, null);
    }

    [Command("addtrainingpoints")]
    [Alias("addtp")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void AddTrainingPointsCommand(string trainingPoints) {
        // Try to parse the training points.
        if (!int.TryParse(trainingPoints, out var trainingPointsInt)) {
            InformSenderClient("Invalid training points amount.");

            return;
        }

        var currentTrainingPoints = Context.Character.MagicSchoolBehavior.TrainingPoints;
        var newTrainingPoints = currentTrainingPoints + trainingPointsInt;
        Context.Character.UpdateTrainingPoints(newTrainingPoints);

        var networkMessage = new WIZARD_12_PROTOCOL.MSG_UPDATETRAINING() {
            TrainingPoints = newTrainingPoints
        };
        Context.SessionActor.Tell(networkMessage, null);

        InformSenderClient($"Added {trainingPointsInt} training points.");
    }

    [Command("settrainingpoints")]
    [Alias("settp")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetTrainingPointsCommand(string trainingPoints) {
        // Try to parse the training points.
        if (!int.TryParse(trainingPoints, out var trainingPointsInt)) {
            InformSenderClient("Invalid training points amount.");

            return;
        }

        Context.Character.UpdateTrainingPoints(trainingPointsInt);

        var networkMessage = new WIZARD_12_PROTOCOL.MSG_UPDATETRAINING() {
            TrainingPoints = trainingPointsInt
        };
        Context.SessionActor.Tell(networkMessage, null);

        InformSenderClient($"Set training points to {trainingPointsInt}.");
    }

    [Command("potionmax")]
    [Alias("pmax")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetPotionMax(string potionMax) {
        if (!int.TryParse(potionMax, out var potionMaxInt)) {
            InformSenderClient("Invalid potion amount.");
            return;
        }

        Context.Character.UpdatePotions(potionMaxInt, potionMaxInt);

        // Inform the player's game client that their potion max has been updated.
        var networkMessage = new WIZARD_12_PROTOCOL.MSG_UPDATEPOTIONS {
            PotionMax = potionMaxInt,
            PotionCharge = potionMaxInt
        };
        Context.SessionActor.Tell(networkMessage, null);
        InformSenderClient($"Set and filled potions to {potionMaxInt}.");
    }
    
    [Command("addxp")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void AddXPCommand(string xp) {
        // Try to parse the XP.
        if (!int.TryParse(xp, out var xpInt)) {
            InformSenderClient("Invalid XP amount.");

            return;
        }

       var msg = new CHARACTER_103_PROTOCOL.MSG_GAINXP() {
            XP = xpInt
        };
        Context.SessionActor.Tell(msg, null);

        InformSenderClient($"Added {xpInt} XP.");
    }

    [Command("setcrowns")]
    [Alias("crowns")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetCrownsCommand(string crowns) {
        if (!int.TryParse(crowns, NumberStyles.Integer, CultureInfo.InvariantCulture, out var crownsAmount) || crownsAmount < 0) {
            InformSenderClient("Invalid Crowns amount.");

            return;
        }

        if (!Context.Account.SetCrowns(crownsAmount)) {
            InformSenderClient("The Crowns could not be saved.");

            return;
        }

        // CacheBalanceForCSSegmentation Updates the internal Crown Shop cache so the client uses this balance
        // for Crown Shop segment checks and affordability calculation
        var msg = new WIZARD_12_PROTOCOL.MSG_CROWNBALANCE {
            Failure = 0,
            TotalCrowns = crownsAmount,
            CharacterID = Context.CharacterObject.m_globalID,
            CacheBalanceForCSSegmentation = 1
        };

        Context.SessionActor.Tell(msg);
        InformSenderClient($"Set Crowns to {crownsAmount}.");
    }

}
