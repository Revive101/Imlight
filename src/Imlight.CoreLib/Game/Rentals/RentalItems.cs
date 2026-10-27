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
 * RENTAL ITEMS
 * ========================================================================
 *
 * PURPOSE:
 *
 * USAGE EXAMPLE:
 *
 * NOTE:
 * m_expireTime on the item instance is an absolute Unix time in seconds. 
 * Unset (0xFFFFFFFF) is the timer that has not started, the value the client keys its "Equip to Start" prompt on; 
 * 0 is an item from before the default existed and counts as not started too.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Rentals;

internal static class RentalItems {

    internal const uint UNSET = uint.MaxValue;
    private const long MINIMUM_CHECK_SECONDS = 5;
    private const long MAXIMUM_CHECK_SECONDS = 3600;

    internal static bool TryGetBehavior(WizClientObjectItem item, out ClientRentalBehavior behavior) {
        if (item?.m_inactiveBehaviors is null) {
            behavior = null;

            return false;
        }

        return CoreObjectFactory.FindBehaviorInstance(item, out behavior);
    }

    internal static bool TryGetTemplate(WizClientObjectItem item, out RentalBehaviorTemplate rentalTemplate, out WizItemTemplate template) {
        template = item is null ? null : ItemHelper.GetItemTemplate(item);

        return TryGetRentalTemplate(template, out rentalTemplate);
    }

    internal static bool TryGetRentalTemplate(WizItemTemplate template, out RentalBehaviorTemplate rentalTemplate) {
        rentalTemplate = template?.m_behaviors?
            .OfType<RentalBehaviorTemplate>()
            .FirstOrDefault();

        return rentalTemplate is not null;
    }

    /// <summary>
    /// Computes the absolute expiry of a rental whose timer starts now.
    /// </summary>
    /// <param name="rentalTemplate">The rental template that holds the duration.</param>
    /// <param name="now">The start time in Unix seconds.</param>
    /// <param name="expireTime">The expiry in Unix seconds, capped below the not started value.</param>
    /// <returns>False if the template is not a calendar rental or its duration cannot be read.</returns>
    internal static bool TryGetExpireTime(RentalBehaviorTemplate rentalTemplate, long now, out uint expireTime) {
        expireTime = UNSET;
        if (rentalTemplate.m_timerType != TimerType.TimerType_Calendar
            || !RentalDuration.TryParse(rentalTemplate.m_expireTime, out var duration)) {
            return false;
        }

        expireTime = (uint) Math.Min(now + (long) duration.TotalSeconds, UNSET - 1L);

        return true;
    }

    /// <summary>
    /// Computes how long to wait before looking at the rentals again.
    /// </summary>
    /// <param name="nextExpireTime">The earliest expiry of the rentals that are still running, in Unix seconds.</param>
    /// <param name="now">The current time in Unix seconds.</param>
    /// <returns>The time to the expiry plus one second, held between five seconds and one hour.</returns>
    internal static TimeSpan CheckDelay(long nextExpireTime, long now)
        => TimeSpan.FromSeconds(Math.Clamp(nextExpireTime - now + 1, MINIMUM_CHECK_SECONDS, MAXIMUM_CHECK_SECONDS));

    internal static bool IsStarted(ClientRentalBehavior behavior)
        => behavior.m_expireTime != UNSET && behavior.m_expireTime != 0;

    internal static bool IsDue(ClientRentalBehavior behavior, long now)
        => IsStarted(behavior) && behavior.m_expireTime <= now;

    /// <summary>
    /// Lists every item of the wizard, in the backpack and worn, that carries a rental behavior instance.
    /// </summary>
    /// <param name="wizard">The wizard to search.</param>
    /// <returns>A snapshot, so callers may remove items while they loop over it.</returns>
    internal static List<(WizClientObjectItem Item, ClientRentalBehavior Behavior)> Enumerate(Wizard wizard) {
        var rentals = new List<(WizClientObjectItem, ClientRentalBehavior)>();
        foreach (var item in AllItems(wizard)) {
            if (TryGetBehavior(item, out var behavior)) {
                rentals.Add((item, behavior));
            }
        }

        return rentals;
    }

    /// <summary>
    /// Checks whether the backpack or the worn items hold a rental that shares a type string with the given rental.
    /// </summary>
    /// <param name="wizard">The wizard to search.</param>
    /// <param name="candidate">The rental template of the item about to be acquired.</param>
    /// <returns>True if one type string of the candidate is also in the type list of an owned rental.</returns>
    internal static bool OwnsType(Wizard wizard, RentalBehaviorTemplate candidate) {
        if (candidate?.m_typeList is null || candidate.m_typeList.Count == 0) {
            return false;
        }

        foreach (var item in AllItems(wizard)) {
            if (!TryGetTemplate(item, out var ownedTemplate, out _) || ownedTemplate.m_typeList is null) {
                continue;
            }

            if (ownedTemplate.m_typeList.Any(candidate.m_typeList.Contains)) {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<WizClientObjectItem> AllItems(Wizard wizard) {
        var inventory = wizard.InventoryBehavior?.Items;
        var equipped = wizard.EquipmentBehavior?.EquippedItems;

        return (inventory ?? []).Concat(equipped ?? []).ToList();
    }

}
