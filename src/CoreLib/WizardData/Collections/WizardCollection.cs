/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Player;
using Raven.Client.Documents;
using SharpDX;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Implementations;

public static class WizardCollection {
    public const string CollectionName = "Wizards";
    private static readonly IDocumentStore s_store;

    static WizardCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    /// <summary>
    /// Creates a character in the database.
    /// </summary>
    /// <param name="character"></param>
    public static bool AddCharacter(Wizard character) {
        using var session = s_store.OpenSession();

        // Return false if the character already exists in the database.
        var existingCharacter = session.Query<Wizard>()
            .FirstOrDefault(x => x.CharId == character.CharId);
        if (existingCharacter is not null) {
            return false;
        }

        session.Store(character);
        var metadata = session.Advanced.GetMetadataFor(character);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        session.SaveChanges();
        return true;
    }

    /// <summary>
    /// Updates a character in the database.
    /// </summary>
    /// <param name="id"></param>
    public static bool DeleteCharacter(ulong id) {
        using var session = s_store.OpenSession();

        var character = session.Query<Wizard>()
            .FirstOrDefault(x => x.CharId == id);
        if (character is null) {
            return false;
        }

        session.Delete(character);
        session.SaveChanges();
        return true;
    }

    /// <summary>
    /// Retrieves a character from the database based on the specified ID.
    /// </summary>
    /// <param name="id">The ID of the character to retrieve.</param>
    /// <returns>The character with the specified ID, or null if not found.</returns>
    public static Wizard GetCharacter(ulong id) {
        using var session = s_store.OpenSession();

        var character = session.Query<Wizard>(collectionName: CollectionName)
            .Include(i => i.InventoryBehavior.InventoryItemIds)
            .FirstOrDefault(x => x.CharId == id);

        // Get each of the items for this character.
        if (character is not null) {
            var items = session.Query<WizClientObjectItem>(collectionName: WizardItemCollection.CollectionName)
                .Where(x => x.m_characterId == id)
                .ToList();
            character.InventoryBehavior.Items = items.ToList();
        }

        return character;
    }

    /// <summary>
    /// Updates the zone information for a character.
    /// </summary>
    /// <param name="character">The character to update.</param>
    /// <param name="zoneName">The name of the zone.</param>
    /// <param name="zoneDisplayName">The display name of the zone.</param>
    public static void UpdateCharacterZone(Wizard character, string zoneName, string zoneDisplayName) {
        using var session = s_store.OpenSession();

        var existingCharacter = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == character.CharId);
        if (existingCharacter is null) {
            return;
        }

        existingCharacter.Zone = zoneName;
        existingCharacter.ZoneDisplayName = zoneDisplayName;
        session.SaveChanges();
    }

    /// <summary>
    /// Updates the location and orientation of a character.
    /// </summary>
    /// <param name="character">The character to update.</param>
    /// <param name="location">The new location of the character.</param>
    /// <param name="orientation">The new orientation of the character.</param>
    public static void UpdateCharacterLocation(Wizard character, Vector3 location, float orientation) {
        using var session = s_store.OpenSession();

        var existingCharacter = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == character.CharId);
        if (existingCharacter is null) {
            return;
        }

        existingCharacter.Location = location;
        existingCharacter.Orientation = new Vector3(0, 0, orientation);
        session.SaveChanges();
    }

    /// <summary>
    /// Updates the equipment of a character in the wizard collection.
    /// </summary>
    /// <param name="wizard">The wizard object containing the updated equipment.</param>
    public static void UpdateCharacterItems(Wizard wizard) {
        using var session = s_store.OpenSession();

        var existingCharacter = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == wizard.CharId);
        if (existingCharacter is null) {
            return;
        }

        existingCharacter.InventoryBehavior = wizard.InventoryBehavior;
        existingCharacter.EquipmentBehavior = wizard.EquipmentBehavior;
        session.SaveChanges();
    }

    /// <summary>
    /// Updates the character level of a wizard.
    /// </summary>
    /// <param name="wizard">The wizard object containing the updated level.</param>
    public static void UpdateCharacterLevel(Wizard wizard) {
        using var session = s_store.OpenSession();

        var existingCharacter = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == wizard.CharId);
        if (existingCharacter is null) {
            return;
        }

        existingCharacter.Level = wizard.Level;
        session.SaveChanges();
    }
}
