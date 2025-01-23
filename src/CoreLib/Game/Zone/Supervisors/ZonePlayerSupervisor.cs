/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using System.Collections.Generic;

namespace Imlight.CoreLib.Game.Zone.Supervisors;

/// <summary>
/// Exists as a child actor of a <see cref="Zone"/> and is the supervisor 
/// for any players that are in the zone.
/// <remarks>
/// Keep in mind that this class does not have players as children actors,
/// That responsibility is left to the <see cref="GameServer"/> itself.
/// </summary>
/// <param name="zone">The zone that this supervisor is responsible for.</param>
internal sealed class ZonePlayerSupervisor(Core.Zone zone) : ZoneEntitySupervisor(zone) {

    private readonly List<IActorRef> _players = [];
    private int _playerPopThresh;

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS))]
    private void ReceiveZoneLoadResults(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS message) {
        // We only care about the ZoneData section of the message.
        var zoneData = message.ZoneData;

        _playerPopThresh = zoneData.m_playerPopThresh;

        // Inform the zone that we have finished initializing.
        var reply = new ZONE_102_PROTOCOL.MSG_ZONESUPERVISORLOADRESULTS { SupervisorName = nameof(ZonePlayerSupervisor) };
        Sender.Tell(reply);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
    private void ReceiveAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message) {
        // If we have reached the player population threshold, we should not add any more players.
        if (_players.Count >= _playerPopThresh) {
            Logger.Error("Player population threshhold for {0} reached ({1} allowed).", 
                Logger.Args(zone.ZoneName, _playerPopThresh));

            return;
        }

        // Inform all currently connected players that a new player has joined the zone.
        var notify = new ZONE_102_PROTOCOL.MSG_PLAYERADDEDTOZONE {
            PlayerActor = message.PlayerActor,
            PlayerObject = message.PlayerObject
        };
        _players.ForEach(p => p.Tell(notify));

        // Add the player to the list of players in the zone.
        _players.Add(message.PlayerActor);

        // Inform the player that they have been added to the zone.
        var rsp = new ZONE_102_PROTOCOL.MSG_ADDPLAYERRSP {
            WizardGameObject = message.PlayerObject
        };
        message.PlayerActor.Tell(rsp);

        Logger.Debug("{Name} added to zone {ZoneName}.",
            Logger.Args(message.ActualWizardName, zone.ZoneName));
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER))]
    private void ReceiveRemovePlayer(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message) {
        _players.Remove(message.PlayerActor);

        var rsp = new ZONE_102_PROTOCOL.MSG_REMOVEPLAYERRSP();
        Sender.Tell(rsp);

        // Inform the player that they have been removed from the zone.
        Logger.Debug("Player {Name} removed from zone {ZoneName}.",
            Logger.Args(message.PlayerActor.Path.Name, zone.ZoneName));

        // Inform all currently connected players that a player has left the zone.
        var notify = new ZONE_102_PROTOCOL.MSG_PLAYERREMOVEDFROMZONE {
            PlayerActor = message.PlayerActor,
            GlobalId = message.GlobalId
        };
        _players.ForEach(p => p.Tell(notify));
    }

}
