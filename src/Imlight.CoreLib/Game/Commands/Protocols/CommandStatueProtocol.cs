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

using System.Globalization;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandStatue : CommandProtocol {

    internal override string Group { get; set; } = "statue";

    [Command("here")]
    [AuthRequired(AuthLevel.Administrator)]
    private void StatueHereCommand() {
        var location = Context.Character.Location;
        var orientation = Context.Character.Orientation;

        InformSenderClient(string.Create(
            CultureInfo.InvariantCulture,
            $"Zone: {Context.Character.Zone}\nLocation: {location.X}, {location.Y}, {location.Z}\nOrientation: {orientation.X}, {orientation.Y}, {orientation.Z}"),
            true);
    }

}
