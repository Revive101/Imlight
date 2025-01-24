/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.Cryptography;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class WorldTeleportDoorComponent(ZoneEntity entity) : BaseZoneComponent(entity), IServiceComponent, IComponentFactory {

    private const uint WORLD_DOOR_TEMPLATE_ID = 84113;

    public string ServiceName     => "UniverseMapService";
    public string NpcIcon         => "GUI/Buttons/Button_Spiral.dds";
    public string NpcNameKey      => "WizardGameObjects_00000070";
    public string NpcTextKey      => "GUI_ObjectInteract";
    public string WizBang         => null;
    public string StateName       => "UniverseTeleport";
    public string InteractWizBang => "Registrar";
    public string DisplayKey      => "GUI_UniverseMap";

    private readonly ObjectSerializer _serializer = new ObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask((SerializerOptions.PropertyFlags) 4);

    public static bool ShouldAttachToEntity(CoreTemplate template) 
        => template is GameObjectTemplate goTemplate 
        && goTemplate.m_templateID == WORLD_DOOR_TEMPLATE_ID;

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard _) 
        => [
            new UniverseMapOption() {
                m_displayKey = DisplayKey,
                m_iconKey = NpcIcon,
                m_serviceName = ServiceName,
            }
        ];

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        SendWorldTeleportOptions(playerActor);
        SendPlayerIntoWizbang(playerObject.m_globalID);
        SendPlayerIntoState(playerObject.m_globalID);
    }

    private void SendWorldTeleportOptions(IActorRef playerActor) {
        var teleportDoorOptions = new WorldTeleportOptions {
            m_worldList = [ // TODO: fetch available worlds for user to teleport to from db
                "WizardCity",
                "Krokotopia",
                "Marleybone",
                "MooShu",
                "Grizzleheim",
                "DragonSpire"
            ]
        };

        var teleportDoorOpen = new WIZARD_12_PROTOCOL.MSG_WORLDTELEPORTLIST {
            GlobalID = Entity.ActiveGameObject.m_globalID,
            Data = _serializer.Serialize(teleportDoorOptions)
        };
        playerActor.Tell(teleportDoorOpen);
    }

    private void SendPlayerIntoWizbang(ulong playerObjID) {
        // Create the wiz bang message, and wrap it in a broadcast message.
        var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG {
            WizBangID = StringHash.Compute(InteractWizBang),
            GameObjectID = playerObjID
        };
        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEPLAYERBROADCAST {
            Message = wizBangMsg,
            Selfless = false,
        };

        Entity.ZoneRef.Tell(broadcastMsg);
    }

    private void SendPlayerIntoState(ulong playerObjID) {
        // Create the change state message, and wrap it in a broadcast message.
        var changeStateMsg = new GAME_5_PROTOCOL.MSG_ENTERSTATE {
            State = StringHash.Compute(StateName),
            GameObjectID = playerObjID
        };
        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEPLAYERBROADCAST {
            Message = changeStateMsg,
            Selfless = false,
        };

        Entity.ZoneRef.Tell(broadcastMsg);
    }

}