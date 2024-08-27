/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.IO;
using Imlight.Common.ObjectProperty;
using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.ServiceOptions;

public class ServiceOptionWorldDoor : ServiceOption {
    public override string ServiceName { get; protected set; } = "UniverseMapService";
    public override string WizBang { get; set; } = "None";
    public override string NpcIconOverride { get; protected set; } = "GUI/Buttons/Button_Spiral.dds";
    public override string NpcNameKeyOverride { get; protected set; } = "WizardGameObjects_00000070";
    public override string NpcTextKeyOverride { get; protected set; } = "GUI_ObjectInteract";
    public override List<ServiceOptionBase> ServiceOptionBases { get; set; } = new() {
        new UniverseMapOption {
            m_displayKey = "GUI_UniverseMap",
            m_forceInteract = false,
            m_iconKey = "UniverseMap",
            m_serviceIndex = 0,
            m_serviceName = "UniverseMapService"
        }
    };

    private readonly ObjectSerializer _serializer = new ObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask((SerializerOptions.PropertyFlags) 4);

    public ServiceOptionWorldDoor(CoreObject ActiveGameObject) : base(ActiveGameObject)
        => RecalculateOnProximityEnter = false;

    public override void OnPlayerInteraction(IActorRef suspect, int serviceIndex) {
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
            GlobalID = ActiveGameObject.m_globalID,
            Data = _serializer.Serialize(teleportDoorOptions)
        };
        suspect.Tell(teleportDoorOpen);
    }

    public override List<ServiceOptionBase> Recalculate(IActorRef suspect)
        => ServiceOptionBases;
}
