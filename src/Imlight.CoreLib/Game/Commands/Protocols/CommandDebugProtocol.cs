/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

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

}
