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
using System.Collections.Generic;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Results.Handlers;

/// <summary>
/// Starts a duel between the player and the dueling creature that has them in aggro range.
/// The result type carries no target data server-side, so the target is resolved from the zone.
/// </summary>
internal sealed class ResInitiateCombatHandler : BaseResultHandler<ResInitiateCombat> {

    private const float QUERY_WIZARD_TIMEOUT_SECONDS = 5.0f;
    private const float QUERY_TARGET_TIMEOUT_SECONDS = 5.0f;

    public override bool Execute(IResultContext context) {
        var zoneActor = context.GetZoneActor();
        if (zoneActor is null) {
            Logger.Information("ResInitiateCombat executed outside a zone context; skipping.");
            return true;
        }

        // Context does not ship with a wizard reference, so we need to query for it.
        var queryWizardMsg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD();
        var queryTimeout = TimeSpan.FromSeconds(QUERY_WIZARD_TIMEOUT_SECONDS);
        var queryResponse = context
            .GetPlayerRef()
            .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(queryWizardMsg, queryTimeout).Result;
        if (queryResponse?.Wizard is not Wizard wizard) {
            Logger.Error("Handler failed to retrieve character data within {0} seconds.",
                Logger.Args(QUERY_WIZARD_TIMEOUT_SECONDS));

            return false;
        }

        // Already in a fight; nothing to initiate.
        if (wizard.IsInDuel || wizard.IsInCombatGrace) {
            return true;
        }

        var targetQuery = new ZONE_102_PROTOCOL.MSG_QUERYNEARESTDUELTARGET {
            PlayerGameObject = context.GetPlayerObj(),
        };
        var targetResponse = zoneActor
            .Ask<ZONE_102_PROTOCOL.MSG_QUERYNEARESTDUELTARGETRSP>(targetQuery, queryTimeout);
        if (targetResponse is null) {
            Logger.Error("Handler failed to retrieve nearest duel target within {0} seconds.",
                Logger.Args(QUERY_TARGET_TIMEOUT_SECONDS));

            return false;
        }

        if (targetResponse.Result.CreatureActor is null) {
            Logger.Debug("No dueling creature in aggro range; skipping combat initiation.");
            return true;
        }

        var startMsg = new ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL {
            StartingParticipants = new Dictionary<IActorRef, CoreObject> {
                { context.GetPlayerRef(), context.GetPlayerObj() },
                { targetResponse.Result.CreatureActor, targetResponse.Result.CreatureObject },
            },
        };
        zoneActor.Tell(startMsg);

        return true;
    }

}
