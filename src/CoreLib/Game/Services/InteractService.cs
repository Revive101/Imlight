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
using Imlight.Common.Cryptography;
using Imlight.Common.ObjectProperty;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Shared.Networking;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Services;
internal class InteractService : MessageService {
    private readonly ObjectSerializer _serializer = new ObjectSerializer()
          .OnBehaviors(SerializerOptions.Behaviors.None)
          .OnPropertyMask((SerializerOptions.PropertyFlags) 4);

    public InteractService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new InteractService(parentActor));

    [MessageHandler(typeof(QUEST_MESSAGES_52_PROTOCOL.MSG_INTERACTNPC))]
    private void ReceiveNpcInteract(QUEST_MESSAGES_52_PROTOCOL.MSG_INTERACTNPC message) {
        var wizard = GetActiveWizard();

        // Todo: Search the WizardZone to find the interactable. That object should return the code below.

        switch (message.ServiceName) {
            case "WizShoppingService":
                var shopItems = new List<GID> {
                    new GID(87226), new GID(87220), new GID(87232), new GID(87196), new GID(87203), new GID(87208), new GID(87214),
                    new GID(87237), new GID(87890), new GID(87885), new GID(87886), new GID(87887), new GID(87888), new GID(87891)
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
                var data = _serializer.Serialize(shopOffering);

                var shopListMsg = new WIZARD_12_PROTOCOL.MSG_SHOPLIST() {
                    GlobalID = message.GlobalID,
                    Data = data,
                    Credits = 0,
                    WebFailure = 0,
                };
                SendToSocket(shopListMsg);

                var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG() {
                    GameObjectID = wizard.CharId,
                    WizBangID = StringHash.Compute("Registrar")
                };
                ZoneBroadcast(wizBangMsg, false);
                break;
            default:
                break;
        }
    }
}
