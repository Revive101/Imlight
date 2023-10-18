/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using Akka.Actor;
using Akka.Util.Internal;
using Imlight.Common.Utilities;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;
using Imlight.Common.Serializable;
using Imlight.Common.Serializable.Caches;
using Imlight.Common.Serializable.ObjectProperty;
using Imlight.Server.Shared.Resources;
using static Imlight.Common.Serializable.Caches.TypeCache;
using static Imlight.Common.Serializable.ObjectSerializer;
using Imlight.Server.Game.Models;

namespace Imlight.Server.Game.Services;

public class InventoryService : MessageService
{
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
        var serializer = new CoreObjectSerializer()
            .WithSerializerFlags(SerializerFlags.None)
            .WithPropertyFlags((PropertyFlags)1);

        var coreObject = GetActiveCoreObject();
        var playerCharacter = GetActiveCharacter();

        // Confirm to the player that we've equipped their item server side.
        // @TODO: There should be some "AntiAmbrose" logic here. Double check that the player meets the requirements
        // to equip this item.

        SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM()
        {
            ItemID = message.ItemID,
            SlotName = message.SlotName,
            IsEquip = message.IsEquip
        });

        // @TODO: Remove this and gather from potential player behavior cache instead.
        if (!CoreObjectFactory.FindBehaviorInstance<ClientWizInventoryBehavior>(coreObject,
                out var inventoryBehavior)) return;
        var itemObj = inventoryBehavior.m_itemList.First(item => item.m_globalID == message.ItemID);

        if (message.IsEquip == 1)
        {
            Log.Information("Player equipped item {Item}",
                Log.Args((byte)inventoryBehavior.m_itemList.IndexOf(itemObj)));
            var templateId = itemObj.m_templateID;
            var template = (WizItemTemplate)CoreObjectFactory.GetCoreTemplate(templateId);
            var item = new WizardEquippedItemInfo()
            {
                m_itemID = (uint)itemObj.m_templateID,
                m_pattern = (FiveBitByte)template.m_numPatterns,
                m_baseColor = (FiveBitByte)template.m_numPrimaryColors,
                m_trimColor = (FiveBitByte)template.m_numSecondaryColors,
            };
            var data = serializer.Serialize(item);
            var hex = Convert.ToHexString(data);
            Log.Information(hex);

            var publicEquipMsg = new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICEQUIPITEM()
            {
                GlobalID = coreObject.m_globalID, // coreObject.CharacterObject.m_globalID ????
                SerializedInfo = data
            };
            ZoneBroadcast(publicEquipMsg, false);
        }
        else
        {
            if (!CoreObjectFactory.FindBehaviorInstance<ClientWizEquipmentBehavior>(coreObject,
                out var equipmentBehavior)) return;
            var templateId = itemObj.m_templateID;
            var template = (WizItemTemplate)CoreObjectFactory.GetCoreTemplate(templateId);
            var slot = (int)Enum.Parse(typeof(EquipmentSlot), template.m_adjectiveList[1].ToString());

            Log.Information("Player unequipped an item");
            /*for (int i = 0; i < 10; i++)
            {
                Log.Logger.Warning($"Could not parse slotName {message.SlotName}");
                return;
            }*/
            Log.Logger.Debug($"Parsed slotname '{message.SlotName}' to {(uint)slot}");
            if (!ItemInInventory(message.ItemID, coreObject))
            {
                Log.Logger.Debug($"Player does not have the item in the inventory!");
                return;
            }
            Log.Logger.Debug($"Player has the item in the inventory");

            var equippedItemInfo = new WizardEquippedItemInfo()
            {
                m_itemID = (uint)itemObj.m_templateID, //!! Must be templateID !!
                m_pattern = (FiveBitByte)template.m_numPatterns,
                m_baseColor = (FiveBitByte)template.m_numPrimaryColors,
                m_trimColor = (FiveBitByte)template.m_numSecondaryColors,
            };

            var currentEquippedItem = equipmentBehavior.m_slotList[(int)slot].m_itemID;
            Log.Logger.Debug($"Current equipped item [{currentEquippedItem.Value}]");

            // Change the equipped item for the CreationMenu
            //creationEquipment.RemoveAll(item => item.m_itemID == itemObj.m_templateID);
            //creationEquipment.Add(equippedItemInfo);
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
                var publicUnequipMsg = new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICUNEQUIPITEM()
                {
                    GlobalID = coreObject.m_globalID,
                    IndexToRemove = (byte)slot
                };

                ZoneBroadcast(publicUnequipMsg, false);
                return;
            }

            var msg = new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICEQUIPITEM()
            {
                GlobalID = coreObject.m_globalID,
                SerializedInfo = serializer.Serialize(equippedItemInfo)
            };

            ZoneBroadcast(msg, false);

            SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM()
            {
                IsEquip = 1,
                ItemID = message.ItemID,
                SlotName = message.SlotName,
            });
        }

        //

        //
        //
        //
        //
        //
        //
        //    sendtosocket(new game_5_protocol.msg_equipitem()
        //    {
        //        itemid = message.itemid,
        //        slotname = message.slotname,
        //        isequip = message.isequip
        //    });
        //    @todo: remove this and gather from potential player behavior cache instead.
        //    if (!coreobjectfactory.findbehaviorinstance<clientwizinventorybehavior>(coreobject,
        //            out var inventorybehavior)) return;

        //    var itemobj = inventorybehavior.m_itemlist.first(item => item.m_globalid == message.itemid);
        //    if (message.isequip == 1)
        //    {
        //        log.information("player equipped item {item}",
        //            log.args((byte)inventorybehavior.m_itemlist.indexof(itemobj)));
        //        var templateid = itemobj.m_templateid;
        //        var template = (wizitemtemplate)coreobjectfactory.getcoretemplate(templateid);

        //        var item = new wizardequippediteminfo()
        //        {
        //            m_itemid = (uint)itemobj.m_templateid,
        //            m_pattern = (fivebitbyte)template.m_numpatterns,
        //            m_basecolor = (fivebitbyte)template.m_numprimarycolors,
        //            m_trimcolor = (fivebitbyte)template.m_numsecondarycolors,
        //        };

        //        var data = serializer.serialize(item);
        //        var hex = convert.tohexstring(data);
        //        log.information(hex);

        //        tellotherservices(new zone_102_protocol.msg_zonebroadcast()
        //        {
        //            selfless = false,
        //            sender = sessionactor.actorref,
        //            message = new game_5_protocol.msg_equipmentbehavior_publicequipitem()
        //            {
        //                globalid = coreobject.m_globalid,
        //                serializedinfo = data
        //            }
        //        });
        //    }
        //    else
        //    {
        //        log.information("player unequipped an item");
        //        for (int i = 0; i < 10; i++)
        //        {
        //            tellotherservices(new zone_102_protocol.msg_zonebroadcast()
        //            {
        //                selfless = false,
        //                sender = sessionactor.actorref,
        //                message = new game_5_protocol.msg_equipmentbehavior_publicunequipitem()
        //                {
        //                    globalid = coreobject.m_globalid,
        //                    indextoremove = (byte)i
        //                }
        //            });
        //        }
        //    }
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

    private bool ItemInInventory(ulong itemId, CoreObject coreObject)
    {
        if (!CoreObjectFactory.FindBehaviorInstance<ClientWizInventoryBehavior>(coreObject,
                out var inventoryBehavior)) return false;

        var invItemList = inventoryBehavior.m_itemList.Any(item => item.m_globalID == itemId);
        return invItemList;
    }
    private uint GetItemSlot(ulong globalId, CoreObject coreObject)
    {
        if (!CoreObjectFactory.FindBehaviorInstance<ClientWizEquipmentBehavior>(coreObject,
                out var equipmentBehavior)) return 99;

        var itemSlot = equipmentBehavior.m_slotList.First(slot => slot.m_itemID == globalId).m_itemSlotNameID;
        Log.Logger.Debug($"ItemSlot: {itemSlot} of ID {globalId}");
        return itemSlot;
    }
    public CoreObject GetItemCoreObject(ulong globalId, CoreObject coreObject)
    {
        if (!CoreObjectFactory.FindBehaviorInstance<ClientWizEquipmentBehavior>(coreObject,
                out var inventoryBehavior)) return null;

        return inventoryBehavior.m_itemList.First(x => x.m_globalID == globalId);
    }
}