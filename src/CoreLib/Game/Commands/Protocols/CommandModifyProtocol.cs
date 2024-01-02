/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Text;
using Imlight.Common.Caches;
using Imlight.Common.Configuration;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandModifyProtocol : CommandProtocol {
    internal override string Group { get; set; } = "mod";

    [Command("levelup")]
    [AuthRequired(AuthLevel.Developer)]
    [Alias("lvlup")]
    private void LevelUpCommand() {
        // Check to see if the new level would be above the max level.
        var isOverMax = (Context.Character.Level + 1) > ConfigurationManager.Settings.MaxLevel;
        if (isOverMax) {
            InformSenderClient("You cannot level up any further.");
            return;
        }

        var msg = new CHARACTER_103_PROTOCOL.MSG_LEVELUP() {
            NewLevel = (byte) (Context.Character.Level + 1)
        };
        Context.SessionActor.Tell(msg, null);
    }

    [Command("level")]
    [AuthRequired(AuthLevel.Developer)]
    private void SetLevelCommand(string level) {
        // Try to parse the level.
        if (!byte.TryParse(level, out var levelByte)) {
            InformSenderClient("Invalid level.");
            return;
        }

        var maxLevel = ConfigurationManager.Settings.MaxLevel;
        var isOverMax = levelByte > maxLevel;
        if (isOverMax) {
            InformSenderClient($"You cannot set level higher than the max level ({maxLevel}).");
            return;
        }

        var msg = new CHARACTER_103_PROTOCOL.MSG_LEVELUP() {
            NewLevel = levelByte
        };
        Context.SessionActor.Tell(msg, null);
    }
}
