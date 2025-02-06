/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Util.Internal;
using Imlight.Common.Caches;
using Imlight.Common.Configuration;
using Imlight.Common.Cryptography;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Game.Cantrips;
using Imlight.CoreLib.Shared.Character;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandModifyProtocol : CommandProtocol {
    internal override string Group { get; set; } = "mod";

    private static readonly uint s_speedEffectName = 6543894;
    private readonly CoreObjectSerializer _effectSerializer = new CoreObjectSerializer()
                    .OnBehaviors(SerializerOptions.Behaviors.None)
                    .OnPropertyMask(SerializerOptions.PropertyFlags.Transmit
                                  | SerializerOptions.PropertyFlags.AuthorityTransmit);

    [Command("levelup")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    [Alias("lvlup")]
    private void LevelUpCommand() {
        // Inform the user of failure if the new level would be above the max level.
        var newLevel = (byte)(Context.Character.MagicSchoolBehavior.Level + 1);
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
            m_effectNameID = s_speedEffectName,
            m_itemSlotID = 100
        };
        var serializedEffect = _effectSerializer.Serialize(effect);

        // Create the network message and send it.
        var networkMessage = new GAME_5_PROTOCOL.MSG_ADDEFFECT() {
            GameObjectID = Context.Character.CharId,
            EffectData = serializedEffect
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

        var serializer = new CoreObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask((SerializerOptions.PropertyFlags)24);

        var networkMessage = new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM {
            GlobalID = Context.Character.CharId,
            SerializedItem = serializer.Serialize(coreObject)
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

        var serializer = new CoreObjectSerializer()
              .OnMode(SerializerOptions.Mode.Compact)
              .OnBehaviors(SerializerOptions.Behaviors.None)
              .OnPropertyMask((SerializerOptions.PropertyFlags) 27);

        var networkMessage = new PET_9_PROTOCOL.MSG_PETSNACKADD {
            GlobalID = Context.Character.CharId,
            Data = serializer.Serialize(snackObj)
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

        var serializer = new CoreObjectSerializer()
              .OnMode(SerializerOptions.Mode.Compact)
              .OnBehaviors(SerializerOptions.Behaviors.None)
              .OnPropertyMask((SerializerOptions.PropertyFlags) 27);

        var networkMessage = new WIZARD_12_PROTOCOL.MSG_REAGENTADD {
            GlobalID = Context.Character.CharId,
            Data = serializer.Serialize(reagentObj)
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
    private void SetNameCommand([Remainder]string name) {
        // Set the name of the character.
        Context.Character.SetNameOverride(name);

        InformSenderClient($"Set name to {name}. Relog to see changes.");
    }

    [Command("badge")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetBadgeCommand([Remainder]string badge) {
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
        var newLevel = (byte)(Context.Character.GameStats.m_cantripLevel + 1);
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
}
