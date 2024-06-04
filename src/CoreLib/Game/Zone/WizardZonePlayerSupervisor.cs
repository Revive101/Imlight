/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone;

public class WizardZonePlayerSupervisor : ReceiveProtocolDispatcher {
    private readonly IActorRef _wizardZoneRef;
    private readonly List<IActorRef> _players;

    public WizardZonePlayerSupervisor(IActorRef wizardZoneRef) {
        this._wizardZoneRef = wizardZoneRef;
        this._players = new List<IActorRef>();
    }

    public static Props Props(IActorRef wizardZoneRef)
        => Akka.Actor.Props.Create(() => new WizardZonePlayerSupervisor(wizardZoneRef));

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST))]
    private void ReceiveZoneBroadcast(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST message) {
        foreach (var player in _players) {
            if (message.Selfless && message.Sender == player) {
                continue;
            }

            player.Tell(message.Message);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
    private void ReceivePlayerAdded(ZONE_102_PROTOCOL.MSG_ADDPLAYER message) {
        var msg = new ZONE_102_PROTOCOL.MSG_PLAYERADDEDTOZONE {
            Player = message.Player,
            PlayerObject = message.PlayerObject,
        };

        // Inform each player that a new player has been added.
        foreach (var player in _players) {
            player.Tell(msg);
        }

        // Add the player to the list of players.
        _players.Add(message.Player);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER))]
    private void ReceivePlayerRemoved(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message) {
        var msg = new ZONE_102_PROTOCOL.MSG_PLAYERREMOVEDFROMZONE {
            Player = message.Player,
            GlobalId = message.GlobalId,
        };

        // Inform each player that a player has been removed.
        foreach (var player in _players) {
            player.Tell(msg);
        }

        // Remove the player from the list of players.
        _players.Remove(message.Player);
    }
}
