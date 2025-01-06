/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */


using System.Linq;
using System.Collections.Generic;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Game.Reagents;
using Imlight.CoreLib.Game.Zone.ServiceOptions;
using static Imlight.Common.Caches.TypeCache;
using Imlight.CoreLib.Game.WizBang;

namespace Imlight.CoreLib.Game.Zone;
internal class WizardZoneReagent : WizardZoneCreature {
    public ServiceMementoBase ServiceMomentoBase { get; private set; }
    public readonly HashSet<ServiceOption> ServiceOptions = new();

    private readonly ObjectSerializer _serializer = new ObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask((SerializerOptions.PropertyFlags) 4);
    private readonly ulong _reagentItemTemplateID;

    // ctor
    public WizardZoneReagent(CoreObject activeGameObject,
                              CoreTemplate template,
                              WizardZonePath path,
                              byte startingNodeIndex,
                              IActorRef wizardZoneRef)
            : base(activeGameObject, template, path, startingNodeIndex, wizardZoneRef) {
        var gObjTemplate = template as GameObjectTemplate;
        var reagentObject = ReagentFactory.GetReagent(gObjTemplate.m_objectName.ToString());

        if (reagentObject is null) {
            return;
        }

        _reagentItemTemplateID = reagentObject.m_templateID;

        SetServiceMomentoBase(); // i guess ?
    }

    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject,
                              CoreTemplate template,
                              WizardZonePath path,
                              byte startingNodeIndex,
                              IActorRef wizardZoneRef) => Akka.Actor.Props.Create(()
            => new WizardZoneReagent(activeGameObject, template, path, startingNodeIndex, wizardZoneRef));

    protected override void OnPlayerProximityEnter(CoreObject suspectObject, IActorRef suspectActor) {
        // If we have no service, we have no options.
        if (ServiceOptions.Count <= 0) {
            return;
        }

        // todo: clean this up a bit
        List<ServiceOptionBase> options = new List<ServiceOptionBase>();

        var newMemento = ServiceMomentoBase;
        newMemento.m_serviceOptions = options;

        var data = _serializer.Serialize(newMemento);

        var npcOptionsMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_SENDNPCOPTIONS {
            MobileID = ActiveGameObject.m_globalID,
            Options = data,
            Reinteract = 0
        };

        suspectActor.Tell(npcOptionsMsg);
    }

    protected override void OnPlayerProximityExit(CoreObject player, IActorRef suspect) {
        base.OnPlayerProximityExit(player, suspect);

        var leaveServiceRangeMsg = new GAME_5_PROTOCOL.MSG_LEAVESERVICERANGE {
            MobileID = ActiveGameObject.m_globalID
        };
        suspect.Tell(leaveServiceRangeMsg);
    }

    protected override void OnPlayerInteraction(QUEST_MESSAGES_52_PROTOCOL.MSG_INTERACTNPC message, IActorRef suspect) {
        var requestedService = message.ServiceName;

        // Find the service option that matches the requested service.
        var serviceOption = ServiceOptions.FirstOrDefault(x => x.ServiceName == requestedService);
        if (serviceOption is null) {
            return;
        }

        serviceOption.OnPlayerInteraction(suspect, (int) message.ServiceIndex);
    }

    private void SetServiceMomentoBase() {
        var gameObjTemplate = Template as GameObjectTemplate;

        var npcIcon = gameObjTemplate.m_sIcon;
        var npcTextKey = "GUI_NPCInteractText";

        ServiceMomentoBase = new ServiceMementoBase() {
            m_bTurnPlayerToFace = false,
            m_clickToInteractOnly = false,
            m_npcFarewellSound = "",
            m_npcGreetingSound = "",
            m_npcIcon = npcIcon,
            m_npcNameKey = "",
            m_npcTextKey = npcTextKey,
            m_personaMadlibs = null,
            m_serviceOptions = new List<ServiceOptionBase>()
        };
    }
}
