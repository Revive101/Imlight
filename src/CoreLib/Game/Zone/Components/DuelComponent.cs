/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.CoreLib.Game.Sigils;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.ServerTypeCache;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class DuelComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IComponentFactory {

    private readonly Dictionary<CoreObject, IActorRef> _entitiesInRange = [];

    private CombatSigilObjectInfo _combatSigilObjectInfo;
    private RenderComponent _renderComponent;
    private CombatSigilTemplate _sigilTemplate;
    private bool _isActive;

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate gameObjectTemplate
        && gameObjectTemplate.m_behaviors.Any(x => x is not null && x.m_behaviorName == "DuelBehavior");

    public override void OnStart() {
        // Disable the RenderComponent. We'll activate it when the sigil is activated.
        _renderComponent = Entity.GetComponentOfType<RenderComponent>();
        _renderComponent?.Disable();

        // Get the sigil template.
        _sigilTemplate = (CombatSigilTemplate) SigilFactory.GetSigilTemplate(_combatSigilObjectInfo.m_sigilType);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_SIGILDETAILS))]
    private void ReceiveSigilDetails(ZONE_102_PROTOCOL.MSG_SIGILDETAILS message)
        => _combatSigilObjectInfo = message.CombatSigilObjectInfo;

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL))]
    private void ReceiveRequestCombatSigil(ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL message) {
        if (_isActive) {
            Logger.Warning("Received start request for already active sigil {0}",
                Logger.Args(Entity.ActiveGameObject.m_globalID));

            return;
        }

        // Activate the sigil.
        _renderComponent.Enable();
        //InitializeDuel(message.StartingParticipants);
        // todo
    }

    [MessageHandler(typeof(WIZARDCOMBAT_51_PROTOCOL.MSG_ENDDUEL))]
    private void ReceiveDuelEnd(WIZARDCOMBAT_51_PROTOCOL.MSG_ENDDUEL message) {
        _isActive = false;
        _renderComponent?.Disable();
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVECOMBATSIGIL))]
    private void ReceiveRemoveSigil(ZONE_102_PROTOCOL.MSG_REMOVECOMBATSIGIL message) {
        // Cleanup and remove the sigil entity
        var removeMsg = new GAME_5_PROTOCOL.MSG_REMOVEOBJECT {
            GameObjectID = Entity.ActiveGameObject.m_globalID
        };

        Entity.ZoneRef.Tell(new ZONE_102_PROTOCOL.MSG_ZONEPLAYERBROADCAST {
            Message = removeMsg,
            Selfless = false
        });
    }

}