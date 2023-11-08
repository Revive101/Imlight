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
    private readonly List<CoreObject> _objsInRadius;

    // ctor
    public WizardZoneVolume(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef, Volume volume)
        : base(activeGameObject, template, wizardZoneRef) {
        this._volume = volume;
        this._objsInRadius = new List<CoreObject>();
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

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
    protected override void ReceiveAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message) {
        base.ReceiveAddPlayer(message);

        // If the player spawns in the volume, do not trigger the enter event.
        if (IsInRadius(message.PlayerObject)) {
            _objsInRadius.Add(message.PlayerObject);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER))]
    protected override void ReceiveRemovePlayer(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message) {
        base.ReceiveRemovePlayer(message);

        // Remove the player object from our radius to clear up any resources.
        _objsInRadius.RemoveAll(x => x.m_globalID == message.GlobalId);
    }
}
