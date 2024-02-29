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
            GlobalID = wizard.GameObject.m_globalID,
            SerializedItem = data,
        };

        // Seems to do nothing; followed by MSG_ITEMACQUISITION in live servers, but where is TemplateID?
        SendToSocket(addItemMsg);
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_DONESHOPPING))]
    private void ReceiveDoneShopping(WIZARD_12_PROTOCOL.MSG_DONESHOPPING message) {
        var wizard = GetActiveWizard();

        var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG() {
            GameObjectID = wizard.GameObject.m_globalID,
            WizBangID = 0
        };
        ZoneBroadcast(wizBangMsg, false);
    }
}
