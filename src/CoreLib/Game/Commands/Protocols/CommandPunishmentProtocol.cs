/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Text;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandPunishmentProtocol : CommandProtocol {
    internal override string Group { get; set; } = "";

    [Command("mute")]
    [AuthRequired(AuthLevel.HallMonitor)]
    public void MuteCommand(string time, [Remainder]string reason) {
        if (Context.SelectedAccount is null) {
            InformSenderClient("You must select a user to use this command.", true);
            return;
        }
        if (Context.SelectedAccount.InfractionHistory.IsCurrentlyMuted) {
            InformSenderClient("This account is already muted.", true);
            return;
        }

        // Parse the time.
        if (!CommandUtilities.TryParseDuration(time, out var duration)) {
            InformSenderClient("Invalid duration.", true);
            return;
        }

        // Convert the timespan into a DateTime of when it will end.
        var end = DateTime.UtcNow.Add(duration);

        var sourceName = Context.Account.Username;
        Context.SelectedAccount.AddInfraction(InfractionType.Mute, reason, sourceName, end);

        InformSenderClient($"Muted {Context.SelectedAccount.Username} until {end}.", true);
    }

    [Command("unmute")]
    [AuthRequired(AuthLevel.HallMonitor)]
    public void UnmuteCommand() {
        if (Context.SelectedAccount is null) {
            InformSenderClient("You must select a user to use this command.", true);
            return;
        }
        if (!Context.SelectedAccount.InfractionHistory.IsCurrentlyMuted) {
            InformSenderClient("This account is not muted.", true);
            return;
        }

        var sourceName = Context.Account.Username;
        Context.SelectedAccount.WaiveCurrentMute(sourceName);

        InformSenderClient($"Unmuted {Context.SelectedAccount.Username}.", true);
    }

    [Command("kick")]
    [AuthRequired(AuthLevel.HallMonitor)]
    public void KickCommand([Remainder]string reason) {
        if (Context.SelectedAccount is null) {
            InformSenderClient("You must select a user to use this command.", true);
            return;
        }

        // Kick the player from the game.
        var kickMsg = new SERVER_100_PROTOCOL.MSG_KICKPLAYER {
            AccountID = Context.SelectedAccount.AccountId
        };
        Context.ServerActor.Tell(kickMsg, Context.SessionActor);

        InformSenderClient($"Kicked {Context.SelectedAccount.Username}.", true);
    }

    [Command("warn")]
    [AuthRequired(AuthLevel.HallMonitor)]
    public void WarnCommand([Remainder]string reason) {
        if (Context.SelectedAccount is null) {
            InformSenderClient("You must select a user to use this command.", true);
            return;
        }

        var sourceName = Context.Account.Username;
        Context.SelectedAccount.AddInfraction(InfractionType.Warn, reason, sourceName);

        InformSenderClient($"Warned {Context.SelectedAccount.Username}.", true);
    }

    [Command("info")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void GetAccountInfoCommand() {
        if (Context.SelectedAccount is null) {
            InformSenderClient("You must select a user to use this command.", true);
            return;
        }

        // Craft the reply.
        var account = Context.SelectedAccount;
        var sb = new StringBuilder();
        sb.Append($"<center>{account.Username}</center>\n");
        sb.Append($"<center>Auth Level: {account.AuthLevel}</center>\n");
        sb.AppendLine("");

        sb.Append($"<left>Creation Time: {account.CreationTime}</left>\n");
        sb.Append($"<left>Last Login Time: {account.LastLoginTime}</left>\n");
        sb.Append($"<left>Last Login Machine ID: {account.LastLoginMachineId}</left>\n");
        sb.Append($"<left>Last Login IP: {account.LastLoginIp}</left>\n");
        sb.AppendLine("");

        var history = account.InfractionHistory;
        sb.Append($"<left>Is Locked: {account.IsLocked}</left>\n");

        if (history.IsCurrentlyBanned) {
            sb.Append($"<left>Ban Ends At: {history.BanEndsAt}</left>\n");
        }

        if (history.IsCurrentlyMuted) {
            sb.Append($"<left>Mute Ends At: {history.MuteEndsAt}</left>\n");
        }

        if (history.Infractions.Count > 1) {
            sb.Append($"<left>Infractions: {history.Infractions.Count}</left>\n");
        }
        else {
            sb.Append($"<left>No infractions.</left>\n");
        }

        sb.Append($"<left>Character IDs:");
        for (int i = 0; i < account.CharacterIds.Count; i++) {
            sb.Append($"{account.CharacterIds[i]}, ");
        }

        InformSenderClient(sb.ToString(), true);
    }
}
