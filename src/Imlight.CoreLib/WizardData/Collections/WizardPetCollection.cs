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
 * WIZARD PET COLLECTION
 * ========================================================================
 *
 * PURPOSE:
 * Stores the server-only counters of each pet, one document per pet.
 *
 * USAGE EXAMPLE:
 * var wins = WizardPetCollection.AddGameWin(charId, petGlobalId, game);
 *
 * NOTE:
 * Documents are created on the first win. A pet with no document has no wins.
 *
 * TODO:
 * 
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Collections.Generic;
using System.Linq;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Player;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace Imlight.CoreLib.WizardData.Collections;

public static class WizardPetCollection {

    public const string CollectionName = "WizardPets";
    private static readonly IDocumentStore s_store;

    static WizardPetCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    /// <summary>
    /// Gets how many times a pet won a game.
    /// </summary>
    /// <param name="charId">The owner's character id.</param>
    /// <param name="petGlobalId">The global id of the pet item.</param>
    /// <param name="game">The game name.</param>
    /// <returns>The number of wins, 0 if the pet never won.</returns>
    public static uint GetGamesWon(ulong charId, ulong petGlobalId, string game) {
        using var session = s_store.OpenSession();

        var record = Find(session, charId, petGlobalId);

        return record is not null && record.GamesWon.TryGetValue(game, out var wins) ? wins : 0;
    }

    /// <summary>
    /// Counts one more win for a pet.
    /// </summary>
    /// <param name="charId">The owner's character id.</param>
    /// <param name="petGlobalId">The global id of the pet item.</param>
    /// <param name="game">The game name.</param>
    /// <returns>The pet's wins in that game after this one.</returns>
    public static uint AddGameWin(ulong charId, ulong petGlobalId, string game) {
        using var session = s_store.OpenSession();

        var record = Find(session, charId, petGlobalId);
        if (record is null) {
            record = new WizardPetRecord { CharId = charId, PetGlobalId = petGlobalId };
            session.Store(record);

            var metadata = session.Advanced.GetMetadataFor(record);
            metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;
        }

        record.GamesWon[game] = record.GamesWon.GetValueOrDefault(game) + 1;
        session.SaveChanges();

        return record.GamesWon[game];
    }

    /// <summary>
    /// Deletes the record of a pet, for when the pet leaves the character.
    /// </summary>
    /// <param name="charId">The owner's character id.</param>
    /// <param name="petGlobalId">The global id of the pet item.</param>
    /// <returns>True if the pet had a record.</returns>
    public static bool Delete(ulong charId, ulong petGlobalId) {
        using var session = s_store.OpenSession();

        var record = Find(session, charId, petGlobalId);
        if (record is null) {
            return false;
        }

        session.Delete(record);
        session.SaveChanges();

        return true;
    }

    /// <summary>
    /// Deletes the records of every pet of a character, for when the character is deleted.
    /// </summary>
    /// <param name="charId">The owner's character id.</param>
    /// <returns>True if the character had any records to delete.</returns>
    public static bool DeleteAll(ulong charId) {
        using var session = s_store.OpenSession();

        var records = session.Query<WizardPetRecord>(collectionName: CollectionName)
            .Customize(query => query.WaitForNonStaleResults())
            .Where(x => x.CharId == charId)
            .ToList();
        if (records.Count == 0) {
            return false;
        }

        foreach (var record in records) {
            session.Delete(record);
        }

        session.SaveChanges();

        return true;
    }

    private static WizardPetRecord Find(IDocumentSession session, ulong charId, ulong petGlobalId)
        => session.Query<WizardPetRecord>(collectionName: CollectionName)
            .Customize(query => query.WaitForNonStaleResults())
            .FirstOrDefault(x => x.CharId == charId && x.PetGlobalId == petGlobalId);

}
