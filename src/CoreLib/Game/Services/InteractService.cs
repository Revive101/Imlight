/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using System.Collections.Generic;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Resources;
using Imlight.Common.ObjectProperty;
using Imlight.Common.ObjectProperty.PropertyReflection;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Services;
internal class InteractService : MessageService {

    public InteractService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new InteractService(parentActor));

    [MessageHandler(typeof(QUEST_MESSAGES_52_PROTOCOL.MSG_INTERACTNPC))]
    private void ReceiveNpcInteract(QUEST_MESSAGES_52_PROTOCOL.MSG_INTERACTNPC message) {
        var wizard = GetActiveWizard();

        var serializer = new ObjectSerializer()
          .OnBehaviors(SerializerOptions.Behaviors.None)
          .OnPropertyMask((SerializerOptions.PropertyFlags) 4);

        switch (message.ServiceName) {
            case "WizShoppingService":
                var shopItems = new List<GID> {
                    new GID(87226), new GID(87220), new GID(87232), new GID(87196), new GID(87203), new GID(87208), new GID(87214)
                };

                var shopOffering = new WizShopOffering() {
                    m_CSRTestShop = false,
                    m_activeHolidayList = null,
                    m_furnitureShop = 0,
                    m_recipeList = null,
                    m_sellModifier = 0.05f,
                    m_shopTitle = "KrocNPC_00000013",
                    m_shopType = 0,
                    m_shopList = shopItems
                };
                var data = serializer.Serialize(shopOffering);

                var shopListMsg = new WIZARD_12_PROTOCOL.MSG_SHOPLIST() {
                    GlobalID = message.GlobalID,
                    Data = data,
                    Credits = 0,
                    WebFailure = 0,
                };
                SendToSocket(shopListMsg);
                break;
            default:
                break;
        }
    }
}
