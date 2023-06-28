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
            var coreObject = GetActiveCoreObject();
            var characterObject = coreObject.CharacterObject;
            var equipmentBehavior = coreObject.Character.equipmentBehaviorCache;
            var creationEquipment = coreObject.Character.CreationData.m_equipmentInfoList.m_infoList;

            if (message.IsEquip == 0) // Un-Equip
            {
                if (!ItemInInventory(message.ItemID))
                {
                    Log.Logger.Debug($"Player does not have the item in the inventory!");
                    return;
                    //@TODO: Remove item from inventory
                }

                var itemObj = GetItemCoreObject(message.ItemID);

                creationEquipment.RemoveAll(i => i.m_itemID == itemObj.m_templateID);

                equipmentBehavior.m_itemList.RemoveAll(item => item.m_globalID == message.ItemID);
                equipmentBehavior.m_publicItemList.RemoveAll(item => item.m_itemID == message.ItemID);
                equipmentBehavior.m_slotList[(int)GetItemSlot(message.ItemID)].m_itemID = (GID)0;

                SendToSessionServices(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST()
                {
                    Selfless = false,
                    Sender = SessionActor.ActorRef,
                    Message = new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICUNEQUIPITEM()
                    {
                        GlobalID = characterObject.m_globalID,
                        IndexToRemove = 0
                    }
                });

                Log.Logger.Debug($"itemID {message.ItemID}");

                SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM()
                {
                    IsEquip = 0,
                    ItemID = message.ItemID,
                    SlotName = message.SlotName,
                });
            }
            else if (message.IsEquip == 1) // Equip
            {
                if (!Enum.TryParse(message.SlotName, out EquipmentSlot slot))
                {
                    Log.Logger.Warning($"Could not parse slotName {message.SlotName}");
                    return;
                }

                Log.Logger.Debug($"Parsed slotname '{message.SlotName}' to {(uint)slot}");

                if (!ItemInInventory(message.ItemID))
                {
                    Log.Logger.Debug($"Player does not have the item in the inventory!");
                    return;
                }
                Log.Logger.Debug($"Player has the item in the inventory");

                var serializer = new CoreObjectSerializer()
                    .WithSerializerFlags(SerializerFlags.None)
                    .WithPropertyFlags((PropertyFlags)1);

                var itemObj = GetItemCoreObject(message.ItemID);
                var itemTemplate = (WizItemTemplate)CoreObjectFactory.GetCoreTemplate(itemObj.m_templateID);

                var equippedItemInfo = new WizardEquippedItemInfo()
                {
                    m_itemID = (uint)itemObj.m_templateID, //!! Must be templateID !!
                    m_pattern = (FiveBitByte)itemTemplate.m_numPatterns,
                    m_baseColor = (FiveBitByte)itemTemplate.m_numPrimaryColors,
                    m_trimColor = (FiveBitByte)itemTemplate.m_numSecondaryColors,
                };

                var currentEquippedItem = equipmentBehavior.m_slotList[(int)slot].m_itemID;
                Log.Logger.Debug($"Current equipped item [{currentEquippedItem.Value}]");

                // Change the equipped item for the CreationMenu
                creationEquipment.RemoveAll(item => item.m_itemID == itemObj.m_templateID);
                creationEquipment.Add(equippedItemInfo);

                // EquipmentBehavior
                // itemList
                equipmentBehavior.m_itemList.RemoveAll(item => item.m_globalID == currentEquippedItem);
                equipmentBehavior.m_itemList.RemoveAll(i => i.m_globalID != itemObj.m_globalID);
                equipmentBehavior.m_itemList.Add(itemObj);

                // publicItemList
                equipmentBehavior.m_publicItemList.RemoveAll(item => item.m_itemID == itemObj.m_templateID);
                equipmentBehavior.m_publicItemList.RemoveAll(i => i.m_itemID != itemObj.m_globalID);
                equipmentBehavior.m_publicItemList.Add(new EquippedItemInfo() { m_itemID = (uint)itemObj.m_templateID });


                // slotList
                equipmentBehavior.m_slotList[(int)slot].m_itemID = (GID)message.ItemID;

                if (currentEquippedItem != 0)
                {
                    SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM()
                    {
                        IsEquip = 0,
                        ItemID = (ulong)currentEquippedItem,
                        SlotName = message.SlotName
                    });

                    SendToSessionServices(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST()
                    {
                        Selfless = false,
                        Sender = SessionActor.ActorRef,
                        Message = new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICUNEQUIPITEM()
                        {
                            GlobalID = characterObject.m_globalID,
                            IndexToRemove = 0
                        }
                    });
                }

                SendToSessionServices(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST()
                {
                    Selfless = false,
                    Sender = SessionActor.ActorRef,
                    Message = new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICEQUIPITEM()
                    {
                        GlobalID = characterObject.m_globalID,
                        SerializedInfo = serializer.Serialize(equippedItemInfo)
                    }
                });

                SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM()
                {
                    IsEquip = 1,
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
            var itemObj = GetItemCoreObject(message.GlobalID);
            var itemTemplate = (WizItemTemplate)CoreObjectFactory.GetCoreTemplate(itemObj.m_templateID);

            SendToSocket(new GAME_5_PROTOCOL.MSG_TRASHINVENTORYITEM()
            {
                GlobalID = message.GlobalID,
                TemplateID = itemTemplate.m_templateID,
            });

            if (ItemInInventory(message.GlobalID))
            {
                Log.Logger.Debug($"Player has item: ItemID {message.GlobalID}, templateID {message.TemplateID}/{itemTemplate.m_templateID}, actor_globalID {coreObject.CharacterObject.m_globalID}");
                SendToSocket(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_REMOVEITEM()
                {
                    GlobalID = coreObject.CharacterObject.m_globalID,
                    ItemID = message.GlobalID
                });

                coreObject.Character.CreationData.m_equipmentInfoList.m_infoList.RemoveAll(i => i.m_itemID == itemTemplate.m_templateID);
                inventoryBehavior.m_itemList.RemoveAll(item => item.m_globalID == message.GlobalID);

                equipmentBehavior.m_slotList.RemoveAll(slot => slot.m_itemID == message.GlobalID);
                equipmentBehavior.m_itemList.RemoveAll(item => item.m_globalID == message.GlobalID);
                equipmentBehavior.m_publicItemList.RemoveAll(item => item.m_itemID == message.GlobalID);
            }
            else
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

        private bool ItemInInventory(ulong itemId)
        {
            var inventoryBehavior = GetActiveCoreObject().Character.inventoryBehaviorCache;
            var invItemList = inventoryBehavior.m_itemList.Any(item => item.m_globalID == itemId);
            return invItemList;
        }

        private uint GetItemSlot(ulong globalId)
        {
            var coreObject = GetActiveCoreObject();
            var equipmentBehavior = coreObject.Character.equipmentBehaviorCache;

            var itemSlot = equipmentBehavior.m_slotList.First(slot => slot.m_itemID == globalId).m_itemSlotNameID;
            Log.Logger.Debug($"ItemSlot: {itemSlot} of ID {globalId}");
            return itemSlot;
        }

        public CoreObject GetItemCoreObject(ulong globalId)
        {
            var inventoryBehavior = GetActiveCoreObject().Character.inventoryBehaviorCache;
            return inventoryBehavior.m_itemList.First(x => x.m_globalID == globalId);
        }
    }
}
