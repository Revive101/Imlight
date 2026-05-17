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

using System.Collections.Generic;
using System.Linq;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.World;
using Raven.Client.Documents;

namespace Imlight.CoreLib.WizardData.Collections;

public static class CreatureSpellbookCollection {

    public const string CollectionName = "CreatureSpellbook";
    private static readonly IDocumentStore s_store;
    private static readonly uint[] s_defaultSpellIds = [
        84361,      // Imp
        2062265892, // Thundersnake
        1496157882, // Frostbeetle
        2143810477, // Scarab
        1731857280, // Dark sprite
        1067010286, // Bloodbat
    ];
    private static readonly List<CreatureSpellbook> s_spellbooks = [];

    static CreatureSpellbookCollection() {
        s_store = WorldDatabase.Instance.Store;
    }

    /// <summary>
    /// Retrieves a creature spellbook by deck name.
    /// </summary>
    /// <param name="deckName">The name of the deck.</param>
    /// <returns>The creature spellbook with the specified deck name, or null if not found.</returns>
    public static CreatureSpellbook GetCreatureSpellbook(string deckName) {
        using var session = s_store.OpenSession();

        // Check if the creature spellbook is already loaded.
        var cachedSpellbook = s_spellbooks.FirstOrDefault(x => x.DeckName == deckName);
        if (cachedSpellbook != null) {
            return cachedSpellbook;
        }

        var creatureSpellbook = session.Query<CreatureSpellbook>(collectionName: CollectionName)
            .FirstOrDefault(x => x.DeckName == deckName);

        return creatureSpellbook;
    }

    /// <summary>
    /// Adds a creature spellbook to the collection.
    /// </summary>
    /// <param name="creatureSpellbook">The creature spellbook to add.</param>
    public static void AddCreatureSpellbook(CreatureSpellbook creatureSpellbook) {
        using var session = s_store.OpenSession();

        session.Store(creatureSpellbook);
        var metaData = session.Advanced.GetMetadataFor(creatureSpellbook);
        metaData[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        session.SaveChanges();
    }

    /// <summary>
    /// Retrieves the default creature spellbook.
    /// </summary>
    public static CreatureSpellbook GetDefaultCreatureSpellbook()
        => new("Default", s_defaultSpellIds);

    /// <summary>
    /// Preloads all creature spellbooks.
    /// </summary>
    public static void PreloadSpellbooks() {
        using var session = s_store.OpenSession();

        s_spellbooks.AddRange(session.Query<CreatureSpellbook>(collectionName: CollectionName));
    }

}
