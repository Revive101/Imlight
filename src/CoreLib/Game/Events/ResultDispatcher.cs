/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.CoreLib.Shared.Packets;
using static Imlight.Common.Caches.ServerTypeCache;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Events;

internal static class ResultDispatcher {
    internal static void DispatchResult(IActorRef zoneRef, IActorRef playerRef, Result result) {
        switch (result) {
            case ServerTypeCache.ResTeleport resTeleport:
                Teleport(playerRef, resTeleport);
                break;
            case ResDisplayText resDisplayText:
                DisplayText(playerRef, resDisplayText);
                break;
        }
    }

    private static void Teleport(IActorRef playerRef, ServerTypeCache.ResTeleport resTeleport) {
        var msg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER {
            DestinationZone = resTeleport.m_destinationZone,
            DestinationLocation = resTeleport.m_destinationLoc,
            SendToClient = true
        };
        playerRef.Tell(msg);
    }

    private static void DisplayText(IActorRef playerRef, ResDisplayText resDisplayText) {
        var msg = new GAME_5_PROTOCOL.MSG_CLIENTNOTIFYTEXT {
            NotifyText = resDisplayText.m_text,
            Type = resDisplayText.m_type,
        };
        playerRef.Tell(msg);
    }
}
