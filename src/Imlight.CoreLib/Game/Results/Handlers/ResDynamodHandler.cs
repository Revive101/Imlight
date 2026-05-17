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
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResDynamodHandler : BaseResultHandler<ResAddDynaMod> {

    public override bool Execute(IResultContext context) {
        var msg = new CHARACTER_103_PROTOCOL.MSG_ADDDYNAMOD {
            DynaMod = Result,
            ContextActor = context.GetPlayerRef()
        };

        var zoneActor = context.GetZoneActor();
        if (zoneActor != null) {
            zoneActor.Tell(msg);
        }

        context.GetPlayerRef().Tell(msg);

        return true;
    }

}

internal sealed class RemoveDynamodHandler : BaseResultHandler<ResRemoveDynaMod> {

    public override bool Execute(IResultContext context) {
        var msg = new CHARACTER_103_PROTOCOL.MSG_REMOVEDYNAMOD {
            DynaMod = Result,
            ContextActor = context.GetPlayerRef()
        };

        var zoneActor = context.GetZoneActor();
        if (zoneActor != null) {
            zoneActor.Tell(msg);
        }

        context.GetPlayerRef().Tell(msg);
        
        return true;
    }

}