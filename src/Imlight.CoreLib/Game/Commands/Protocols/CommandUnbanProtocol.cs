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

using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandUnbanProtocol : CommandProtocol {

    internal override string Group { get; set; } = "unban";

    [Command("account")]
    [AuthRequired(AuthLevel.HallMonitor)]
    [Alias("user", "username")]
    private void UnbanCommand(string username) {
        var account = AccountCollection.GetAccount(username);
        if (account is null) {
            InformSenderClient("Account not found.");

            return;
        }

        if (!account.InfractionHistory.IsCurrentlyBanned) {
            InformSenderClient("Account is not banned.");

            return;
        }

        var result = account.WaiveCurrentBan(Context.Account.Username);
        if (!result) {
            InformSenderClient("Failed to waive ban.");

            return;
        }

        InformSenderClient("Account unbanned.");
    }

    [Command("machine")]
    [AuthRequired(AuthLevel.HallMonitor)]
    [Alias("pc", "computer")]
    private void UnbanMachineCommand(string machineId) {
        if (!ulong.TryParse(machineId, out var machineIdLong)) {
            InformSenderClient("Invalid machine ID");

            return;
        }

        if (!InfractionCollection.IsMachineBanned(machineIdLong)) {
            InformSenderClient("Machine is not banned");

            return;
        }

        var result = InfractionCollection.RemoveMachineBan(machineIdLong);
        if (!result) {
            InformSenderClient("Failed to unban machine.");

            return;
        }

        InformSenderClient("Machine unbanned.");
    }

    [Command("ip")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void UnbanIpCommand(string ip) {
        if (!InfractionCollection.IsIpBanned(ip)) {
            InformSenderClient("IP is not banned");

            return;
        }

        var result = InfractionCollection.RemoveIpBan(ip);
        if (!result) {
            InformSenderClient("Failed to unban IP.");

            return;
        }

        InformSenderClient("IP unbanned.");
    }

}
