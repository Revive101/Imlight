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

using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using System.Text;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandDebugProtocol : CommandProtocol {

    internal override string Group { get; set; } = "debug";

    [Command("gps")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void GpsCommand() {
        var player = Context.CharacterObject;
        var playerPosition = player.m_location;
        var playerRotation = player.m_orientation;
        var zone = Context.Character.Zone;

        var message = new StringBuilder()
            .AppendLine($"<center>Showing the information saved on Imlight:</center>\n")
            .AppendLine($"Position: {playerPosition}")
            .AppendLine($"Rotation: {playerRotation}")
            .AppendLine($"Zone: {zone}");

        InformSenderClient(message.ToString(), true);
    }

    [Command("disableobj")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void DisableObjectCommand([Remainder] string zoneTag) {
        var stateChangeMsg = new ZONE_102_PROTOCOL.MSG_ENTERSTATE {
            ObjectName = zoneTag,
            StateName = "Off",
            ExclusiveToSender = true,
            Sender = Context.SessionActor
        };

        var message = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Messages = [stateChangeMsg],
            Sender = Context.SessionActor,
            Targets = ZoneBroadcastTarget.Objects,
        };

        Context.ZoneActor.Tell(message, Context.SessionActor);
    }

    [Command("enableobj")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void EnableObjectCommand([Remainder] string zoneTag) {
        var stateChangeMsg = new ZONE_102_PROTOCOL.MSG_ENTERSTATE {
            ObjectName = zoneTag,
            StateName = "On",
            ExclusiveToSender = true,
            Sender = Context.SessionActor
        };

        var message = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Messages = [stateChangeMsg],
            Sender = Context.SessionActor,
            Targets = ZoneBroadcastTarget.Objects,
        };

        Context.ZoneActor.Tell(message, Context.SessionActor);
    }

}
