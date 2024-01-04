using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.Cryptography;
using Imlight.Common.ObjectProperty;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Services;

public class EquipmentService : MessageService {
    private readonly CoreObjectSerializer _itemSerializer = new CoreObjectSerializer()
                    .OnBehaviors(SerializerOptions.Behaviors.None)
                    .OnPropertyMask((SerializerOptions.PropertyFlags) 1);
    private readonly CoreObjectSerializer _effectSerializer = new CoreObjectSerializer()
                    .OnBehaviors(SerializerOptions.Behaviors.None)
                    .OnPropertyMask(SerializerOptions.PropertyFlags.Transmit
                                  | SerializerOptions.PropertyFlags.AuthorityTransmit);

    public EquipmentService(SessionActor sessionActor) : base(sessionActor) { }

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
        // This doesn't have to be done in a try/catch.
        try {
            // We don't persistently save equipment effects, so we need to re-apply them on login.
            // This is the very first step in that process. Start by telling the Wizard to apply
            // all of the effects for the equipment it has equipped.
            var playerCharacter = GetActiveWizard();
            playerCharacter.ApplyEffectsForAllEquipment();

            // Now that the effects have been applied, we need to tell the client about them.
            var effects = playerCharacter.GameEffects;
            SendAddEffects(effects);
        }
        catch  (Exception ex) {
            Logger.Error("Error while attaching effects: {0} {1}", Logger.Args(ex.Message, ex.StackTrace));
        }
    }

    private void EquipItem(GAME_5_PROTOCOL.MSG_EQUIPITEM message) {
        var wizard = GetActiveWizard();
        var account = GetActiveAccount();
        var itemId = message.ItemID;

        var item = wizard.InventoryGetItem(itemId);
        if (item is null) {
            var infractionText = $"Player tried to equip item {itemId} that they do not have in their inventory!";
            account.AddInfraction(InfractionType.SuspiciousBehavior, infractionText);

            Logger.Warning("Player tried to equip item {0} that they do not have in their inventory."
                        + " This has been logged as suspicious behavior.",
                Logger.Args(itemId));

            return;
        }

        // Todo: Check if player meets requirements to equip item. If not, log an infraction.

        // Check to see if the player already has this item equipped. If they do, broadcast the removal of it.
        var slot = wizard.EquipmentGetItemSlotIndex(itemId);
        var hasItemEquipped = wizard.EquipmentHasEquippedItem(itemId);

        var addedEffects = wizard.EquipmentEquipItem(itemId);
        if (addedEffects is null) {
            Logger.Warning("Equip failed on item {0}", Logger.Args(itemId));
            return;
        }

        if (hasItemEquipped) {
            SendUnequipItem(slot, itemId);
        }

        SendEquipItem(item, message.SlotName);
        SendAddEffects(addedEffects);
    }

    private void UnEquipItem(GAME_5_PROTOCOL.MSG_EQUIPITEM message) {
        var playerCharacter = GetActiveWizard();
        var account = GetActiveAccount();
        var itemId = message.ItemID;

        // Check to see if the player has this item equipped. If they don't, log an infraction.
        var item = playerCharacter.InventoryGetItem(itemId);
        if (item is null) {
            var infractionText = $"Player tried to unequip item {itemId} that they do not have in their inventory!";
            account.AddInfraction(InfractionType.SuspiciousBehavior, infractionText);

            Logger.Warning("Player tried to unequip item {0} that they do not have in their inventory."
                        + " This has been logged as a suspicious behavior infraction.",
                Logger.Args(itemId));

            return;
        }

        // This needs to be done before we unequip the item, because we need to know what slot it was in.
        var slot = playerCharacter.EquipmentGetItemSlotIndex(itemId);

        var removedEffects = playerCharacter.EquipmentUnequipItem(itemId);
        if (removedEffects is null) {
            Logger.Warning("Unequip failed on item {0}", Logger.Args(itemId));
            return;
        }

        SendUnequipItem(slot, itemId);
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
        var data = _itemSerializer.Serialize(pubItem);
        ZoneBroadcast(new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICEQUIPITEM() {
            GlobalID = GetActiveGameObject().m_globalID,
            SerializedInfo = data
        }, false);
    }

    private void SendUnequipItem(byte slot, ulong itemId) {
        // This one goes to the client.
        SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM() {
            ItemID = itemId,
            // The client doesn't really care about these fields below.
            SlotName = "",
            IsEquip = 0
        });

        // This one goes to the zone.
        ZoneBroadcast(new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICUNEQUIPITEM() {
            GlobalID = GetActiveGameObject().m_globalID,
            IndexToRemove = slot
        }, false);
    }

    private void SendAddEffects(List<GameEffectBase> effects) {
        var charObjId = GetActiveGameObject().m_globalID;

        foreach (var effect in effects) {
            var effectSerializedData = _effectSerializer.Serialize(effect);
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
