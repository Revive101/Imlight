/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Zone;

public class WizardPrivateZone : WizardZoneLegacy {
    private readonly IActorRef _owner;

    // ctor
    public WizardPrivateZone(string zoneName, IActorRef owner) : base(zoneName) {
        _owner = owner;
    }

    // Akka.NET ctor
    public static Props Props(string zoneName, IActorRef owner)
        => Akka.Actor.Props.Create(() => new WizardPrivateZone(zoneName, owner));

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER))]
    protected override void ReceiveRemovePlayer(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message) {
        if (message.Player == _owner) {
            // Owner left the zone, destroy it
            CloseZone();
            return;
        }

        base.ReceiveRemovePlayer(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PLAYERCOUNTUPDATE))]
    protected override void ReceivePlayerCountUpdate(ZONE_102_PROTOCOL.MSG_PLAYERCOUNTUPDATE message) {
        if (message.PlayerCount == 0) {
            // No players left, destroy the zone
            CloseZone();
            return;
        }

        base.ReceivePlayerCountUpdate(message);
    }

    private ushort GetHardPlayerLimit() => (ushort) ZoneData.m_nHardLimit;
}
