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
 *
 * ========================================================================
 * HOUSING SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * A chat command that gives the caller a house of any template, so every house zone can be reached and tested.
 * 
 * USAGE EXAMPLE:
 * .housegrant <templateId>
 * 
 * NOTE:
 * - The template is a deed or a structure. A deed that names a structure brings the structure too.
 * - The house is created when the character first enters it.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Globalization;
using Imlight.CoreLib.Game.Housing.Houses;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandHousing : CommandProtocol {

    internal override string Group { get; set; } = "";

    [Command("housegrant")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void HouseGrantCommand(string templateId) {
        if (!uint.TryParse(templateId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || !HouseTemplates.TryGet(id, out var template)) {
            InformSenderClient($"Template {templateId} is not a house.");

            return;
        }

        var wizard = Context.Character;
        var grant = HouseDeeds.Issue(wizard, id);
        foreach (var item in grant.Created) {
            ItemSync.SendAdded(Context.SessionActor, wizard, item);
        }

        if (!grant.Succeeded) {
            InformSenderClient(grant.Failure == HouseGrantFailure.BackpackFull
                ? "Your backpack is too full for that house."
                : $"Could not give you house {id}.");

            return;
        }

        InformSenderClient(grant.Created.Count == 0
            ? $"You already have house {id}, zone {template.LotZone}."
            : $"Gave you {grant.Created.Count} house item(s) for zone {template.LotZone}.");
    }

}
