/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Akka.Actor;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using static Imlight.Common.Caches.ServerTypeCache;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone;

public class WizardZoneVolume : WizardZoneObject {
    private readonly Volume _volume;

    // ctor
    public WizardZoneVolume(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef, Volume volume)
        : base(activeGameObject, template, wizardZoneRef) {
        this._volume = volume;
        base.InteractionRadius = volume.m_radius;
    }

    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef, Volume volume) {
        return Akka.Actor.Props.Create(() => new WizardZoneVolume(activeGameObject, template, wizardZoneRef, volume));
    }

    protected override void OnPlayerInteractionEnter(CoreObject player, IActorRef suspect) {
        base.OnPlayerInteractionEnter(player, suspect);

        foreach (var ev in _volume.m_enterEvents) {
            var msg = new ZONE_102_PROTOCOL.MSG_TRIGGER() {
                TriggerName = ev,
                Suspect = suspect
            };
            WizardZoneRef.Tell(msg);
        }
    }

    protected override void OnPlayerInteractionExit(CoreObject player, IActorRef suspect) {
        base.OnPlayerInteractionExit(player, suspect);

        foreach (var ev in _volume.m_exitEvents) {
            var msg = new ZONE_102_PROTOCOL.MSG_TRIGGER() {
                TriggerName = ev,
                Suspect = suspect
            };
            WizardZoneRef.Tell(msg);
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
