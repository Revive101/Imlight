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

/// <summary>
/// This is a zone ObjectState which manages itself as an actor.
/// An ObjectState are typically objects that the wizard can interact with, ie the universe teleport door.
/// This acts similar to an NPC.
/// </summary>
public class WizardZoneStatefulObject : WizardZoneObject {
    private static readonly string UniverseTeleportName = "UniverseTeleport";
    private static readonly uint UniverseTeleportId = 84113;

    public bool IsWorldTeleporter { get; set; }
    public ServiceMementoBase ServiceMomentoBase { get; private set; }

    private readonly ObjectSerializer _serializer = new ObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask((SerializerOptions.PropertyFlags) 4);
    private readonly string _npcNameKey = "WizardGameObjects_00000070";
    private readonly string _npcTextKey = "GUI_ObjectInteract";

    // ctor
    public WizardZoneStatefulObject(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef)
        : base(activeGameObject, template, wizardZoneRef) {
        if (Template is not GameObjectTemplate gameObjTemplate) {
            return;
        }

        SetServiceMomentoBase();

        // Check to see if we're a teleport door
        var objectName = gameObjTemplate.m_objectName.ToString();
        var objectId = gameObjTemplate.m_templateID;
        if (objectName == UniverseTeleportName || objectId == UniverseTeleportId) {
            SetWorldTeleporter();
        }
    }

    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef)
        => Akka.Actor.Props.Create(() => new WizardZoneStatefulObject(activeGameObject, template, wizardZoneRef));

    protected override void OnPlayerJoin(CoreObject player, IActorRef suspect) {
        base.OnPlayerJoin(player, suspect);

        Sender.Tell(new ZONE_102_PROTOCOL.MSG_ADDOBJECTRSP());
    }

    protected override void OnPlayerInteractionEnter(CoreObject player, IActorRef suspect) {
        if (IsWorldTeleporter) {
            var data = _serializer.Serialize(ServiceMomentoBase);

            var npcOptionsMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_SENDNPCOPTIONS {
                MobileID = ActiveGameObject.m_globalID,
                Options = data,
                Reinteract = 0
            };

            suspect.Tell(npcOptionsMsg);
        }
    }

    protected override void OnPlayerInteractionExit(CoreObject player, IActorRef suspect) {
        base.OnPlayerInteractionExit(player, suspect);

        if (Template is not GameObjectTemplate gameObjTemplate) {
            return;
        }

        var leaveServiceRangeMsg = new GAME_5_PROTOCOL.MSG_LEAVESERVICERANGE {
            MobileID = ActiveGameObject.m_globalID
        };
        suspect.Tell(leaveServiceRangeMsg);
    }


    private void SetServiceMomentoBase() {
        var gameObjTemplate = Template as GameObjectTemplate;
        ServiceMomentoBase = new ServiceMementoBase() {
            m_bTurnPlayerToFace = true, // in my capture this is true, which doesnt make sense. how can a door turn to face the player lmfao. bro on a turntable
            m_clickToInteractOnly = false,
            m_npcFarewellSound = "",
            m_npcGreetingSound = "",
            m_npcIcon = "",
            m_npcNameKey = _npcNameKey,
            m_npcTextKey = _npcTextKey,
            m_personaMadlibs = null,
            m_serviceOptions = new List<ServiceOptionBase>()
        };
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
        IsWorldTeleporter = true;
    }
}
