/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * INVENTORY SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages player inventory interactions, including item management, 
 * quick selling, and special item handling.
 * 
 * USAGE EXAMPLE:
 * Internal service handling various inventory-related messages within 
 * the game server session.
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty, Joji
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.Shared.Utilities;

namespace Imlight.CoreLib.Game.Services;

internal class InventoryService(SessionActor sessionActor) : MessageService(sessionActor) {

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new InventoryService(parentActor));

    #region Destroy/Feed Inventoryitem

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_TRASHINVENTORYITEM))]
    private void ReceiveTrashInventoryItem(GAME_5_PROTOCOL.MSG_TRASHINVENTORYITEM message) {
        var wizard = GetActiveWizard();

        wizard.RemoveItemFromInventory(message.GlobalID);

        SendToSocket(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_REMOVEITEM() {
            GlobalID = wizard.CharId,
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
        var serializer = new ObjectSerializer(
            Behaviors: SerializerFlags.None
        );

        var wizard = GetActiveWizard();
        int goldSum = 0;

        if (!serializer.Deserialize<QuickSellItemList>(message.Data, 4, out var quickSellItemList)) {
            Logger.Log.Error("Failed to deserialize quicksell item list.");

            return;
        }

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

                var value = (int) Math.Ceiling(template.m_baseCost * 0.05f);
                if (template.m_numPrimaryColors != 1 && template.m_numSecondaryColors != 0) {
                    value = (int) Math.Ceiling(value * 1.2275f); // Dyed items are more expensive.
                }

                goldSum += value;
            }
        }

        // Update player with their new gold balance.
        wizard.AddGold(goldSum);
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD() {
            Gold = wizard.GameStats.m_currentGold,
            MaxGold = wizard.GameStats.m_baseGoldPouch,
        });

        // End quicksell process with empty message.
        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_QUICKSELLREQUEST());
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

    #region Snacks

    [MessageHandler(typeof(PET_9_PROTOCOL.MSG_PETSNACKREMOVEREQUEST))]
    private void ReceivePetSnackRemoveRequest(PET_9_PROTOCOL.MSG_PETSNACKREMOVEREQUEST message) {
        var wizard = GetActiveWizard();

        var hasSnack = wizard.PetSnackBehavior.HasSnackID(message.GlobalID);
        if (!hasSnack) {
            Logger.Log.Debug("Tried to remove snack with global id {0} that does not exist in player snack bag.",
                Logger.Args(message.GlobalID));

            return;
        }

        wizard.RemoveSnack(message.GlobalID, out var updatedSnack);

        if (updatedSnack.m_quantity > 0) {
            SendToSocket(new PET_9_PROTOCOL.MSG_PETSNACKUPDATE() {
                GlobalID = wizard.CharId,
                ItemID = updatedSnack.m_globalID,
                Quantity = updatedSnack.m_quantity
            });

            return;
        }

        SendToSocket(new PET_9_PROTOCOL.MSG_PETSNACKREMOVE() {
            GlobalID = wizard.CharId,
            ItemID = updatedSnack.m_globalID,
        });
    }

    #endregion

    #region Reagents

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_REAGENTREMOVEREQUEST))]
    private void ReceiveReagentRemoveRequest(WIZARD_12_PROTOCOL.MSG_REAGENTREMOVEREQUEST message) {
        var wizard = GetActiveWizard();

        var hasReagent = wizard.AlchemyBehavior.HasReageant(message.GlobalID);
        if (!hasReagent) {
            Logger.Log.Debug("Tried to remove reagent with global id {0} that does not exist in player reagent bag.",
                Logger.Args(message.GlobalID));

            return;
        }

        var reagent = wizard.AlchemyBehavior.GetReagent(message.GlobalID);
        wizard.RemoveReagent(reagent.m_globalID, out var updatedReagent);

        if (updatedReagent.m_quantity > 0) {
            SendToSocket(new WIZARD_12_PROTOCOL.MSG_REAGENTUPDATE() {
                GlobalID = wizard.CharId,
                ItemID = updatedReagent.m_globalID,
                Quantity = updatedReagent.m_quantity
            });

            return;
        }

        SendToSocket(new WIZARD_12_PROTOCOL.MSG_REAGENTREMOVE() {
            GlobalID = wizard.CharId,
            ItemID = updatedReagent.m_globalID,
        });
    }

    #endregion

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PLAYERWIZBANG))]
    private void ReceivePlayerWizbang(WIZARD_12_PROTOCOL.MSG_PLAYERWIZBANG message) {
        var wizard = GetActiveWizard();

        switch (message.StateName) {
            case "SpellbookWizbang":
                ZoneBroadcast(new GAME_5_PROTOCOL.MSG_WIZBANG() {
                    GameObjectID = wizard.CharId,
                    WizBangID = StringHash.Compute("Registrar")
                }, false);
                break;
            default:
                ZoneBroadcast(new GAME_5_PROTOCOL.MSG_WIZBANG() {
                    GameObjectID = wizard.CharId,
                    WizBangID = 0
                }, false);
                break;
        }
    }

}
