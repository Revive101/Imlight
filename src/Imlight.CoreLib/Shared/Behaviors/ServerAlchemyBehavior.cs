/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Imlight.Common;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerAlchemyBehavior : IClientBehaviorProvider<ClientAlchemyBehavior> {

    [JsonIgnore] public bool NoTransfer { get; set; } = false;

    private static readonly int s_maxReagentStackAllowed = 999;

    public List<ulong> ReagentItemIds { get; set; }

    [JsonIgnore] public List<ClientReagentItem> Reagents { get; set; }
    [JsonIgnore] public List<CraftingSlot> CraftingSlots { get; set; }
    [JsonIgnore] public List<Recipe> Recipes { get; set; }

    /// <summary>
    /// Adds a reagent to the player's reagent bag.
    /// </summary>
    /// <param name="reagent">The reagent object to be added.</param>
    /// <returns><c>true</c> if the reagent was successfully added, <c>false</c> otherwise.</returns>
    public bool AddReagent(ClientReagentItem reagent) {
        Reagents ??= [];
        ReagentItemIds ??= [];

        // Check if the reagent is already in the reagent bag, update quantity
        var existingReagent = Reagents.FirstOrDefault(x => x.m_templateID == reagent.m_templateID);
        if (existingReagent is not null) {
            if (existingReagent.m_quantity >= s_maxReagentStackAllowed) {
                Logger.Debug("Player reagent stack is full. Cannot add reagent with global id {0}.", Logger.Args(reagent.m_globalID));

                return false;
            }

            existingReagent.m_quantity++;

            return true;
        }

        ReagentItemIds.Add(reagent.m_globalID);
        Reagents.Add(reagent);

        return true;
    }

    /// <summary>
    /// Removes a reagent from the player's reagent bag based on its unique identifier.
    /// </summary>
    /// <param name="reagentId">The unique identifier of the reagent to be removed.</param>
    /// <returns><c>true</c> if the reagent was successfully removed; otherwise, <c>false</c>.</returns>
    public bool RemoveReagent(ulong reagentId, out ClientReagentItem updatedReagent) {
        Reagents ??= [];
        ReagentItemIds ??= [];
        updatedReagent = null;

        // Get the actual item from the inventory.
        var reagent = Reagents.FirstOrDefault(x => x.m_globalID == reagentId);
        if (reagent is null) {
            Logger.Debug("Tried to remove reagent with global id {0} that does not exist in player reagent bag.",
                Logger.Args(reagentId));

            return false;
        }

        reagent.m_quantity--;
        updatedReagent = reagent;

        if (reagent.m_quantity <= 0) {
            if (!Reagents.Remove(reagent)) {
                Logger.Debug("Tried to remove reagent with global id {0} that does not exist in player inventory.",
                    Logger.Args(reagent.m_globalID));

                return false;
            }

            if (!ReagentItemIds.Remove(reagentId)) {
                Logger.Debug("Tried to remove reagent with global id {0} that does not exist in player inventory.",
                    Logger.Args(reagent.m_globalID));
                    
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if the reagent bag contains a reagent with the specified global ID.
    /// </summary>
    /// <param name="globalId">The global ID of the reagent to check.</param>
    /// <returns>True if the reagent bag contains an reagent with the specified global ID, otherwise false.</returns>
    public bool HasReagentID(ulong globalId) => Reagents?.Any(reagent => reagent.m_globalID == globalId) ?? false;

    /// <summary>
    /// Checks if the reagent bag contains a reagent with the specified template ID.
    /// </summary>
    /// <param name="templateId">The template ID of the reagent to check.</param>
    /// <returns>True if the reagent bag contains an reagent with the specified template ID, otherwise false.</returns>
    public bool HasReageant(ulong templateId) => Reagents?.Any(reagent => reagent.m_templateID == templateId) ?? false;

    /// <summary>
    /// Returns the reagent with the specified template ID.
    /// </summary>
    /// <param name="templateId">The template ID of the reagent to get.</param>
    /// <returns>Returns the reagent object with the specified template ID, otherwise null.</returns>
    public ClientReagentItem GetReagent(ulong templateId) => Reagents?.FirstOrDefault(reagent => reagent.m_templateID == templateId) ?? null;

    public ClientAlchemyBehavior GetClientBehaviorInstance() => new() {
        m_reagentBag = new ObjectBag() {
            m_maxItemStack = s_maxReagentStackAllowed,
            m_itemList = Reagents?.ConvertAll(item => (CoreObject) item) ?? []
        },
        m_craftingSlotsBag = new ObjectBag() {
            m_maxItemStack = 1,
            m_itemList = CraftingSlots?.ConvertAll(item => (CoreObject) item) ?? []
        },
        m_recipeBag = new RecipeBag() {
            m_maxItemStack = 1,
            m_itemList = Recipes?.ConvertAll(item => (CoreObject) item) ?? []
        },
        m_maxReagentStack = s_maxReagentStackAllowed
    };

}
