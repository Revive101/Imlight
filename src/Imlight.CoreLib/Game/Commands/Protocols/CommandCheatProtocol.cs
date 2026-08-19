/*
 * Imlight
 * Copyright (C) 2026 Revive101
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
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandCheatProtocol : CommandProtocol {

    internal override string Group { get; set; } = "cheat";

    [Command("win")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void InstaWinCommand() 
        => Context.SessionActor.Tell(new COMBAT_106_PROTOCOL.MSG_CHEATINSTAWIN());

    [Command("cinematic")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void ToggleInstantCinematicsCommand() 
        => Context.SessionActor.Tell(new COMBAT_106_PROTOCOL.MSG_CHEATTOGGLECINEMATICS());

    [Command("nofizzle")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void ToggleNoFizzleCommand() 
        => Context.SessionActor.Tell(new COMBAT_106_PROTOCOL.MSG_CHEATTOGGLENOFIZZLE());

}
