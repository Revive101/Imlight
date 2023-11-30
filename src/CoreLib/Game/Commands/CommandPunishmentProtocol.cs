using Imlight.CoreLib.WizardData.Models;
using System;

namespace Imlight.CoreLib.Game.Commands;

internal class CommandPunishmentProtocol : CommandProtocol {
    internal override string Group { get; set; } = "";

    [Command("mute")]
    public void MuteCommand(string time, [Remainder]string reason) {
        if (Context.SelectedAccount is null) {
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

        Context.SelectedAccount.AddInfraction(InfractionType.Mute, reason, Context.Account.Username, end);

        InformSenderClient($"Muted {Context.SelectedAccount.Username} until {end}.", true);
    }
}
