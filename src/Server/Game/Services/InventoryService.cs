using Akka.Actor;
using Imlight.Common.Utilities;
using Imlight.Server.Shared.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WizUnraveler.Cache;

namespace Imlight.Server.Game.Services
{
    public class InventoryService : MessageService
    {
        public InventoryService(SessionActor sessionActor) : base(sessionActor) { }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new InventoryService(parentActor));
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_EQUIPITEM))]
        private void ReceiveEquipItem(GAME_5_PROTOCOL.MSG_EQUIPITEM message)
        {
            SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM()
            {
                ItemID = message.ItemID,
                SlotName = message.SlotName,
                IsEquip = message.IsEquip
            });
        }





    }
}
