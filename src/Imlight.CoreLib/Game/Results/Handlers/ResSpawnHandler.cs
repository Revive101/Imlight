/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using Akka.Actor;
using Imlight.CoreLib.Shared.Packets;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal static class SpawnHandlerHelper {

    public static Wizard GetWizard(IActorRef playerRef) {
        if (playerRef is null) {
            return null;
        }

        try {
            return playerRef
                .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD(), TimeSpan.FromSeconds(5))
                .Result?.Wizard;
        }
        catch (AggregateException ex) when (ex.InnerException is AskTimeoutException) {
            return null;
        }
    }

}

internal sealed class ResSpawnHandler : BaseResultHandler<ResSpawn> {
    
    public override bool Execute(IResultContext context) {
        var zoneActor = context.GetZoneActor();
        if (zoneActor == null) {
            return false;
        }

        if (Result is null) {
            return false;
        }

        var spawnMsg = new ZONE_102_PROTOCOL.MSG_ZONEPATHSPAWN {
            SpawnObjectID = (uint) Result.m_spawnID,
            Activate = Result.m_activate,
        };

        var playerRef = context.GetPlayerRef();
        var playerObj = context.GetPlayerObj();
        var wizard = SpawnHandlerHelper.GetWizard(playerRef);

        if (Result.m_activate) {
            // Let the spawned creature check its aggro radius against the causing player right away.
            if (wizard is not null && playerObj is not null) {
                spawnMsg.PlayerObject = playerObj;
                spawnMsg.PlayerActor = playerRef;
                spawnMsg.PlayerWizard = wizard;
            }
        }

        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Messages = [spawnMsg],
            Targets = ZoneBroadcastTarget.Paths,
        };

        zoneActor.Tell(broadcastMsg);
        
        return true;
    }

}

internal sealed class ResDespawnHandler : BaseResultHandler<ResDespawn> {
    
    public override bool Execute(IResultContext context) {
        var zoneActor = context.GetZoneActor();
        if (zoneActor == null) {
            return false;
        }

        if (Result is null) {
            return false;
        }

        // Path-spawned creatures live under the path supervisor; include Paths so they see the removal.
        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Messages = [new ZONE_102_PROTOCOL.MSG_REMOVEOBJECT {
                TemplateID = Result.m_templateID,
            }],
            Targets = ZoneBroadcastTarget.Objects | ZoneBroadcastTarget.Paths,
        };

        zoneActor.Tell(broadcastMsg);
        
        return true;
    }

}