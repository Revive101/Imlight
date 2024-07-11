/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Collections;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Services;

internal class AuctionHouseService : MessageService {
    private readonly CoreObjectSerializer _itemSerializer = new CoreObjectSerializer()
                    .OnBehaviors(SerializerOptions.Behaviors.None)
                    .OnPropertyMask((SerializerOptions.PropertyFlags) 1);
    private readonly ObjectSerializer _serializer = new ObjectSerializer()
                .OnBehaviors(SerializerOptions.Behaviors.None)
                .OnPropertyMask((SerializerOptions.PropertyFlags) 1);

    public AuctionHouseService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new AuctionHouseService(parentActor));

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_AUCTIONHOUSEREQUEST))]
    private void ReceiveAuctionHouseRequest(WIZARD_12_PROTOCOL.MSG_AUCTIONHOUSEREQUEST message) {
        switch (message.Command) {
            case 0:
                SendAuctionHouseContents(message.npcGlobalID, message.key);
                break;
            case 1:
                ConfirmBuyFromAuctionHouse(message.itemTemplateID, message.key);
                break;
            case 2:
                SellToAuctionHouse(message.itemGlobalID, message.key);
                break;
            case 3:
                BuyFromAuctionHouse(message.itemTemplateID, message.texture, message.decal, message.key);
                break;
            case 4:
                // ?
                break;
            case 5:
                // ?
                break;
            case 6:
                // ?
                break;
            case 9:
                // ?
                break;
            default:
                break;
        }
    }

    private void SendAuctionHouseContents(ulong npcId, uint key) {
        // Todo: minimize number of database calls, each player service does not need to fetch
        // all auction house entries. this should be stored in memory somewhere shared.

        // Retrieve all Auction House entries.
        var houseEntryList = AuctionHouseCollection.GetAllAuctionHouseEntries();

        while (houseEntryList.Count > 0) {
            // Contents sent in blocks of up to 50 entries.
            var houseEntryBlock = (houseEntryList.Count >= 50)
                ? houseEntryList.GetRange(0, 50) : houseEntryList.GetRange(0, houseEntryList.Count);

            var auctionHouseEntries = new AuctionHouseOffering {
                m_auctionHousePurchaseKey = key,
                m_auctionList = houseEntryBlock
            };
            var auctionHouseEntriesData = _serializer.Serialize(auctionHouseEntries);

            var auctionHouseContentsMsg = new WIZARD_12_PROTOCOL.MSG_AUCTIONHOUSECONTENTS {
                Contents = auctionHouseEntriesData,
                GlobalID = npcId
            };
            SendToSocket(auctionHouseContentsMsg);

            // Remove first 50 entries from list.
            if (houseEntryList.Count >= 50) {
                houseEntryList.RemoveRange(0, 50);
            } else {
                houseEntryList.RemoveRange(0, houseEntryList.Count);
            }
        }
    }

    private void ConfirmBuyFromAuctionHouse(ulong templateId, uint key) {
        var entry = AuctionHouseCollection.GetAuctionHouseEntry(templateId);

        // Todo: check if entry exists.

        var auctionRspMsg = new WIZARD_12_PROTOCOL.MSG_AUCTIONRESPONSE {
            Command = 1,
            ItemTemplateID = templateId,
            Cost = entry.m_buyPrice,
            ReturnCode = 0
        };
        SendToSocket(auctionRspMsg);
    }

    private void BuyFromAuctionHouse(ulong templateId, int texture, int decal, uint key) {
        var wizard = GetActiveWizard();
        var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(templateId);

        var item = (WizClientObjectItem) CoreObjectFactory.FinalizeCoreObject(templateId);
        item.m_primaryColor = texture;
        item.m_secondaryColor = decal;
        var itemData = _itemSerializer.Serialize(item);

        var entry = AuctionHouseCollection.GetAuctionHouseEntry(templateId);
        var goldCost = entry.m_buyPrice;

        // Update stock and push to database.
        entry.m_numForSale -= 1;
        if (entry.m_numForSale < 1) {
            // Remove entry if no more stock.
            AuctionHouseCollection.RemoveAuctionHouseEntry(templateId);
        }
        else {
            AuctionHouseCollection.UpdateAuctionHouseEntry(entry);
        }

        // Inform of update.
        var houseEntryData = _serializer.Serialize(entry);
        var auctionUpdateMsg = new GAME_5_PROTOCOL.MSG_AUCTIONHOUSEUPDATE {
            UpdateInfo = houseEntryData,
            CharacterID = wizard.CharId
        };
        SendToSocket(auctionUpdateMsg);

        // Add item to inventory
        var addItemMsg = new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM {
            GlobalID = wizard.CharId,
            SerializedItem = itemData,
        };
        SendToSocket(addItemMsg);

        // Add item to inventory after message to prevent crashes.
        wizard.AddItemToInventory(item);

        // Alert client of new item.
        var itemAcqMsg = new WIZARD2_53_PROTOCOL.MSG_ITEMACQUISITION {
            ItemGlobalID = item.m_globalID,
            ItemTemplateID = (uint) item.m_templateID,
            ItemLocation = 1,
        };
        SendToSocket(itemAcqMsg);

        // Update gold balances.
        wizard.RemoveGold(goldCost);
        var goldUpdateMsg = new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD {
            Gold = wizard.GameStats.m_currentGold,
            MaxGold = wizard.GameStats.m_baseGoldPouch
        };
        SendToSocket(goldUpdateMsg);

        // Confirm transaction.
        var shopBuyConfirmMsg = new WIZARD_12_PROTOCOL.MSG_SHOPBUYCONFIRM {
            Failure = 0,
            WebFailure = 0,
            Credits = 0
        };
        SendToSocket(shopBuyConfirmMsg);
    }

    private void SellToAuctionHouse(ulong itemGlobalId, uint key) {
        var wizard = GetActiveWizard();
        var item = wizard.InventoryBehavior.GetItem(itemGlobalId);

        // Check player has item.
        var removedItemSuccess = wizard.RemoveItemFromInventory(itemGlobalId);
        if (!removedItemSuccess) {
            var auctionRspErrorMsg = new WIZARD_12_PROTOCOL.MSG_AUCTIONRESPONSE {
                Command = 2,
                ItemTemplateID = 0, // Can't get this ID if item doesn't exist.
                Cost = 0,
                ReturnCode = 1
            };
            SendToSocket(auctionRspErrorMsg);
            return;
        }

        var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(item.m_templateID);

        // Calculate gold sell value.
        var gold = (int) Math.Ceiling(template.m_baseCost * 0.5f); // Bazaar buys items at 50% of their value.
        if (template.m_numPrimaryColors != 1 && template.m_numSecondaryColors != 0) {
            gold = (int) Math.Ceiling(gold * 1.225f);
        }

        var auctionRspMsg = new WIZARD_12_PROTOCOL.MSG_AUCTIONRESPONSE {
            Command = 2,
            ItemTemplateID = item.m_templateID,
            Cost = gold,
            ReturnCode = 0
        };
        SendToSocket(auctionRspMsg);

        // Update stock and push to database.
        var entry = AuctionHouseCollection.GetAuctionHouseEntry(item.m_templateID);
        if (entry is null) {
            entry = new AuctionHouseEntry {
                m_templateID = (GID) item.m_templateID,
                m_buyPrice = (int) template.m_baseCost * 2, // Bazaar sells items at 200% of their value.
                m_numForSale = 1,
                m_sellPrice = (int) (template.m_baseCost * 0.5f)
            };
            AuctionHouseCollection.AddAuctionHouseEntry(entry);
        }
        else {
            if (entry.m_numForSale < 99) { // Stock limit of 99. Players can still sell item but stock will not increase.
            entry.m_numForSale += 1;
            AuctionHouseCollection.UpdateAuctionHouseEntry(entry);
        }
        }

        // Inform of update.
        var houseEntryData = _serializer.Serialize(entry);
        var auctionUpdateMsg = new GAME_5_PROTOCOL.MSG_AUCTIONHOUSEUPDATE {
            UpdateInfo = houseEntryData,
            CharacterID = wizard.CharId
        };
        SendToSocket(auctionUpdateMsg);

        // Update gold balances.
        wizard.AddGold(gold);

        var updateGoldMsg = new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD {
            Gold = wizard.GameStats.m_currentGold,
            MaxGold = wizard.GameStats.m_baseGoldPouch
        };
        SendToSocket(updateGoldMsg);

        // Remove item from inventory.
        var removeItemMsg = new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_REMOVEITEM {
            GlobalID = wizard.CharId,
            ItemID = item.m_globalID
        };
        SendToSocket(removeItemMsg);

        var shopSellConfirmMsg = new WIZARD_12_PROTOCOL.MSG_SHOPSELLCONFIRM {
            ClientRequestID = 0,
            GlobalID = 0,
            Failure = 0
        };
        SendToSocket(shopSellConfirmMsg);
        
        // Todo: fix removal of items? seems broken at the moment.
        wizard.RemoveItemFromInventory(item.m_globalID);
    }

}
