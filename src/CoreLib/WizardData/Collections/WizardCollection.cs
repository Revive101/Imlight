/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Collections;
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

        // Delete related items/dynamods/quests data/etc.
        WizardItemCollection.DeleteInventory(id);
        WizardPetSnackCollection.DeleteSnackBag(id);
        DynamodCollection.DeleteAllDynamodSets(id);

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
    /// Updates the marked location, orientation, and zone of a character.
    /// <paramref name="character"/>The character to update.</param>
    /// <param name="location">The new location of the character.</param>
    /// <param name="orientation">The new orientation of the character.</param>
    /// <param name="ZoneName">The new zone of the character.</param>
    public static void UpdateCharacterMarkedLocation(Wizard character, Vector3 location, Vector3 orientation, string ZoneName) {
        using var session = s_store.OpenSession();

        var existingCharacter = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == character.CharId);
        if (existingCharacter is null) {
            return;
        }

        existingCharacter.MarkedLocation = location;
        existingCharacter.MarkedOrientation = orientation;
        existingCharacter.MarkedZone = ZoneName;
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
        existingCharacter.PetSnackBehavior = wizard.PetSnackBehavior;
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

        existingCharacter.MagicSchoolBehavior.Level = wizard.MagicSchoolBehavior.Level;
        session.SaveChanges();
    }

    /// <summary>
    /// Updates the character mount for a wizard.
    /// </summary>
    /// <param name="wizard">The wizard whose character mount is being updated.</param>
    public static void UpdateCharacterMount(Wizard wizard) {
        using var session = s_store.OpenSession();

        var existingCharacter = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == wizard.CharId);
        if (existingCharacter is null) {
            return;
        }

        existingCharacter.MountOwnerBehavior = wizard.MountOwnerBehavior;
        session.SaveChanges();
    }

    /// <summary>
    /// Updates the character name override for a wizard.
    /// </summary>
    /// <param name="wizard">The wizard object containing the updated character name override.</param>
    public static void UpdateCharacterNameOverride(Wizard wizard) {
        using var session = s_store.OpenSession();

        var existingCharacter = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == wizard.CharId);
        if (existingCharacter is null) {
            return;
        }

        existingCharacter.PlayerNameBehavior.NameOverride = wizard.PlayerNameBehavior.NameOverride;
        session.SaveChanges();
    }

    /// <summary>
    /// Updates the character game stats for a wizard.
    /// </summary>
    /// <param name="wizard">The wizard object containing the updated game stats</param>
    public static void UpdateCharacterGameStats(Wizard wizard) {
        using var session = s_store.OpenSession();

        var existingCharacter = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == wizard.CharId);
        if (existingCharacter is null) {
            return;
        }

        existingCharacter.GameStats = wizard.GameStats;
        session.SaveChanges();
    }

    /// <summary>
    /// Updates the character's last time they clicked the "go to ___ (ex. commons)" button.
    /// Confusingly, Kingsisle calls this the home button, while the wizard's house is called their dorm.
    /// </summary>
    /// <param name="wizard">The wizard to update the time for</param>
    public static void UpdateCharacterTimeWentHome(Wizard wizard, long time) {
        using var session = s_store.OpenSession();

        var existingCharacter = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == wizard.CharId);
        if (existingCharacter is null) {
            return;
        }

        existingCharacter.TimeHomeLastClicked = time;
        session.SaveChanges();
    }

    /// <summary>
    /// Updates the training points of a wizard.
    /// </summary>
    /// <param name="wizard">The wizard object containing the updated training points count.</param>
    public static void UpdateCharacterTrainingPoints(Wizard wizard) {
        using var session = s_store.OpenSession();

        var existingCharacter = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == wizard.CharId);
        if (existingCharacter is null) {
            return;
        }

        existingCharacter.MagicSchoolBehavior.TrainingPoints = wizard.MagicSchoolBehavior.TrainingPoints;
        session.SaveChanges();
    }

    /// <summary>
    /// Adds a spell to the spellbook of a wizard.
    /// </summary>
    /// <param name="wizard">The wizard to add the spell to.</param>
    /// <param name="spellTemplateId">The ID of the spell template to add.</param>
    public static void LearnSpell(Wizard wizard, uint spellTemplateId) {
        using var session = s_store.OpenSession();

        var existingCharacter = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == wizard.CharId);
        if (existingCharacter is null) {
            return;
        }

        existingCharacter.SpellbookBehavior.LearnedSpellTemplateIds.Add(spellTemplateId);
        session.SaveChanges();
    }

    /// <summary>
    /// Removes a spell from the spellbook of a wizard.
    /// </summary>
    /// <param name="wizard">The wizard whose spellbook will be modified.</param>
    /// <param name="spellTemplateId">The ID of the spell template to be removed.</param>
    public static void UnlearnSpell(Wizard wizard, uint spellTemplateId) {
        using var session = s_store.OpenSession();

        var existingCharacter = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == wizard.CharId);
        if (existingCharacter is null) {
            return;
        }

        existingCharacter.SpellbookBehavior.LearnedSpellTemplateIds.Remove(spellTemplateId);
        session.SaveChanges();
    }
}
