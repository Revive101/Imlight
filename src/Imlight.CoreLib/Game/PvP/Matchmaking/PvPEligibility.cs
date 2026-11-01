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
 * PVP
 * ========================================================================
 * 
 * PURPOSE:
 * The rules for who may open a league window and queue: the requirements of the league and the current
 * state of the player.
 * 
 * USAGE EXAMPLE:
 * var refusal = PvPEligibility.GetQueueRefusal(wizard);
 * 
 * NOTE:
 * The requirements of a league are its entry level. Both the kiosk and the PvP window of a session check them,
 * because the client only hides what it cannot use.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.PvP.Arena;
using Imlight.CoreLib.Game.Requirements;
using Imlight.CoreLib.Game.Requirements.Contexts;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.PvP.Matchmaking;

internal static class PvPEligibility {

    public static bool MeetsLeagueRequirements(PvPLeagueTemplate league, IActorRef playerActor, CoreObject playerObject, Wizard wizard)
        => RequirementDispatcher.EvaluateRequirements(
            league.m_requirementsList,
            new GenericRequirementContext(league.m_requirementsList, playerActor, playerObject, wizard)
        );

    /// <summary>
    /// Gets the arena error key of the reason the player cannot start queueing now, or null when nothing stops them.
    /// </summary>
    public static string GetQueueRefusal(Wizard wizard) {
        if (PvPRegistry.IsQueued(wizard.CharId)) {
            return PvPArenaErrors.ALREADY_QUEUED;
        }

        if (PvPRegistry.IsHosting(wizard.CharId)) {
            return PvPArenaErrors.ALREADY_QUEUED;
        }

        if (PvPRegistry.IsInMatch(wizard.CharId)) {
            return PvPArenaErrors.ALREADY_IN_MATCH;
        }

        if (wizard.IsInDuel || wizard.IsInCombatGrace) {
            return PvPArenaErrors.PLAYER_INELIGIBLE;
        }

        return null;
    }

}
