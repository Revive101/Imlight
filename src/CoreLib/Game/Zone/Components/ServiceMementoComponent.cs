/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.Cryptography;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Game.Zone.ServiceOptions;
using Imlight.CoreLib.WizardData.Models.Player;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class ServiceMementoComponent : BaseZoneComponent, IComponentFactory {

    private readonly float _interactionRadius = 300.0f;
    private readonly List<IServiceComponent> _serviceComponents = [];
    private readonly Dictionary<CoreObject, IActorRef> _playersInRange = [];
    private readonly ObjectSerializer _serializer = new ObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask((SerializerOptions.PropertyFlags) 4);
    private ServiceMementoBase _serviceMemento;
    private bool _searchedForServiceComponents = false;

    // ctor
    internal ServiceMementoComponent(ZoneEntity entity) : base(entity) {
        if (entity.Template is not GameObjectTemplate) {
            throw new System.Exception("ServiceMomentComponent can only be attached to GameObjects");
        }
    }

    public static bool ShouldAttachToEntity(CoreTemplate template) =>
        template is GameObjectTemplate gameObjTemplate &&
        gameObjTemplate.m_behaviors.Any(x => x is NPCBehaviorTemplate);

    public override void OnPlayerJoin(CoreObject playerObject, IActorRef playerActor, Wizard playerWizard) {
        // It's difficult to do this in the constructor because not all service options are available at that time.
        if (!_searchedForServiceComponents) {
            // Find all service components on this entity.
            var serviceComponents = Entity
                .GetComponentsOfType<IServiceComponent>()
                .Cast<IServiceComponent>();
            _serviceComponents.AddRange(serviceComponents);

            RefreshServiceMomento();
            _searchedForServiceComponents = true;
        }

        // Deduce what WizBang to use based on the highest priority service component.
        if (_serviceComponents.Count <= 0) {
            return;
        }

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

    public override void OnPlayerLeave(IActorRef playerActor, ulong id) {
        if (_playersInRange.Any(x => x.Value == playerActor)) {
            var playerObj = _playersInRange.First(x => x.Value == playerActor).Key;
            _playersInRange.Remove(playerObj);
        }
    }

    public override void OnPlayerMove(CoreObject playerObj, IActorRef playerActor) {
        if (IsInRadius(playerObj, _interactionRadius) && !_playersInRange.ContainsKey(playerObj)) {
            _playersInRange.Add(playerObj, playerActor);
            SendActorServiceOptions(playerActor);
        }
        else if (!IsInRadius(playerObj, _interactionRadius) && _playersInRange.ContainsKey(playerObj)) {
            _playersInRange.Remove(playerObj);
        }
    }

    private void SendActorServiceOptions(IActorRef playerActor) {
        var data = _serializer.Serialize(_serviceMemento);
        var npcOptionsMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_SENDNPCOPTIONS {
            MobileID = Entity.ActiveGameObject.m_globalID,
            Options = data,
            Reinteract = 0
        };

        playerActor.Tell(npcOptionsMsg);
    }

    private void RefreshServiceMomento() {
        var gameObjTemplate = Entity.Template as GameObjectTemplate;

        // Get all service options.
        var allOptions = _serviceComponents.SelectMany(c => c.GetServiceOptions()).ToList();

        // Get UI overrides based on priority.
        var sortedComponents = SortComponentsByPriority(_serviceComponents);
        var highestPriority = sortedComponents.FirstOrDefault();

        _serviceMemento = new ServiceMementoBase {
            m_bTurnPlayerToFace = false,
            m_clickToInteractOnly = false,
            m_npcFarewellSound = "",
            m_npcGreetingSound = "",
            m_npcIcon = highestPriority?.NpcIcon ?? gameObjTemplate.m_sIcon,
            m_npcNameKey = highestPriority?.NpcNameKey ?? "NPCFormats_Name",
            m_npcTextKey = highestPriority?.NpcTextKey ?? "GUI_NPCInteractText",
            m_serviceOptions = allOptions
        };
    }

    private static IOrderedEnumerable<IServiceComponent> SortComponentsByPriority(IEnumerable<IServiceComponent> components) {
        // Sort by WizBang priority
        var wizBangs = components.Select(c => c.WizBang).Where(w => w != null);
        var prioritySortedWizBangs = WizBangPriority.GetPrioritySortedWizBangs(wizBangs.ToList());

        return components.OrderBy(c => prioritySortedWizBangs.IndexOf(c.WizBang ?? "None"));
    }

}