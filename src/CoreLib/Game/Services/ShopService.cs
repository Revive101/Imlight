/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Imlight.Common.Caches;
using Imlight.Common.Utilities;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Resources;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Services;
internal class ShopService : MessageService {
    private readonly CoreObjectSerializer _itemSerializer = new CoreObjectSerializer()
                    .OnBehaviors(SerializerOptions.Behaviors.None)
                    .OnPropertyMask((SerializerOptions.PropertyFlags) 1);

    public ShopService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new InteractService(parentActor));

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_SHOPBUYREQUEST))]
    private void ReceiveShopBuyRequest(WIZARD_12_PROTOCOL.MSG_SHOPBUYREQUEST message) {
        var wizard = GetActiveWizard();

        var primaryDye = message.texture;
        var secondaryDye = message.decal;

        var gid = RandomGen.GenerateGUID();
        var inactiveBehaviors = new List<BehaviorInstance>() { null };
        var item = new WizClientObjectItem() {
            m_templateID = message.ShopID,
            m_globalID = gid,
            m_permID = gid,
            m_inactiveBehaviors = inactiveBehaviors,
            m_fScale = 1
        };
        var data = _itemSerializer.Serialize(item);

        var addItemMsg = new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM {
            GlobalID = wizard.CharId,
            SerializedItem = data,
        };
        SendToSocket(addItemMsg);

        var itemAcqMsg = new WIZARD2_53_PROTOCOL.MSG_ITEMACQUISITION {
            ItemGlobalID = gid,
            ItemTemplateID = 87777, // Has no effect ??
            ItemLocation = 1,
        };
        SendToSocket(itemAcqMsg);

        var shopConfirmMsg = new WIZARD_12_PROTOCOL.MSG_SHOPBUYCONFIRM {
            Failure = 0,
            WebFailure = 0,
            Credits = 0
        };
        SendToSocket(shopConfirmMsg);
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_SHOPSELLREQUEST))]
    private void ReceiveShopSellRequest(WIZARD_12_PROTOCOL.MSG_SHOPSELLREQUEST message) {
        var wizard = GetActiveWizard();

        var removedItemSuccess = wizard.InventoryBehavior.RemoveItem(message.GlobalID, out var item);
        if (!removedItemSuccess) {
            return;
        }

        var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(item.m_templateID);
        var gold = (int) (template.m_baseCost * 0.05f);

        wizard.AddGold(gold);

        var updateGoldMsg = new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD {
            Gold = wizard.GameStats.m_currentGold,
            MaxGold = wizard.GameStats.m_baseGoldPouch
        };
        SendToSocket(updateGoldMsg);

        var removeItemMsg = new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_REMOVEITEM {
            GlobalID = wizard.CharId,
            ItemID = message.GlobalID
        };
        SendToSocket(removeItemMsg);
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_DONESHOPPING))]
    private void ReceiveDoneShopping(WIZARD_12_PROTOCOL.MSG_DONESHOPPING message) {
        var wizard = GetActiveWizard();

        var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG() {
            GameObjectID = wizard.CharId,
            WizBangID = 0
        };
        ZoneBroadcast(wizBangMsg, false);
    }
}
