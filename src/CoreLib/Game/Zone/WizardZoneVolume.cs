/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using static Imlight.Common.Caches.ServerTypeCache;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone;

/// <summary>
/// A volume is a space in the game world that can be used to trigger events when a player enters or exits it.
/// </summary>
public class WizardZoneVolume : WizardZoneObject {
    private Volume _volume;

    // ctor
    public WizardZoneVolume(CoreObject activeGameObject,
                            CoreTemplate template,
                            IActorRef wizardZoneRef,
                            Volume volume)
        : base(activeGameObject, template, wizardZoneRef) {
        base.InteractionRadius = volume.m_radius;
        this._volume = volume;
    }

    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject,
                              CoreTemplate template,
                              IActorRef wizardZoneRef,
                              Volume volume)
        => Akka.Actor.Props.Create(() => new WizardZoneVolume(activeGameObject,
                                                              template,
                                                              wizardZoneRef,
                                                              volume));

    protected override void OnPlayerInteractionEnter(CoreObject player, IActorRef suspect) {
        base.OnPlayerInteractionEnter(player, suspect);

        foreach (var enterEvent in _volume.m_enterEvents) {
            var postEventMsg = new ZONE_102_PROTOCOL.MSG_POSTEVENT {
                EventName = enterEvent,
                ZoneActor = WizardZoneRef,
                SenderActor = suspect,
                SenderGameObject = player
            };

            WizardZoneRef.Tell(postEventMsg);
        }
    }

    protected override void OnPlayerInteractionExit(CoreObject player, IActorRef suspect) {
        base.OnPlayerInteractionExit(player, suspect);

        foreach (var exitEvent in _volume.m_exitEvents) {
            var postEventMsg = new ZONE_102_PROTOCOL.MSG_POSTEVENT {
                EventName = exitEvent,
                ZoneActor = WizardZoneRef,
                SenderActor = suspect,
                SenderGameObject = player
            };

            WizardZoneRef.Tell(postEventMsg);
        }
    }

    protected override void OnStatusCheck() {
        // Volumes don't have templates like normal zone objects do.
        var failure = ActiveGameObject == null;
        var reason = failure ? "Object or template is null." : null;

        var rsp = new ZONE_102_PROTOCOL.MSG_OBJECTSTATUSCHECKRSP {
            ZoneObject = this,
            CoreObject = ActiveGameObject,
            Failure = failure,
            Error = reason
        };

        Sender.Tell(rsp);
    }
}
