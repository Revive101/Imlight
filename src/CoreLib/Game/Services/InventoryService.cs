/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using System.Collections.Generic;
using Akka.Actor;
using Akka.Util.Internal;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.Utilities;
using Imlight.Common.Cryptography;
using Imlight.Common.ObjectProperty;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Resources;
using static Imlight.Common.Caches.TypeCache;
using static Imlight.Common.ObjectProperty.SerializerOptions;

namespace Imlight.CoreLib.Game.Services;

public class InventoryService : MessageService {

    public InventoryService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new InventoryService(parentActor));

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTRADIALQUICKCHAT))]
    private void ReceiveRequestRadialQuickChat(GAME_5_PROTOCOL.MSG_REQUESTRADIALQUICKCHAT message) {
        // todo: make this a chat command
        new int[] { 2066, 860841451, 2537945, 203556948 }.ForEach(spellId => {
            SendToSocket(new WIZARD_12_PROTOCOL.MSG_ADDSPELLTOBOOK() {
                SpellID = spellId
            });
        });
    }

    #region Destroy/Feed Inventoryitem

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_TRASHINVENTORYITEM))]
    private void ReceiveTrashInventoryItem(GAME_5_PROTOCOL.MSG_TRASHINVENTORYITEM message) {
        var wizard = GetActiveWizard();

        wizard.RemoveItemFromInventory(message.GlobalID);

        SendToSocket(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_REMOVEITEM() {
            GlobalID = wizard.GameObject.m_globalID,
            ItemID = message.GlobalID
        });
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_FEEDINVENTORYITEM))]
    private void ReceiveFeedInventoryItem(GAME_5_PROTOCOL.MSG_FEEDINVENTORYITEM message) {
        SendToSocket(new GAME_5_PROTOCOL.MSG_FEEDINVENTORYITEM() {
            FedObjectID = message.FedObjectID,
            PetID = message.PetID,
        });
    }

    #endregion

    #region Quicksell from Inventory

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_REQUESTQUICKSELL))]
    private void ReceiveRequestQuickSell(WIZARD_12_PROTOCOL.MSG_REQUESTQUICKSELL message) {
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_REQUESTQUICKSELL() {
            FromTemplateID = message.FromTemplateID,
            Section = message.Section,
            SellModifier = message.SellModifier + 0.05f, // (?) Live server uses ~0.05f.
        });
    }

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_QUICKSELLREQUEST))]
    private void ReceiveQuickSellRequest(WIZARD2_53_PROTOCOL.MSG_QUICKSELLREQUEST message) {
        var serializer = new ObjectSerializer()
          .OnBehaviors(SerializerOptions.Behaviors.None)
          .OnPropertyMask((SerializerOptions.PropertyFlags) 4);

        var wizard = GetActiveWizard();

        var quickSellItemList = (QuickSellItemList) serializer.Deserialize(message.Data);
        int goldSum = 0;

        // Remove items from inventory and equipment, tally up gold sum.
        foreach (QuickSellItem quickSellItem in quickSellItemList.m_quickSellItemList) {
            var item = wizard.InventoryBehavior.GetItem(quickSellItem.m_sellItemGID);
            var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(item.m_templateID);

            wizard.RemoveItemFromInventory(item.m_globalID);

            // Some items (snack, reagents) are stackable.
            for (int i = 0; i < quickSellItem.m_quantity; i++) {
                SendToSocket(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_REMOVEITEM() {
                    GlobalID = wizard.CharId,
                    ItemID = quickSellItem.m_sellItemGID
                });

                goldSum += (int) Math.Ceiling(template.m_baseCost * 0.05f);
            }
        }

        // Update player with their new gold balance.
        wizard.GameStats.m_currentGold += goldSum;
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD() {
            Gold = wizard.GameStats.m_currentGold,
            MaxGold = wizard.GameStats.m_baseGoldPouch,
        });

        // End quicksell process with empty message.
        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_QUICKSELLREQUEST() {});
    }

    #endregion

    #region Jewels

    // JEWELS
    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_EQUIPJEWELREQUEST))]
    private void ReceiveEquipJewelRequest(WIZARD2_53_PROTOCOL.MSG_EQUIPJEWELREQUEST message) {
        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_EQUIPJEWELREQUEST() {
            ItemGID = message.ItemGID,
            JewelGID = message.JewelGID,
            SocketNumber = message.SocketNumber,
        });

        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_EQUIPJEWELTOITEM() {
            ItemGID = message.ItemGID,
            JewelGID = message.JewelGID,
            SocketNumber = message.SocketNumber,
            GlobalID = RandomGen.GenerateGUID()
        });
    }

    #endregion

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PLAYERWIZBANG))]
    private void ReceivePlayerWizbang(WIZARD_12_PROTOCOL.MSG_PLAYERWIZBANG message) {
        var wizard = GetActiveWizard();

        switch (message.StateName) {
            case "SpellbookWizbang":
                ZoneBroadcast(new GAME_5_PROTOCOL.MSG_WIZBANG() {
                    GameObjectID = wizard.GameObject.m_globalID,
                    WizBangID = StringHash.Compute("Registrar")
                }, false);
                break;
            default:
                ZoneBroadcast(new GAME_5_PROTOCOL.MSG_WIZBANG() {
                    GameObjectID = wizard.GameObject.m_globalID,
                    WizBangID = 0
                }, false);
                break;
        }
    }

}
