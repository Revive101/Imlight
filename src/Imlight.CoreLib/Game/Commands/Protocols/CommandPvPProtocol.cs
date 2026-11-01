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
 * COMMANDS
 * ========================================================================
 * 
 * PURPOSE:
 * Quality assurance commands that show, set, reset and clear the PvP rating of the sender in a league.
 * 
 * USAGE EXAMPLE:
 * pvp rating set 1600
 * pvp rank set 5 SomeLeague
 * pvp season reset
 * 
 * NOTE:
 * Every command only sends MSG_RATINGCOMMAND; PvPRatingService does the work. The league is optional and defaults to the first rated league the sender may use. The commands work whether or not ranked play is switched on.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/18/2026
 */

using System;
using System.Linq;
using Akka.Actor;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandPvP : CommandProtocol {

    internal override string Group { get; set; } = "pvp";

    [Command("rating")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void PvPRatingCommand([Remainder] string arguments) {
        var tokens = Split(arguments);
        switch (tokens.FirstOrDefault()) {
            case "show":
                SendRatingAction("show", string.Empty, Get(tokens, 1));
                break;
            case "clear":
                SendRatingAction("clear", string.Empty, Get(tokens, 1));
                break;
            case "set" when tokens.Length >= 2:
                SendRatingAction("set", tokens[1], Get(tokens, 2));
                break;
            default:
                InformSenderClient("Usage: pvp rating show [league] | set <rating> [league] | clear [league]");
                break;
        }
    }

    [Command("rank")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void PvPRankCommand([Remainder] string arguments) {
        var tokens = Split(arguments);
        if (tokens.Length < 2 || tokens[0] != "set") {
            InformSenderClient("Usage: pvp rank set <index or name> [league]");

            return;
        }

        SendRatingAction("rankset", tokens[1], Get(tokens, 2));
    }

    [Command("season")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void PvPSeasonCommand([Remainder] string arguments) {
        var tokens = Split(arguments);
        if (tokens.Length < 1 || tokens[0] != "reset") {
            InformSenderClient("Usage: pvp season reset [league]");

            return;
        }

        SendRatingAction("seasonreset", string.Empty, Get(tokens, 1));
    }

    private static string[] Split(string arguments)
        => (arguments ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Get(string[] tokens, int index)
        => index < tokens.Length ? tokens[index] : string.Empty;

    private void SendRatingAction(string action, string value, string league)
        => Context.SessionActor.Tell(new PVP_111_PROTOCOL.MSG_RATINGCOMMAND {
            Action = action,
            Value = value,
            League = league,
        });

}
