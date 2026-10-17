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
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
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
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.Game.Pet;
using Imlight.CoreLib.Game.Rentals;
using Imlight.CoreLib.WizardData.Models.Player;

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
    private static readonly bool s_hidePetWhileMounted
        = ConfigurationManager.Settings["Character.HidePetWhileMounted"].AsBool();
    private const long EXPIRED_RENTAL_GRACE_SECONDS = 60;

    private readonly Dictionary<ulong, long> _expiredRentals = [];
    private ulong _summonedPetId;

    internal static bool HidePetWhileMounted => s_hidePetWhileMounted;

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

    private void EquipItem(GAME_5_PROTOCOL.MSG_EQUIPITEM message) {
        var wizard = GetActiveWizard();
        var account = GetActiveAccount();
        var itemId = message.ItemID;

        var item = wizard.InventoryBehavior.GetItem(itemId);
        if (item is null) {
            // The client may have clicked in the moment its rental ran out.
            if (WasRentalJustExpired(itemId)) {
                return;
            }

            var infractionText = $"Player tried to equip item {itemId} that they do not have in their inventory!";
            account.AddInfraction(InfractionType.SuspiciousBehavior, infractionText);

            Logger.Warning("Player tried to equip item {0} that they do not have in their inventory."
                        + " This has been logged as suspicious behavior.",
                Logger.Args(itemId));

            return;
        }

        if (RentalItems.TryGetBehavior(item, out var rental) && RentalItems.IsDue(rental, DateTimeOffset.UtcNow.ToUnixTimeSeconds())) {
            Logger.Debug("Player tried to equip rental {0}, which has run out.", Logger.Args(itemId));
            TellOtherServices(new CHARACTER_103_PROTOCOL.MSG_CHECKRENTALS());

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

        if (RentalItems.TryGetBehavior(item, out _)) {
            TellOtherServices(new CHARACTER_103_PROTOCOL.MSG_STARTRENTAL { ItemId = itemId });
        }

        // A mount equipped indoors is dismounted right away; the reconcile no-ops in the open world.
        if (Enum.TryParse<EquipmentSlotType>(message.SlotName, true, out var equippedSlot)
            && equippedSlot == EquipmentSlotType.Mount) {
            SessionActor.ActorRef.Tell(new ZONE_102_PROTOCOL.MSG_ENFORCEINTERIORMOUNT());
            if (s_hidePetWhileMounted) {
                DismissPetEntity();
            }
        }

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
        var wizEquipmentBehavior = GetActiveWizard().EquipmentBehavior;
        var account = GetActiveAccount();
        var itemId = message.ItemID;

        // Check to see if the player has this item equipped. If they don't, log an infraction.
        if (!wizEquipmentBehavior.HasItemEquipped(itemId)) {
            if (WasRentalJustExpired(itemId)) {
                return;
            }

            var infractionText = $"Player tried to unequip item {itemId} that they do not have in their inventory!";
            account.AddInfraction(InfractionType.SuspiciousBehavior, infractionText);

            Logger.Warning("Player tried to unequip item {0} that they do not have in their inventory."
                        + " This has been logged as a suspicious behavior infraction.",
                Logger.Args(itemId));

            return;
        }

        UnequipById(itemId, message.SlotName);
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_UNEQUIPITEM))]
    private void ReceiveUnequipItem(CHARACTER_103_PROTOCOL.MSG_UNEQUIPITEM message) {
        var wizEquipmentBehavior = GetActiveWizard().EquipmentBehavior;

        try {
            var slot = wizEquipmentBehavior.GetSlotOfItem(message.ItemId);
            if (slot < wizEquipmentBehavior.SlotList.Count) {
                UnequipById(message.ItemId, wizEquipmentBehavior.SlotList[slot].SlotType.ToString());
            }
        }
        catch (Exception ex) {
            Logger.Error("Error while unequipping item {0}: {1} {2}",
                Logger.Args(message.ItemId, ex.Message, ex.StackTrace));
        }

        Sender.Tell(new CHARACTER_103_PROTOCOL.MSG_UNEQUIPITEMRSP {
            Success = !wizEquipmentBehavior.HasItemEquipped(message.ItemId)
        });
    }

    private void UnequipById(ulong itemId, ByteString slotName) {
        var wizard = GetActiveWizard();
        var wizEquipmentBehavior = wizard.EquipmentBehavior;

        // This needs to be done before we unequip the item, because we need to know what slot it was in.
        var slot = wizEquipmentBehavior.GetSlotOfItem(itemId);

        // The client's unequip request carries no slot name, so the pet is recognized by its slot.
        var isPet = wizEquipmentBehavior.GetEquippedPetId() == itemId;

        if (!wizard.EquipmentToInventoryTransfer(itemId, out var removedEffects)) {
            // If this fails, there is perhaps desync between the server and the client.
            // Send a message to the client to assure them that the server does not have the item equipped.
            SendUnequipItem(slotName, slot, itemId);

            Logger.Warning("Unequip failed on item {0}",
                Logger.Args(itemId));

            return;
        }

        SendUnequipItem(slotName, slot, itemId);
        SendRemoveEffects(removedEffects);

        // A manual unequip abandons any pending auto-remount.
        if (Enum.TryParse<EquipmentSlotType>(slotName, true, out var unequippedSlot)
            && unequippedSlot == EquipmentSlotType.Mount
            && wizard.InteriorStowedMountId != 0) {
            wizard.InteriorStowedMountId = 0;
            WizardCollection.UpdateCharacterInteriorStowedMount(wizard);
        }

        if (s_hidePetWhileMounted && (EquipmentSlotType) slot == EquipmentSlotType.Mount) {
            SpawnPetEntity();
        }

        if (isPet) {
            DismissPetEntity();
        }
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_EXPIRERENTAL))]
    private void ReceiveExpireRental(CHARACTER_103_PROTOCOL.MSG_EXPIRERENTAL message) {
        try {
            var wizard = GetActiveWizard();
            var item = wizard.EquipmentBehavior.GetItem(message.ItemId) ?? wizard.InventoryBehavior.GetItem(message.ItemId);

            // A message that outlived its item, or one sent before the timer ran out, changes nothing.
            if (item is null || !RentalItems.TryGetBehavior(item, out var behavior)) {
                return;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (!RentalItems.IsDue(behavior, now)) {
                return;
            }

            if (!wizard.RemoveRentalItem(message.ItemId, out var removal)) {
                return;
            }

            RememberExpiredRental(message.ItemId);

            // The client moves a worn item to its backpack with the unequip, and the remove only touches the backpack.
            if (removal.WasEquipped) {
                SendUnequipItem(removal.SlotName, removal.SlotIndex, message.ItemId);
                SendRemoveEffects(removal.RemovedEffects);

                if (s_hidePetWhileMounted && removal.SlotType == EquipmentSlotType.Mount) {
                    SpawnPetEntity();
                }
                if (removal.SlotType == EquipmentSlotType.Pet) {
                    DismissPetEntity();
                }
            }

            ItemSync.SendRemoved(SessionActor.ActorRef, wizard, message.ItemId);
            SendToSocket(new WIZARD_12_PROTOCOL.MSG_RENTALUPDATE { TemplateID = removal.TemplateId, Reason = 0 });
        } catch (Exception ex) {
            Logger.Error("Error while expiring rental {0}: {1} {2}", Logger.Args(message.ItemId, ex.Message, ex.StackTrace));
        }
    }

    private void RememberExpiredRental(ulong itemId) {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var staleIds = _expiredRentals
            .Where(expired => now - expired.Value > EXPIRED_RENTAL_GRACE_SECONDS)
            .Select(expired => expired.Key)
            .ToList();
        foreach (var staleId in staleIds) {
            _expiredRentals.Remove(staleId);
        }

        _expiredRentals[itemId] = now;
    }

    private bool WasRentalJustExpired(ulong itemId)
        => _expiredRentals.TryGetValue(itemId, out var expiredAt)
            && DateTimeOffset.UtcNow.ToUnixTimeSeconds() - expiredAt <= EXPIRED_RENTAL_GRACE_SECONDS;

    private void SpawnPetEntity(WizClientObjectItem petItem = null) {
        var zoneActor = SessionActor.GetZoneActor();
        if (zoneActor is null) {
            return;
        }

        if (s_hidePetWhileMounted && GetActiveWizard()?.EquipmentBehavior?.GetItemInSlot(EquipmentSlotType.Mount) is not null) {
            DismissPetEntity();

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

        var playerObj = GetActiveGameObject();
        if (playerObj is null) {
            return;
        }

        // The generic "PetObject" template (ID 2) is used for all pet zone entities.
        // The specific breed appearance comes from behaviors on the template.
        var coreObj = PetFactory.CreatePetGameObject(petItem, playerObj.m_globalID);
        if (coreObj is null) {
            return;
        }

        // Place the pet at the player's location.
        coreObj.m_location = playerObj.m_location;
        coreObj.m_orientation = playerObj.m_orientation;

        // Only one pet follows its owner: a swap or a repeated attach replaces the previous summon.
        DismissPetEntity();

        var spawnMsg = new ZONE_102_PROTOCOL.MSG_SPAWNENTITY {
            CoreObject = coreObj,
            Template = CoreObjectFactory.GetCoreTemplate(PetFactory.GENERIC_PET_TEMPLATE_ID),
            Requester = SessionActor.ActorRef
        };

        zoneActor.Tell(spawnMsg, Self);
        _summonedPetId = coreObj.m_globalID;
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_RESUMMONPET))]
    private void ReceiveResummonPet(CHARACTER_103_PROTOCOL.MSG_RESUMMONPET message) {
        // A pet that is not out picks the change up on its next summon.
        if (_summonedPetId == 0) {
            return;
        }

        var equipment = GetActiveWizard()?.EquipmentBehavior;
        if (equipment is null || equipment.GetEquippedPetId() != message.PetItemId) {
            return;
        }

        SpawnPetEntity(equipment.GetItem(message.PetItemId));
    }

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_SYNC_PET_FOR_MOUNT))]
    private void ReceiveSyncPetForMount()
        => SpawnPetEntity();

    private void DismissPetEntity() {
        if (_summonedPetId == 0) {
            return;
        }

        // Sent straight to the zone, like the spawn, so a dismiss never overtakes the spawn it targets.
        SessionActor.GetZoneActor()?.Tell(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Messages = [new ZONE_102_PROTOCOL.MSG_DISMISSPET { PetGlobalId = _summonedPetId }],
            Targets = ZoneBroadcastTarget.Objects,
        }, Self);

        _summonedPetId = 0;
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ENFORCEINTERIORMOUNT))]
    private void ReceiveEnforceInteriorMount(ZONE_102_PROTOCOL.MSG_ENFORCEINTERIORMOUNT message) {
        // Zone data flags disallow mounts (m_noMounts): really unequip on entry (model and speed effect) and
        // re-equip on leaving. The stowed GID is persisted so it survives the zone transfer. Force (a
        // dungeon-sigil pad on a street) stows the mount regardless of the zone's no-mounts flag.
        try {
            var wizard = GetActiveWizard();
            var equip = wizard.EquipmentBehavior;

            if (message.Force || ZoneDisallowsMounts()) {
                var mount = equip.GetItemInSlot(EquipmentSlotType.Mount);
                if (mount is null) {
                    return; // not mounted, nothing to dismount
                }

                var slot = equip.GetSlotOfItem(mount.m_globalID);
                if (!wizard.EquipmentToInventoryTransfer(mount.m_globalID, out var removedEffects)) {
                    Logger.Warning("Interior dismount transfer failed for mount {0}", Logger.Args(mount.m_globalID));

                    return;
                }

                wizard.InteriorStowedMountId = mount.m_globalID;
                WizardCollection.UpdateCharacterInteriorStowedMount(wizard);
                SendUnequipItem("Mount", slot, mount.m_globalID); // drops the model client-side
                SendRemoveEffects(removedEffects);                // drops the mount speed effect
                if (s_hidePetWhileMounted) {
                    SpawnPetEntity();
                }
            }
            else if (wizard.InteriorStowedMountId != 0) {
                var gid = wizard.InteriorStowedMountId;
                wizard.InteriorStowedMountId = 0;
                WizardCollection.UpdateCharacterInteriorStowedMount(wizard);

                if (!wizard.InventoryToEquipmentTransfer(gid, out var addedEffects, out _)) {
                    Logger.Warning("Outdoor remount transfer failed for mount {0}", Logger.Args(gid));

                    return;
                }

                var item = equip.GetItemInSlot(EquipmentSlotType.Mount);
                if (item is not null) {
                    SendEquipItem(item, "Mount");
                }
                SendAddEffects(addedEffects); // restores the mount speed
                if (s_hidePetWhileMounted) {
                    DismissPetEntity();
                }
            }
        }
        catch (Exception ex) {
            Logger.Error("Error while reconciling the mount for the zone: {0} {1}",
                Logger.Args(ex.Message, ex.StackTrace));
        }
    }

    private bool ZoneDisallowsMounts() {
        var zoneActor = SessionActor.GetZoneActor();
        if (zoneActor is null) {
            return false;
        }

        var rsp = zoneActor.Ask<ZONE_102_PROTOCOL.MSG_QUERYZONEDATARSP>(
            new ZONE_102_PROTOCOL.MSG_QUERYZONEDATA(), TimeSpan.FromSeconds(5)).Result;

        return rsp?.ZoneData?.m_noMounts ?? false;
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
            wizardObj = GetActiveWizard()?.GetInitializedGameObject();

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

