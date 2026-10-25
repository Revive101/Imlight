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
 * Turns the duel modifier names of a tournament into the rules the duel runs by.
 * 
 * USAGE EXAMPLE:
 * if (PvPGameTypes.TryResolve(tournament.m_pvpDuelModifiers, out var rules)) { }
 * 
 * NOTE:
 * The practice tournament lists one game type that has no file in the game data: the client treats it as the
 * alternate turns rule with pips for both teams. Any other name is not supported yet.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Collections.Generic;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;

namespace Imlight.CoreLib.Game.PvP;

internal sealed record PvPDuelRules(bool AlternateTurns, bool PipsForBothTeamsAtOnce);

internal static class PvPGameTypes {

    public const string ALTERNATING_TURNS_PIPS_AT_ONCE = "AlternatingTurns_PipsAtOnce";

    public static bool TryResolve(IEnumerable<string> modifierNames, out PvPDuelRules rules) {
        var alternateTurns = false;
        var pipsForBothTeams = false;
        foreach (var name in modifierNames ?? []) {
            if (name == ALTERNATING_TURNS_PIPS_AT_ONCE) {
                alternateTurns = true;
                pipsForBothTeams = true;

                continue;
            }

            Logger.Error("The PvP duel modifier {Name} is not supported.", Logger.Args(name));
            rules = null;

            return false;
        }

        rules = new PvPDuelRules(alternateTurns, pipsForBothTeams);

        return true;
    }

    public static AlternateTurnsCombatRule ToCombatRule(PvPDuelRules rules)
        => new() {
            m_alternateTurns = rules.AlternateTurns,
            // Alternating turns gain pips per phase, so the client must not show them arriving together.
            m_pipsForBothTeamsAtOnce = rules.PipsForBothTeamsAtOnce && !rules.AlternateTurns
        };

}
