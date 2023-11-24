using System;
using System.Text;
using Imlight.CoreLib.Login.Models;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models;

namespace Imlight.CoreLib.Game.Commands;

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
        if (!TimeSpan.TryParse(time, out var timeSpan)) {
            InformSenderClient("Invalid time format");
            return;
        }

        // Now with the timespan, get a DateTime for when the ban will expire.
        var banExpiration = DateTime.UtcNow + timeSpan;

        var source = Context.Account.Username;
        account.AddInfraction(InfractionType.Ban, reason, source, banExpiration);

        InformSenderClient($"Account {account.Username} has been banned");
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
        if (!TimeSpan.TryParse(time, out var timeSpan)) {
            InformSenderClient("Invalid time format");
            return;
        }

        // Now with the timespan, get a DateTime for when the ban will expire.
        var banExpiration = DateTime.UtcNow + timeSpan;

        InfractionCollection.AddMachineBan(machineIdLong, banExpiration);

        InformSenderClient($"Machine {machineIdLong} has been banned");
    }

    [Command("ip")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void BanIpCommand(string ip, string time, [Remainder] string reason) {
        if (InfractionCollection.IsIpBanned(ip)) {
            InformSenderClient("IP is already banned");
            return;
        }

        // The time will be in the format of 1d2h3m4s. Parse it into a TimeSpan.
        if (!TimeSpan.TryParse(time, out var timeSpan)) {
            InformSenderClient("Invalid time format");
            return;
        }

        // Now with the timespan, get a DateTime for when the ban will expire.
        var banExpiration = DateTime.UtcNow + timeSpan;

        InfractionCollection.AddIpBan(ip, banExpiration);

        InformSenderClient($"IP {ip} has been banned");
    }

    [Command("info")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void BanInfoCommand(string username) {
        var account = AccountCollection.GetAccount(username);
        if (account == null) {
            InformSenderClient("Account not found");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Account: {account.Username}");
        sb.AppendLine($"Banned: {account.InfractionHistory.IsCurrentlyBanned}");
        sb.AppendLine($"Ban ends at: {account.InfractionHistory.BanEndsAt}");
        sb.AppendLine($"Muted: {account.InfractionHistory.IsCurrentlyMuted}");
        sb.AppendLine($"Mute ends at: {account.InfractionHistory.MuteEndsAt}");
        sb.AppendLine($"Last infraction: {account.InfractionHistory.LastInfractionTime}");

        InformSenderClient(sb.ToString(), true);
    }
}
