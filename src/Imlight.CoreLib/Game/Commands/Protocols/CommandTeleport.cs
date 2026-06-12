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

using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Game.World;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using System.Linq;
using System.Threading.Tasks;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandTeleport : CommandProtocol {

    internal override string Group { get; set; } = "";

    private static readonly string s_gmIslandZoneName = "WizardCity/QA_SpawnRate";
    private static readonly string[] s_gmIslandShortcutNames = [
        "gm", "gmisland", "gm_island", "gmis", "gm_is", "gm_isl", "gm_isla", "gm_islan", "gm_island"
    ];

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
            SendToClient = true,
            OwnerCharId = Context.Character.CharId
        };
        Context.SessionActor.Tell(msg);
    }

}
