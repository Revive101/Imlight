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
 * CROWNSHOP SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Handles the functionality for the crown shop
 * 
 * 
 * USAGE EXAMPLE:
 *
 * 
 * NOTE:
 * 
 * 
 * TODO:
 * - Populating with correct data
 * - Item purchasing
 * - Removing from wishlist (& Wishlist privacy)
 * - Daily Spiral quests
 * 
 * Created by: Phill030
 * Version: KALI 1.0
 * Last Updated: 28.09.2026
 */

using Akka.Actor;
using Imcodec.CoreObject;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.CrownShop;
using Imlight.CoreLib.Game.Packs;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Linq;

namespace Imlight.CoreLib.Game.Services;


internal class CrownShopService(SessionActor sessionActor) : MessageService(sessionActor) {
    protected static Props Props(SessionActor parentActor)
            => Akka.Actor.Props.Create(() => new CrownShopService(parentActor));


    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_SEGDATA_REQUEST))]
    private void ReceiveCrownShopSegDataRequest(WIZARD_12_PROTOCOL.MSG_PCS_SEGDATA_REQUEST message) {
        var wizard = GetActiveWizard();
        var schoolStr = wizard.MagicSchoolBehavior.MagicSchool.ToString();
        var schoolCode = schoolStr.Length >= 2 ? schoolStr[..2] : schoolStr;

        var segmentationInputData = new SegmentationInputData() {
            m_bIsValidSegmentationData = true,
            m_playerLevel = wizard.MagicSchoolBehavior.Level,
            m_playerSchoolOfFocus = schoolCode,
            m_accountNDaysAged = (int) (DateTime.Now - wizard.Account.CreationTime).TotalDays,
            m_accountNDaysSinceLastLogin = 0,
            m_accountNDaysSinceLastPurchase = 0,
            m_accountNDaysLastCrownsPurchase = 0,
            m_accountIsMember = 0,
            m_accountIsCSR = wizard.Account.AuthLevel > AuthLevel.None ? 1 : 0, // todo: Change this back before prod!
            m_accountNCrownsSpent = 0,
            m_accountNCrownsInWallet = wizard.Account.Crowns,
            m_accountNDaysSinceItemPurchased = [],
            m_numOfParticularItemInInventory = [],
            m_numItemsOfCategoryInInventory = [],
            m_playerHasBadge = [], // Used for calculating Requirements
            m_accountNHighestWorld = 0
        };

        var serializer = new ObjectSerializer(
            Versionable: false,
            Behaviors: SerializerFlags.None
        );
        var propertyFlags = PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit;
        if (!serializer.Serialize(segmentationInputData, propertyFlags, out var serializedData)) {
            Logger.Error("Failed to serialize SegmentationInputData");
            return;
        }

        SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_SEGDATA_RESPONSE {
            Success = (byte) 1,
            Data = serializedData
        });

        // We need to call this to "sync" the CrownShop Crown-Balance, else it just says 0
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_CROWNBALANCE {
            Failure = (byte) 0,
            TotalCrowns = wizard.Account.Crowns,
            CharacterID = wizard.CharId,
            CacheBalanceForCSSegmentation = (byte) 1
        });
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_LIST_REQUEST))]
    private void ReceiveCrownShopListRequest(WIZARD_12_PROTOCOL.MSG_PCS_LIST_REQUEST message) {
        uint nextUpdateId = message.UpdateID == 0 ? 1 : message.UpdateID + 1;
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_LIST_RESPONSE {
            Data = CrownShopHandler.GetCrownShopData(),
            Updates = "",
            UpdateID = nextUpdateId,
            Error = 0,
        });
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_UPDATEUSERWISHLIST))]
    private void ReceiveWishlistUpdate(WIZARD_12_PROTOCOL.MSG_PCS_UPDATEUSERWISHLIST message) { }

    // Is this saleID still valid and actively running?
    // Does this saleID grant the price the client is asking to lock
    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_PRICE_LOCK_REQUEST))]
    private void ReceivePriceLockReq(WIZARD_12_PROTOCOL.MSG_PCS_PRICE_LOCK_REQUEST message) {
        Logger.Information("Received MSG_PCS_PRICE_LOCK_REQUEST for item {0}", Logger.Args(message.Item));

        if (!CrownShopHandler.TryGetCrownShopItem(message.Item, out var item)) {
            Logger.Warning("Item {0} not found in CrownShop.", Logger.Args(message.Item));
            SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_PRICE_LOCK_RESPONSE {
                CostCrowns = 0,
                CostGold = 0,
                CostTickets = 0,
                Error = 1,
                Item = message.Item
            });
            return;
        }

        SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_PRICE_LOCK_RESPONSE {
            CostCrowns = item.m_crownsCost,
            CostGold = item.m_goldCost,
            CostTickets = 0,
            Error = 0,
            Item = message.Item
        });
    }

    // The client requests to buy [N amount] of this item (and possibly gift it to another player),
    // which will be removed(?) from the list of things they can buy (if enabled).
    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_REQUEST))]
    private void ReceivePurchaseRequest(WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_REQUEST message) {
        var wizard = GetActiveWizard();

        if (message.Count < 1 || message.Count > CrownShopHandler.s_maxBuyCount || message.SaleID != 0) {
            Logger.Warning("Rejected purchase request for item {0}.", Logger.Args(message.Item));

            SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_RESPONSE {
                Error = 1,
            });
            return;
        }

        // Authoritative cost lookup from catalog
        if(!CrownShopHandler.TryGetCrownShopItem(message.Item, out var catalogItem)) {
            Logger.Warning("Item {0} not found in CrownShop.", Logger.Args(message.Item));
            SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_RESPONSE {
                Error = 1,
            });
            return;
        }

        int amountToPay = message.Count * catalogItem.m_crownsCost;
        if (wizard.Account.Crowns < amountToPay) {
            SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_RESPONSE {
                Error = 1,
            });
            return;
        }

        wizard.Account.SetCrowns(wizard.Account.Crowns - amountToPay);
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_RESPONSE {
            Item = message.Item,
            Error = 0,
            Cost = amountToPay,
            Count = message.Count,
            Gifted = (byte) (message.Recipient == 0 ? 0 : 1),
            Type = message.Type
        });

        // Sync crowns
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_CROWNBALANCE {
            Failure = 0,
            TotalCrowns = wizard.Account.Crowns,
            CharacterID = wizard.CharId,
            CacheBalanceForCSSegmentation = 1
        });

        var itemTemplate = CoreObjectFactory.GetCoreTemplate(message.Item);
        switch(itemTemplate) {
            case BoosterPackTemplate:
                for (uint i = 0; i < message.Count; i++) {
                    PackManager.OpenPack(SessionActor.ActorRef, wizard, message.Item);
                }
                return;
            default:
                // Check if the item is emote or teleport effect
                var customEmote = itemTemplate?.m_behaviors?.OfType<CustomEmoteBehaviorTemplate>().FirstOrDefault();
                if (customEmote != null && customEmote.m_bitFieldNumber >= 0) {
                    if (customEmote.m_emoteType == CustomEmoteType.CE_Teleport) {
                        wizard.UnlockCustomTeleportEffect(customEmote.m_bitFieldNumber);
                    }
                    else {
                        wizard.UnlockCustomEmote(customEmote.m_bitFieldNumber);
                    }
                }

                // todo: serialize the item only once   
                var coSerializer = new CoreObjectSerializer(
                    behaviors: Imcodec.ObjectProperty.SerializerFlags.None
                );

                for (uint i = 0; i < message.Count; i++) {
                    if (!wizard.AddItemToInventory(message.Item, out WizClientObjectItem itemCoreObject)) {
                        Logger.Warning("Could not add item to inventory.");

                        SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_RESPONSE {
                            Error = 1,
                        });
                        return;
                    }

                    if (!coSerializer.Serialize(itemCoreObject, 24, out var serializedItem)) {
                        Logger.Warning("Failed to serialize core object.");
                        return;
                    }

                    SendToSocket(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM {
                        GlobalID = wizard.GameObjectID,
                        SerializedItem = serializedItem
                    });
                }
                break;
        }
    }

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_CrownShopLogging))]
    private void ReceiveCrownShopLogging(WIZARD2_53_PROTOCOL.MSG_CrownShopLogging message) {
        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_CrownShopLogging {
            Enabled = 0
        });
    }
}
