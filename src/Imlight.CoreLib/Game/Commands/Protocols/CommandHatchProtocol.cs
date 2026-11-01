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
 * HATCH COMMAND
 * ========================================================================
 *
 * PURPOSE:
 * Quality assurance command that hatches two of the wizard's own pets without the hatchery: it lists
 * the pets that can be parents, or lays the egg of two of them.
 *
 * USAGE EXAMPLE:
 * hatch
 * hatch 1 2
 *
 * NOTE:
 * A pet is named by its number in the list, its global id or its name without spaces. No gold is taken.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/22/2026
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Pet.Hatching;
using Imlight.CoreLib.Game.Pet.Progression;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandHatchProtocol : CommandProtocol {

    private const int PARENTS_PER_EGG = 2;

    internal override string Group { get; set; } = "";

    [Command("hatch")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void HatchCommand([Remainder] string parents = "") {
        var candidates = PetHatching.GetParentCandidates(Context.Character);
        var names = parents.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (names.Length == 0) {
            ListParents(candidates);

            return;
        }

        if (names.Length != PARENTS_PER_EGG) {
            InformSenderClient("Name two pets: hatch (pet) (pet). Type hatch alone to list them.");

            return;
        }

        if (!TryFindParent(candidates, names[0], out var first) || !TryFindParent(candidates, names[1], out var second)) {
            return;
        }

        LayEgg(first, second);
    }

    private void LayEgg(WizClientObjectItem first, WizClientObjectItem second) {
        if (first.m_globalID == second.m_globalID) {
            InformSenderClient("Pick two different pets.");

            return;
        }

        if (!Context.Character.PetOwnerBehavior.HasFreeMorphingSlot()) {
            InformSenderClient("Your last egg has not hatched yet.");

            return;
        }

        if (!PetHatching.TryLayEgg(Context.SessionActor, Context.Character, first, second, out var egg)) {
            InformSenderClient("The egg could not be made.");

            return;
        }

        var minutes = Math.Ceiling(TimeSpan.FromSeconds(PetHatching.GetSecondsToHatch(egg)).TotalMinutes);
        InformSenderClient($"{GetName(first)} and {GetName(second)} made {GetName(egg)}, which hatches in {minutes} minutes.");
    }

    private void ListParents(List<WizClientObjectItem> candidates) {
        if (candidates.Count == 0) {
            InformSenderClient("None of your pets is old enough to hatch.");

            return;
        }

        var list = new StringBuilder("Pets that can hatch:");
        for (var i = 0; i < candidates.Count; i++) {
            var pet = candidates[i];
            PetProgression.TryGetPet(pet, out var behavior, out _);
            list.Append(CultureInfo.InvariantCulture, $"\n{i + 1}. {GetName(pet)}, level {behavior?.m_level}, id {pet.m_globalID.Full}");
        }

        InformSenderClient(list.ToString());
    }

    private bool TryFindParent(List<WizClientObjectItem> candidates, string name, out WizClientObjectItem pet) {
        pet = FindByNumber(candidates, name)
            ?? candidates.FirstOrDefault(candidate => candidate.m_globalID.Full.ToString(CultureInfo.InvariantCulture) == name)
            ?? candidates.FirstOrDefault(candidate => string.Equals(GetName(candidate).Replace(" ", ""), name, StringComparison.OrdinalIgnoreCase));
        if (pet is null) {
            InformSenderClient($"You have no pet {name} that can hatch.");
        }

        return pet is not null;
    }

    private static WizClientObjectItem FindByNumber(List<WizClientObjectItem> candidates, string name)
        => int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 1 && number <= candidates.Count
            ? candidates[number - 1]
            : null;

    private static string GetName(WizClientObjectItem pet)
        => CoreObjectFactory.FindBehaviorInstance<ClientPetNameBehavior>(pet, out var name)
            ? WizardNameBank.GetPetEnglishName(name.m_nameKeys)
            : pet.m_globalID.Full.ToString(CultureInfo.InvariantCulture);

}
