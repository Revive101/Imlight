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
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.Game.Zone.ServiceOptions;
using Imlight.CoreLib.Game.WizBang;
using Imlight.Common.Cryptography;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone;

/// <summary>
/// This is a zone NPC which manages itself as an actor.
/// </summary>
public sealed class WizardZoneNpc : WizardZoneObject {
    public bool IsSpellTrainer { get; set; }
    public ServiceMementoBase ServiceMomentoBase { get; private set; }
    public readonly HashSet<ServiceOption> ServiceOptions = new();

    private readonly ObjectSerializer _serializer = new ObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask((SerializerOptions.PropertyFlags) 4);
    private readonly string _npcNameKey = "NPCFormats_Name";
    private readonly bool _turnTowardsPlayer;
    private MadlibBlock _madlibBlock;

    // ctor
    public WizardZoneNpc(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef)
        : base(activeGameObject, template, wizardZoneRef) {
        if (Template is not GameObjectTemplate gameObjTemplate) {
            throw new System.Exception("Template is not GameObjectTemplate");
        }

        SetMadLibBlock();
        SetServiceMomentoBase();
    }

    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef)
        => Akka.Actor.Props.Create(() => new WizardZoneNpc(activeGameObject, template, wizardZoneRef));

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDSERVICEOPTION))]
    public bool AddServiceOption(ZONE_102_PROTOCOL.MSG_ADDSERVICEOPTION message) {
        var serviceOption = message.ServiceOption;

        if (serviceOption is null) {
            return false;
        }

        var addedToMomento = ServiceOptions.Add(serviceOption);
        if (!addedToMomento) {
            return false;
        }

        // Add to the service options in the momento base.
        foreach (var service in serviceOption.ServiceOptionBases) {
            ServiceMomentoBase.m_serviceOptions.Add(service);
        }

        return true;
    }

    public bool RemoveServiceOption(ServiceOption serviceOption) {
        if (serviceOption is null) {
            return false;
        }

        // Remove from the service options in the momento base.
        foreach (var service in serviceOption.ServiceOptionBases) {
            ServiceMomentoBase.m_serviceOptions.Remove(service);
        }

        return ServiceOptions.Remove(serviceOption);
    }

    protected override void OnPlayerJoin(CoreObject player, IActorRef suspect, Wizard wizard) {
        base.OnPlayerJoin(player, suspect, wizard);

        // If we have no service options, we have no WizBangs.
        if (ServiceOptions.Count == 0) {
            return;
        }

        // Get the WizBangs from the service options.
        var wizBangs = new List<string>();
        foreach (var serviceOption in ServiceOptions) {
            wizBangs.Add(serviceOption.WizBang);
        }

        // Deduce the highest priority WizBang from the list.
        var wizBang = WizBangPriority.GetHighestPriorityWizBang(wizBangs) ?? "None";
        var wizBangHash = StringHash.Compute(wizBang);

        // Send the WizBang message to the player.
        var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG {
            WizBangID = wizBangHash,
            GameObjectID = ActiveGameObject.m_globalID
        };
        suspect.Tell(wizBangMsg);

        Sender.Tell(new ZONE_102_PROTOCOL.MSG_ADDOBJECTRSP());
    }

    protected override void OnPlayerProximityEnter(CoreObject player, IActorRef suspect) {
        // If we have no service, we have no options.
        if (ServiceOptions.Count <= 0) {
            return;
        }

        // todo: clean this up a bit
        List<ServiceOptionBase> options = new List<ServiceOptionBase>();

        // Some options may need to be recalculated for each player when they enter the proximity.
        foreach (var serviceOption in ServiceOptions) {
            if (serviceOption.RecalculateOnProximityEnter) {
                var recalcOptions = serviceOption.Recalculate(suspect);
                options.AddRange(recalcOptions);
            } else {
                options.AddRange(serviceOption.ServiceOptionBases);
            }
        }

        var newMemento = ServiceMomentoBase;
        newMemento.m_serviceOptions = options;

        var data = _serializer.Serialize(newMemento);

        var npcOptionsMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_SENDNPCOPTIONS {
            MobileID = ActiveGameObject.m_globalID,
            Options = data,
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

    protected override void OnPlayerInteraction(QUEST_MESSAGES_52_PROTOCOL.MSG_INTERACTNPC message, IActorRef suspect) {
        var requestedService = message.ServiceName;

        // Find the service option that matches the requested service.
        var serviceOption = ServiceOptions.FirstOrDefault(x => x.ServiceName == requestedService);
        if (serviceOption is null) {
            return;
        }

        serviceOption.OnPlayerInteraction(suspect, (int) message.ServiceIndex);
    }

    private void SetMadLibBlock() {
        // NPCs normally have a madlib of first name, last name, and title.
        // To avoid hardcoding these values, we use the display name of the template.
        // We'll also set the madlib token to just "NAME" so the client displays the name as-is.

        if (Template is not GameObjectTemplate gameObjTemplate) {
            return;
        }

        var madlibList = new List<MadlibArg> {
            new MadlibArgT_std_string() {
                m_madlibArgument = gameObjTemplate.m_displayName,
                m_madlibToken = "NAME"
            },
        };

        _madlibBlock = new MadlibBlock() {
            m_blockToken = "NPC",
            m_madlibs = madlibList
        };
    }

    private void SetServiceMomentoBase() {
        var gameObjTemplate = Template as GameObjectTemplate;

        var npcIcon = gameObjTemplate.m_sIcon;
        var npcNameKey = _npcNameKey;
        var npcTextKey = "GUI_NPCInteractText";

        // If we have more than one service option, we need to check for the highest priority
        // overrides, if they exist.
        if (ServiceOptions.Count > 1) {
            // Check the WizBangs of each service option. If any of them are "None", we'll
            // use that as the highest priority.
            var wizBangs = new List<string>();
            foreach (var serviceOption in ServiceOptions) {
                wizBangs.Add(serviceOption.WizBang);
            }

            // Sort the service options by priority.
            var sortPriorityWizbangs = WizBangPriority.GetPrioritySortedWizBangs(wizBangs);
            var comparer = new ServiceOptionPriorityComparer(sortPriorityWizbangs);
            var sortedServiceOptions = ServiceOptions.OrderBy(x => x, comparer).ToList();

            // Pick the highest priority overrides.
            npcIcon = sortedServiceOptions.FirstOrDefault(x => x.NpcIconOverride != null)?.NpcIconOverride ?? npcIcon;
            npcNameKey = sortedServiceOptions.FirstOrDefault(x => x.NpcNameKeyOverride != null)?.NpcNameKeyOverride ?? npcNameKey;
            npcTextKey = sortedServiceOptions.FirstOrDefault(x => x.NpcTextKeyOverride != null)?.NpcTextKeyOverride ?? npcTextKey;
        }
        else {
            // If we only have one or less service options, we can just use the overrides from that option.
            var serviceOption = ServiceOptions.FirstOrDefault();
            if (serviceOption != null) {
                npcIcon = serviceOption.NpcIconOverride ?? npcIcon;
                npcNameKey = serviceOption.NpcNameKeyOverride ?? npcNameKey;
                npcTextKey = serviceOption.NpcTextKeyOverride ?? npcTextKey;
            }
        }

        ServiceMomentoBase = new ServiceMementoBase() {
            m_bTurnPlayerToFace = _turnTowardsPlayer,
            m_clickToInteractOnly = false,
            m_npcFarewellSound = "",
            m_npcGreetingSound = "",
            m_npcIcon = npcIcon,
            m_npcNameKey = npcNameKey,
            m_npcTextKey = npcTextKey,
            m_personaMadlibs = _madlibBlock,
            m_serviceOptions = new List<ServiceOptionBase>()
        };
    }
}
