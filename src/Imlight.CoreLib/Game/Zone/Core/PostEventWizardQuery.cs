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
using Imcodec.MessageLayer.Generated;
using Imlight.Common;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Zone.Core;

/// <summary>
/// Fetches a posting player's wizard without blocking the asking actor, then re-delivers the event with it.
/// </summary>
internal static class PostEventWizardQuery {

    private const int WIZARD_QUERY_TIMEOUT_SECONDS = 5;

    /// <summary>
    /// True when the event already carries the wizard, or has no player to ask. Otherwise starts a timed ask,
    /// re-delivers the event to <paramref name="self"/> with the wizard attached, and returns false.
    /// A timed-out or failed ask drops the event.
    /// </summary>
    public static bool TryResolve(IActorRef self, IActorRef sender, ZONE_102_PROTOCOL.MSG_POSTEVENT message) {
        if (message.PlayerWizard is not null || message.PlayerActor is null) {
            return true;
        }

        if (message.PlayerActor is IInternalActorRef { IsTerminated: true }) {
            Logger.Debug("Dropped event {0}: its player is gone.", Logger.Args(message.EventName));

            return false;
        }

        message.PlayerActor
            .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD(),
                                                       TimeSpan.FromSeconds(WIZARD_QUERY_TIMEOUT_SECONDS))
            .ContinueWith(task => {
                if (!task.IsCompletedSuccessfully || task.Result.Wizard is null) {
                    Logger.Debug("Dropped event {0}: its player did not answer the wizard query.", Logger.Args(message.EventName));

                    return;
                }

                self.Tell(new ZONE_102_PROTOCOL.MSG_POSTEVENT {
                    EventName = message.EventName,
                    PlayerActor = message.PlayerActor,
                    PlayerGameObject = message.PlayerGameObject,
                    SuppressTeleportResults = message.SuppressTeleportResults,
                    PlayerSpawned = message.PlayerSpawned,
                    RequirementsChecked = message.RequirementsChecked,
                    Adjectives = message.Adjectives,
                    PlayerWizard = task.Result.Wizard,
                }, sender);
            });

        return false;
    }

}
