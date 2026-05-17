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

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.WizardData.Databases;
using Raven.Client.Documents;
using System.Linq;

namespace Imlight.CoreLib.WizardData.Collections;

internal sealed class WizardReagentCollection {

    public const string CollectionName = "WizardReagents";
    private static readonly IDocumentStore s_store;

    static WizardReagentCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    /// <summary>
    /// Adds a reagent to reagent collection. Assumes that the caller has already
    /// added this particular reagent to the player's inventory.
    /// </summary>
    /// <param name="reagentItem">The reagent to add.</param>
    public static void AddReagent(ClientReagentItem reagentItem) {
        using var session = s_store.OpenSession();

        // Check if the player already has this reagent. If so, increase the quantity.
        var existingReagent = session.Query<ClientReagentItem>(collectionName: CollectionName)
            .FirstOrDefault(x => x.m_characterId == reagentItem.m_characterId
                              && x.m_templateID == reagentItem.m_templateID);
        if (existingReagent != null) {
            existingReagent.m_quantity += reagentItem.m_quantity;
            session.SaveChanges();

            return;
        }

        // Otherwise, add the reagent to the collection.
        session.Store(reagentItem);
        var metaData = session.Advanced.GetMetadataFor(reagentItem);
        metaData[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;


        session.SaveChanges();
    }

    /// <summary>
    /// Removes a reagent from the reagent collection. Assumes that the caller has already
    /// removed this particular reagent from the player's inventory.
    /// </summary>
    /// <param name="reagentItem">The reagent to remove.</param>
    public static void RemoveReagent(ClientReagentItem reagentItem) {
        using var session = s_store.OpenSession();

        // Check if the player has this reagent. If so, decrease the quantity.
        var existingReagent = session.Query<ClientReagentItem>(collectionName: CollectionName)
            .FirstOrDefault(x => x.m_characterId == reagentItem.m_characterId
                              && x.m_templateID == reagentItem.m_templateID);
        if (existingReagent is null) {
            return;
        }

        existingReagent.m_quantity -= reagentItem.m_quantity;

        if (existingReagent.m_quantity <= 0) {
            session.Delete(existingReagent);
        }

        session.SaveChanges();
    }

    /// <summary>
    /// Updates a reagent in the WizardReagent collection for a specific player.
    /// </summary>
    /// <param name="reagent">The updated reagent to be saved.</param>
    /// <returns>True if the reagent was successfully updated, false otherwise.</returns>
    public static bool UpdateReagent(ClientReagentItem reagent) {
        using var session = s_store.OpenSession();

        // Get the reagent from the reagents collection.
        var associatedReagent = session
            .Query<ClientReagentItem>(collectionName: CollectionName)
            .FirstOrDefault(x => x.m_characterId == reagent.m_characterId && x.m_globalID == reagent.m_globalID);

        // If the reagent was not found, return false.
        if (associatedReagent == null) {
            return false;
        }

        // Update the reagent in the reagents collection.
        associatedReagent.m_quantity = reagent.m_quantity;

        session.SaveChanges();
        return true;
    }

    /// <summary>
    /// Removes all reagents from the reagent collection for a given character.
    /// </summary>
    /// <param name="charId">The character ID.</param>
    public static void DeleteReagentBag(ulong charId) {
        using var session = s_store.OpenSession();

        var reagents = session.Query<ClientReagentItem>(collectionName: CollectionName)
            .Where(x => x.m_characterId == charId)
            .ToList();

        foreach (var reagent in reagents) {
            session.Delete(reagent);
        }

        session.SaveChanges();
    }

}
