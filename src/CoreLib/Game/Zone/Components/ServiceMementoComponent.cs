/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.Cryptography;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class ServiceMementoComponent(ZoneEntity entity) : BaseZoneComponent(entity), IComponentFactory {

    private const string DEFAULT_NAME_KEY = "NPCFormats_Name";
    private const string DEFAULT_TEXT_KEY = "GUI_NPCInteractText";

    private readonly float _interactionRadius = 300.0f;
    private readonly Dictionary<CoreObject, IActorRef> _playersInRange = [];
    private readonly ObjectSerializer _serializer = new ObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask((SerializerOptions.PropertyFlags) 4);
    private List<IServiceComponent> _serviceComponents = [];
    private ServiceMementoBase _serviceMemento;
    private MadlibBlock _madlibBlock;

    public static bool ShouldAttachToEntity(CoreTemplate template) 
        => true;

    public override void OnZoneStart() 
        => RefreshServiceMomento(null);

    public override void OnPlayerJoin(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) 
        => SendWizBang(playerActor);

    public override void OnPlayerLeave(IActorRef playerActor, ulong id) {
        if (_playersInRange.Any(x => x.Value == playerActor)) {
            var playerObj = _playersInRange.First(x => x.Value == playerActor).Key;
            _playersInRange.Remove(playerObj);
        }
    }

    public override void OnPlayerMove(CoreObject playerObj, IActorRef playerActor) {
        if (_serviceComponents.Count <= 0) {
            return;
        }

        if (IsInRadius(playerObj, _interactionRadius) && !_playersInRange.ContainsKey(playerObj)) {
            _playersInRange.Add(playerObj, playerActor);
            SendActorServiceOptions(playerActor);
        }
        else if (!IsInRadius(playerObj, _interactionRadius) && _playersInRange.ContainsKey(playerObj)) {
            _playersInRange.Remove(playerObj);
            SendLeaveServiceRange(playerActor);
        }
    }

    public void PlayerInteraction(
        IActorRef playerActor,
        Wizard playerCharacter,
        CoreObject playerObject,
        string serviceName,
        uint serviceIndex) {
        Logger.Debug("Player {0} interacted with NPC {1} using service {2} at index {3}",
            Logger.Args(playerActor.Path.Name, Entity.ActiveGameObject.m_globalID, serviceName, serviceIndex));

        if (_serviceComponents.Count <= 0) {
            Logger.Warning("No service components found for NPC {0}", Logger.Args(Entity.ActiveGameObject.m_debugName));

            return;
        }

        // Find the service component that corresponds to the service name.
        // If the service component is not found, log a warning and return.
        var serviceComponent = _serviceComponents.FirstOrDefault(c => c.ServiceName == serviceName);
        if (serviceComponent == null) {
            Logger.Warning("Service component not found for NPC {0} with service name {1}",
                Logger.Args(Entity.ActiveGameObject.m_debugName, serviceName));

            return;
        }

        // Call the service component's interaction method.
        serviceComponent.OnServiceInteraction(playerActor, playerCharacter, playerObject, serviceIndex);
    }

    private void SendActorServiceOptions(IActorRef playerActor) {
        // Get interacting wizard
        var queryCharacterMsg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD();
        var wizard = playerActor
            .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(queryCharacterMsg)
            .Result
            .Wizard;
        RefreshServiceMomento(wizard);

        var data = _serializer.Serialize(_serviceMemento);
        var npcOptionsMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_SENDNPCOPTIONS {
            // Do not let this property name fool you. 
            // The client incorrectly labels this property as "MobileID." It is in fact
            // the global ID of the NPC. It will not work if you set it to the mobile ID.
            MobileID = Entity.ActiveGameObject.m_globalID,
            Options = data,
            Reinteract = 0
        };

        playerActor.Tell(npcOptionsMsg);
    }

    private void SendLeaveServiceRange(IActorRef playerActor) {
        var msg = new GAME_5_PROTOCOL.MSG_LEAVESERVICERANGE {
            // Do not let this property name fool you. 
            // The client incorrectly labels this property as "MobileID." It is in fact
            // the global ID of the NPC. It will not work if you set it to the mobile ID.
            MobileID = Entity.ActiveGameObject.m_globalID
        };

        playerActor.Tell(msg);
    }

    private void RefreshServiceMomento(Wizard playerCharacter) {
        _serviceComponents = [.. Entity.GetComponentsOfType<IServiceComponent>()];
        if (_serviceComponents.Count <= 0) {
            return;
        }

        var gameObjTemplate = Entity.Template as GameObjectTemplate;

        // Get all service options.
        var allOptions = _serviceComponents.SelectMany(c => c.GetServiceOptions(playerCharacter)).ToList();

        // Get UI overrides based on priority.
        var sortedComponents = SortComponentsByPriority(_serviceComponents);
        var highestPriority = sortedComponents.FirstOrDefault();

        SetMadLibBlock();

        _serviceMemento = new ServiceMementoBase {
            m_npcIcon = highestPriority?.NpcIcon ?? gameObjTemplate.m_sIcon,
            m_npcNameKey = highestPriority?.NpcNameKey ?? DEFAULT_NAME_KEY,
            m_npcTextKey = highestPriority?.NpcTextKey ?? DEFAULT_TEXT_KEY,
            m_serviceOptions = allOptions,
            m_personaMadlibs = _madlibBlock
        };
    }

    private void SetMadLibBlock() {
        // NPCs normally have a madlib of first name, last name, and title.
        // To avoid hardcoding these values, we use the display name of the template.
        // We'll also set the madlib token to just "NAME" so the client displays the name as-is.
        if (Entity.Template is not GameObjectTemplate gameObjTemplate) {
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

    private void SendWizBang(IActorRef playerActor) {
        if (_serviceComponents.Count <= 0) {
            return;
        }

        // There's a number of network speed factors that can cause this message
        // to be received before the player's game client has fully loaded.
        // This delay is to ensure that the player's game client has fully loaded.
        System.Threading.Tasks.Task.Delay(800).Wait();

        // Out of the service options, deduce which WizBang is the highest priority.
        // Thankfully, game client data has a priority list for WizBangs.
        var wizBangs = _serviceComponents.Select(c => c.WizBang).Where(w => w != null);
        var prioritySortedWizBangs = WizBangPriority.GetPrioritySortedWizBangs(wizBangs.ToList());
        var highestPriority = SortComponentsByPriority(_serviceComponents).FirstOrDefault();

        // Send the WizBang to the player.
        var wizBang = highestPriority?.WizBang ?? "None";
        var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG {
            WizBangID = StringHash.Compute(wizBang),
            GameObjectID = Entity.ActiveGameObject.m_globalID
        };

        playerActor.Tell(wizBangMsg);
    }

    private static IOrderedEnumerable<IServiceComponent> SortComponentsByPriority(IEnumerable<IServiceComponent> components) {
        // Sort by WizBang priority
        var wizBangs = components.Select(c => c.WizBang).Where(w => w != null);
        var prioritySortedWizBangs = WizBangPriority.GetPrioritySortedWizBangs(wizBangs.ToList());

        return components.OrderBy(c => prioritySortedWizBangs.IndexOf(c.WizBang ?? "None"));
    }

}