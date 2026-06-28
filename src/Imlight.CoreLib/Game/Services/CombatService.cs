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
 * COMBAT SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages player combat interactions, including duel mechanics, 
 * mount equipment, and post-combat state management.
 * 
 * USAGE EXAMPLE:
 * Internal service handling combat-related messages and player 
 * state transitions during battles.
 * 
 * NOTE:
 * - Handles duel entry, combat moves, and victory/defeat scenarios
 * - Manages mount equipment and unequipment during combat
 * - Implements no-aggro grace period after combat
 * 
 * TODO:
 * - Improve error handling for equipment transfers
 * 
 * Created by: Jooty
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
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

internal class CombatService(SessionActor sessionActor) : MessageService(sessionActor) {

    private const uint NO_AGGRO_EFFECT_STRINGID = 1618528611;
    private const uint NO_AGGRO_EFFECT_DURATION_IN_SECONDS = 3;

    private readonly CoreObjectSerializer _effectSerializer = new(
        behaviors: SerializerFlags.None
    );
    private readonly CoreObjectSerializer _itemSerializer = new(
        behaviors: SerializerFlags.None
    );

    private IActorRef _currentDuelActor;
    private ulong _cachedMountId;

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new CombatService(parentActor));

    protected override void OnDispose() {
        var message = new GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT();
        _currentDuelActor?.Tell(message, SessionActor.ActorRef);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_ACTORADDEDTODUEL))]
    private void RecieveDuelAdd(COMBAT_106_PROTOCOL.MSG_ACTORADDEDTODUEL message) {
        _currentDuelActor = message.DuelActor;

        //Unequip mounts if the player has one equipped
        UnEquipMount();

        // Set the persistent location and orientation of the wizard
        var wizard = GetActiveWizard();
        wizard.SetPersistentLocation(message.SlotPosition);

        // Orientation is given in radians. It must be converted to degrees and then to a byte.
        var orientationRadians = message.SlotOrientation;
        var orientationDegrees = (float) (orientationRadians * (180 / Math.PI));
        var orientation = (byte) (orientationDegrees / 360 * 256);
        wizard.SetPersistentOrientation(orientation);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_COMBATDEFEAT))]
    private void ReceiveCombatDefeat(COMBAT_106_PROTOCOL.MSG_COMBATDEFEAT message) {
        EquipMountSubtle();

        // We've fled or have been defeated in this duel. Send us back to the world hub.
        var hubMsg = new ZONE_102_PROTOCOL.MSG_SENDTOHUB();
        TellOtherServices(hubMsg);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_COMBATWIN))]
    private void ReceiveCombatVictory(COMBAT_106_PROTOCOL.MSG_COMBATWIN message) {
        EquipMount();
        SetNoAggroGrace();

        // Send a message to ourselves to end the no aggro grace period.
        var noAggroGraceOverMsg = new COMBAT_106_PROTOCOL.MSG_NOAGGROGRACEOVER();
        Timers.StartSingleTimer(
            "NoAggroGraceOver",
            noAggroGraceOverMsg,
            TimeSpan.FromSeconds(NO_AGGRO_EFFECT_DURATION_IN_SECONDS));

        // Gain 3 XP per pip used in the duel.
        var usedPips = message.UsedPips;
        var xpGained = usedPips * 3;

        var msg = new CHARACTER_103_PROTOCOL.MSG_GAINXP {
            XP = xpGained,
        };
        TellOtherServices(msg);
    }

    [MessageHandler(typeof(DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_COMBATDRAW))]
    private void ReceiveCombatDraw(DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_COMBATDRAW message) {
        if (_currentDuelActor == null) {
            return;
        }

        var msg = new COMBAT_106_PROTOCOL.MSG_ACTORCOMBATDRAW {
            Actor = SessionActor.ActorRef
        };
        _currentDuelActor.Tell(msg);
    }

    [MessageHandler(typeof(DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_COMBATMOVE))]
    private void ReceiveCombatMove(DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_COMBATMOVE message) {
        if (_currentDuelActor == null) {
            throw new Exception("Combat move received without a duel actor.");
        }

        // The spell target given by the client is logarithmic. We need to convert it to a linear scale.
        // A selection of 0 means a target of self.
        int actualSelection = (int) Math.Log(message.SpellTarget, 2);

        var msg = new COMBAT_106_PROTOCOL.MSG_ACTORCOMBATMOVE {
            Actor = SessionActor.ActorRef,
            MoveType = message.MoveType,
            SpellSelection = message.SpellSelection,
            SpellTarget = (uint) actualSelection,
            TimeLeft = message.TimeLeft
        };
        _currentDuelActor.Tell(msg);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_NOAGGROGRACEOVER))]
    private void ReceiveNoAggroGraceOver()
        => RemoveNoAggroEffect();

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT))]
    private void ReceiveClientDisconnect(GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT message)
        => _currentDuelActor?.Tell(message, SessionActor.ActorRef);

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_QUERY_LOGOUT))]
    private void ReceiveQueryLogout(GAME_5_PROTOCOL.MSG_QUERY_LOGOUT message)
        => _currentDuelActor?.Tell(message, SessionActor.ActorRef);

    private void EquipMount() {
        if (_cachedMountId == 0) {
            // We don't have a cached mount.
            return;
        }

        var wizard = GetActiveWizard();
        var wizEquipmentBehavior = wizard.EquipmentBehavior;
        var slot = wizEquipmentBehavior.GetSlotOfItem(_cachedMountId);

        // We can discard the removed effects because we know for sure they are not present.
        if (!wizard.InventoryToEquipmentTransfer(_cachedMountId, out var addedEffects, out var _)) {
            // If this fails, there is perhaps desync between the server and the client.
            // Send a message to the client to assure them that the server does not have the item equipped.
            SendUnequipItem("Mount", slot, _cachedMountId);

            Logger.Warning("Equip failed on item {0}", Logger.Args(_cachedMountId));
            return;
        }

        var item = wizEquipmentBehavior.GetItemInSlot(EquipmentSlotType.Mount);

        SendEquipItem(item, "Mount");
        SendAddEffects(addedEffects);
        _cachedMountId = 0;
    }

    private void EquipMountSubtle() {
        // If we have a cached mount, equip it.
        // Don't inform the client of it.
        if (_cachedMountId == 0) {
            return;
        }

        var wizard = GetActiveWizard();
        if (!wizard.InventoryToEquipmentTransfer(_cachedMountId, out var _, out var _)) {
            Logger.Warning("Equip failed on item {0}", Logger.Args(_cachedMountId));
            return;
        }

        _cachedMountId = 0;
    }

    private void UnEquipMount() {
        var wizard = GetActiveWizard();
        var wizEquipmentBehavior = wizard.EquipmentBehavior;

        var item = wizEquipmentBehavior.GetItemInSlot(EquipmentSlotType.Mount);
        if (item == null) {
            // We don't have a mount equipped.
            return;
        }

        _cachedMountId = item.m_globalID;
        var slot = wizEquipmentBehavior.GetSlotOfItem(_cachedMountId);

        if (!wizard.EquipmentToInventoryTransfer(_cachedMountId, out var removedEffects)) {
            // If this fails, there is perhaps desync between the server and the client.
            // Send a message to the client to assure them that the server does not have the item equipped.
            SendUnequipItem("Mount", slot, _cachedMountId);

            Logger.Warning("Unequip failed on item {0}", Logger.Args(_cachedMountId));
            return;
        }

        SendUnequipItem("Mount", slot, _cachedMountId);
        SendRemoveEffects(removedEffects);
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

        if (_itemSerializer.Serialize(pubItem, PropertyFlags.Prop_Transmit, out var data)) {
            Logger.Error("Failed to serialize item {0}", Logger.Args(item.m_globalID));

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
            wizardObj = GetActiveWizard().GameObject;

            if (wizardObj is null) {
                Logger.Error("Failed to get wizard object for adding effects.");
                return;
            }
        }

        var charObjId = wizardObj.m_globalID;

        foreach (var effect in effects) {
            var flags = PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit;
            if (!_effectSerializer.Serialize(effect, flags, out var effectSerializedData)) {
                Logger.Error("Failed to serialize effect {0}", Logger.Args(effect.m_effectNameID));

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

    private void SetNoAggroGrace() {
        var wizard = GetActiveWizard();
        var charObjId = GetActiveGameObject().m_globalID;
        var effectInternalId = wizard.GameEffects.Count + 1;

        var epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var endTime = epoch + NO_AGGRO_EFFECT_DURATION_IN_SECONDS;

        var effect = new NamedEffect {
            m_effectNameID = NO_AGGRO_EFFECT_STRINGID,
            m_internalID = effectInternalId,
            m_endTime = (uint) endTime,
            m_overrideName = "NoAggro"
        };

        wizard.GameEffects.Add(effect);
        wizard.IsInCombatGrace = true;

        if (!_effectSerializer.Serialize(effect, PropertyFlags.Prop_Transmit, out var effectSerializedData)) {
            Logger.Error("Failed to serialize effect {0}", Logger.Args(effect.m_effectNameID));

            return;
        }

        SendToSocket(new GAME_5_PROTOCOL.MSG_ADDEFFECT() {
            GameObjectID = charObjId,
            EffectData = effectSerializedData
        });
    }

    private void RemoveNoAggroEffect() {
        var wizard = GetActiveWizard();
        var charObjId = GetActiveGameObject().m_globalID;

        var effect = wizard.GameEffects.Find(e => e.m_effectNameID == NO_AGGRO_EFFECT_STRINGID);
        if (effect is null) {
            return;
        }

        wizard.GameEffects.Remove(effect);
        wizard.IsInCombatGrace = false;

        SendToSocket(new GAME_5_PROTOCOL.MSG_REMOVEEFFECT() {
            GameObjectID = charObjId,
            EffectNameID = effect.m_effectNameID,
            InternalID = effect.m_internalID,
        });
    }

}
