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
using WizUnraveler;
using Akka.Util.Internal;
using Imlight.Common.Serializable;
using Imlight.Server.Database.Records.Character;
using static Imlight.Server.Shared.Packets.CHARACTER_103_PROTOCOL;
using WizUnraveler.IO;
using Newtonsoft.Json;

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
            Log.Logger.Fatal(JsonConvert.SerializeObject(character.Character.equipmentBehaviorCache));
            switch (message.IsEquip)
            {
                case 1:
                    if (!Enum.TryParse(message.SlotName, out EquipmentSlot slot))
                    {
                        Log.Logger.Warning($"Could not parse slotName ${message.SlotName}!");
                        return;
                    }

                    var equipResult = EquipItem(slot, message.ItemID, out WizardEquippedItemInfo itemInfo, out var replacedItem);
                    if (equipResult != EquipmentStatus.Success)
                    {
                        Log.Logger.Warning($"There was an error equipping an item! {equipResult}");
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
                    break;
                case 2:
                    var unequipResult = UnequipItem(message.ItemID, out var indexToRemove);

                    if (unequipResult != EquipmentStatus.Success)
                    {
                        Log.Logger.Warning($"There was an error unequipping an item: {unequipResult}");
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
                    break;
            }

        }

        #region Destroy/Feed Inventoryitem
        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_TRASHINVENTORYITEM))]
        private void ReceiveTrashInventoryItem(GAME_5_PROTOCOL.MSG_TRASHINVENTORYITEM message)
        {
            SendToSocket(new GAME_5_PROTOCOL.MSG_TRASHINVENTORYITEM()
            {
                GlobalID = message.GlobalID,
                TemplateID = message.TemplateID,
            });
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

        private EquipmentStatus EquipItem(EquipmentSlot slot, ulong itemId, out WizardEquippedItemInfo? itemInfo, out ulong? replacedId)
        {
            var coreObject = GetActiveCoreObject();
            var equipmentBehavior = coreObject.Character.equipmentBehaviorCache;

            itemInfo = default;
            replacedId = default;

            try
            {
                if (!HasItem(equipmentBehavior, itemId)) return EquipmentStatus.ItemNotInInventory;
                if (equipmentBehavior.m_itemList.Any(i => i.m_globalID == itemId))
                    return EquipmentStatus.ItemAlreadyEquipped;

                var itemObj = GetItemObject(equipmentBehavior, itemId);
                var itemTemplate = (WizItemTemplate)CoreObjectFactory.GetCoreTemplate(itemObj.m_templateID);

                var equippedItemInfo = new WizardEquippedItemInfo()
                {
                    m_itemID = (uint)itemObj.m_templateID,
                    m_pattern = (FiveBitByte)itemTemplate.m_numPatterns,
                    m_baseColor = (FiveBitByte)itemTemplate.m_numPrimaryColors,
                    m_trimColor = (FiveBitByte)itemTemplate.m_numSecondaryColors,
                };

                itemInfo = equippedItemInfo;

                var inventory = coreObject.Character.CreationData.m_equipmentInfoList.m_infoList;
                inventory.RemoveAll(item => item.m_itemID == itemObj.m_templateID); // Remove all duplicate items with the same templateID
                coreObject.Character.CreationData.m_equipmentInfoList.m_infoList.Add(equippedItemInfo);
                //@TODO: Save to DB

                // Replace
                var replace = equipmentBehavior.m_slotList[(int)slot].m_itemID;
                equipmentBehavior.m_itemList.RemoveAll(i => i.m_globalID == replace);
                equipmentBehavior.m_publicItemList.RemoveAll(i => i.m_itemID == itemObj.m_templateID);
                equipmentBehavior.m_slotList[(int)slot].m_itemID = (GID)itemId; // change in EquippedSlotInfoList
                replacedId = replace; // service needs to send a message back to client

                // add to coreobject list
                if (equipmentBehavior.m_itemList.All(i => i.m_globalID != itemObj.m_globalID)) // no dupes
                    equipmentBehavior.m_itemList.Add(itemObj);
                if (equipmentBehavior.m_publicItemList.All(i => i.m_itemID != itemObj.m_globalID))
                    equipmentBehavior.m_publicItemList.Add(new EquippedItemInfo() { m_itemID = (uint)itemObj.m_templateID });

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

            indexToRemove = default;
            try
            {
                // if inventory contains item
                if (!HasItem(equipmentBehavior, globalId)) return EquipmentStatus.ItemNotInInventory;
                var itemObj = GetItemObject(equipmentBehavior, globalId);

                // change in CharacterCreationInfo
                var infList = coreObject.Character.CreationData.m_equipmentInfoList.m_infoList;
                infList.RemoveAll(i => i.m_itemID == itemObj.m_templateID); // dupes

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


        private bool HasItem(ClientWizEquipmentBehavior equipmentBehavior, ulong itemId) 
        {
            equipmentBehavior.m_itemList.ForEach(item =>
            {
                Log.Logger.Debug($"DisplayKey: {item.m_displayKey}, templateId: {item.m_templateID}, globalId: {item.m_globalID}");
            });
            return equipmentBehavior.m_itemList.Any(item => item.m_globalID == itemId);
        }
        private EquipmentSlot GetItemSlot(ClientWizEquipmentBehavior equipmentBehavior, ulong globalId) 
        {
            return (EquipmentSlot)equipmentBehavior.m_slotList.First(slot => slot.m_itemID == globalId).m_itemSlotNameID;
        }

        public CoreObject GetItemObject(ClientWizEquipmentBehavior equipmentBehavior, ulong globalId)
        {
            return equipmentBehavior.m_itemList.First(x => x.m_globalID == globalId);
        }
    }
}
