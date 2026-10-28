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
 *
 * ========================================================================
 * PET ACTIVITY GUARD
 * ========================================================================
 *
 * PURPOSE:
 * Allows one pet activity at a time per wizard, for pet games now and the derby later.
 *
 * USAGE EXAMPLE:
 * if (!PetActivityGuard.TryBegin(wizard, PetActivity.Game, onAbort)) { return; }
 * ...
 * PetActivityGuard.End(wizard);
 *
 * NOTE:
 * End is for a normal finish and runs no abort callback. Abort is for a disconnect, zone change or logout, and
 * runs the callback given to TryBegin, which is where the session refunds the energy.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System;
using System.Collections.Concurrent;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Pet.Games;

/// <summary>
/// The pet activity each wizard is in, if any.
/// </summary>
internal static class PetActivityGuard {

    private sealed record ActivityEntry(PetActivity Activity, Action<Wizard> OnAbort);

    private static readonly ConcurrentDictionary<ulong, ActivityEntry> s_activities = new();

    /// <summary>
    /// Starts an activity for a wizard.
    /// </summary>
    /// <param name="wizard">The wizard.</param>
    /// <param name="activity">The activity to start.</param>
    /// <param name="onAbort">Runs if the activity is aborted, may be null.</param>
    /// <returns>False if the wizard is already in an activity.</returns>
    internal static bool TryBegin(Wizard wizard, PetActivity activity, Action<Wizard> onAbort = null)
        => s_activities.TryAdd(wizard.GameObjectID, new ActivityEntry(activity, onAbort));

    /// <summary>
    /// Finishes the wizard's activity normally.
    /// </summary>
    /// <param name="wizard">The wizard.</param>
    /// <returns>False if the wizard was in no activity.</returns>
    internal static bool End(Wizard wizard)
        => s_activities.TryRemove(wizard.GameObjectID, out _);

    /// <summary>
    /// Ends the wizard's activity because the wizard left it, and runs its abort callback.
    /// </summary>
    /// <param name="wizard">The wizard.</param>
    /// <returns>False if the wizard was in no activity.</returns>
    internal static bool Abort(Wizard wizard) {
        if (!s_activities.TryRemove(wizard.GameObjectID, out var entry)) {
            return false;
        }

        entry.OnAbort?.Invoke(wizard);

        return true;
    }

    /// <summary>
    /// Checks whether the wizard is in an activity.
    /// </summary>
    /// <param name="wizard">The wizard.</param>
    /// <param name="activity">The activity the wizard is in.</param>
    /// <returns>False if the wizard is in none.</returns>
    internal static bool TryGetActivity(Wizard wizard, out PetActivity activity) {
        activity = default;
        if (!s_activities.TryGetValue(wizard.GameObjectID, out var entry)) {
            return false;
        }

        activity = entry.Activity;

        return true;
    }

}
