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
 * Last Updated: 09/26/2026
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
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.Effects;
using Imlight.CoreLib.Game.Elixirs;
using Imlight.CoreLib.Game.Pet;
using Imlight.CoreLib.Game.Requirements;
using Imlight.CoreLib.Game.Requirements.Contexts;
using Imlight.CoreLib.Game.Results;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

internal class EquipmentService(SessionActor sessionActor) : MessageService(sessionActor) {

    private const string ELIXIR_TICK_TIMER_KEY = "elixir-tick";
    private const int ELIXIR_SAVE_INTERVAL_SECONDS = 30;

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
    private ulong _summonedPetId;
    private int _secondsSinceLastElixirSave;
    private bool _isElixirTickerStarted;

    internal static bool HidePetWhileMounted => s_hidePetWhileMounted;

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new EquipmentService(parentActor));

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

            // Restore active elixirs HUD countdown and start the playtime ticker.
            RestoreActiveElixirs(playerCharacter);
            StartElixirPlaytimeTicker();
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
            var infractionText = $"Player tried to equip item {itemId} that they do not have in their inventory!";
            account.AddInfraction(InfractionType.SuspiciousBehavior, infractionText);

            Logger.Warning("Player tried to equip item {0} that they do not have in their inventory."
                        + " This has been logged as suspicious behavior.",
                Logger.Args(itemId));

            return;
        }

        // Check if the item is an elixir being used from inventory.
        var itemTemplate = ItemHelper.GetItemTemplate(item);
        if (itemTemplate?.m_equipRequirements != null) {
            var context = new GenericRequirementContext(
                itemTemplate.m_equipRequirements,
                SessionActor.ActorRef,
                wizard.GetInitializedGameObject(),
                wizard);

            if (!RequirementDispatcher.EvaluateRequirements(itemTemplate.m_equipRequirements, context)) {
                Logger.Warning("Player {0} tried to equip/use item {1} (template {2}) without meeting equip requirements.",
                    Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), itemId, item.m_templateID));
                return;
            }
        }
        var elixirTemplate = itemTemplate?.m_behaviors?.OfType<ElixirBehaviorTemplate>().FirstOrDefault();
        if (elixirTemplate is not null) {
            EquipElixirFromInventory(wizard, item, itemTemplate, elixirTemplate);
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

        // A mount equipped indoors is dismounted right away; the reconcile no-ops in the open world.
        if (Enum.TryParse<EquipmentSlotType>(message.SlotName, true, out var equippedSlot)) {
            if (equippedSlot == EquipmentSlotType.Mount) {
              SessionActor.ActorRef.Tell(new ZONE_102_PROTOCOL.MSG_ENFORCEINTERIORMOUNT());
              if (s_hidePetWhileMounted) {
                  DismissPetEntity();
              }
            }
            else if (equippedSlot == EquipmentSlotType.PIITeleportEffect) {
                ZoneBroadcast(new WIZARD2_53_PROTOCOL.MSG_UPDATETELEPORTEFFECT {
                    TeleportEffect = (uint) item.m_templateID,
                    PlayerGID = wizard.GameObjectID
                }, false);
            }
            else if (equippedSlot == EquipmentSlotType.PIIVictoryDance) {
                ZoneBroadcast(new WIZARD2_53_PROTOCOL.MSG_UPDATEAFTERCOMBATDANCE {
                    AfterCombatDance = (uint) item.m_templateID,
                    PlayerGID = wizard.GameObjectID
                }, false);
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

        // Active elixirs cannot be transferred back to backpack inventory.
        var slotObj = wizEquipmentBehavior.SlotList?.Find(s => s.ItemId == itemId);
        if (slotObj?.SlotType == EquipmentSlotType.Elixir) {
            Logger.Warning("Player {0} tried to unequip elixir {1} to backpack inventory.",
                Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), itemId));
            return;
        }

        // The client's unequip request carries no slot name, so the pet is recognized by its slot.
        var isPet = wizEquipmentBehavior.GetEquippedPetId() == itemId;

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

        // A manual unequip abandons any pending auto-remount.
        if (Enum.TryParse<EquipmentSlotType>(message.SlotName, true, out var unequippedSlot)
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

        var isTeleportEffect = slotObj?.SlotType == EquipmentSlotType.PIITeleportEffect;
        if (isTeleportEffect) {
            ZoneBroadcast(new WIZARD2_53_PROTOCOL.MSG_UPDATETELEPORTEFFECT {
                TeleportEffect = 0,
                PlayerGID = wizard.GameObjectID
            }, false);
        }

        var isVictoryDance = slotObj?.SlotType == EquipmentSlotType.PIIVictoryDance;
        if (isVictoryDance) {
            ZoneBroadcast(new WIZARD2_53_PROTOCOL.MSG_UPDATEAFTERCOMBATDANCE {
                AfterCombatDance = 0,
                PlayerGID = wizard.GameObjectID
            }, false);
        }
    }

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

    private void EquipElixirFromInventory(Wizard wizard,
                                          WizClientObjectItem item,
                                          WizItemTemplate itemTemplate,
                                          ElixirBehaviorTemplate elixirTemplate) {
        uint durationSeconds = 0;
        if (!string.IsNullOrEmpty(elixirTemplate.m_expireTime)) {
            _ = uint.TryParse(elixirTemplate.m_expireTime.ToString(), out durationSeconds);
        }

        // Case 0: Level Up or World Elixir
        if (elixirTemplate is LevelUpElixirBehaviorTemplate levelUpElixir) {
            if (levelUpElixir.m_setCharacterToLevel > 0 && wizard.MagicSchoolBehavior.Level >= levelUpElixir.m_setCharacterToLevel) {
                Logger.Warning("Wizard {0} (level {1}) tried to use level-up elixir {2} with target level {3}.",
                    Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), wizard.MagicSchoolBehavior.Level, item.m_templateID, levelUpElixir.m_setCharacterToLevel));
                return;
            }
            if (!wizard.InventoryBehavior.RemoveItem(item.m_globalID, out _)) {
                return;
            }

            WizardItemCollection.RemoveItem(wizard.CharId, item.m_globalID);
            WizardCollection.UpdateCharacterItems(wizard);

            SendToSocket(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_REMOVEITEM {
                GlobalID = wizard.GameObjectID,
                ItemID = item.m_globalID
            });

            LevelUpElixirHelper.ApplyElixir(
                wizard,
                levelUpElixir,
                SendToSocket,
                (msg, selfless, target) => ZoneBroadcast(msg, selfless, target),
                Teleport,
                PlaySound
            );
            return;
        }

        // Case 1: Instant refill (duration == 0).
        if (durationSeconds == 0) {
            if (!wizard.InventoryBehavior.RemoveItem(item.m_globalID, out _)) {
                return;
            }

            WizardItemCollection.RemoveItem(wizard.CharId, item.m_globalID);
            WizardCollection.UpdateCharacterItems(wizard);

            SendToSocket(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_REMOVEITEM {
                GlobalID = wizard.GameObjectID,
                ItemID = item.m_globalID
            });

            if (elixirTemplate.m_equipActionList?.m_results?.Count > 0) {
                ResultDispatcher.ExecuteResults(
                    Context,
                    elixirTemplate.m_equipActionList,
                    SessionActor.ActorRef,
                    wizard.GetInitializedGameObject());
            }

            PlaySound(elixirTemplate.m_equipSound);
            SyncHealthAndMana(wizard);
            return;
        }

        // Case 2: Timed elixir.
        var equipBehavior = wizard.EquipmentBehavior;
        if (!equipBehavior.CanEquipElixir(elixirTemplate)) {
            Logger.Warning("Player tried to equip elixir {0} but cannot equip it.", Logger.Args(item.m_globalID));
            return;
        }

        if (!wizard.InventoryBehavior.RemoveItem(item.m_globalID, out _)) {
            return;
        }

        if (CoreObjectFactory.FindBehaviorInstance<ClientElixirBehavior>(item, out var clientElixir)) {
            clientElixir.m_expireTime = durationSeconds;
            clientElixir.m_statsApplied = true;
        }

        if (!equipBehavior.EquipElixir(item, durationSeconds)) {
            wizard.InventoryBehavior.AddItem(item);
            return;
        }

        var addedEffects = CharacterEffectHelper.AddEffectsToWizard(wizard, itemTemplate);

        WizardCollection.UpdateCharacterItems(wizard);

        // Transfer the item from ClientInventoryBehavior to ClientEquipmentBehavior on the client.
        SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM {
            ItemID = item.m_globalID,
            SlotName = "Elixir",
            IsEquip = 1
        });

        SendAddEffects(addedEffects);

        // Start HUD countdown timer (which also dispatches EQUIPMENT_EquipItem on the client) and enable HUD icon.
        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_SETELIXIRTIMER {
            GlobalID = item.m_globalID,
            TimerTime = durationSeconds
        });

        SendToSocket(new WIZARD_12_PROTOCOL.MSG_ELIXIRSTATECHANGE {
            parentID = item.m_globalID,
            EffectEnabled = 1
        });

        PlaySound(elixirTemplate.m_equipSound);
        SyncHealthAndMana(wizard);
        StartElixirPlaytimeTicker();
    }

    private void RestoreActiveElixirs(Wizard wizard) {
        var equipment = wizard.EquipmentBehavior;
        if (equipment?.SlotList is null) {
            return;
        }

        equipment.SyncActiveElixirTimes();

        var expiredSlots = new List<EquipmentSlot>();
        foreach (var (slot, _) in equipment.GetActiveElixirs()) {
            if (slot.RemainingSeconds > 0) {
                // Resume client HUD timer and icon.
                SendToSocket(new WIZARD2_53_PROTOCOL.MSG_SETELIXIRTIMER {
                    GlobalID = slot.ItemId.Value,
                    TimerTime = slot.RemainingSeconds
                });

                SendToSocket(new WIZARD_12_PROTOCOL.MSG_ELIXIRSTATECHANGE {
                    parentID = slot.ItemId.Value,
                    EffectEnabled = 1
                });
            }
            else {
                expiredSlots.Add(slot);
            }
        }

        foreach (var expiredSlot in expiredSlots) {
            ExpireElixir(wizard, expiredSlot);
        }
    }

    private void StartElixirPlaytimeTicker() {
        if (_isElixirTickerStarted) {
            return;
        }

        Timers.StartPeriodicTimer(
            ELIXIR_TICK_TIMER_KEY,
            new CHARACTER_103_PROTOCOL.MSG_DOELIXIRTICK(),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1));
        _isElixirTickerStarted = true;
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_DOELIXIRTICK))]
    private void ReceiveElixirPlaytimeTick(CHARACTER_103_PROTOCOL.MSG_DOELIXIRTICK message) {
        var wizard = GetActiveWizard();
        var equipment = wizard?.EquipmentBehavior;
        if (equipment?.SlotList is null) {
            return;
        }

        var activeElixirs = equipment.GetActiveElixirs().ToList();
        if (activeElixirs.Count == 0) {
            return;
        }

        var stateChanged = false;
        var expiredSlots = new List<EquipmentSlot>();

        foreach (var (slot, item) in activeElixirs) {
            if (slot.RemainingSeconds > 0) {
                slot.RemainingSeconds--;

                if (CoreObjectFactory.FindBehaviorInstance<ClientElixirBehavior>(item, out var clientElixir)) {
                    clientElixir.m_expireTime = slot.RemainingSeconds;
                }

                stateChanged = true;

                if (slot.RemainingSeconds == 0) {
                    expiredSlots.Add(slot);
                }
            }
            else {
                expiredSlots.Add(slot);
            }
        }

        foreach (var expiredSlot in expiredSlots) {
            ExpireElixir(wizard, expiredSlot);
        }

        if (stateChanged) {
            _secondsSinceLastElixirSave++;
            if (_secondsSinceLastElixirSave >= ELIXIR_SAVE_INTERVAL_SECONDS) {
                _secondsSinceLastElixirSave = 0;
                WizardCollection.UpdateCharacterItems(wizard);
            }
        }
    }

    private void ExpireElixir(Wizard wizard, EquipmentSlot slot) {
        var itemId = slot.ItemId.Value;
        Logger.Information("Elixir {0} ({1}) expired for character {2}",
            Logger.Args(slot.ItemName, itemId, wizard.CharId));

        // Destroy client HUD button and unequip item in client.
        SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_UNEQUIPITEM {
            GlobalID = wizard.GameObjectID,
            ItemID = itemId
        });

        // Remove effects from wizard and client.
        var item = wizard.EquipmentBehavior.GetItem(itemId);
        if (item is not null) {
            var template = ItemHelper.GetItemTemplate(item);
            if (template is not null) {
                var removedEffects = CharacterEffectHelper.RemoveEffectsFromWizard(wizard, template);
                if (removedEffects is { Count: > 0 }) {
                    SendRemoveEffects(removedEffects);
                }

                if (template.m_behaviors?.OfType<ElixirBehaviorTemplate>().FirstOrDefault() is { } elixirTemplate) {
                    PlaySound(elixirTemplate.m_unequipSound);
                }
            }
        }

        // Remove from server equipment behavior and database.
        wizard.EquipmentBehavior.UnequipItem(itemId);
        WizardItemCollection.RemoveItem(wizard.CharId, itemId);
        WizardCollection.UpdateCharacterItems(wizard);

        SyncHealthAndMana(wizard, clampToMax: true);
    }

    private void SyncHealthAndMana(Wizard wizard, bool clampToMax = false) {
        var clientStats = wizard.GameStats.GetClientTypeAlternative();
        if (clampToMax) {
            if (wizard.GameStats.m_currentHitpoints > clientStats.m_baseHitpoints) {
                wizard.GameStats.m_currentHitpoints = clientStats.m_baseHitpoints;
            }

            if (wizard.GameStats.m_currentMana > clientStats.m_baseMana) {
                wizard.GameStats.m_currentMana = clientStats.m_baseMana;
            }
        }

        SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEHEALTH {
            CharacterID = wizard.GameObjectID,
            NewHealth = wizard.GameStats.m_currentHitpoints,
            NewHealthMax = clientStats.m_baseHitpoints,
            DisplayDiff = 1
        });
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEMANA {
            Mana = wizard.GameStats.m_currentMana,
            MaxMana = clientStats.m_baseMana,
            DisplayDiff = 1
        });
    }

    protected override void OnPreDispose() {
        Timers.Cancel(ELIXIR_TICK_TIMER_KEY);

        try {
            var wizard = GetActiveWizard();
            if (wizard?.EquipmentBehavior?.SlotList is not null
                && wizard.EquipmentBehavior.SlotList.Any(s => s.SlotType == EquipmentSlotType.Elixir)) {
                WizardCollection.UpdateCharacterItems(wizard);
            }
        }
        catch (Exception ex) {
            Logger.Warning("Failed to persist elixirs during pre-dispose: {0}", Logger.Args(ex.Message));
        }

        base.OnPreDispose();
    }

}

