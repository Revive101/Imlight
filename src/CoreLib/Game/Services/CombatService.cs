/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.IO;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.IO;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Misc;
using static Imlight.Common.Caches.TypeCache;


namespace Imlight.CoreLib.Game.Services;

public class CombatService : MessageService {
    private IActorRef _currentDuelActor;

    public CombatService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor) => Akka.Actor.Props.Create(() => new CombatService(parentActor));

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_ACTORADDEDTODUEL))]
    private void RecieveDuelAdd(COMBAT_106_PROTOCOL.MSG_ACTORADDEDTODUEL message) {
        var wizard = GetActiveWizard();

        _currentDuelActor = message.DuelActor;
        //Unequip mounts if the player has one equipped
        UnEquipMount();

        // Set the persistent location and orientation of the wizard
        wizard.SetPersistentLocation(message.SlotPosition);

        // Orientation is given in radians. It must be converted to degrees and then to a byte.
        var orientationRadians = message.SlotOrientation;
        var orientationDegrees = (float)(orientationRadians * (180 / Math.PI));
        var orientation = (byte)(orientationDegrees / 360 * 256);
        wizard.SetPersistentOrientation(orientation);
    }

    [MessageHandler(typeof(WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATMOVE))]
    private void ReceiveCombatMove(WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATMOVE message) {
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

    private void UnEquipMount() {
        var wizard = GetActiveWizard();
        var wizEquipmentBehavior = wizard.EquipmentBehavior;
        ulong itemId;

        try {
            itemId = wizEquipmentBehavior.GetItemInSlot(WizardData.Models.Player.EquipmentSlotType.Mount).m_globalID;
        }
        catch (Exception ex) {
            //Logger.Debug("Player Has no mount equipped good to go");
            return;
        }

        // This needs to be done before we unequip the item, because we need to know what slot it was in.
        var slot = wizEquipmentBehavior.GetSlotOfItem(itemId);

        if (!wizard.EquipmentToInventoryTransfer(itemId, out var removedEffects)) {
            // If this fails, there is perhaps desync between the server and the client.
            // Send a message to the client to assure them that the server does not have the item equipped.
            SendUnequipItem("Mount", slot, itemId);

            Logger.Warning("Unequip failed on item {0}", Logger.Args(itemId));
            return;
        }

        SendUnequipItem("Mount", slot, itemId);
        SendRemoveEffects(removedEffects);
    }

    private void SendUnequipItem(Common.IO.ByteString slotName, byte slot, ulong itemId) {
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
