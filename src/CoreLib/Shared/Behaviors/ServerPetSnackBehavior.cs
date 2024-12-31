/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Imlight.Common;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerPetSnackBehavior : ServerBehaviorInstance {
    [JsonIgnore] public override bool NoTransfer { get; set; } = false;

    private static readonly int s_maxSnacksAllowed = 999;

    public List<ClientPetSnackItem> Snacks { get; set; }

    /// <summary>
    /// Adds a snack to the player's snack bag.
    /// </summary>
    /// <param name="snack">The snack to be added.</param>
    /// <returns>True if the snack was successfully added, false otherwise.</returns>
    public bool AddSnack(ClientPetSnackItem snack) {
        var totalSnacks = 0;
        foreach (var s in Snacks) {
            totalSnacks += s.m_quantity;
        }

        if (totalSnacks >= s_maxSnacksAllowed) {
            Logger.Debug("Player snack bag is full. Cannot add snack with global id {0}.", Logger.Args(snack.m_globalID));
            return false;
        }

        // Check if the snack is already in the snack bag, update quantity
        var existingSnack = Snacks.FirstOrDefault(x => x.m_templateID == snack.m_templateID);
        if (existingSnack is not null) {
            existingSnack.m_quantity++;
            return true;
        }

        Snacks.Add(snack);
        return true;
    }

    /// <summary>
    /// Removes a snack from the player's snack bag based on its unique identifier.
    /// </summary>
    /// <param name="snackId">The unique identifier of the snack to be removed.</param>
    /// <returns><c>true</c> if the snack was successfully removed; otherwise, <c>false</c>.</returns>
    public bool RemoveSnack(ulong snackId, out ClientPetSnackItem updatedSnack) {
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

        return true;
    }

    /// <summary>
    /// Checks if the snack bag contains a snack with the specified global ID.
    /// </summary>
    /// <param name="globalId">The global ID of the snack to check.</param>
    /// <returns>True if the snack bag contains an snack with the specified global ID, otherwise false.</returns>
    public bool HasSnackID(ulong globalId) => Snacks.Any(snack => snack.m_globalID == globalId);

    /// <summary>
    /// Checks if the snack bag contains a snack with the specified template ID.
    /// </summary>
    /// <param name="templateId">The template ID of the snack to check.</param>
    /// <returns>True if the snack bag contains an snack with the specified template ID, otherwise false.</returns>
    public bool HasSnack(ulong templateId) => Snacks.Any(snack => snack.m_templateID == templateId);

    /// <summary>
    /// Returns the snack with the specified template ID.
    /// </summary>
    /// <param name="templateId">The template ID of the snack to check.</param>
    /// <returns>Returns the snack object with the specified template ID, otherwise null.</returns>
    public ClientPetSnackItem GetSnack(ulong templateId) => Snacks.FirstOrDefault(snack => snack.m_templateID == templateId);

    public override ClientPetSnackBehavior GetClientBehaviorInstance() {
        // todo: Clean this up, testing purposes only currently
        Snacks = new List<ClientPetSnackItem>();

        ClientPetSnackBehavior behavior = new ClientPetSnackBehavior();
        behavior.m_snackBag = new ObjectBag();
        behavior.m_snackBag.m_maxItemStack = s_maxSnacksAllowed;
        behavior.m_snackBag.m_itemList = Snacks.ConvertAll(snack => (CoreObject) snack);
        return behavior;
    }
}
