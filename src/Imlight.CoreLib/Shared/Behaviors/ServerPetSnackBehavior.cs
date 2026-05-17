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

using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Imlight.Common;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerPetSnackBehavior : IClientBehaviorProvider<ClientPetSnackBehavior> {

    [JsonIgnore] public bool NoTransfer { get; set; } = false;

    private static readonly int s_maxSnackStackAllowed = 999;

    public List<ulong> SnackItemIds { get; set; }

    [JsonIgnore] public List<ClientPetSnackItem> Snacks { get; set; }

    /// <summary>
    /// Adds a snack to the player's snack bag.
    /// </summary>
    /// <param name="snack">The snack to be added.</param>
    /// <returns>True if the snack was successfully added, false otherwise.</returns>
    public bool AddSnack(ClientPetSnackItem snack) {
        Snacks ??= [];
        SnackItemIds ??= [];

        // Check if the snack is already in the snack bag, update quantity
        var existingSnack = Snacks.FirstOrDefault(x => x.m_templateID == snack.m_templateID);
        if (existingSnack is not null) {
            if (existingSnack.m_quantity >= s_maxSnackStackAllowed) {
                Logger.Debug("Player snack stack is full. Cannot add snack with global id {0}.", Logger.Args(snack.m_globalID));

                return false;
            }

            existingSnack.m_quantity++;

            return true;
        }

        SnackItemIds.Add(snack.m_globalID);
        Snacks.Add(snack);

        return true;
    }

    /// <summary>
    /// Removes a snack from the player's snack bag based on its unique identifier.
    /// </summary>
    /// <param name="snackId">The unique identifier of the snack to be removed.</param>
    /// <returns><c>true</c> if the snack was successfully removed; otherwise, <c>false</c>.</returns>
    public bool RemoveSnack(ulong snackId, out ClientPetSnackItem updatedSnack) {
        Snacks ??= [];
        SnackItemIds ??= [];
        updatedSnack = null;

        // Get the actual item from the inventory.
        var snack = Snacks.FirstOrDefault(x => x.m_globalID == snackId);
        if (snack is null) {
            Logger.Debug("Tried to remove snack with global id {0} that does not exist in player snack bag.",
                Logger.Args(snackId));

            return false;
        }

        snack.m_quantity--;
        updatedSnack = snack;

        if (snack.m_quantity <= 0) {
            if (!Snacks.Remove(snack)) {
                Logger.Debug("Tried to remove snack with global id {0} that does not exist in player inventory.",
                    Logger.Args(snack.m_globalID));

                return false;
            }

            if (!SnackItemIds.Remove(snackId)) {
                Logger.Debug("Tried to remove snack with global id {0} that does not exist in player inventory.",
                    Logger.Args(snack.m_globalID));

                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if the snack bag contains a snack with the specified global ID.
    /// </summary>
    /// <param name="globalId">The global ID of the snack to check.</param>
    /// <returns>True if the snack bag contains an snack with the specified global ID, otherwise false.</returns>
    public bool HasSnackID(ulong globalId) 
        => Snacks?.Any(snack => snack.m_globalID == globalId) ?? false;

    /// <summary>
    /// Checks if the snack bag contains a snack with the specified template ID.
    /// </summary>
    /// <param name="templateId">The template ID of the snack to check.</param>
    /// <returns>True if the snack bag contains an snack with the specified template ID, otherwise false.</returns>
    public bool HasSnack(ulong templateId) 
        => Snacks?.Any(snack => snack.m_templateID == templateId) ?? false;

    /// <summary>
    /// Returns the snack with the specified template ID.
    /// </summary>
    /// <param name="templateId">The template ID of the snack to get.</param>
    /// <returns>Returns the snack object with the specified template ID, otherwise null.</returns>
    public ClientPetSnackItem GetSnack(ulong templateId) 
        => Snacks?.FirstOrDefault(snack => snack.m_templateID == templateId) ?? null;

    public ClientPetSnackBehavior GetClientBehaviorInstance() => new() {
        m_snackBag = new ObjectBag() {
            m_maxItemStack = s_maxSnackStackAllowed,
            m_itemList = Snacks?.ConvertAll(item => (CoreObject) item) ?? []
        }
    };

}
