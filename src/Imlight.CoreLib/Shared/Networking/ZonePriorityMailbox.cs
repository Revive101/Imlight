using Akka.Actor;
using Akka.Configuration;
using Akka.Dispatch;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Shared.Networking;

/// <summary>
/// Zone mailbox that serves moves and broadcasts first. It is stable: messages of equal priority keep their
/// send order, where the plain priority mailbox hands a backlog of them back almost in reverse.
/// </summary>
public class ZonePriorityMailbox : UnboundedStablePriorityMailbox {

    public ZonePriorityMailbox(Settings settings, Config config)
        : base(settings, config) { }

    protected override int PriorityGenerator(object message) {
        return message switch {
            ZONE_102_PROTOCOL.MSG_PLAYERMOVE    => 0,
            ZONE_102_PROTOCOL.MSG_CREATUREMOVE  => 1,
            ZONE_102_PROTOCOL.MSG_ZONEBROADCAST => 2,
            // A summoned pet's spawn must stay ahead of the broadcast that dismisses it.
            ZONE_102_PROTOCOL.MSG_SPAWNENTITY   => 2,
            _ => 10,
        };
    }

}
