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
using System.Text;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandBanProtocol : CommandProtocol {

    internal override string Group { get; set; } = "ban";

    [Command("account")]
    [AuthRequired(AuthLevel.HallMonitor)]
    [Alias("user", "username")]
    private void BanAccountCommand(string username, string time, [Remainder] string reason) {
        var account = AccountCollection.GetAccount(username);
        if (account == null) {
            InformSenderClient("Account not found");

            return;
        }

        if (account.InfractionHistory.IsCurrentlyBanned) {
            InformSenderClient("Account is already banned");

            return;
        }

        // The time will be in the format of 1d2h3m4s. Parse it into a TimeSpan.
        if (!CommandUtilities.TryParseDuration(time, out var timeSpan)) {
            InformSenderClient("Invalid time format");

            return;
        }

        // Now with the timespan, get a DateTime for when the ban will expire.
        var banExpiration = DateTime.UtcNow + timeSpan;

        var source = Context.Account.Username;
        account.AddInfraction(InfractionType.Ban, reason, source, banExpiration);

        // Kick the player from the game.
        var kickMsg = new SERVER_100_PROTOCOL.MSG_KICKPLAYER {
            AccountID = account.AccountId
        };
        Context.ServerActor.Tell(kickMsg, Context.SessionActor);

        InformSenderClient($"Account {account.Username} has been banned until {banExpiration}", true);
    }

    [Command("machine")]
    [AuthRequired(AuthLevel.HallMonitor)]
    [Alias("pc", "computer")]
    private void BanMachineCommand(string machineId, string time, [Remainder] string reason) {
        if (!ulong.TryParse(machineId, out var machineIdLong)) {
            InformSenderClient("Invalid machine ID");

            return;
        }

        if (InfractionCollection.IsMachineBanned(machineIdLong)) {
            InformSenderClient("Machine is already banned");

            return;
        }

        // The time will be in the format of 1d2h3m4s. Parse it into a TimeSpan.
        if (!CommandUtilities.TryParseDuration(time, out var timeSpan)) {
            InformSenderClient("Invalid time format");

            return;
        }

        // Now with the timespan, get a DateTime for when the ban will expire.
        var banExpiration = DateTime.UtcNow + timeSpan;
        InfractionCollection.AddMachineBan(machineIdLong, banExpiration);
        InformSenderClient($"Machine {machineIdLong} has been banned until {banExpiration}");
    }

    [Command("ip")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void BanIpCommand(string ip, string time, [Remainder] string reason) {
        if (InfractionCollection.IsIpBanned(ip)) {
            InformSenderClient("IP is already banned");
            return;
        }

        // The time will be in the format of 1d2h3m4s. Parse it into a TimeSpan.
        if (!CommandUtilities.TryParseDuration(time, out var timeSpan)) {
            InformSenderClient("Invalid time format");
            return;
        }

        // Now with the timespan, get a DateTime for when the ban will expire.
        var banExpiration = DateTime.UtcNow + timeSpan;

        InfractionCollection.AddIpBan(ip, banExpiration);

        InformSenderClient($"IP {ip} has been banned until {banExpiration}");
    }

    [Command("info")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void BanInfoCommand(string username) {
        var account = AccountCollection.GetAccount(username);
        if (account == null) {
            InformSenderClient("Account not found");

            return;
        }

        var sb = new StringBuilder()
            .AppendLine($"Account: {account.Username}")
            .AppendLine($"Banned: {account.InfractionHistory.IsCurrentlyBanned}")
            .AppendLine($"Ban ends at: {account.InfractionHistory.BanEndsAt}")
            .AppendLine($"Muted: {account.InfractionHistory.IsCurrentlyMuted}")
            .AppendLine($"Mute ends at: {account.InfractionHistory.MuteEndsAt}")
            .AppendLine($"Last infraction: {account.InfractionHistory.LastInfractionTime}");

        InformSenderClient(sb.ToString(), true);
    }

}
