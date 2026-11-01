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
 * PET HATCHING
 * ========================================================================
 *
 * PURPOSE:
 * Gives a wizard the egg two parents make: the egg goes into the inventory and a morphing slot, is
 * saved and shown, and the PetService starts its hatch timer. Also lists the pets a wizard can
 * hatch with and prices a hatch.
 *
 * USAGE EXAMPLE:
 * if (PetHatching.TryLayEgg(session, wizard, firstParent, secondParent, out var egg)) { ... }
 *
 * NOTE:
 * Used by the hatching room and the hatch command. Gold is not taken here.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/22/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Pet.Config;
using Imlight.CoreLib.Game.Pet.Progression;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Pet.Hatching;

/// <summary>
/// Lays eggs and answers what a wizard can hatch with.
/// </summary>
internal static class PetHatching {

    /// <summary>
    /// Gets the pets a wizard can hatch with: hatched pets in the inventory or equipped, at least the youngest parent level.
    /// </summary>
    /// <param name="wizard">The wizard.</param>
    /// <returns>The pet items, equipped pet first.</returns>
    internal static List<WizClientObjectItem> GetParentCandidates(Wizard wizard) {
        var items = (wizard.EquipmentBehavior?.EquippedItems ?? []).Concat(wizard.InventoryBehavior?.Items ?? []);

        return [.. items.Where(IsParentCandidate)];
    }

    /// <summary>
    /// Finds one of a wizard's possible parents by its global id.
    /// </summary>
    /// <param name="wizard">The wizard.</param>
    /// <param name="petId">The global id of the pet item.</param>
    /// <returns>The pet item, or null if the wizard has no such parent.</returns>
    internal static WizClientObjectItem FindParentCandidate(Wizard wizard, ulong petId)
        => GetParentCandidates(wizard).FirstOrDefault(pet => pet.m_globalID == petId);

    /// <summary>
    /// Gets the gold a hatching wizard pays for an egg of two parents.
    /// </summary>
    /// <param name="firstParent">One parent pet.</param>
    /// <param name="secondParent">The other parent pet.</param>
    /// <returns>The [Hatching] gold cost, or the game's in-person cost when that is not set.</returns>
    internal static int GetGoldCost(WizClientObjectItem firstParent, WizClientObjectItem secondParent) {
        // todo: does each hatching player pay the pair's whole price?
        if (HatchingRules.GoldCost is { } goldCost) {
            return goldCost;
        }

        if (!PetProgression.TryGetPet(firstParent, out var first, out _) || !PetProgression.TryGetPet(secondParent, out var second, out _)) {
            return 0;
        }

        return PetMorphingConfig.GetInPersonHatchCost(first, second);
    }

    /// <summary>
    /// Gives a wizard the egg two parents make. The egg is stored, shown to the client and put in a morphing slot.
    /// </summary>
    /// <param name="session">The wizard's session.</param>
    /// <param name="wizard">The wizard that gets the egg.</param>
    /// <param name="firstParent">One parent pet, which may belong to another wizard.</param>
    /// <param name="secondParent">The other parent pet.</param>
    /// <param name="egg">The egg item.</param>
    /// <returns>False if the wizard has no free morphing slot or the egg could not be made or stored.</returns>
    internal static bool TryLayEgg(IActorRef session, Wizard wizard, WizClientObjectItem firstParent, WizClientObjectItem secondParent,
                                   out WizClientObjectItem egg) {
        egg = null;
        if (!wizard.PetOwnerBehavior.HasFreeMorphingSlot()) {
            return false;
        }

        egg = PetBreeding.CreateEgg(wizard.CharId, firstParent, secondParent, Random.Shared);
        if (egg is null || !wizard.AddPetToInventory(egg)) {
            Logger.Warning("Could not give {CharId} the egg of pets {FirstPet} and {SecondPet}.",
                Logger.Args(wizard.CharId, firstParent.m_globalID.Full, secondParent.m_globalID.Full));

            return false;
        }

        if (!ItemSync.SendAdded(session, wizard, egg)) {
            wizard.RemoveItemFromInventory(egg.m_globalID);
            WizardItemCollection.RemoveItem(wizard.CharId, egg.m_globalID);

            return false;
        }

        wizard.PetOwnerBehavior.CreatePetEgg(egg.m_templateID.Full, GetSecondsToHatch(egg), egg.m_globalID.Full);
        WizardCollection.UpdateCharacterPetOwnerBehavior(wizard);
        session.Tell(new CHARACTER_103_PROTOCOL.MSG_PETEGGLAID { EggGlobalId = egg.m_globalID });

        Logger.Information("{CharId} got the egg {EggId} of template {TemplateId}.",
            Logger.Args(wizard.CharId, egg.m_globalID.Full, egg.m_templateID.Full));

        return true;
    }

    /// <summary>
    /// Gets the seconds until an egg hatches.
    /// </summary>
    /// <param name="egg">The egg item.</param>
    /// <returns>The seconds left, zero if the egg is ready.</returns>
    internal static uint GetSecondsToHatch(WizClientObjectItem egg) {
        if (!PetProgression.TryGetPet(egg, out var behavior, out _)) {
            return 0;
        }

        return (uint) Math.Max(0, behavior.m_hatchedTimeSecs - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    private static bool IsParentCandidate(WizClientObjectItem item)
        => item is not null
            && PetFactory.IsPetTemplate((uint) item.m_templateID)
            && PetProgression.TryGetPet(item, out var behavior, out _)
            && behavior.m_level >= HatchingRules.MinParentLevel;

}
