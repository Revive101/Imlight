/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Utilities;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WizUnraveler.Cache;
using static WizUnraveler.ObjectProperty.ObjectSerializer;
using WizUnraveler.ObjectProperty;
using Imlight.Server.Database;
using static WizUnraveler.Cache.TypeCache;
using Akka.Util.Internal;
using Imlight.Common.Serializable;
using Imlight.Server.Database.Records.Character;
using static Imlight.Server.Shared.Packets.CHARACTER_103_PROTOCOL;
using Akka.Routing;
using System.Net.Http;
using System.Net;
using System.IO;

namespace Imlight.Server.Game.Services
{
    public class InventoryService : MessageService
    {
        private enum EquipmentStatus
        {
            Success,
            Failure,
            ItemNotInInventory,
            ItemAlreadyEquipped
        }

        public InventoryService(SessionActor sessionActor) : base(sessionActor) { }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new InventoryService(parentActor));
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTRADIALQUICKCHAT))]
        private void ReceiveRequestRadialQuickChat(GAME_5_PROTOCOL.MSG_REQUESTRADIALQUICKCHAT message)
        {
            new int[] { 2066, 860841451, 2537945, 203556948 }.ForEach(spellId =>
            {
                SendToSocket(new WIZARD_12_PROTOCOL.MSG_ADDSPELLTOBOOK()
                {
                    SpellID = spellId
                });
            });
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_EQUIPITEM))]
        private void ReceiveEquipItem(GAME_5_PROTOCOL.MSG_EQUIPITEM message)
        {
            var character = GetActiveCoreObject();
            var coreObject = character.CharacterObject;

            if(message.IsEquip == 1)
            {
                if (!Enum.TryParse(message.SlotName, out EquipmentSlot slot))
                {
                    Log.Logger.Warning($"Could not parse slotName {message.SlotName}");
                    return;
                }

                var equipResult = EquipItem(slot, message.ItemID, out WizardEquippedItemInfo itemInfo, out var replacedItem);
                if (equipResult != EquipmentStatus.Success)
                {
                    Log.Logger.Warning($"There was an {equipResult} error equipping an item");
                    return;
                }

                var serializer = new CoreObjectSerializer()
                    .WithSerializerFlags(SerializerFlags.None)
                    .WithPropertyFlags((PropertyFlags)1);

                SendToSessionServices(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST()
                {
                    Selfless = false,
                    Sender = SessionActor.ActorRef,
                    Message = new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICEQUIPITEM()
                    {
                        GlobalID = coreObject.m_globalID,
                        SerializedInfo = serializer.Serialize(itemInfo)
                    }
                });

                SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM()
                {
                    IsEquip = 1,
                    ItemID = message.ItemID,
                    SlotName = message.SlotName,
                });

                if (replacedItem != null)
                {
                    SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM()
                    {
                        IsEquip = 0,
                        ItemID = (ulong)replacedItem,
                        SlotName = message.SlotName
                    });
                }
            }
            else
            {
                var unequipResult = UnequipItem(message.ItemID, out var indexToRemove);

                if (unequipResult != EquipmentStatus.Success)
                {
                    Log.Logger.Warning($"There was an {unequipResult} error unequipping an item");
                    return;
                }

                SendToSessionServices(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST()
                {
                    Selfless = false,
                    Sender = SessionActor.ActorRef,
                    Message = new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICUNEQUIPITEM()
                    {
                        GlobalID = coreObject.m_globalID,
                        IndexToRemove = indexToRemove
                    }
                });

                SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM()
                {
                    IsEquip = 0,
                    ItemID = message.ItemID,
                    SlotName = message.SlotName,
                });
            }
        }

        #region Destroy/Feed Inventoryitem
        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_TRASHINVENTORYITEM))]
        private void ReceiveTrashInventoryItem(GAME_5_PROTOCOL.MSG_TRASHINVENTORYITEM message)
        {
            var coreObject = GetActiveCoreObject();
            var inventoryBehavior = coreObject.Character.inventoryBehaviorCache;
            var equipmentBehavior = coreObject.Character.equipmentBehaviorCache;

            // Idk why but TemplateID is always 0 :pepeShrug: so we'll just get the templateID using the GlobalID
            var itemObj = GetItemCoreObject(inventoryBehavior, message.GlobalID);
            var itemTemplate = (WizItemTemplate)CoreObjectFactory.GetCoreTemplate(itemObj.m_templateID);

            SendToSocket(new GAME_5_PROTOCOL.MSG_TRASHINVENTORYITEM()
            {
                GlobalID = message.GlobalID,
                TemplateID = itemTemplate.m_templateID,
            });

            if (HasItem(inventoryBehavior, message.GlobalID))
            {
                Log.Logger.Debug($"Player has item: ItemID {message.GlobalID}, templateID {message.TemplateID}/{itemTemplate.m_templateID}, actor_globalID {coreObject.CharacterObject.m_globalID}");
                SendToSocket(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_REMOVEITEM()
                {
                    GlobalID = coreObject.CharacterObject.m_globalID,
                    ItemID = message.GlobalID
                });

                coreObject.Character.CreationData.m_equipmentInfoList.m_infoList.RemoveAll(i => i.m_itemID == itemTemplate.m_templateID);
                inventoryBehavior.m_itemList.RemoveAll(item => item.m_globalID == message.GlobalID);
                equipmentBehavior.m_itemList.RemoveAll(item => item.m_globalID == message.GlobalID);
            } else
            {
                Log.Logger.Debug("Player does not have the item");
            }
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_FEEDINVENTORYITEM))]
        private void ReceiveFeedInventoryItem(GAME_5_PROTOCOL.MSG_FEEDINVENTORYITEM message)
        {
            SendToSocket(new GAME_5_PROTOCOL.MSG_FEEDINVENTORYITEM()
            {
                FedObjectID = message.FedObjectID,
                PetID = message.PetID,
            });
        }
        #endregion

        #region Quicksell from Inventory
        // QUICKSELL FROM INVENTORY
        [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_REQUESTQUICKSELL))]
        private void ReceiveRequestQuickSell(WIZARD_12_PROTOCOL.MSG_REQUESTQUICKSELL message)
        {
            SendToSocket(new WIZARD_12_PROTOCOL.MSG_REQUESTQUICKSELL()
            {
                FromTemplateID = message.FromTemplateID,
                Section = message.Section,
                SellModifier = message.SellModifier,
            });
        }

        [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_QUICKSELLREQUEST))]
        private void ReceiveQuickSellRequest(WIZARD2_53_PROTOCOL.MSG_QUICKSELLREQUEST message)
        {
            // @TODO: Remove items from inventory & add gold to player
            SendToSocket(new WIZARD2_53_PROTOCOL.MSG_QUICKSELLREQUEST()
            {
                Data = message.Data,
            });
        }
        #endregion

        #region Jewels
        // JEWELS
        [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_EQUIPJEWELREQUEST))]
        private void ReceiveEquipJewelRequest(WIZARD2_53_PROTOCOL.MSG_EQUIPJEWELREQUEST message)
        {
            SendToSocket(new WIZARD2_53_PROTOCOL.MSG_EQUIPJEWELREQUEST()
            {
                ItemGID = message.ItemGID,
                JewelGID = message.JewelGID,
                SocketNumber = message.SocketNumber,
            });

            SendToSocket(new WIZARD2_53_PROTOCOL.MSG_EQUIPJEWELTOITEM()
            {
                ItemGID = message.ItemGID,
                JewelGID = message.JewelGID,
                SocketNumber = message.SocketNumber,
                GlobalID = RandomGen.GenerateGUID()
            });
        }
        #endregion

        private MSG_CHARACTER GetActiveCoreObject()
        {
            var msg = new MSG_QUERYACTIVECHARACTER();
            var response = AskSessionServices<MSG_CHARACTER>(msg);

            return response;
        }

        private EquipmentStatus EquipItem(EquipmentSlot slot, ulong itemId, out WizardEquippedItemInfo? equippedItem, out ulong? replacedId)
        {
            var coreObject = GetActiveCoreObject();
            var equipmentBehavior = coreObject.Character.equipmentBehaviorCache;
            var inventoryBehavior = coreObject.Character.inventoryBehaviorCache;

            equippedItem = default;
            replacedId = default;

            try
            {
                if (!HasItem(inventoryBehavior, itemId))
                {
                    Log.Logger.Warning("Player doesn't have that item in the inventory!");
                    return EquipmentStatus.ItemNotInInventory;
                }
                if (equipmentBehavior.m_slotList.Any(i => i.m_itemID == itemId))
                {
                    Log.Logger.Warning("That item is already equipped!");
                    return EquipmentStatus.ItemAlreadyEquipped;
                }

                var itemObj = GetItemCoreObject(inventoryBehavior, itemId);
                var itemTemplate = (WizItemTemplate)CoreObjectFactory.GetCoreTemplate(itemObj.m_templateID);
                var creationInventory = coreObject.Character.CreationData.m_equipmentInfoList.m_infoList;


                var equippedItemInfo = new WizardEquippedItemInfo()
                {
                    m_itemID = (uint)itemObj.m_templateID,
                    m_pattern = (FiveBitByte)itemTemplate.m_numPatterns,
                    m_baseColor = (FiveBitByte)itemTemplate.m_numPrimaryColors,
                    m_trimColor = (FiveBitByte)itemTemplate.m_numSecondaryColors,
                };
                equippedItem = equippedItemInfo;


                // Remove all duplicate items with the same templateID from the creationInventory
                creationInventory.RemoveAll(item => item.m_itemID == itemObj.m_templateID);
                creationInventory.Add(equippedItemInfo); //

                // Get the current equipped item at the slot
                var currentSlotItem = equipmentBehavior.m_slotList[(int)slot].m_itemID;

                // Remove every item from the equipmentList, which matches the current item in the slot
                equipmentBehavior.m_itemList.RemoveAll(i => i.m_globalID == currentSlotItem);
                // Remove every item from the public list, which matches the new item
                equipmentBehavior.m_publicItemList.RemoveAll(i => i.m_itemID == itemObj.m_templateID);
                // Get the current slot and set the equiped item to our new item
                equipmentBehavior.m_slotList[(int)slot].m_itemID = (GID)itemId; // change in EquippedSlotInfoList
                replacedId = currentSlotItem;

                // add to coreobject list
                if (equipmentBehavior.m_itemList.All(i => i.m_globalID != itemObj.m_globalID)) // no dupes
                {
                    equipmentBehavior.m_itemList.Add(itemObj);
                }


                if (equipmentBehavior.m_publicItemList.All(i => i.m_itemID != itemObj.m_globalID))
                {
                    equipmentBehavior.m_publicItemList.Add(new EquippedItemInfo() { m_itemID = (uint)itemObj.m_templateID });
                }

                return EquipmentStatus.Success;
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
                return EquipmentStatus.Failure;
            }

        }

        private EquipmentStatus UnequipItem(ulong globalId, out byte indexToRemove)
        {
            var coreObject = GetActiveCoreObject();
            var equipmentBehavior = coreObject.Character.equipmentBehaviorCache;
            var inventoryBehavior = coreObject.Character.inventoryBehaviorCache;

            indexToRemove = default;
            try
            {
                // if inventory contains item
                if (!HasItem(inventoryBehavior, globalId)) return EquipmentStatus.ItemNotInInventory;
                var itemObj = GetItemCoreObject(inventoryBehavior, globalId);

                // change in CharacterCreationInfo
                var creationInventory = coreObject.Character.CreationData.m_equipmentInfoList.m_infoList;
                creationInventory.RemoveAll(i => i.m_itemID == itemObj.m_templateID); // dupes

                equipmentBehavior.m_itemList.RemoveAll(i => i.m_globalID == globalId);
                equipmentBehavior.m_publicItemList.RemoveAll(i => i.m_itemID == itemObj.m_templateID);
                equipmentBehavior.m_slotList[(int)GetItemSlot(equipmentBehavior, itemObj.m_globalID)].m_itemID = (GID)0; // change in EquippedSlotInfoList
                //@TODO: Save to DB

                return EquipmentStatus.Success;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex.Message);
                return EquipmentStatus.Failure;
            }
        }


        private bool HasItem(ClientWizInventoryBehavior inventoryBehavior, ulong itemId)
        {
            var hasItem = inventoryBehavior.m_itemList.Any(item => item.m_globalID == itemId);
            Log.Logger.Debug($"Player has item: [{itemId}]:[{hasItem}]");
            return hasItem;
        }
        private uint GetItemSlot(ClientWizEquipmentBehavior equipmentBehavior, ulong globalId)
        {
            var itemSlot = equipmentBehavior.m_slotList.First(slot => slot.m_itemID == globalId).m_itemSlotNameID;
            Log.Logger.Debug($"ItemSlot: {itemSlot} of ID {globalId}");
            return itemSlot;
        }

        public CoreObject GetItemCoreObject(ClientWizInventoryBehavior inventoryBehavior, ulong globalId)
        {
            return inventoryBehavior.m_itemList.First(x => x.m_globalID == globalId);
        }
    }
}
