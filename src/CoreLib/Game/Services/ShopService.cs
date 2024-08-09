/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using System;
using Imlight.Common;
using Imlight.Common.IO;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Game.Zone;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.World;
using Imlight.CoreLib.WizardData.Models.Player;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Services;
internal class ShopService : MessageService {
    // Notice: It is currently unknown where this constant originates from. It is used on MSG_SHOPBUYREQUEST
    // to know which item a player has purchased. That being, this constant + the template ID of the item.
    // If you see it change, please let us know.
    private const long ShopOffset = 9895604649984;

    private readonly CoreObjectSerializer _itemSerializer = new CoreObjectSerializer()
                    .OnBehaviors(SerializerOptions.Behaviors.None)
                    .OnPropertyMask((SerializerOptions.PropertyFlags) 1);

    public ShopService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new InteractService(parentActor));

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_SHOPBUYREQUEST))]
    private void ReceiveShopBuyRequest(WIZARD_12_PROTOCOL.MSG_SHOPBUYREQUEST message) {
        var wizard = GetActiveWizard();
        var npc = GetZoneObject(message.npcGlobalID);

        // Check to see if the NPC exists in the zone.
        if (npc == null) {
            Logger.Warning("Failed to find NPC {0} in zone for shop purchase", Logger.Args(message.npcGlobalID));

            var shopDenyMsg = new WIZARD_12_PROTOCOL.MSG_SHOPBUYCONFIRM {
                Failure = 1,
                WebFailure = 0,
                Credits = 0
            };
            SendToSocket(shopDenyMsg);
            return;
        }

        var itemTemplateID = message.ShopID - ShopOffset;
        var npcObject = (WizardZoneNpc) npc;

        // Check to see if the shopkeeper actually sells the item.
        if (!npcObject.Inventory.Contains((GID) itemTemplateID)) {
            var shopDenyMsg = new WIZARD_12_PROTOCOL.MSG_SHOPBUYCONFIRM {
                Failure = 1,
                WebFailure = 0,
                Credits = 0
            };
            SendToSocket(shopDenyMsg);

            // Log infraction
            var account = GetActiveAccount();
            var infractionText = $"Player tried to purchase item {itemTemplateID} from NPC " +
                $"{message.npcGlobalID} that is not in its inventory!";
            account.AddInfraction(InfractionType.SuspiciousBehavior, infractionText);

            Logger.Warning("Player tried to purchase item {0} from an NPC that it did not have in its inventory."
                + " This has been logged as suspicious behavior.",
                Logger.Args(itemTemplateID));

            return;
        }

        var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(itemTemplateID);
        var item = (WizClientObjectItem) CoreObjectFactory.FinalizeCoreObject(itemTemplateID);
        item.m_primaryColor = message.texture;
        item.m_secondaryColor = message.decal;

        var goldCost = (int) template.m_baseCost;
        if (template.m_numPrimaryColors != 1 && template.m_numSecondaryColors != 0) {
            goldCost = (int) Math.Ceiling(goldCost * 1.225f) + 1; // Dyed items are more expensive.
        }

        // Deny transaction if player cannot afford item
        if (goldCost > wizard.GameStats.m_currentGold) {
            var shopDenyMsg = new WIZARD_12_PROTOCOL.MSG_SHOPBUYCONFIRM {
                Failure = 1,
                WebFailure = 0,
                Credits = 0
            };
            SendToSocket(shopDenyMsg);
            return;
        }

        // Add the item to the player's inventory
        var data = _itemSerializer.Serialize(item);
        var addItemMsg = new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM {
            GlobalID = wizard.CharId,
            SerializedItem = data,
        };
        SendToSocket(addItemMsg);

        // Add the item to the player's inventory. We do this after sending the message to the client
        // because adding it to the inventory will initialize all the behaviors. The client will crash
        // if we serialize those behaviors.
        wizard.AddItemToInventory(item);

        // Inform the client of the new item
        var itemAcqMsg = new WIZARD2_53_PROTOCOL.MSG_ITEMACQUISITION {
            ItemGlobalID = item.m_globalID,
            ItemTemplateID = (uint) itemTemplateID,
            ItemLocation = 1,
        };
        SendToSocket(itemAcqMsg);

        // Update and inform of new gold balance
        wizard.RemoveGold(goldCost);
        var goldUpdateMsg = new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD {
            Gold = wizard.GameStats.m_currentGold,
            MaxGold = wizard.GameStats.m_baseGoldPouch
        };
        SendToSocket(goldUpdateMsg);

        // Inform the client that all previous transactions were successful
        var shopConfirmMsg = new WIZARD_12_PROTOCOL.MSG_SHOPBUYCONFIRM {
            Failure = 0,
            WebFailure = 0,
            Credits = 0
        };
        SendToSocket(shopConfirmMsg);
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_SHOPSELLREQUEST))]
    private void ReceiveShopSellRequest(WIZARD_12_PROTOCOL.MSG_SHOPSELLREQUEST message) {
        var wizard = GetActiveWizard();
        var item = wizard.InventoryBehavior.GetItem(message.GlobalID);

        var removedItemSuccess = wizard.RemoveItemFromInventory(message.GlobalID);
        if (!removedItemSuccess) {
            return;
        }

        var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(item.m_templateID);

        var gold = (int) Math.Ceiling(template.m_baseCost * 0.05f);
        if (template.m_numPrimaryColors != 1 && template.m_numSecondaryColors != 0) {
            gold = (int) Math.Ceiling(gold * 1.225f); // This value is slightly higher for some reason.
        }

        wizard.AddGold(gold);

        var updateGoldMsg = new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD {
            Gold = wizard.GameStats.m_currentGold,
            MaxGold = wizard.GameStats.m_baseGoldPouch
        };
        SendToSocket(updateGoldMsg);

        var removeItemMsg = new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_REMOVEITEM {
            GlobalID = wizard.CharId,
            ItemID = message.GlobalID
        };
        SendToSocket(removeItemMsg);
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_DYEREQUEST))]
    private void ReceiveDyeRequest(WIZARD_12_PROTOCOL.MSG_DYEREQUEST message) {
        var wizard = GetActiveWizard();
        var item = wizard.InventoryBehavior.GetItem(message.itemGlobalID);
        var isEquipped = false;
        if (item == null) {
            // Failsafe: item may be equipped instead
            item = wizard.EquipmentBehavior.GetItem(message.itemGlobalID);

            if (item == null) {
                Logger.Error("Failed to find item {0} in inventory for dyes", Logger.Args(message.itemGlobalID));

                var dyeDenyMsg = new WIZARD_12_PROTOCOL.MSG_DYECONFIRM { Failure = 1 };
                SendToSocket(dyeDenyMsg);
                return;
            }
            else {
                isEquipped = true;
            }
        }

        // Cost is 22.5% of the item's base cost
        var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(item.m_templateID);
        var dyeCost = (int) Math.Ceiling(template.m_baseCost * 0.225f);
        if (dyeCost > wizard.GameStats.m_currentGold) {
            var dyeDenyMsg = new WIZARD_12_PROTOCOL.MSG_DYECONFIRM { Failure = 1 };
            SendToSocket(dyeDenyMsg);
            return;
        }

        // Deduct the cost from the player
        wizard.RemoveGold(dyeCost);
        var goldUpdateMsg = new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD {
            Gold = wizard.GameStats.m_currentGold,
            MaxGold = wizard.GameStats.m_baseGoldPouch
        };
        SendToSocket(goldUpdateMsg);

        // Apply the dyes
        var texture = (DyeColor) message.texture;
        var decal = (DyeColor) message.decal;
        var decal2 = (DyeColor) message.decal2;
        DyeMapper.ApplyAllDye(item, texture, decal, decal2);

        // If the item was equipped, tell the client to re-equip it
        if (isEquipped) {
            var slotIndex = wizard.EquipmentBehavior.GetSlotOfItem(item.m_globalID);
            var slotName = ItemHelper.GetItemSlot(template).ToString();

            // Unequip newly dyed item
            var unequipMsg = new GAME_5_PROTOCOL.MSG_EQUIPITEM() {
                ItemID = item.m_globalID,
                SlotName = slotName,
                IsEquip = 0
            };
            SendToSocket(unequipMsg);

            var publicUnequipMsg = new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICUNEQUIPITEM() {
                GlobalID = wizard.CharId,
                IndexToRemove = slotIndex
            };
            ZoneBroadcast(publicUnequipMsg, false);

            // Reequip item
            var equipMsg = new GAME_5_PROTOCOL.MSG_EQUIPITEM() {
                ItemID = item.m_globalID,
                SlotName = slotName,
                IsEquip = 1
            };
            SendToSocket(equipMsg);

            var pubItem = ItemHelper.GetPublicItem(item);
            var data = _itemSerializer.Serialize(pubItem);
            ZoneBroadcast(new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICEQUIPITEM() {
                GlobalID = wizard.CharId,
                SerializedInfo = data
            }, false);
        }

        // Confirm success to the client.
        var msgConfirm = new WIZARD_12_PROTOCOL.MSG_DYECONFIRM {
            Failure = 0,
            itemGID = message.itemGlobalID,
            firstLayer = message.texture,
            secondLayer = message.decal,
            thirdLayer = message.decal2
        };
        SendToSocket(msgConfirm);
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_DONESHOPPING))]
    private void ReceiveDoneShopping(WIZARD_12_PROTOCOL.MSG_DONESHOPPING message) {
        // A wizard has complete shopping and is leaving the shop.
        var wizard = GetActiveWizard();

        // Reenable player movement
        var enableMovementStateMsg = new GAME_5_PROTOCOL.MSG_ENTERSTATE() {
            GameObjectID = wizard.CharId,
            State = 1685237158,
            Data = "",
            IgnoreIfCurrentStateIsOff = 0
        };
        SendToSocket(enableMovementStateMsg);

        var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG() {
            GameObjectID = wizard.CharId,
            WizBangID = (uint) WizBangs.None
        };
        ZoneBroadcast(wizBangMsg, false);
    }
}
