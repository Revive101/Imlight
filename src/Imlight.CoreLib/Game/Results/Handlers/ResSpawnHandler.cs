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

using Akka.Actor;
using Imlight.CoreLib.Shared.Packets;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResSpawnHandler : BaseResultHandler<ResSpawn> {
    
    public override bool Execute(IResultContext context) {
        var zoneActor = context.GetZoneActor();
        if (zoneActor == null) {
            return false;
        }

        if (Result is null) {
            return false;
        }

        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Messages = [new ZONE_102_PROTOCOL.MSG_ZONEPATHSPAWN {
                SpawnObjectID = (uint) Result.m_spawnID
            }],
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

        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Messages = [new ZONE_102_PROTOCOL.MSG_REMOVEOBJECT {
                TemplateID = Result.m_templateID,
            }],
            Targets = ZoneBroadcastTarget.Objects,
        };

        zoneActor.Tell(broadcastMsg);
        
        return true;
    }

}