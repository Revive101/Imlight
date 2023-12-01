/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Akka.Actor;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// This class is responsible for managing all duels in a <see cref="WizardZone"/>.
/// </summary>
public class DuelActorSupervisor : ReceiveProtocolDispatcher {
    private readonly IActorRef _wizardZoneRef;
    private List<IActorRef> _duels;

    public DuelActorSupervisor(IActorRef wizardZoneRef) {
        _wizardZoneRef = wizardZoneRef;
        _duels = new List<IActorRef>();
    }

    public static Props Props(IActorRef wizardZoneRef)
        => Akka.Actor.Props.Create(() => new DuelActorSupervisor(wizardZoneRef));

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_STARTDUEL))]
    private void ReceiveStartDuel(COMBAT_106_PROTOCOL.MSG_STARTDUEL message) {
        // Create the duel as a child of this supervisor. Add it to our references so we can manage it.
        var duelProps = DuelActor.Props(_wizardZoneRef);
        var duelActor = CreateChildActor(duelProps);
        _duels.Add(duelActor);

        duelActor.Forward(message);
    }

    // Todo: move this to base class
    private IActorRef CreateChildActor(Props props) => Context.ActorOf(props);
}
