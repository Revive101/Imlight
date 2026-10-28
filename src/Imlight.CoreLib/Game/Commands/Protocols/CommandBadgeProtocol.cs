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
 * BADGE COMMANDS
 * ========================================================================
 *
 * PURPOSE:
 * Quality assurance commands that grant badges, add to badge counters, list the held badges and reset them.
 *
 * USAGE EXAMPLE:
 * badge grant Level01-Cantrips
 * badge progress Badges_00002780 49
 *
 * NOTE:
 * Every command only sends MSG_BADGECOMMAND; BadgeService does the work through the same paths as play.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Globalization;
using Akka.Actor;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandBadge : CommandProtocol {

    internal override string Group { get; set; } = "badge";

    [Command("grant")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void BadgeGrantCommand([Remainder] string idOrName)
        => SendBadgeAction("grant", idOrName, 0);

    [Command("progress")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void BadgeProgressCommand(string counterKey, string amount) {
        if (!ulong.TryParse(amount, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedAmount)) {
            InformSenderClient("Invalid amount.");

            return;
        }

        SendBadgeAction("progress", counterKey, parsedAmount);
    }

    [Command("list")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void BadgeListCommand()
        => SendBadgeAction("list", string.Empty, 0);

    [Command("reset")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void BadgeResetCommand()
        => SendBadgeAction("reset", string.Empty, 0);

    private void SendBadgeAction(string action, string argument, ulong amount)
        => Context.SessionActor.Tell(new CHARACTER_103_PROTOCOL.MSG_BADGECOMMAND {
            Action = action,
            Argument = argument,
            Amount = amount,
        });

}
