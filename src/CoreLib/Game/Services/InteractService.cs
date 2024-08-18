/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using System.Collections.Generic;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.IO;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Game.Zone;
using Imlight.CoreLib.Game.Zone.NPC;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Models.World;
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

        // A player is closing their shop
        if (message.ServiceName == "") {
            var clearWizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG() {
                GameObjectID = wizard.CharId,
                WizBangID = (uint) WizBangs.None
            };
            ZoneBroadcast(clearWizBangMsg, false);
            return;
        }

        // Search for the interaction object.
        var npc = GetZoneObject(message.GlobalID);
        if (npc == null) {
            Logger.Error("{0} searched for NPC by global ID {1} but one was not found",
                Logger.Args(wizard.CharId, message.GlobalID));
            return;
        }

        var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG() {
            GameObjectID = wizard.CharId,
            WizBangID = (uint) WizBangs.Registrar
        };
        ZoneBroadcast(wizBangMsg, false);

        // Check if the object is a vendor or teleport door.
        if (npc is WizardZoneVendor zoneVendor) {
            if (!zoneVendor.ServiceMomentoBase.m_serviceOptions.Any(x => x.m_serviceName == message.ServiceName)) {
                Logger.Error("{0} interacted with NPC by global ID {1} but the service {2} was not found",
                    Logger.Args(wizard.CharId, message.GlobalID, message.ServiceName));
                return;
            }

            zoneVendor.ActorRef.Tell(message, SessionActor.ActorRef);
            return;
        }

        if (npc is WizardZoneDyer zoneDyer) {
            if (!zoneDyer.ServiceMomentoBase.m_serviceOptions.Any(x => x.m_serviceName == message.ServiceName)) {
                Logger.Error("{0} interacted with NPC by global ID {1} but the service {2} was not found",
                    Logger.Args(wizard.CharId, message.GlobalID, message.ServiceName));
                return;
            }

            zoneDyer.ActorRef.Tell(message, SessionActor.ActorRef);
            return;
        }

        if (npc is WizardZoneTeleportDoor teleportDoor) {
            if (!teleportDoor.ServiceMomentoBase.m_serviceOptions.Any(x => x.m_serviceName == message.ServiceName)) {
                Logger.Error("{0} interacted with Teleport Door by global ID {1} but the service {2} was not found",
                    Logger.Args(wizard.CharId, message.GlobalID, message.ServiceName));
                return;
            }

            teleportDoor.ActorRef.Tell(message, SessionActor.ActorRef);
            return;
        }

        Logger.Error("{0} searched for NPC by global ID {1} but the object found was not a {2} or {3}",
            Logger.Args(wizard.CharId, message.GlobalID, nameof(WizardZoneNpc), nameof(WizardZoneTeleportDoor)));
        return;
    }
}
