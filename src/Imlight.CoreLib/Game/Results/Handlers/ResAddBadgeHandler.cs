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
 * ADD BADGE RESULT HANDLER
 * ========================================================================
 *
 * PURPOSE:
 * Awards the badge a ResAddBadge result names, for example when a quest completes.
 *
 * USAGE EXAMPLE:
 * Found by reflection in ResultDispatcher; runs for every result list that holds a ResAddBadge.
 *
 * NOTE:
 * Results run off the session thread, so the handler only tells the player's session; BadgeService writes the badge.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResAddBadgeHandler : BaseResultHandler<ResAddBadge> {

    public override bool Execute(IResultContext context) {
        var playerRef = context.GetPlayerRef();
        if (playerRef is null || Result is null || string.IsNullOrEmpty(Result.m_badgeName)) {
            return false;
        }

        playerRef.Tell(new CHARACTER_103_PROTOCOL.MSG_BADGEEVENT {
            Kind = BadgeEventKind.Award,
            BadgeId = StringHash.Compute(Result.m_badgeName)
        }, ActorRefs.NoSender);

        return true;
    }

}
