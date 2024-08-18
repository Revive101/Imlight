/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Shared.Packets;
using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone;

public class WizardZoneTeleportDoor : WizardZoneObject {
    private const string DOOR_NAME_KEY = "WizardGameObjects_00000070";
    private const string DOOR_TEXT_KEY = "GUI_ObjectInteract";

    public ServiceMementoBase ServiceMomentoBase { get; private set; }

    private readonly ObjectSerializer _serializer = new ObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask((SerializerOptions.PropertyFlags) 4);
    private byte[] _serializedServiceMomentoBase;

    // ctor
    public WizardZoneTeleportDoor(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef)
        : base(activeGameObject, template, wizardZoneRef) {
        SetServiceMomentoBase();
    }

    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef)
        => Akka.Actor.Props.Create(() => new WizardZoneTeleportDoor(activeGameObject, template, wizardZoneRef));

    protected override void OnPlayerProximityEnter(CoreObject player, IActorRef suspect) {
        var npcOptionsMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_SENDNPCOPTIONS {
            MobileID = ActiveGameObject.m_globalID,
            Options = _serializedServiceMomentoBase,
            Reinteract = 0
        };

        suspect.Tell(npcOptionsMsg);
    }

    protected override void OnPlayerProximityExit(CoreObject player, IActorRef suspect) {
        base.OnPlayerProximityExit(player, suspect);

        var leaveServiceRangeMsg = new GAME_5_PROTOCOL.MSG_LEAVESERVICERANGE {
            MobileID = ActiveGameObject.m_globalID
        };
        suspect.Tell(leaveServiceRangeMsg);
    }

    private void SetServiceMomentoBase() {
        ServiceMomentoBase = new ServiceMementoBase() {
            m_bTurnPlayerToFace = true, // in my capture this is true, which doesnt make sense. how can a door turn to face the player lmfao. bro on a turntable
            m_clickToInteractOnly = false,
            m_npcFarewellSound = "",
            m_npcGreetingSound = "",
            m_npcIcon = "",
            m_npcNameKey = DOOR_NAME_KEY,
            m_npcTextKey = DOOR_TEXT_KEY,
            m_personaMadlibs = null,
            m_serviceOptions = new List<ServiceOptionBase>()
        };

        SetWorldTeleporter();

        _serializedServiceMomentoBase = _serializer.Serialize(ServiceMomentoBase);
    }

    private void SetWorldTeleporter() {
        var universeService = new UniverseMapOption {
            m_displayKey = "GUI_UniverseMap",
            m_forceInteract = false,
            m_iconKey = "UniverseMap",
            m_serviceIndex = 0,
            m_serviceName = "UniverseMapService"
        };
        ServiceMomentoBase.m_serviceOptions.Add(universeService);
        ServiceMomentoBase.m_npcIcon = "GUI/Buttons/Button_Spiral.dds";
    }
}
