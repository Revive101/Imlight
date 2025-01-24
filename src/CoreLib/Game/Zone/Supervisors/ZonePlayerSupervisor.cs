/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

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

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS))]
    public override void ReceiveZoneLoadResults(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS message) {
        // We don't have any actual processing to do here. 
        // Inform the zone that we have finished initializing.
        var reply = new ZONE_102_PROTOCOL.MSG_ZONESUPERVISORLOADRESULTS { SupervisorName = nameof(ZonePlayerSupervisor) };
        Sender.Tell(reply);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONESUPERVISORBROADCAST))]
    public override void ReceiveZoneSupervisorBroadcast(ZONE_102_PROTOCOL.MSG_ZONESUPERVISORBROADCAST message) {
        foreach (var internalMessage in message.Messages) {
            // Check if this is the add player or remove player message.
            switch (internalMessage) {
                case ZONE_102_PROTOCOL.MSG_ADDPLAYER addPlayer:
                    HandleAddPlayer(addPlayer);
                    return;
                case ZONE_102_PROTOCOL.MSG_REMOVEPLAYER removePlayer:
                    HandleRemovePlayer(removePlayer);
                    return;
            }
        }
    }

    // If this handler was left to the base class, it would cause a stack overflow.
    // Players attempt to query a zone entity, which is sent to themselves, which sends this message again.
    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUERYZONEENTITY))]
    public override void ReceiveQueryEntityObject(ZONE_102_PROTOCOL.MSG_QUERYZONEENTITY message)  { }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEPLAYERBROADCAST))]
    private void ReceiveZonePlayerBroadcast(ZONE_102_PROTOCOL.MSG_ZONEPLAYERBROADCAST message) {
        foreach (var entity in EntityActors) {
            if (   message.Sender is not null 
                && entity.Path.Name == message.Sender.Path.Name 
                && message.Selfless) {
                continue;
            }

            entity.Tell(message.Message);
        }
    }

    private void HandleAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message) {
        // Inform all currently connected players that a new player has joined the zone.
        var notify = new ZONE_102_PROTOCOL.MSG_PLAYERADDEDTOZONE {
            PlayerActor = message.PlayerActor,
            PlayerObject = message.PlayerObject
        };
        EntityActors.ForEach(p => p.Tell(notify));

        // Add the player to the list of players in the zone.
        EntityActors.Add(message.PlayerActor);

        // Inform the player that they have been added to the zone.
        var rsp = new ZONE_102_PROTOCOL.MSG_ADDPLAYERRSP {
            WizardGameObject = message.PlayerObject
        };
        message.PlayerActor.Tell(rsp);

        Logger.Debug("{Name} added to zone {ZoneName}.",
            Logger.Args(message.ActualWizardName, zone.ZoneName));
    }

    private void HandleRemovePlayer(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message) {
        EntityActors.Remove(message.PlayerActor);

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
        EntityActors.ForEach(p => p.Tell(notify));
    }

}
