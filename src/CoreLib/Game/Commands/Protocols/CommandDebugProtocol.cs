using Imlight.CoreLib.AntiAmbrose;
using Imlight.CoreLib.Login.Models;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models;
using System;
using System.Text;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandDebugProtocol : CommandProtocol {
    internal override string Group { get; set; } = "debug";

    [Command("gps")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void GpsCommand() {
        var player = Context.CharacterObject;
        var playerPosition = player.m_location;
        var playerRotation = player.m_orientation;

        var message = new StringBuilder();
        message.AppendLine($"Position: {playerPosition}");
        message.AppendLine($"Rotation: {playerRotation}");

        InformSenderClient(message.ToString(), true);
    }
}
