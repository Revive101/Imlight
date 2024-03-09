/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common.Caches;
using Imlight.Common.Configuration;
using Imlight.Common.Cryptography;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;
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
        // Check to see if the new level would be above the max level.
        var isOverMax = (Context.Character.MagicSchoolBehavior.Level + 1) > ConfigurationManager.Settings.MaxLevel;
        if (isOverMax) {
            InformSenderClient("You cannot level up any further.");
            return;
        }

        var msg = new CHARACTER_103_PROTOCOL.MSG_LEVELUP() {
            NewLevel = (byte) (Context.Character.MagicSchoolBehavior.Level + 1)
        };
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

        //var maxLevel = ConfigurationManager.Settings.MaxLevel;
        var maxLevel = byte.MaxValue;
        var isOverMax = levelByte > maxLevel;
        if (isOverMax) {
            InformSenderClient($"You cannot set level higher than the max level ({maxLevel}).");
            return;
        }

        var msg = new CHARACTER_103_PROTOCOL.MSG_LEVELUP() {
            NewLevel = levelByte
        };
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

    [Command("name")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetNameCommand([Remainder]string name) {
        // Set the name of the character.
        Context.Character.SetNameOverride(name);

        InformSenderClient($"Set name to {name}. Relog to see changes.");
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
}
