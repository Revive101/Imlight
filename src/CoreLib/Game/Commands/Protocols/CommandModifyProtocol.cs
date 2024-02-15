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
        var isOverMax = (Context.Character.Level + 1) > ConfigurationManager.Settings.MaxLevel;
        if (isOverMax) {
            InformSenderClient("You cannot level up any further.");
            return;
        }

        var msg = new CHARACTER_103_PROTOCOL.MSG_LEVELUP() {
            NewLevel = (byte) (Context.Character.Level + 1)
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

        var maxLevel = ConfigurationManager.Settings.MaxLevel;
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

        var coreObject = (WizClientObjectItem)CoreObjectFactory.FinalizeCoreObject(templateIdLong);
        var addedItemSuccess = Context.Character.InventoryBehavior.AddItem(coreObject);

        if (!addedItemSuccess) {
            InformSenderClient("Could not add item to inventory.");
            return;
        }

        // todo: Why doesn't this work? No changes on client. I checked disasembly and we have the right flags here.
        //var serializer = new CoreObjectSerializer()
        //    .OnBehaviors(SerializerOptions.Behaviors.None)
        //    .OnPropertyMask((SerializerOptions.PropertyFlags)24);
        //var networkMessage = new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM {
        //    GlobalID = coreObject.m_globalID,
        //    SerializedItem = serializer.Serialize(coreObject)
        //};
        //Context.SessionActor.Tell(networkMessage, null);

        InformSenderClient($"Added item {coreObject.m_debugName} to inventory. Relog to see changes.");
    }
}
