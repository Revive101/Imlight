using System.Collections.Generic;
using Akka.Actor;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// This class is responsible for managing all duels in a <see cref="WizardZone"/>.
/// </summary>
public class DuelActorSupervisor : ReceiveProtocolDispatcher {
    private List<IActorRef> _duels;

    public DuelActorSupervisor() {
        _duels = new List<IActorRef>();
    }

    public static Props Props() => Akka.Actor.Props.Create(() => new DuelActorSupervisor());

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_STARTDUEL))]
    private void ReceiveStartDuel(COMBAT_106_PROTOCOL.MSG_STARTDUEL message) {
        // Create the duel as a child of this supervisor. Add it to our references so we can manage it.
        var duelProps = DuelActor.Props();
        var duelActor = CreateChildActor(duelProps);
        _duels.Add(duelActor);

        duelActor.Forward(message);
    }

    // Todo: move this to base class
    private IActorRef CreateChildActor(Props props) => Context.ActorOf(props);
}
