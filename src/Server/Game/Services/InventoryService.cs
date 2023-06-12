using Akka.Actor;
using Imlight.Common.Utilities;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WizUnraveler.Cache;
using static WizUnraveler.ObjectProperty.ObjectSerializer;
using WizUnraveler.ObjectProperty;
using Imlight.Server.Database;
using static WizUnraveler.Cache.TypeCache;
using WizUnraveler;
using Akka.Util.Internal;

namespace Imlight.Server.Game.Services
{
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
            var serializer = new ObjectSerializer()
                .WithSerializerFlags(SerializerFlags.None)
                .WithPropertyFlags(PropertyFlags.Public | PropertyFlags.Transmit | PropertyFlags.AuthorityTransmit);
            var coreObject = GetActiveCoreObject();

            // Confirm to the player that we've equipped their item server side.
            // @TODO: There should be some "AntiAmbrose" logic here. Double check that the player meets the requirements
            // to equip this item.

            SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM()
            {
                ItemID = message.ItemID,
                SlotName = message.SlotName,
                IsEquip = message.IsEquip
            });

            // @todo: Remove this and gather from potential player behavior cache instead.
            if (!CoreObjectFactory.FindBehaviorInstance<ClientWizInventoryBehavior>(coreObject,
                    out var inventoryBehavior)) return;

            Log.Logger.Debug("BehaviorInstance is not null, yeeeeeeeeeeeeeee");
            var itemObj = inventoryBehavior.m_itemList.First(item => item.m_globalID == message.ItemID);

            if (itemObj == null)
            {
                Log.Logger.Error("Item cannot be changed when global_ID is not found!");
                return;
            }

            if (message.IsEquip == 1)
            {
                var templateId = itemObj.m_templateID;
                var template = CoreObjectFactory.GetTemplate<WizItemTemplate>(templateId);
                Log.Logger.Debug("Equippping item is not null, Name: " + template.m_objectName);

                var item = new WizardEquippedItemInfo()
                {
                    m_itemID = (uint)message.ItemID,
                    m_pattern = (FiveBitByte)template.m_numPatterns,
                    m_baseColor = (FiveBitByte)template.m_numPrimaryColors,
                    m_trimColor = (FiveBitByte)template.m_numSecondaryColors,
                };

                /*SendToSessionServices(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST()
                {
                    Selfless = false,
                    Sender = SessionActor.ActorRef,
                    Message = new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICEQUIPITEM()
                    {
                        GlobalID = coreObject.m_globalID,
                        SerializedInfo = serializer.Serialize(item)
                    }
                });*/
                SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICEQUIPITEM()
                {
                    GlobalID = coreObject.m_globalID,
                    SerializedInfo = serializer.Serialize(item)
                });

                /*
                    SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_EQUIPITEM()
                    {
                        GlobalID = coreObject.m_globalID,
                        IsValid = 1,
                        SerializedItem = serializer.Serialize(item),
                        SlotName = "2",
                    });
                 */

            }
            //@TODO: PUBLICEQUIPITEM & PUBLICUNEQUIPITEM (Behavior_XX ??)
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

        private TypeCache.CoreObject GetActiveCoreObject()
        {
            var msg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVECHARACTER();
            var response = AskSessionServices<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(msg);

            return response.CharacterObject;
        }
    }
}
