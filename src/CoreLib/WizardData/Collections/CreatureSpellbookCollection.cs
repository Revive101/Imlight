/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.World;
using Raven.Client.Documents;

namespace Imlight.CoreLib.WizardData.Implementations;

public static class CreatureSpellbookCollection {
    public const string CollectionName = "CreatureSpellbook";
    private static readonly IDocumentStore s_store;
    private static readonly uint[] s_defaultSpellIds = new uint[] {
        84361,      // Imp
        2062265892, // Thundersnake
        1496157882, // Frostbeetle
        2143810477, // Scarab
        1731857280, // Dark sprite
        1067010286, // Bloodbat
    };

    static CreatureSpellbookCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    /// <summary>
    /// Retrieves a creature spellbook by deck name.
    /// </summary>
    /// <param name="deckName">The name of the deck.</param>
    /// <returns>The creature spellbook with the specified deck name, or null if not found.</returns>
    public static CreatureSpellbook GetCreatureSpellbook(string deckName) {
        using var session = s_store.OpenSession();

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
    /// <returns></returns>
    public static CreatureSpellbook GetDefaultCreatureSpellbook()
        => new("Default", s_defaultSpellIds);
}
