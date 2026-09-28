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
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.CrownShop;
using Imlight.CoreLib.Game.Packs;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Models.World;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.Game.Services;


internal class CrownShopService(SessionActor sessionActor) : MessageService(sessionActor) {


    private static readonly Lazy<Dictionary<ulong, string>> s_packDisplayPriorities = new(() => {
        if (!RootArchiveLoader.IsLoaded) {
            RootArchiveLoader.ReloadRootWad();
        }

        var wad = RootArchiveLoader.GetRootWad();
        var priorities = new Dictionary<ulong, string>();
        if (wad == null) {
            return priorities;
        }

        ReadOnlySpan<byte> snackToken = "Snacks"u8;
        ReadOnlySpan<byte> purreauToken = "PurreauPack"u8;
        ReadOnlySpan<byte> tcToken = "TreasureCards"u8;
        ReadOnlySpan<byte> gardenTcToken = "GardenTreasureCards"u8;
        ReadOnlySpan<byte> reagentToken = "Reagents"u8;

        foreach (var t in CoreObjectFactory.TemplateManifest.m_serializedTemplates) {
            string fn = t.m_filename;
            if (!fn.StartsWith("ObjectData/BoosterPack-")
                && !fn.StartsWith("ObjectData/SpecialSets/CrownShopBundles/BoosterPack")
                && !fn.Contains("MegaSnackPack")) {
                continue;
            }

            // Default: Hoard & Lore Packs
            string priority = "25:1,19:1,0:1"; 

            var data = wad.OpenFile(fn);
            if (data.HasValue) {
                var span = data.Value.Span;
                // Cat 26: Pet Snack Packs (shared in Packs Tab 38 and Pets Tab 40)
                if (span.IndexOf(snackToken) >= 0 || span.IndexOf(purreauToken) >= 0 || fn.Contains("Snack")) {
                    priority = "26:1,19:1,0:1"; 
                }
                // Cat 20: Booster Packs (TC)
                else if (span.IndexOf(tcToken) >= 0 || span.IndexOf(gardenTcToken) >= 0) {
                    priority = "20:1,19:1,0:1";
                }
                // Cat 28: Reagents
                else if (span.IndexOf(reagentToken) >= 0) {
                    priority = "28:1,19:1,0:1";
                }
            }
            else if (fn.Contains("Snack")) {
                priority = "26:1,19:1,0:1";
            }

            priorities[t.m_id] = priority;
        }

        Logger.Information("CrownShop: Indexed {0} pack priorities from archive.", Logger.Args(priorities.Count));
        return priorities;
    });

    private static readonly Lazy<HashSet<ulong>> s_rentalMountTemplateIds = new(() => {
        if (!RootArchiveLoader.IsLoaded) {
            RootArchiveLoader.ReloadRootWad();
        }

        var wad = RootArchiveLoader.GetRootWad();
        var rentalIds = new HashSet<ulong>();

        if (wad == null) {
            return rentalIds;
        }

        foreach (var t in CoreObjectFactory.TemplateManifest.m_serializedTemplates) {
            if (!t.m_filename.StartsWith("ObjectData/Mounts/")
                && !t.m_filename.StartsWith("ObjectData/SpecialSets/Mounts/")) {
                continue;
            }

            var data = wad.OpenFile(t.m_filename);
            if (data.HasValue) {
                var span = data.Value.Span;
                if (span.IndexOf("RentalBehavior"u8) >= 0 || span.IndexOf("TimedItemBehavior"u8) >= 0) {
                    rentalIds.Add(t.m_id);
                }
            }
        }

        Logger.Information("CrownShop: Identified {0} rental mounts from archive behaviors.", Logger.Args(rentalIds.Count));
        return rentalIds;
    });



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

    // This checks if the item *can* be bought (NOT if the player has enough money) (Event time ran out, etc.)
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

        if (message.Count < 1 || message.Count > CrownShopHandler.s_maxBuyCount) {
            Logger.Warning("Rejected purchase request for item {0}: invalid count {1}.", Logger.Args(message.Item, message.Count));

            SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_RESPONSE {
                Item = message.Item,
                Error = 1,
                Cost = 0,
                Count = message.Count,
                Gifted = 0,
                Type = message.Type
            });
            return;
        }

        // Authoritative cost lookup from catalog
        if(!CrownShopHandler.TryGetCrownShopItem(message.Item, out var catalogItem)) {
            Logger.Warning("Item {0} not found in CrownShop.", Logger.Args(message.Item));
            SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_RESPONSE {
                Item = message.Item,
                Error = 1,
                Cost = 0,
                Count = message.Count,
                Gifted = 0,
                Type = message.Type
            });
            return;
        }

        int amountToPay = message.Count * catalogItem.m_crownsCost;
        if (wizard.Account.Crowns < amountToPay) {
            var msg = new WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_RESPONSE {
                Item = message.Item,
                Error = 1,
                Cost = amountToPay,
                Count = message.Count,
                Gifted = 0,
                Type = message.Type
            };
            SendToSocket(msg);
            return;
        }

        var isBooster = PackManager.IsBoosterPack(message.Item);
        if (!isBooster) {
            // Add item to inventory

            // Check if the item is emote or teleport effect
            var template = CoreObjectFactory.GetCoreTemplate(message.Item);
            var customEmote = template?.m_behaviors?.OfType<CustomEmoteBehaviorTemplate>().FirstOrDefault();
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

                    var msg = new WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_RESPONSE {
                        Item = message.Item,
                        Error = 1,
                        Cost = amountToPay,
                        Count = message.Count,
                        Gifted = 0,
                        Type = message.Type
                    };
                    SendToSocket(msg);
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
            Failure = (byte) 0,
            TotalCrowns = wizard.Account.Crowns,
            CharacterID = wizard.CharId,
            CacheBalanceForCSSegmentation = (byte) 1
        });

        if (isBooster) {
            for (uint i = 0; i < message.Count; i++) {
                PackManager.OpenPack(SessionActor.ActorRef, wizard, message.Item);
            }
        }
    }

    //private static List<CrownShopItem> GetOrCreateCatalog() {
    //    if (s_catalogCache != null) {
    //        return s_catalogCache;
    //    }

    //    lock (s_catalogLock) {
    //        if (s_catalogCache != null) {
    //            return s_catalogCache;
    //        }

    //        var catalog = new List<CrownShopItem>();
    //        var templates = CoreObjectFactory.TemplateManifest.m_serializedTemplates;
    //        foreach (var entry in templates) {
    //            string path = entry.m_filename;
    //            ulong id = entry.m_id;

    //            if (path.Contains("Dummy", StringComparison.OrdinalIgnoreCase)
    //                || path.Contains("DONOTUSE", StringComparison.OrdinalIgnoreCase)
    //                || path.Contains("Test", StringComparison.OrdinalIgnoreCase)
    //                || path.Contains("MOB-ONLY", StringComparison.OrdinalIgnoreCase)) {
    //                continue;
    //            }

    //            string displayPriority = null;

    //            // 1. Mounts (Permanent & Rental)
    //            if (path.StartsWith("ObjectData/Mounts/", StringComparison.OrdinalIgnoreCase)
    //                || path.StartsWith("ObjectData/SpecialSets/Mounts/", StringComparison.OrdinalIgnoreCase)) {
    //                bool isRental = s_rentalMountTemplateIds.Value.Contains(id);
    //                displayPriority = isRental ? "3:1,19:1,0:1" : "2:1,19:1,0:1"; // Cat 3: Rental Mounts, Cat 2: Permanent Mounts
    //            }

    //            // 2. Packs (Hoard & Lore, Booster, Pet Snack Packs)
    //            else if (s_packDisplayPriorities.Value.TryGetValue(id, out var packPriority)) {
    //                displayPriority = packPriority;
    //            }

    //            // 3. Pets
    //            else if (path.StartsWith("ObjectData/Pets/", StringComparison.OrdinalIgnoreCase)
    //                     || path.StartsWith("ObjectData/SpecialSets/Pets/", StringComparison.OrdinalIgnoreCase)) {
    //                displayPriority = "4:1,19:1,0:1"; // Cat 4: Pets
    //            }

    //            // 4. Elixirs
    //            else if (path.Contains("Elixir", StringComparison.OrdinalIgnoreCase)
    //                     && path.StartsWith("ObjectData/", StringComparison.OrdinalIgnoreCase)) {
    //                displayPriority = "16:1,19:1,0:1"; // Cat 16: Elixirs
    //            }

    //            // 5. Transformations (Gear Subcategory 17)
    //            else if (path.StartsWith("ObjectData/Transformations/", StringComparison.OrdinalIgnoreCase)
    //                     || path.StartsWith("ObjectData/Transformation-", StringComparison.OrdinalIgnoreCase)) {
    //                displayPriority = "17:1,19:1,0:1"; // Cat 17: Transformations
    //            }

    //            // 6. Hairstyles / Wigs (Gear Subcategory 23)
    //            else if (path.StartsWith("ObjectData/Wigs/", StringComparison.OrdinalIgnoreCase)) {
    //                displayPriority = "23:1,19:1,0:1"; // Cat 23: Hairstyles (Wigs)
    //            }

    //            // 7. Gear pieces & bundles (SpecialSets, Purchased Character Gear, and Crown accessories)
    //            else if (path.StartsWith("ObjectData/SpecialSets/", StringComparison.OrdinalIgnoreCase)
    //                     || path.StartsWith("ObjectData/Purchased Character Gear/", StringComparison.OrdinalIgnoreCase)
    //                     || path.StartsWith("ObjectData/CrownItems/", StringComparison.OrdinalIgnoreCase)
    //                     || path.StartsWith("ObjectData/Athames/", StringComparison.OrdinalIgnoreCase)) {

    //                bool isCrownAccessorySeries = path.StartsWith("ObjectData/CrownItems/Series2/", StringComparison.OrdinalIgnoreCase)
    //                    || path.StartsWith("ObjectData/CrownItems/Series3/", StringComparison.OrdinalIgnoreCase)
    //                    || path.StartsWith("ObjectData/CrownItems/Series4/", StringComparison.OrdinalIgnoreCase)
    //                    || path.StartsWith("ObjectData/CrownItems/Series5/", StringComparison.OrdinalIgnoreCase)
    //                    || path.StartsWith("ObjectData/CrownItems/Series41/", StringComparison.OrdinalIgnoreCase)
    //                    || path.StartsWith("ObjectData/CrownItems/Series50/", StringComparison.OrdinalIgnoreCase);

    //                if (path.Contains("/Hats/", StringComparison.OrdinalIgnoreCase) || path.Contains("/Hat/", StringComparison.OrdinalIgnoreCase)) {
    //                    if (!path.StartsWith("ObjectData/CrownItems/", StringComparison.OrdinalIgnoreCase)) {
    //                        displayPriority = "11:1,19:1,0:1"; // Cat 11: Hats
    //                    }
    //                }
    //                else if (path.Contains("/Robes/", StringComparison.OrdinalIgnoreCase) || path.Contains("/Robe/", StringComparison.OrdinalIgnoreCase)) {
    //                    if (!path.StartsWith("ObjectData/CrownItems/", StringComparison.OrdinalIgnoreCase)) {
    //                        displayPriority = "9:1,19:1,0:1"; // Cat 9: Robes
    //                    }
    //                }
    //                else if (path.Contains("/Shoes/", StringComparison.OrdinalIgnoreCase) || path.Contains("/Boots/", StringComparison.OrdinalIgnoreCase)) {
    //                    if (!path.StartsWith("ObjectData/CrownItems/", StringComparison.OrdinalIgnoreCase)) {
    //                        displayPriority = "10:1,19:1,0:1"; // Cat 10: Shoes
    //                    }
    //                }
    //                else if (path.Contains("/Wands/", StringComparison.OrdinalIgnoreCase) || path.Contains("/Weapons/", StringComparison.OrdinalIgnoreCase)) {
    //                    if (!path.StartsWith("ObjectData/CrownItems/", StringComparison.OrdinalIgnoreCase)) {
    //                        displayPriority = "15:1,19:1,0:1"; // Cat 15: Weapons
    //                    }
    //                }
    //                else if (path.Contains("/Athames/", StringComparison.OrdinalIgnoreCase)
    //                         || path.StartsWith("ObjectData/Athames/", StringComparison.OrdinalIgnoreCase)) {
    //                    if (!path.StartsWith("ObjectData/CrownItems/", StringComparison.OrdinalIgnoreCase) || isCrownAccessorySeries) {
    //                        displayPriority = "14:1,19:1,0:1"; // Cat 14: Athames
    //                    }
    //                }
    //                else if (path.Contains("/Amulet/", StringComparison.OrdinalIgnoreCase) || path.Contains("/Amulets/", StringComparison.OrdinalIgnoreCase)) {
    //                    if (!path.StartsWith("ObjectData/CrownItems/", StringComparison.OrdinalIgnoreCase) || isCrownAccessorySeries) {
    //                        displayPriority = "13:1,19:1,0:1"; // Cat 13: Amulets
    //                    }
    //                }
    //                else if (path.Contains("/Rings/", StringComparison.OrdinalIgnoreCase) || path.Contains("/Ring/", StringComparison.OrdinalIgnoreCase)) {
    //                    if (!path.StartsWith("ObjectData/CrownItems/", StringComparison.OrdinalIgnoreCase) || isCrownAccessorySeries) {
    //                        displayPriority = "12:1,19:1,0:1"; // Cat 12: Rings
    //                    }
    //                }
    //                else if (path.Contains("/CrownShopBundles/", StringComparison.OrdinalIgnoreCase)) {
    //                    // Handled above if booster/snack pack
    //                }
    //                else if (path.StartsWith("ObjectData/SpecialSets/", StringComparison.OrdinalIgnoreCase)) {
    //                    displayPriority = "8:1,19:1,0:1"; // Cat 8: Clothing Bundles
    //                }
    //            }

    //            // 8. Houses
    //            else if (path.StartsWith("ObjectData/Housing/Deeds/", StringComparison.OrdinalIgnoreCase)
    //                     || path.EndsWith("PropertyDeed.xml", StringComparison.OrdinalIgnoreCase)) {
    //                displayPriority = "5:1,19:1,0:1"; // Cat 5: Houses (Tab 44)
    //            }

    //            // 9. Emotes & Teleport Effects
    //            else if (path.StartsWith("ObjectData/Emotes/", StringComparison.OrdinalIgnoreCase)) {
    //                if (path.Contains("Teleport", StringComparison.OrdinalIgnoreCase)) {
    //                    displayPriority = "32:1,19:1,0:1"; // Cat 32: Teleport Effects (Tab 45)
    //                }
    //                else {
    //                    displayPriority = "31:1,19:1,0:1"; // Cat 31: Emotes (Tab 45)
    //                }
    //            }

    //            if (displayPriority != null) {
    //                catalog.Add(new CrownShopItem {
    //                    m_itemTemplateId = id,
    //                    m_itemFlags = 0,
    //                    m_goldCost = 0,
    //                    m_crownsCost = 1,
    //                    m_ticketCost = 0,
    //                    m_displayPriority = displayPriority,
    //                    m_strikethruCrowns = 0,
    //                    m_strikethruGold = 0,
    //                    m_description = "", // Client pulls the real name/desc via Template ID
    //                    m_saleID = 5129,
    //                    m_recommendIfOwned = false,
    //                    m_combatOnly = false,
    //                    m_noGift = false,
    //                    m_segReqsStatement = "",
    //                    m_segReqsPoolsStatements = []
    //                });
    //            }
    //        }

    //        s_catalogCache = catalog;
    //        s_catalogById = catalog
    //            .GroupBy(i => (ulong) i.m_itemTemplateId)
    //            .ToDictionary(g => g.Key, g => g.First());

    //        Logger.Information("CrownShop: Loaded {0} special items into the Crown Shop.", Logger.Args(s_catalogCache.Count));
    //        return s_catalogCache;
    //    }
}
