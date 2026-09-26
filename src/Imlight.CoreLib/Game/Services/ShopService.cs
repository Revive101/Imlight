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
 * SHOP SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages player shop interactions, including item buying, selling, 
 * dyeing and pet renaming within the game server session.
 * 
 * USAGE EXAMPLE:
 * Internal service handling shop-related messages and transactions 
 * for player inventory and economic interactions.
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty, Joji
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

using System;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.CoreObject;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.Game.Pet;
using Imlight.CoreLib.Game.Zone.Components;
using Imlight.CoreLib.Game.WizBang;

namespace Imlight.CoreLib.Game.Services;

internal class ShopService(SessionActor sessionActor) : MessageService(sessionActor) {

    private const float SELL_MODIFIER = 0.05f;
    private const float DYED_ITEM_COST_MULTIPLIER = 1.225f;
    // Public equipment carries each dye layer in 5 bits.
    private const int MaxDye = 31;
    private const PropertyFlags EquippedItemMask = PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit;

    private static readonly CoreObjectSerializer s_itemSerializer = new(
        behaviors: Imcodec.ObjectProperty.SerializerFlags.None
    );

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new InteractService(parentActor));

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_SHOPBUYREQUEST))]
    private void ReceiveShopBuyRequest(WIZARD_12_PROTOCOL.MSG_SHOPBUYREQUEST message) {
        // Ensure that the player has interacted with an object in the zone.
        var interactedObject = GetZoneObject(message.npcGlobalID);
        if (interactedObject is null) {
            Logger.Warning("Failed to find NPC {0} in zone for shop purchase",
                Logger.Args(message.npcGlobalID));
            var shopDenyMsg = new WIZARD_12_PROTOCOL.MSG_SHOPBUYCONFIRM { Failure = 1 };
            SendToSocket(shopDenyMsg);

            return;
        }

        // Ensure that the interacted object is a vendor.
        var vendorComponent = interactedObject.GetComponentOfType<InteractVendorComponent>();
        if (vendorComponent is null) {
            Logger.Warning("Failed to find VendorComponent for NPC {0} in zone for shop purchase",
                Logger.Args(message.npcGlobalID));
            var shopDenyMsg = new WIZARD_12_PROTOCOL.MSG_SHOPBUYCONFIRM { Failure = 1 };
            SendToSocket(shopDenyMsg);

            return;
        }

        // Ensure that the vendor has the item that the player is trying to purchase.
        var itemTemplateID = new GID(message.ShopID).MParts.TemplateId;
        if (!vendorComponent.HasItem((GID) message.ShopID)) {
            HandleIllegalPurchaseAttempt(itemTemplateID, message.npcGlobalID);

            return;
        }

        var playerWizard = GetActiveWizard();
        var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(itemTemplateID);
        var item = (WizClientObjectItem) CoreObjectFactory.FinalizeCoreObject(itemTemplateID);
        item.m_primaryColor = message.texture;
        item.m_secondaryColor = message.decal;

        var goldCost = CalculateItemCost(template);

        // Check if the user can afford the item.
        if (playerWizard.GameStats.m_currentGold >= goldCost) {
            ProcessSuccessfulPurchase(playerWizard, item, itemTemplateID, goldCost);

            return;
        }

        SendShopDenyMessage();
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_SHOPSELLREQUEST))]
    private void ReceiveShopSellRequest(WIZARD_12_PROTOCOL.MSG_SHOPSELLREQUEST message) {
        var wizard = GetActiveWizard();
        var item = wizard.InventoryBehavior.GetItem(message.GlobalID);

        var removedItemSuccess = wizard.RemoveItemFromInventory(message.GlobalID);
        if (!removedItemSuccess) {
            Logger.Warning("Failed to find item {0} in inventory for shop sell", Logger.Args(message.GlobalID));
            ProcessFailedSale();

            return;
        }

        var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(item.m_templateID);
        var gold = CalculateItemSellValue(template);

        ProcessSuccessfulSale(wizard, message.GlobalID, gold);
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

        var template = CoreObjectFactory.GetCoreTemplate(item.m_templateID) as WizItemTemplate;
        if (template is null || !IsValidDye(item, template, message)) {
            Logger.Warning("Rejected dye {0}/{1}/{2} for item {3} (template {4})",
                Logger.Args(message.texture, message.decal, message.decal2, item.m_globalID, item.m_templateID.Full));

            SendToSocket(new WIZARD_12_PROTOCOL.MSG_DYECONFIRM { Failure = 1 });

            return;
        }

        var dyeCost = PriceModifiersConfig.GetDyeCost(template, message.texture, message.decal);
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

        // If the item is equipped, update the client's local item cache with the
        // new colors IN PLACE using MSG_EQUIPMENTBEHAVIOR_EQUIPITEM. 
        if (isEquipped) {
            SendEquippedItemRefresh(wizard, item, template);
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

        ResummonIfEquippedPet(wizard, item);
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PETRENAMEREQUEST))]
    private void ReceivePetRenameRequest(WIZARD_12_PROTOCOL.MSG_PETRENAMEREQUEST message) {
        var wizard = GetActiveWizard();

        // The dye shop's pet name tab lists backpack and equipped pets alike.
        var pet = wizard.InventoryBehavior.GetItem(message.itemGlobalID)
            ?? wizard.EquipmentBehavior.GetItem(message.itemGlobalID);
        if (pet is null || !CoreObjectFactory.FindBehaviorInstance<ClientPetNameBehavior>(pet, out _)) {
            Logger.Warning("Player tried to rename item {0}, which is not one of their pets.",
                Logger.Args(message.itemGlobalID));

            SendPetRenameDeny();

            return;
        }

        if (!WizardNameBank.IsValidPetName(message.petName)) {
            Logger.Warning("Player tried to rename pet {0} with name keys {1}, which are not in the pet name tables.",
                Logger.Args(pet.m_globalID, message.petName));

            SendPetRenameDeny();

            return;
        }

        var renameCost = PriceModifiersConfig.GetPetRenameCost();
        if (renameCost > wizard.GameStats.m_currentGold) {
            SendPetRenameDeny();

            return;
        }

        if (!WizardItemCollection.ApplyPetName(pet, message.petName)) {
            Logger.Error("Failed to save name keys {0} for pet {1}",
                Logger.Args(message.petName, pet.m_globalID));

            SendPetRenameDeny();

            return;
        }

        PetFactory.TrySetPetName(pet, message.petName);

        wizard.RemoveGold(renameCost);
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD {
            Gold = wizard.GameStats.m_currentGold,
            MaxGold = wizard.GameStats.m_baseGoldPouch
        });

        // The client renames its own copy of the pet on success; the confirm carries no name.
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_PETRENAMECONFIRM { Failure = 0 });

        Logger.Information("Pet {0} renamed to {1} for {2} gold.",
            Logger.Args(pet.m_globalID, WizardNameBank.GetPetEnglishName(message.petName), renameCost));

        ResummonIfEquippedPet(wizard, pet);
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_DONESHOPPING))]
    private void ReceiveDoneShopping() {
        // A wizard has complete shopping and is leaving the shop.
        var wizard = GetActiveWizard();

        if (wizard.Zone.Contains("Phantom")) {
            return;
        }

        // Reenable player movement
        var enableMovementStateMsg = new GAME_5_PROTOCOL.MSG_ENTERSTATE() {
            GameObjectID = wizard.GameObjectID,
            State = 1685237158,
            Data = "",
            IgnoreIfCurrentStateIsOff = 0
        };
        SendToSocket(enableMovementStateMsg);

        var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG() {
            GameObjectID = wizard.GameObjectID,
            WizBangID = (uint) WizBangs.None
        };
        ZoneBroadcast(wizBangMsg, false);
    }

    private void ProcessSuccessfulSale(Wizard wizard, ulong itemID, int goldValue) {
        wizard.AddGold(goldValue);

        // Inform the game client of the successful sale.
        var updateGoldMsg = new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD {
            Gold = wizard.GameStats.m_currentGold,
            MaxGold = wizard.GameStats.m_baseGoldPouch
        };
        SendToSocket(updateGoldMsg);

        // Inform the game client of the successful sale.
        var removeItemMsg = new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_REMOVEITEM {
            GlobalID = wizard.GameObjectID,
            ItemID = itemID
        };
        SendToSocket(removeItemMsg);
    }

    private void ProcessFailedSale() {
        var shopDenyMsg = new WIZARD_12_PROTOCOL.MSG_SHOPSELLCONFIRM {
            Failure = 1,
        };
        SendToSocket(shopDenyMsg);
    }

    private void SendPetRenameDeny()
        => SendToSocket(new WIZARD_12_PROTOCOL.MSG_PETRENAMECONFIRM { Failure = 1 });

    private void SendEquippedItemRefresh(Wizard wizard, WizClientObjectItem item, WizItemTemplate template) {
        var slot = ItemHelper.GetItemSlot(template);
        if (slot is null) {
            Logger.Error("Dyed item {0} has no equipment slot", Logger.Args(item.m_globalID));

            return;
        }

        // Serialize the item with new colors for the local player's cache.
        if (!s_itemSerializer.Serialize(item, EquippedItemMask, out var localData)) {
            Logger.Error("Failed to serialize dyed item {0} for local update",
                Logger.Args(item.m_globalID));

            return;
        }

        SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_EQUIPITEM {
            GlobalID = wizard.GameObjectID,
            SlotName = slot.SlotType.ToString(),
            IsValid = 1,
            SerializedItem = localData
        });

        // Broadcast updated public appearance so other players see the new colors.
        var pubItem = ItemHelper.GetPublicItem(item);
        if (!s_itemSerializer.Serialize(pubItem, 1, out var pubData)) {
            Logger.Error("Failed to serialize item {0} for dye broadcast",
                Logger.Args(item.m_globalID));

            return;
        }

        ZoneBroadcast(new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICEQUIPITEM {
            GlobalID = wizard.GameObjectID,
            SerializedInfo = pubData
        }, false);
    }

    private void ResummonIfEquippedPet(Wizard wizard, WizClientObjectItem item) {
        // A summoned pet carries its look and name from when it was summoned.
        if (wizard.EquipmentBehavior.GetEquippedPetId() == item.m_globalID) {
            TellOtherServices(new CHARACTER_103_PROTOCOL.MSG_RESUMMONPET { PetItemId = item.m_globalID });
        }
    }

    private static bool IsValidDye(WizClientObjectItem item, WizItemTemplate template,
                                   WIZARD_12_PROTOCOL.MSG_DYEREQUEST message) {
        if (!IsDyeInRange(message.texture) || !IsDyeInRange(message.decal) || !IsDyeInRange(message.decal2)) {
            return false;
        }

        if (!PetFactory.IsPetTemplate(template.m_templateID)) {
            return true;
        }

        // A pet's colors are its template's own texture choices, so no dye beyond them is offered.
        return IsPetLayerDye(message.texture, item.m_primaryColor, template.m_numPrimaryColors)
            && IsPetLayerDye(message.decal, item.m_secondaryColor, template.m_numSecondaryColors)
            && IsPetLayerDye(message.decal2, item.m_pattern, template.m_numPatterns);
    }

    private static bool IsDyeInRange(int dye) => dye is >= 0 and <= MaxDye;

    private static bool IsPetLayerDye(int dye, int currentDye, int templateColorCount)
        => dye == currentDye || (templateColorCount > 1 && dye < templateColorCount);

    private void SendShopDenyMessage() {
        var shopDenyMsg = new WIZARD_12_PROTOCOL.MSG_SHOPBUYCONFIRM {
            Failure = 1,
            WebFailure = 0,
            Credits = 0
        };
        SendToSocket(shopDenyMsg);
    }

    private void ProcessSuccessfulPurchase(Wizard playerWizard, WizClientObjectItem item, uint itemTemplateID, int goldCost) {
        // Serialize the item without behaviors.
        if (!s_itemSerializer.Serialize(item, 1, out var serializedItemWithoutBehaviors)) {
            Logger.Error("Failed to serialize item {0} for purchase", 
                Logger.Args(item.m_globalID));

            return;
        }
        playerWizard.AddItemToInventory(item);
        playerWizard.RemoveGold(goldCost);

        // Inform the game client that a new item has been added to the player's inventory.
        var addItemMsg = new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM {
            GlobalID = playerWizard.GameObjectID,
            SerializedItem = serializedItemWithoutBehaviors,
        };
        SendToSocket(addItemMsg);

        // Inform the game client that the player has acquired a new item.
        var itemAcqMsg = new WIZARD2_53_PROTOCOL.MSG_ITEMACQUISITION {
            ItemGlobalID = item.m_globalID,
            ItemTemplateID = itemTemplateID,
            ItemLocation = 1,
        };
        SendToSocket(itemAcqMsg);

        // Inform the game client that the player's gold has been updated.
        var goldUpdateMsg = new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD {
            Gold = playerWizard.GameStats.m_currentGold,
            MaxGold = playerWizard.GameStats.m_baseGoldPouch
        };
        SendToSocket(goldUpdateMsg);

        // Inform the game client that the purchase was successful.
        var shopConfirmMsg = new WIZARD_12_PROTOCOL.MSG_SHOPBUYCONFIRM();
        SendToSocket(shopConfirmMsg);
    }

    private void HandleIllegalPurchaseAttempt(uint itemTemplateID, ulong interactedObjectGID) {
        var account = GetActiveAccount();
        SendShopDenyMessage();

        var infractionText = $"Player tried to purchase item {itemTemplateID} from NPC {interactedObjectGID} that is not in its inventory!";
        account.AddInfraction(WizardData.Models.Misc.InfractionType.SuspiciousBehavior, infractionText);

        Logger.Warning("Player tried to purchase item {0} from an NPC that it did not have in its inventory. "
            + "This has been logged as suspicious behavior.",
            Logger.Args(itemTemplateID));
    }

    private static int CalculateItemSellValue(WizItemTemplate template) {
        var goldValue = (int) Math.Ceiling(template.m_baseCost * SELL_MODIFIER);
        if (template.m_numPrimaryColors != 1 && template.m_numSecondaryColors != 0) {
            goldValue = (int) Math.Ceiling(goldValue * DYED_ITEM_COST_MULTIPLIER) + 1;
        }

        return goldValue;
    }

    private static int CalculateItemCost(WizItemTemplate template) {
        var goldCost = (int) template.m_baseCost;
        if (template.m_numPrimaryColors != 1 && template.m_numSecondaryColors != 0) {
            goldCost = (int) Math.Ceiling(goldCost * DYED_ITEM_COST_MULTIPLIER) + 1;
        }

        return goldCost;
    }

}
