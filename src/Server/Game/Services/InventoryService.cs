using Akka.Actor;
using Imlight.Common.Utilities;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;
using System;
using System.Linq;
using static Imlight.Common.Serializable.ObjectSerializer;
using Akka.Util.Internal;
using Imlight.Common.Serializable;
using Imlight.Common.Serializable.Caches;
using Imlight.Common.Serializable.ObjectProperty;
using Imlight.Server.Shared.Resources;

namespace Imlight.Server.Game.Services;

public class InventoryService : MessageService
{
    public InventoryService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor)
    {
        return Akka.Actor.Props.Create(() => new InventoryService(parentActor));
    }

    [MessageHandler(typeof(GAME.MSG_REQUESTRADIALQUICKCHAT))]
    private void ReceiveRequestRadialQuickChat(GAME.MSG_REQUESTRADIALQUICKCHAT message)
    {
        new int[] { 2066, 860841451, 2537945, 203556948 }.ForEach(spellId =>
        {
            SendToSocket(new WIZARD.MSG_ADDSPELLTOBOOK()
            {
                SpellID = spellId
            });
        });
    }

    [MessageHandler(typeof(GAME.MSG_EQUIPITEM))]
    private void ReceiveEquipItem(GAME.MSG_EQUIPITEM message)
    {
        var serializer = new CoreObjectSerializer()
            .WithSerializerFlags(SerializerFlags.None)
            .WithPropertyFlags((PropertyFlags)1);
            
        var coreObject = GetActiveCoreObject();
        var playerCharacter = GetActiveCharacter();

        // Confirm to the player that we've equipped their item server side.
        // @TODO: There should be some "AntiAmbrose" logic here. Double check that the player meets the requirements
        // to equip this item.

        SendToSocket(new GAME.MSG_EQUIPITEM()
        {
            ItemID = message.ItemID,
            SlotName = message.SlotName,
            IsEquip = message.IsEquip
        });
        // @TODO: Remove this and gather from potential player behavior cache instead.
        if (!CoreObjectFactory.FindBehaviorInstance<TypeCache.ClientWizInventoryBehavior>(coreObject,
                out var inventoryBehavior)) return;

        var itemObj = inventoryBehavior.m_itemList.First(item => item.m_globalID == message.ItemID);
        if (message.IsEquip == 1)
        {
            Log.Information("Player equipped item {Item}", 
                Log.Args((byte)inventoryBehavior.m_itemList.IndexOf(itemObj)));
            var templateId = itemObj.m_templateID;
            var template = (TypeCache.WizItemTemplate)CoreObjectFactory.GetCoreTemplate(templateId);

            var item = new TypeCache.WizardEquippedItemInfo()
            {
                m_itemID = (uint)itemObj.m_templateID,
                m_pattern = (FiveBitByte)template.m_numPatterns,
                m_baseColor = (FiveBitByte)template.m_numPrimaryColors,
                m_trimColor = (FiveBitByte)template.m_numSecondaryColors,
            };

            var data = serializer.Serialize(item);
            var hex = Convert.ToHexString(data);
            Log.Information(hex);

            TellOtherServices(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST()
            {
                Selfless = false,
                Sender = SessionActor.ActorRef,
                Message = new GAME.MSG_EQUIPMENTBEHAVIOR_PUBLICEQUIPITEM()
                {
                    GlobalID = coreObject.m_globalID,
                    SerializedInfo = data
                }
            });
        }
        else
        {
            Log.Information("Player unequipped an item");
            for(int i = 0; i < 10; i++)
            {
                TellOtherServices(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST()
                {
                    Selfless = false,
                    Sender = SessionActor.ActorRef,
                    Message = new GAME.MSG_EQUIPMENTBEHAVIOR_PUBLICUNEQUIPITEM()
                    {
                        GlobalID = coreObject.m_globalID,
                        IndexToRemove = (byte)i
                    }
                });
            }
        }
    }

    #region Destroy/Feed Inventoryitem
    [MessageHandler(typeof(GAME.MSG_TRASHINVENTORYITEM))]
    private void ReceiveTrashInventoryItem(GAME.MSG_TRASHINVENTORYITEM message)
    {
        SendToSocket(new GAME.MSG_TRASHINVENTORYITEM()
        {
            GlobalID = message.GlobalID,
            TemplateID = message.TemplateID,
        });
    }

    [MessageHandler(typeof(GAME.MSG_FEEDINVENTORYITEM))]
    private void ReceiveFeedInventoryItem(GAME.MSG_FEEDINVENTORYITEM message)
    {
        SendToSocket(new GAME.MSG_FEEDINVENTORYITEM()
        {
            FedObjectID = message.FedObjectID,
            PetID = message.PetID,
        });
    }
    #endregion

    #region Quicksell from Inventory
    // QUICKSELL FROM INVENTORY
    [MessageHandler(typeof(WIZARD.MSG_REQUESTQUICKSELL))]
    private void ReceiveRequestQuickSell(WIZARD.MSG_REQUESTQUICKSELL message)
    {
        SendToSocket(new WIZARD.MSG_REQUESTQUICKSELL()
        {
            FromTemplateID = message.FromTemplateID,
            Section = message.Section,
            SellModifier = message.SellModifier,
        });
    }

    [MessageHandler(typeof(WIZARD2.MSG_QUICKSELLREQUEST))]
    private void ReceiveQuickSellRequest(WIZARD2.MSG_QUICKSELLREQUEST message)
    {
        // @TODO: Remove items from inventory & add gold to player
        SendToSocket(new WIZARD2.MSG_QUICKSELLREQUEST()
        {
            Data = message.Data,
        });
    }
    #endregion

    #region Jewels
    // JEWELS
    [MessageHandler(typeof(WIZARD2.MSG_EQUIPJEWELREQUEST))]
    private void ReceiveEquipJewelRequest(WIZARD2.MSG_EQUIPJEWELREQUEST message)
    {
        SendToSocket(new WIZARD2.MSG_EQUIPJEWELREQUEST()
        {
            ItemGID = message.ItemGID,
            JewelGID = message.JewelGID,
            SocketNumber = message.SocketNumber,
        });

        SendToSocket(new WIZARD2.MSG_EQUIPJEWELTOITEM()
        {
            ItemGID = message.ItemGID,
            JewelGID = message.JewelGID,
            SocketNumber = message.SocketNumber,
            GlobalID = RandomGen.GenerateGUID()
        });
    }
    #endregion
}