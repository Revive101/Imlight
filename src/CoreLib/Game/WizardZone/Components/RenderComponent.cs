/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Game.WizardZone.Core;
using Imlight.CoreLib.WizardData.Models.Player;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.WizardZone.Components;

internal sealed class RenderComponent : BaseZoneComponent {

    public override bool NoTransfer { get; set; } = true;

    private readonly CoreObjectSerializer _serializer = new CoreObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask(SerializerOptions.PropertyFlags.Public 
                | SerializerOptions.PropertyFlags.Transmit 
                | SerializerOptions.PropertyFlags.AuthorityTransmit);

    public override bool ShouldAttachToEntity(CoreTemplate template) =>
        // All objects that should be visible to players need a RenderComponent.
        true;

    public override void OnPlayerJoin(CoreObject player, IActorRef suspect, Wizard wizard) {
        // Send object data to the new player
        var newObjectMsg = new GAME_5_PROTOCOL.MSG_NEWOBJECT {
            Data = _serializer.Serialize(Entity.GetClientTypeAlternative())
        };
        suspect.Tell(newObjectMsg);
    }

    public override void OnPlayerLeave(IActorRef suspect, ulong id) {
        // Tell the client to remove the object
        var removeMsg = new GAME_5_PROTOCOL.MSG_REMOVEOBJECT {
            GameObjectID = Entity.ActiveGameObject.m_globalID
        };
        suspect.Tell(removeMsg);
    }

    public override BehaviorInstance GetClientBehaviorInstance() => throw new System.NotImplementedException();

}