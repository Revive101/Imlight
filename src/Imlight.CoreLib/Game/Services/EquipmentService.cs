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
 *
 * ========================================================================
 * EQUIPMENT SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages player equipment interactions, including item equipping, 
 * unequipping, and associated effect handling.
 * 
 * USAGE EXAMPLE:
 * Internal service handling equipment-related messages within the 
 * game server session.
 * 
 * NOTE:
 * - Supports item equip and unequip operations
 * - Manages equipment-related effects and broadcasts
 * - Implements security checks for suspicious equipment actions
 * 
 * TODO:
 * - Enhance error handling for equipment transfers
 * - Review and improve effect serialization mechanisms
 * - Implement additional validation for equipment actions
 * 
 * Created by: Joji
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Collections.Generic;
using Akka.Actor;
using Imcodec.CoreObject;
using Imcodec.IO;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.Game.Pet;

namespace Imlight.CoreLib.Game.Services;

internal class EquipmentService(SessionActor sessionActor) : MessageService(sessionActor) {

    private readonly CoreObjectSerializer _itemSerializer = new(
        versionable: false,
        behaviors: SerializerFlags.None
    );
    private readonly CoreObjectSerializer _effectSerializer = new(
        versionable: false,
        behaviors: SerializerFlags.None
    );

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new InventoryService(parentActor));

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_EQUIPITEM))]
    private void ReceiveEquipItem(GAME_5_PROTOCOL.MSG_EQUIPITEM message) {
        try {
            if (message.IsEquip == 1) {
                EquipItem(message);
            }
            else {
                UnEquipItem(message);
            }
        }
        catch (Exception ex) {
            Logger.Error("Error while equipping item: {0} {1}", Logger.Args(ex.Message, ex.StackTrace));
        }
    }

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE))]
    private void ReceiveAttachComplete(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE message) {
        try {
            var playerCharacter = GetActiveWizard();
            var effects = playerCharacter.GameEffects;

            SendAddEffects([.. effects]);

            // Spawn the equipped pet as a zone entity if one is equipped.
            if (playerCharacter.PetOwnerBehavior.EquippedPetTemplateId != 0) {
                SpawnPetEntity();
            }
        }
        catch (Exception ex) {
            Logger.Error("Error while attaching effects: {0} {1}",
                Logger.Args(ex.Message, ex.StackTrace));

            throw new ServiceRetryException("Error while attaching effects.", ex);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_SPAWNENTITYRSP))]
    private void ReceiveSpawnEntityResponse(ZONE_102_PROTOCOL.MSG_SPAWNENTITYRSP message) {
        if (message.SpawnedObject is null) {
            return;
        }

        // The zone has created the ZoneEntity for the pet, so now we need to leash it to the player and position it correctly.

        var playerObj = GetActiveGameObject();
        if (playerObj is null) {
            return;
        }

        // MSG_LEASH: tell the client this zone entity is leashed to the player.
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_LEASH {
            GlobalID = message.SpawnedObject.m_globalID,
            OwnerID = playerObj.m_globalID,
            Leashed = 1
        });

        // MSG_LEASHOFFSET: position the pet behind the player.
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_LEASHOFFSET {
            GlobalID = message.SpawnedObject.m_globalID,
            Radius = 75f,
            Angle = 180f
        });
    }

    private void EquipItem(GAME_5_PROTOCOL.MSG_EQUIPITEM message) {
        var wizard = GetActiveWizard();
        var account = GetActiveAccount();
        var itemId = message.ItemID;

        var item = wizard.InventoryBehavior.GetItem(itemId);
        if (item is null) {
            var infractionText = $"Player tried to equip item {itemId} that they do not have in their inventory!";
            account.AddInfraction(InfractionType.SuspiciousBehavior, infractionText);

            Logger.Warning("Player tried to equip item {0} that they do not have in their inventory."
                        + " This has been logged as suspicious behavior.",
                Logger.Args(itemId));

            return;
        }

        // Check to see if the player already has this item equipped. If they do, broadcast the removal of it.
        // We don't have to remove it here because the InventoryToEquipmentTransfer method will do that for us.
        if (wizard.EquipmentBehavior.SlotInUse(message.SlotName, out var index)) {
            // Debug log.
            Logger.Debug("{0} tried to equip item {1} in slot {2} that is already in use. Unequipping from index {3}",
                Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), itemId, message.SlotName, index));

            // Get the item that is currently equipped in this slot.
            var equippedItemId = wizard.EquipmentBehavior.GetItemInSlot(message.SlotName).m_globalID;
            SendUnequipItem(message.SlotName, index, equippedItemId);
        }

        if (!wizard.InventoryToEquipmentTransfer(itemId, out var effects, out var removedEffects)) {
            Logger.Warning("Equip failed on item {0}",
                Logger.Args(itemId));

            return;
        }

        SendEquipItem(item, message.SlotName);
        SendAddEffects(effects);

        // If removedEffects is not null, the Wizard replaced an item with another item that has different effects.
        // We need to remove the old effects from the client.
        if (removedEffects is not null) {
            SendRemoveEffects(removedEffects);
        }

        // If this is a pet, spawn it as a zone entity so it appears in the world.
        if (message.SlotName == "Pet") {
            SpawnPetEntity(item);
        }
    }

    private void UnEquipItem(GAME_5_PROTOCOL.MSG_EQUIPITEM message) {
        var wizard = GetActiveWizard();
        var wizEquipmentBehavior = wizard.EquipmentBehavior;
        var account = GetActiveAccount();
        var itemId = message.ItemID;

        // Check to see if the player has this item equipped. If they don't, log an infraction.
        if (!wizEquipmentBehavior.HasItemEquipped(itemId)) {
            var infractionText = $"Player tried to unequip item {itemId} that they do not have in their inventory!";
            account.AddInfraction(InfractionType.SuspiciousBehavior, infractionText);

            Logger.Warning("Player tried to unequip item {0} that they do not have in their inventory."
                        + " This has been logged as a suspicious behavior infraction.",
                Logger.Args(itemId));

            return;
        }

        // This needs to be done before we unequip the item, because we need to know what slot it was in.
        var slot = wizEquipmentBehavior.GetSlotOfItem(itemId);

        if (!wizard.EquipmentToInventoryTransfer(itemId, out var removedEffects)) {
            // If this fails, there is perhaps desync between the server and the client.
            // Send a message to the client to assure them that the server does not have the item equipped.
            SendUnequipItem(message.SlotName, slot, itemId);

            Logger.Warning("Unequip failed on item {0}",
                Logger.Args(itemId));

            return;
        }

        SendUnequipItem(message.SlotName, slot, itemId);
        SendRemoveEffects(removedEffects);

        // TODO: Unleash pet zone entity when spawn/despawn is implemented.
    }

    private void SpawnPetEntity(WizClientObjectItem petItem = null) {
        var zoneActor = SessionActor.GetZoneActor();
        if (zoneActor is null) {
            return;
        }

        // If petItem is null, we will use the equipped pet from the player's character.
        if (petItem is null) {
            var wizard = GetActiveWizard();
            if (wizard is null) {
                return;
            }

            var wizEquipmentBehavior = wizard.EquipmentBehavior;
            if (wizEquipmentBehavior is null) {
                return;
            }

            var equippedPetId = wizEquipmentBehavior.GetEquippedPetId();
            if (equippedPetId == 0) {
                return;
            }

            petItem = wizEquipmentBehavior.GetItem(equippedPetId);
        }

        // The generic "PetObject" template (ID 2) is used for all pet zone entities.
        // The specific breed appearance comes from behaviors on the template.
        var coreObj = PetFactory.CreatePetGameObject(petItem);

        // Place the pet at the player's location.
        var playerObj = GetActiveGameObject();
        if (playerObj is not null) {
            coreObj.m_location = playerObj.m_location;
        }

        var spawnMsg = new ZONE_102_PROTOCOL.MSG_SPAWNENTITY {
            CoreObject = coreObj,
            Template = template,
            Requester = SessionActor.ActorRef
        };

        zoneActor.Tell(spawnMsg, Self);
    }

    private void SendEquipItem(WizClientObjectItem item, string slotName) {
        // Confirm to the player that we've equipped their item server side.
        SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM() {
            ItemID = item.m_globalID,
            SlotName = slotName,
            IsEquip = 1
        });

        // Serialize item and broadcast equip action to other players.
        var pubItem = ItemHelper.GetPublicItem(item);

        if (!_itemSerializer.Serialize(pubItem, 1, out var data)) {
            Logger.Error("Failed to serialize item {0} for equip broadcast.",
                Logger.Args(item.m_globalID));

            return;
        }

        ZoneBroadcast(new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICEQUIPITEM() {
            GlobalID = GetActiveGameObject().m_globalID,
            SerializedInfo = data
        }, false);
    }

    private void SendUnequipItem(ByteString slotName, byte slot, ulong itemId) {
        // This one goes to the client.
        SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM() {
            ItemID = itemId,
            SlotName = slotName,
            IsEquip = 0
        });

        // This one goes to the zone.
        ZoneBroadcast(new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICUNEQUIPITEM() {
            GlobalID = GetActiveGameObject().m_globalID,
            IndexToRemove = slot
        }, false);
    }

    private void SendAddEffects(List<GameEffectBase> effects) {
        if (effects is null || effects.Count == 0) {
            return;
        }

        // This may fail since it is accessed immediately after attach. This means the CharacterService
        // hasn't had enough time to set its Wizard reference yet.
        var wizardObj = GetActiveGameObject();
        if (wizardObj is null) {
            wizardObj = GetActiveWizard()?.GameObject;

            if (wizardObj is null) {
                throw new ServiceRetryException("Failed to get active game object for add effects broadcast.");
            }
        }

        var charObjId = wizardObj.m_globalID;

        foreach (var effect in effects) {
            var flags = PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit;
            if (!_effectSerializer.Serialize(effect, flags, out var effectSerializedData)) {
                Logger.Error("Failed to serialize effect {0} for add effects broadcast.",
                    Logger.Args(effect.m_effectNameID));

                continue;
            }

            SendToSocket(new GAME_5_PROTOCOL.MSG_ADDEFFECT() {
                GameObjectID = charObjId,
                EffectData = effectSerializedData
            });
        }
    }

    private void SendRemoveEffects(List<GameEffectBase> effects) {
        var charObjId = GetActiveGameObject().m_globalID;

        foreach (var effect in effects) {
            SendToSocket(new GAME_5_PROTOCOL.MSG_REMOVEEFFECT() {
                GameObjectID = charObjId,
                EffectNameID = effect.m_effectNameID,
                InternalID = effect.m_internalID,
            });
        }
    }
}

