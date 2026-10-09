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
 *
 * ========================================================================
 * CINEMATIC ACTOR RESULT HANDLER
 * ========================================================================
 *
 * PURPOSE:
 * Plays a ResCinematicActor (a zone trigger's scripted scene) for the player who fired the trigger.
 *
 * USAGE EXAMPLE:
 * A quest goal posts an event (ResPostEvent), the zone trigger listening for it runs this result.
 *
 * NOTE:
 * The scene itself runs in the player's CinematicService.
 *
 * TODO:
 *
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 09/27/2026
 */

using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResCinematicActorHandler : BaseResultHandler<ResCinematicActor> {

    public override bool Execute(IResultContext context) {
        var playerRef = context.GetPlayerRef();
        if (playerRef is null || Result is null) {
            return false;
        }

        playerRef.Tell(new CHARACTER_103_PROTOCOL.MSG_PLAYCINEMATICACTOR {
            Cinematic = Result,
            ZoneActor = context.GetZoneActor()
        });

        return true;
    }

}
