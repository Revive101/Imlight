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
using Imcodec.CoreObject;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Cantrips;
using Imlight.CoreLib.Shared.Character;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandModifyProtocol : CommandProtocol {

    private const uint SPEED_EFFECT_NAME = 6543894;

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
    private void SetSpeedCommand(string speedMultiplier) {
        // Try to parse the speed multiplier.
        if (!int.TryParse(speedMultiplier, out var speedMultiplierInt)) {
            InformSenderClient("Invalid speed multiplier.");

            return;
        }

        // Create the speed effect.
        var effect = new SpeedEffect() {
            m_speedMultiplier = speedMultiplierInt,
            m_effectNameID = SPEED_EFFECT_NAME,
            m_itemSlotID = 100
        };
        var coreObjectSerializer = new CoreObjectSerializer(
            behaviors: Imcodec.ObjectProperty.SerializerFlags.None
        );
        if (!coreObjectSerializer.Serialize(effect, 1, out var serializedEffect)) {
            InformSenderClient("Failed to serialize speed effect.");

            return;
        }

        // Create the network message and send it.
        var networkMessage = new GAME_5_PROTOCOL.MSG_ADDEFFECT() {
            GameObjectID = Context.Character.CharId,
            EffectData = serializedEffect.ToString()
        };
        Context.SessionActor.Tell(networkMessage, null);

        InformSenderClient($"Increased speed multiplier by {speedMultiplierInt}.");
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

        var addedItemSuccess = Context.Character.AddItemToInventory(templateIdLong, out var coreObject);
        if (!addedItemSuccess) {
            InformSenderClient("Could not add item to inventory.");

            return;
        }

        var coSerializer = new CoreObjectSerializer(
            behaviors: Imcodec.ObjectProperty.SerializerFlags.None
        );
        if (!coSerializer.Serialize(coreObject, 24, out var serializedCoreObject)) {
            InformSenderClient("Failed to serialize core object.");

            return;
        }

        var networkMessage = new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM {
            GlobalID = Context.Character.CharId,
            SerializedItem = serializedCoreObject
        };
        Context.SessionActor.Tell(networkMessage, null);

        InformSenderClient($"Added item {coreObject.m_debugName} to inventory.");
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
            GlobalID = Context.Character.CharId,
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
            GlobalID = Context.Character.CharId,
            ItemID = snackObj.m_globalID,
            Quantity = snackObj.m_quantity
        };
        Context.SessionActor.Tell(updateMessage, null);

        InformSenderClient($"Added snack {snackObj.m_debugName} to snack bag.");
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
            GlobalID = Context.Character.CharId,
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
            GlobalID = Context.Character.CharId,
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
        // Set the badge of the character.
        Context.Character.SetBadgeOverride(badge);

        InformSenderClient($"Set badge to {badge}. Relog to see changes.");
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

}
