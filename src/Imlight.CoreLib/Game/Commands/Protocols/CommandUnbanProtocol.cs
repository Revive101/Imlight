/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Implementations;
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
