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
using Imlight.CoreLib.Game.Zone;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Models.Player;
using static Imlight.Common.Caches.TypeCache;
using Imlight.CoreLib.WizardData.Models.World;
using Imlight.Common.IO;

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

        // A player is closing their shop
        if (message.ServiceName == "") {
            return;
        }

        // Search for the interaction object.
        var npc = GetZoneObject(message.GlobalID);
        if (npc == null) {
            Logger.Error("{0} searched for NPC by global ID {1} but one was not found",
                Logger.Args(wizard.CharId, message.GlobalID));
            return;
        }

        // Check if the object is a shopkeeper or teleport door.
        if (npc is WizardZoneNpc zoneNpc) {
            if (!zoneNpc.ServiceMomentoBase.m_serviceOptions.Any(x => x.m_serviceName == message.ServiceName)) {
                Logger.Error("{0} interacted with NPC by global ID {1} but the service {2} was not found",
                    Logger.Args(wizard.CharId, message.GlobalID, message.ServiceName));
                return;
            }

            // Check to see if this NPC is capable of providing the service.


            switch (message.ServiceName) {
                case "WizShoppingService":
                    InteractShopkeeper(message, wizard, zoneNpc);
                    break;
                case "DyeShopService":
                    InteractDyeShop(message, wizard, zoneNpc);
                    break;
                default:
                    break;
            }
        }
        else if (npc is WizardZoneTeleportDoor zoneObjState) {
            if (zoneObjState.IsWorldTeleporter) {
                InteractTeleportDoor(message, wizard, zoneObjState);
            }
        }
        else {
            Logger.Error("{0} searched for NPC by global ID {1} but the object found was not a {2} or {3}",
                Logger.Args(wizard.CharId, message.GlobalID, nameof(WizardZoneNpc), nameof(WizardZoneTeleportDoor)));
            return;
        }
    }

    private void InteractShopkeeper(QUEST_MESSAGES_52_PROTOCOL.MSG_INTERACTNPC message, Wizard wizard, WizardZoneNpc zoneNpc) {
        if (!zoneNpc.IsShopkeeper) {
            Logger.Error("{0} interacted with NPC by global ID {1} but the object found was not a shopkeeper",
                Logger.Args(wizard.CharId, message.GlobalID));
            return;
        }

        var shopOffering = new WizShopOffering() {
            m_CSRTestShop = false,
            m_activeHolidayList = null,
            m_furnitureShop = 0,
            m_recipeList = null,
            m_sellModifier = 0.05f,
            m_shopTitle = "KrocNPC_00000013",
            m_shopList = zoneNpc.Inventory,

            // Changes the type of currency that is used
            // 0 - Gold
            // 1 - PvP tickets
            m_shopType = 0,
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
            WizBangID = (uint) WizBangs.Registrar
        };
        ZoneBroadcast(wizBangMsg, false);
    }

    private void InteractDyeShop(QUEST_MESSAGES_52_PROTOCOL.MSG_INTERACTNPC message, Wizard wizard, WizardZoneNpc zoneNpc) {
        if (!zoneNpc.IsShopkeeper) {
            Logger.Error("{0} interacted with NPC by global ID {1} but the object found was not a shopkeeper",
                Logger.Args(wizard.CharId, message.GlobalID));
            return;
        }

        var dyeShopOpen = new WIZARD_12_PROTOCOL.MSG_DYESHOPOPEN() {
            GlobalID = message.GlobalID,
            Title = "WC-NPCs_00000718"
        };
        SendToSocket(dyeShopOpen);

        var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG() {
            GameObjectID = wizard.CharId,
            WizBangID = (uint) WizBangs.Registrar
        };
        ZoneBroadcast(wizBangMsg, false);
    }

    private void InteractTeleportDoor(QUEST_MESSAGES_52_PROTOCOL.MSG_INTERACTNPC message, Wizard wizard, WizardZoneTeleportDoor zoneNpc) {
        if (!zoneNpc.IsWorldTeleporter) {
            Logger.Error("{0} interacted with NPC by global ID {1} but the object found was not a teleport door",
                Logger.Args(wizard.CharId, message.GlobalID));
            return;
        }

        var teleportDoorOptions = new WorldTeleportOptions {
            m_worldList = new List<ByteString> { // TODO: fetch available worlds for user to teleport to from db
                "WizardCity",
                "Krokotopia",
                "Marleybone",
                "MooShu",
                "Grizzleheim",
                "DragonSpire"
            }
        };

        var teleportDoorOpen = new WIZARD_12_PROTOCOL.MSG_WORLDTELEPORTLIST {
            GlobalID = message.GlobalID,
            Data = _serializer.Serialize(teleportDoorOptions)
        };
        SendToSocket(teleportDoorOpen);

        var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG() {
            GameObjectID = wizard.CharId,
            WizBangID = (uint) WizBangs.Registrar
        };

        ZoneBroadcast(wizBangMsg, false);
    }
}
