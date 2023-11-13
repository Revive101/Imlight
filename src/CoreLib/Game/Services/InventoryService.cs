/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using Akka.Actor;
using Akka.Util.Internal;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.Utilities;
using Imlight.Common.ObjectProperty;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Resources;
using static Imlight.Common.Caches.TypeCache;
using static Imlight.Common.ObjectProperty.SerializerOptions;

namespace Imlight.CoreLib.Game.Services;

public class InventoryService : MessageService {
    public InventoryService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor) {
        return Akka.Actor.Props.Create(() => new InventoryService(parentActor));
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTRADIALQUICKCHAT))]
    private void ReceiveRequestRadialQuickChat(GAME_5_PROTOCOL.MSG_REQUESTRADIALQUICKCHAT message) {
        new int[] { 2066, 860841451, 2537945, 203556948 }.ForEach(spellId => {
            SendToSocket(new WIZARD_12_PROTOCOL.MSG_ADDSPELLTOBOOK() {
                SpellID = spellId
            });
        });
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_EQUIPITEM))]
    private void ReceiveEquipItem(GAME_5_PROTOCOL.MSG_EQUIPITEM message) {
        var serializer = new CoreObjectSerializer()
          .OnBehaviors(SerializerOptions.Behaviors.None)
          .OnPropertyMask((SerializerOptions.PropertyFlags) 1);

        var coreObject = GetActiveCoreObject();
        var playerCharacter = GetActiveCharacter();

        // @TODO: Remove this and gather from potential player behavior cache instead.
        if (!CoreObjectFactory.FindBehaviorInstance<ClientWizInventoryBehavior>(coreObject,
                out var inventoryBehavior)) return;
        if (!CoreObjectFactory.FindBehaviorInstance<ClientWizEquipmentBehavior>(coreObject,
            out var equipmentBehavior)) return;

        // Get item object and its template.
        var itemObj = inventoryBehavior.m_itemList.First(item => item.m_globalID == message.ItemID);
        var templateId = itemObj.m_templateID;
        var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(templateId);

        if (message.IsEquip == 1) {
            // @TODO: There should be some "AntiAmbrose" logic here. Double check that the player meets the requirements
            // to equip this item and that the player does not already have an item equipped in its slot.

            if (!ItemInInventory(message.ItemID, coreObject)) {
                // @TODO: Respond to client appropriately.
                Logger.Debug($"Player does not have the item in their inventory!");
                return;
            }

            // Check equipped items to see if player already has an item equipped in the target slot.
            foreach (CoreObject obj in equipmentBehavior.m_itemList) {
                var objTemplate = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(obj.m_templateID);
                if (objTemplate.m_adjectiveList[1] == template.m_adjectiveList[1]) {
                    Logger.Debug("Player swapping items in slot {Slot}", Logger.Args(template.m_adjectiveList[1].ToString()));

                    // Get current equipped item and its slot.
                    var slot = equipmentBehavior.m_slotList.FindIndex(slot => slot.m_itemID == obj.m_globalID);
                    var currentEquippedItem = equipmentBehavior.m_slotList[slot].m_itemID;

                    Logger.Debug("Unequipping item from slot {Slot} | {Name}",
                        Logger.Args(slot, template.m_objectName.ToString()));

                    // Remove item from equipment behavior lists.
                    equipmentBehavior = RemoveSlotFromEquipmentSlotList(slot, equipmentBehavior);
                    equipmentBehavior.m_itemList.RemoveAll(item => item.m_globalID == currentEquippedItem);
                    //equipmentBehavior.m_publicItemList.RemoveAll(item => item.m_itemID == itemObj.m_templateID);
                    //creationEquipment.RemoveAll(item => item.m_itemID == itemObj.m_templateID);

                    // Unequip the previous item.
                    if (currentEquippedItem != 0) {
                        SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM() {
                            ItemID = obj.m_globalID,
                            SlotName = "",
                            IsEquip = 0
                        });

                        var publicUnequipMsg = new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICUNEQUIPITEM() {
                            GlobalID = coreObject.m_globalID,
                            IndexToRemove = (byte) slot
                        };
                        ZoneBroadcast(publicUnequipMsg, false);
                    }
                    break;
                }
            }

            // Confirm to the player that we've equipped their item server side.
            SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM() {
                ItemID = message.ItemID,
                SlotName = message.SlotName,
                IsEquip = message.IsEquip
            });

            // Put ID of equipped item in first empty slot in the slot list, and update itemList, creationList, publicItemList.
            var index = equipmentBehavior.m_slotList.FindIndex(slot => slot.m_itemID == 0);
            equipmentBehavior.m_slotList[index].m_itemID = (GID) message.ItemID;

            equipmentBehavior.m_itemList.Add(itemObj);
            //equipmentBehavior.m_publicItemList.Add(new EquippedItemInfo() { m_itemID = (uint)itemObj.m_templateID });
            //creationEquipment.Add(equippedItemInfo);

            Logger.Debug("Player equipped item from inventory, index {Item} | {Name}",
                Logger.Args((byte) inventoryBehavior.m_itemList.IndexOf(itemObj), template.m_objectName.ToString()));
            Logger.Debug("Equipped item to slot {Slot}", Logger.Args(index));

            // Serialize item and broadcast equip action to other players.
            var item = new WizardEquippedItemInfo() {
                m_itemID = (uint) itemObj.m_templateID,
                m_pattern = (Bui5) template.m_numPatterns,
                m_baseColor = (Bui5) template.m_numPrimaryColors,
                m_trimColor = (Bui5) template.m_numSecondaryColors,
            };

            var data = serializer.Serialize(item);
            var publicEquipMsg = new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICEQUIPITEM() {
                GlobalID = coreObject.m_globalID,
                SerializedInfo = data
            };
            ZoneBroadcast(publicEquipMsg, false);
        }
        else {
            if (!ItemInInventory(message.ItemID, coreObject)) {
                // @TODO: Respond to client appropriately.
                Logger.Debug($"Player does not have the item in their inventory!");
                return;
            }

            // Confirm to the player that we've unequipped their item server side.
            SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM() {
                ItemID = message.ItemID,
                SlotName = message.SlotName,
                IsEquip = message.IsEquip
            });

            // Get slot index of item to unequip and number of total equipped items.
            var slot = equipmentBehavior.m_slotList.FindIndex(slot => slot.m_itemID == message.ItemID);
            var currentEquippedItem = equipmentBehavior.m_slotList[slot].m_itemID;

            Logger.Debug("Unequipping item from slot {Slot} | {Name}",
                Logger.Args(slot, template.m_objectName.ToString()));

            // Remove item from equipment behavior lists.
            equipmentBehavior = RemoveSlotFromEquipmentSlotList(slot, equipmentBehavior);
            equipmentBehavior.m_itemList.RemoveAll(item => item.m_globalID == currentEquippedItem);
            //equipmentBehavior.m_publicItemList.RemoveAll(item => item.m_itemID == itemObj.m_templateID);
            //creationEquipment.RemoveAll(item => item.m_itemID == itemObj.m_templateID);

            if (currentEquippedItem != 0) {
                var publicUnequipMsg = new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICUNEQUIPITEM() {
                    GlobalID = coreObject.m_globalID,
                    IndexToRemove = (byte) slot
                };

                ZoneBroadcast(publicUnequipMsg, false);
            }
        }
    }

    #region Destroy/Feed Inventoryitem
    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_TRASHINVENTORYITEM))]
    private void ReceiveTrashInventoryItem(GAME_5_PROTOCOL.MSG_TRASHINVENTORYITEM message) {
        var coreObject = GetActiveCoreObject();

        // @TODO: Remove this and gather from potential player behavior cache instead.
        if (!CoreObjectFactory.FindBehaviorInstance<ClientWizInventoryBehavior>(coreObject,
                out var inventoryBehavior)) return;
        if (!CoreObjectFactory.FindBehaviorInstance<ClientWizEquipmentBehavior>(coreObject,
            out var equipmentBehavior)) return;

        inventoryBehavior.m_itemList.RemoveAll(item => item.m_globalID == message.GlobalID);
        equipmentBehavior.m_itemList.RemoveAll(item => item.m_globalID == message.GlobalID);

        SendToSocket(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_REMOVEITEM() {
            GlobalID = coreObject.m_globalID,
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
        var serializer = new CoreObjectSerializer()
          .OnBehaviors(SerializerOptions.Behaviors.None)
          .OnPropertyMask((SerializerOptions.PropertyFlags) 1);

        var coreObject = GetActiveCoreObject();
        var playerCharacter = GetActiveCharacter();

        // @TODO: Remove this and gather from potential player behavior cache instead.
        if (!CoreObjectFactory.FindBehaviorInstance<ClientWizInventoryBehavior>(coreObject,
            out var inventoryBehavior)) return;
        if (!CoreObjectFactory.FindBehaviorInstance<ClientWizEquipmentBehavior>(coreObject,
            out var equipmentBehavior)) return;

        var quickSellItemList = (QuickSellItemList) serializer.Deserialize(message.Data);
        int goldSum = 0;

        // Remove items from inventory and equipment, tally up gold sum.
        foreach (QuickSellItem quickSellItem in quickSellItemList.m_quickSellItemList) {
            var item = inventoryBehavior.m_itemList.First(item => item.m_globalID == quickSellItem.m_sellItemGID);
            var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(item.m_templateID);

            inventoryBehavior.m_itemList.Remove(item);
            equipmentBehavior.m_itemList.Remove(item);

            // Some items (snack, reagents) are stackable.
            for (int i = 0; i < quickSellItem.m_quantity; i++) {
                SendToSocket(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_REMOVEITEM() {
                    GlobalID = coreObject.m_globalID,
                    ItemID = quickSellItem.m_sellItemGID
                });

                goldSum += (int) Math.Ceiling(template.m_baseCost * 0.05f); // @TODO: Fix payout, slightly less than client calculation.
            }
        }

        // Update player with their new gold balance.
        playerCharacter.GameStats.m_currentGold += goldSum;
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD() {
            Gold = playerCharacter.GameStats.m_currentGold,
            MaxGold = playerCharacter.GameStats.m_baseGoldPouch,
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

    private bool ItemInInventory(ulong itemId, CoreObject coreObject) {
        if (!CoreObjectFactory.FindBehaviorInstance<ClientWizInventoryBehavior>(coreObject,
                out var inventoryBehavior)) return false;

        var invItemList = inventoryBehavior.m_itemList.Any(item => item.m_globalID == itemId);
        return invItemList;
    }

    private ClientWizEquipmentBehavior RemoveSlotFromEquipmentSlotList(int slot, ClientWizEquipmentBehavior equipmentBehavior) {
        // Zero-out item from slot list and move all items down to fill "empty" zero slots, should they exist.
        var numEquippedItemsInSlots = equipmentBehavior.m_slotList.Count(slot => slot.m_itemID != 0);
        equipmentBehavior.m_slotList[slot].m_itemID = (GID) 0;

        if (slot < numEquippedItemsInSlots - 1) {
            for (int i = slot; i < numEquippedItemsInSlots; i++) {
                if (equipmentBehavior.m_slotList[i].m_itemID != 0) {
                    equipmentBehavior.m_slotList[i - 1].m_itemID = equipmentBehavior.m_slotList[i].m_itemID;
                    equipmentBehavior.m_slotList[i].m_itemID = (GID) 0;
                }
            }
        }
        return equipmentBehavior;
    }
}
