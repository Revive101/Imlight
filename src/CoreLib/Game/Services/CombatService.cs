/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Services;

public class CombatService : MessageService {
    private IActorRef _currentDuelActor;

    public CombatService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor) => Akka.Actor.Props.Create(() => new CombatService(parentActor));

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_ACTORADDEDTODUEL))]
    private void RecieveDuelAdd(COMBAT_106_PROTOCOL.MSG_ACTORADDEDTODUEL message) {
        var wizard = GetActiveWizard();

        _currentDuelActor = message.DuelActor;

        // Set the persistent location and orientation of the wizard
        wizard.SetPersistentLocation(message.SlotPosition);

        // Orientation is given in radians. It must be converted to degrees and then to a byte.
        var orientationRadians = message.SlotOrientation;
        var orientationDegrees = (float)(orientationRadians * (180 / Math.PI));
        var orientation = (byte)(orientationDegrees / 360 * 256);
        wizard.SetPersistentOrientation(orientation);
    }
}
