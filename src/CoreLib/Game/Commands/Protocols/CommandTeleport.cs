/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;
using System.Linq;
using System.Threading.Tasks;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandTeleport : CommandProtocol {
    internal override string Group { get; set; } = "";

    private static readonly string s_gmIslandZoneName = "WizardCity/QA_SpawnRate";
    private static readonly string[] s_gmIslandShortcutNames = new[] {
        "gm", "gmisland", "gm_island", "gmis", "gm_is", "gm_isl", "gm_isla", "gm_islan", "gm_island"
    };

    [Command("teleport")]
    [Alias("tp", "port")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void TeleportCommand(string zone) {
        var actualZoneName = zone;
        if (!s_gmIslandShortcutNames.Any(x => x == zone)) {
            // Fallback to the zone name that is contained in the zone name.
            actualZoneName = AccessPassManager.GetContainedZoneName(zone);

            if (actualZoneName == null | actualZoneName == "") {
                Logger.Warning("Teleport command was given an invalid zone name {0}", Logger.Args(zone));
                InformSenderClient($"Zone {zone} does not exist.");
                return;
            }
        }
        else if (s_gmIslandShortcutNames.Any(x => x == zone)) {
            // Check the account auth level to see if they can teleport to the GM Island.
            if (Context.Account.AuthLevel < AuthLevel.HallMonitor) {
                InformSenderClient($"Zone {zone} does not exist.");
                return;
            }

            actualZoneName = s_gmIslandZoneName;
        }

        var teleportEffectsMsg = new CHARACTER_103_PROTOCOL.MSG_DOTELEPORTEFFECTS();
        Context.SessionActor.Tell(teleportEffectsMsg);

        // Wait 2 seconds.
        Task.Delay(2000).Wait();

        var msg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER() {
            DestinationZone = actualZoneName,
            DestinationLocation = "Start",
            SendToClient = true
        };
        Context.SessionActor.Tell(msg);
    }
}
