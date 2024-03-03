/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Imlight.Common.Caches;
using Imlight.Common.Utilities;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Resources;
using static Imlight.Common.Caches.TypeCache;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.Common;

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

        var itemTemplateID = message.ShopID - ShopOffset; // Do this, for some reason

        // Todo: should double check here to make sure this shopkeeper even sells this object.

        var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(itemTemplateID);
        var item = (WizClientObjectItem) CoreObjectFactory.FinalizeCoreObject(itemTemplateID);
        item.m_primaryColor = message.texture;
        item.m_secondaryColor = message.decal;

        var goldCost = (int) Math.Ceiling(template.m_baseCost * 1.2275f); // Necessary to match client values, Wizard101 taxes?

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

        wizard.AddItemToInventory(item);

        // Add the item to the player's inventory
        var data = _itemSerializer.Serialize(item);
        var addItemMsg = new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM {
            GlobalID = wizard.CharId,
            SerializedItem = data,
        };
        SendToSocket(addItemMsg);

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

        var removedItemSuccess = wizard.InventoryBehavior.RemoveItem(message.GlobalID, out var item);
        if (!removedItemSuccess) {
            return;
        }

        var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(item.m_templateID);
        var gold = (int) (template.m_baseCost * 0.05f);

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

        // Cost is 25% of the item's base cost
        var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(item.m_templateID);
        var dyeCost = (int) Math.Ceiling(template.m_baseCost * 0.25f);
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
        var decal = (DyeColor) message.decal2;
        var decal2 = (DyeColor) message.decal2;
        DyeMapper.ApplyAllDye(item, texture, decal, decal2);

        // todo: If the item was equipped, tell the client to re-equip it

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

        var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG() {
            GameObjectID = wizard.CharId,
            WizBangID = 0
        };
        ZoneBroadcast(wizBangMsg, false);
    }
}
