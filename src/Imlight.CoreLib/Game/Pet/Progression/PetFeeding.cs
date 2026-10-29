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
 * PET FEEDING
 * ========================================================================
 *
 * PURPOSE:
 * Feeds the equipped pet snacks from the snack bag for happiness: takes the snacks out of the bag,
 * raises the happiness and saves both. Nothing here sends; the caller does.
 *
 * USAGE EXAMPLE:
 * var result = PetFeeding.Feed(wizard, wizard.GetEquippedPetItem(), message.SnackTemplateID, message.FeedStack != 0);
 *
 * NOTE:
 * A snack gives the total of its stat changes as happiness, which is the number the feed window shows on
 * the snack's card. The stack button feeds snacks until the pet is full or the stack runs out.
 *
 * TODO:
 * - What the stack button should consume when the pet could eat more than it needs is unverified.
 * - Does a pet that is already full eat a snack? It does not here.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System.Linq;
using System;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Pet.Config;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Pet.Progression;

/// <summary>
/// Feeding a pet snacks for happiness.
/// </summary>
internal static class PetFeeding {

    /// <summary>
    /// Gets the happiness one snack gives.
    /// </summary>
    /// <param name="snack">The snack template.</param>
    /// <returns>The total of the snack's stat changes, 0 if it has none.</returns>
    internal static uint GetHappiness(PetSnackItemTemplate snack)
        => (uint) Math.Clamp(snack.m_statModifierSet?.m_modifications?.Sum(change => (long) change.m_change) ?? 0, 0, uint.MaxValue);

    /// <summary>
    /// Feeds a snack of the wizard's snack bag to a pet.
    /// </summary>
    /// <param name="wizard">The owner, whose snack bag the snacks come from.</param>
    /// <param name="pet">The pet item, which must be hatched.</param>
    /// <param name="snackTemplateId">The template id of the snack, which picks the stack in the bag.</param>
    /// <param name="wholeStack">True to feed snacks until the pet is full or the stack is empty, false to feed one.</param>
    /// <returns>What the pet ate. Nothing is eaten, and the bag and pet are left as they were, 
    /// if the bag lacks the snack, the snack gives no happiness, or the pet is full.</returns>
    internal static PetFeedResult Feed(Wizard wizard, WizClientObjectItem pet, uint snackTemplateId, bool wholeStack) {
        var stack = wizard.PetSnackBehavior?.GetSnack(snackTemplateId);
        if (stack is null || !PetProgression.TryGetPet(pet, out var behavior, out _) || behavior.m_level == 0) {
            return PetFeedResult.None;
        }

        if (CoreObjectFactory.GetCoreTemplate(snackTemplateId) is not PetSnackItemTemplate template) {
            Logger.Warning("The snack stack {SnackId} has the template {TemplateId}, which is not a pet snack.", 
                Logger.Args(stack.m_globalID.Full, snackTemplateId));

            return PetFeedResult.None;
        }

        PetProgression.EnsureInitialized(pet);
        var perSnack = GetHappiness(template);
        var room = behavior.m_maximumHappiness - Math.Min(behavior.m_happiness, behavior.m_maximumHappiness);
        if (perSnack == 0 || room == 0) {
            return PetFeedResult.None;
        }

        var needed = ((ulong) room + perSnack - 1) / perSnack;
        var count = (int) Math.Min((ulong) stack.m_quantity, wholeStack ? needed : 1);
        if (!wizard.RemoveSnack(stack.m_globalID, count, out var left)) {
            return PetFeedResult.None;
        }

        PetProgression.SetHappiness(pet, (uint) Math.Min(behavior.m_happiness + (ulong) perSnack * (uint) count, behavior.m_maximumHappiness));

        wizard.SavePet(pet);

        return new PetFeedResult(left, count);
    }

    /// <summary>
    /// Creates the message that shows the client what a feed took from the snack bag.
    /// </summary>
    /// <param name="wizard">The owner.</param>
    /// <param name="result">The feed.</param>
    /// <param name="update">The new quantity of the stack, or its removal once the last snack went.</param>
    /// <returns>False if the pet ate nothing.</returns>
    internal static bool TryCreateSnackBagUpdate(Wizard wizard, PetFeedResult result, out IMessage update) {
        update = null;
        if (!result.Fed) {
            return false;
        }

        update = result.Snack.m_quantity > 0
            ? new PET_9_PROTOCOL.MSG_PETSNACKUPDATE {
                GlobalID = wizard.GameObjectID,
                ItemID = result.Snack.m_globalID,
                Quantity = result.Snack.m_quantity
            }
            : new PET_9_PROTOCOL.MSG_PETSNACKREMOVE {
                GlobalID = wizard.GameObjectID,
                ItemID = result.Snack.m_globalID
            };

        return true;
    }

}
