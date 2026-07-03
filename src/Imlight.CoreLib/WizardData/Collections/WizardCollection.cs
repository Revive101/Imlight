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

using System.Linq;
using Raven.Client.Documents;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Player;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Math;

namespace Imlight.CoreLib.WizardData.Collections;

public static class WizardCollection {

    public const string CollectionName = "Wizards";
    private static readonly IDocumentStore s_store;

    static WizardCollection()
        => s_store = PlayerDatabase.Instance.Store;

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
        WizardReagentCollection.DeleteReagentBag(id);

        session.Delete(character);
        session.SaveChanges();

        return true;
    }

    /// <summary>
    /// Retrieves a character from the database based on the specified ID.
    /// 
    /// Completely loads the character, including their inventory, equipment, etc.
    /// This is a blocking call and may take some time to complete. Alternatively, use
    /// `GetCharacterUnloaded` to retrieve a character without loading their data.
    /// </summary>
    /// <param name="id">The ID of the character to retrieve.</param>
    /// <returns>The character with the specified ID, or null if not found.</returns>
    public static Wizard GetCharacter(ulong id) {
        using var session = s_store.OpenSession();

        var character = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == id);

        // Query for the account.
        var account = session.Query<Account>(collectionName: AccountCollection.CollectionName)
            .FirstOrDefault(x => x.AccountId == character.AccountId);
        if (account is not null) {
            character.Account = account;
            account.Characters.Add(character);
        }

        return LoadWizard(character);
    }

    /// <summary>
    /// Loads all characters based on the specified account ID. Returns the characters loaded, so
    /// additional queries are made to load the character's inventory, equipment, etc.
    /// </summary>
    /// <param name="accountId">The account ID of the character to retrieve.</param>
    /// <param name="account">The account associated with the character. Characters will be added
    /// to the account.</param>
    /// <returns>The character with the specified account ID, or null if not found.</returns>
    public static Wizard[] LoadWizardsOntoAccount(ulong accountId, ref Account account) {
        using var session = s_store.OpenSession();

        var characters = session.Query<Wizard>(collectionName: CollectionName)
            .Where(x => x.AccountId == accountId)
            .ToList();

        for (var i = 0; i < characters.Count; i++) {
            characters[i].Account = account;
            account.Characters.Add(characters[i]);
        }

        // Load all of the characters. We must do this here because
        // `Wizard` initialization may require the account to be in full; aka, we need
        // all the characters to be on the account ahead of time.
        for (var i = 0; i < characters.Count; i++) {
            characters[i] = LoadWizard(characters[i]);
        }

        return [.. characters];
    }

    /// <summary>
    /// Retrieves all characters based on the specified account ID. Returns the characters unloaded, so no
    /// additional queries are made to load the character's inventory, equipment, etc.
    /// </summary>
    /// <param name="accountId">The account ID of the character to retrieve.</param>
    /// <param name="getAccount">Whether to retrieve the account associated with the character.</param>
    /// <returns>The character with the specified account ID, or null if not found.</returns>
    public static Wizard GetCharacterUnloaded(ulong charId) {
        using var session = s_store.OpenSession();

        var character = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == charId);

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

        existingCharacter.PreviousZone = existingCharacter.PreviousZone;
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
    /// <param name="zoneDisplayName">The display name of the new zone.</param>
    public static void UpdateCharacterMarkedLocation(Wizard character,
                                                     Vector3 location,
                                                     Vector3 orientation,
                                                     string ZoneName,
                                                     string zoneDisplayName) {
        using var session = s_store.OpenSession();

        var existingCharacter = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == character.CharId);
        if (existingCharacter is null) {
            return;
        }

        existingCharacter.MarkedLocation = location;
        existingCharacter.MarkedOrientation = orientation;
        existingCharacter.MarkedZone = ZoneName;
        existingCharacter.MarkedZoneDisplayName = zoneDisplayName;
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
        existingCharacter.AlchemyBehavior = wizard.AlchemyBehavior;
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
        existingCharacter.MagicSchoolBehavior.ExperiencePoints = wizard.MagicSchoolBehavior.ExperiencePoints;
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
    /// Updates the character badge override for a wizard.
    /// </summary>
    /// <param name="wizard">The wizard object containing the updated character badge override.</param>
    public static void UpdateCharacterBadgeOverride(Wizard wizard) {
        using var session = s_store.OpenSession();

        var existingCharacter = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == wizard.CharId);
        if (existingCharacter is null) {
            return;
        }

        existingCharacter.PlayerNameBehavior.BadgeTitle = wizard.PlayerNameBehavior.BadgeTitle;
        session.SaveChanges();
    }

    /// <summary>
    /// Updates the character spellbook behavior for a wizard — persists the
    /// excluded item spell list and other spellbook state to the database.
    /// </summary>
    /// <param name="wizard">The wizard whose spellbook behavior should be persisted.</param>
    public static void UpdateCharacterSpellbookBehavior(Wizard wizard) {
        using var session = s_store.OpenSession();

        var existingCharacter = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == wizard.CharId);
        if (existingCharacter is null) {
            return;
        }

        existingCharacter.SpellbookBehavior = wizard.SpellbookBehavior;
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
    /// Updates the character pet owner behavior for a wizard.
    /// </summary>
    /// <param name="wizard">The wizard object containing the updated pet owner behavior.</param>
    public static void UpdateCharacterPetOwnerBehavior(Wizard wizard) {
        using var session = s_store.OpenSession();

        var existingCharacter = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == wizard.CharId);
        if (existingCharacter is null) {
            return;
        }

        existingCharacter.PetOwnerBehavior = wizard.PetOwnerBehavior;
        session.SaveChanges();
    }

    /// <summary>
    /// Updates the character's last time they clicked the "go to ___ (ex. commons)" button.
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
    /// Updates the friend behavior for a wizard — persists pending friend requests
    /// and other in-memory friend state to the database.
    /// </summary>
    /// <param name="wizard">The wizard whose friend behavior should be persisted.</param>
    public static void UpdateCharacterFriendBehavior(Wizard wizard) {
        using var session = s_store.OpenSession();

        var existingCharacter = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == wizard.CharId);
        if (existingCharacter is null) {
            return;
        }

        existingCharacter.FriendsBehavior = wizard.FriendsBehavior;
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

    /// <summary>
    /// Adds a treasure card to the spellbook of a wizard.
    /// </summary>
    /// <param name="wizard">The wizard to add the treasure card to.</param>
    /// <param name="spellTemplateId">The template ID of the spell to add as a treasure card.</param>
    public static void AddTreasureCard(Wizard wizard, uint spellTemplateId) {
        using var session = s_store.OpenSession();

        var existingCharacter = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == wizard.CharId);
        if (existingCharacter is null) {
            return;
        }

        existingCharacter.SpellbookBehavior.AddTreasureCard(spellTemplateId);
        session.SaveChanges();
    }

    /// <summary>
    /// Removes a treasure card from the spellbook of a wizard.
    /// </summary>
    /// <param name="wizard">The wizard whose spellbook will be modified.</param>
    /// <param name="spellTemplateId">The template ID of the treasure card to remove.</param>
    public static void RemoveTreasureCard(Wizard wizard, uint spellTemplateId) {
        using var session = s_store.OpenSession();

        var existingCharacter = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == wizard.CharId);
        if (existingCharacter is null) {
            return;
        }

        existingCharacter.SpellbookBehavior.RemoveTreasureCard(spellTemplateId);
        session.SaveChanges();
    }

    /// <summary>
    /// Adds a new relationship to the friends behavior of a wizard.
    /// </summary>
    /// <param name="wizard">The wizard to add the relationship to.</param>
    /// <param name="relationship">The relationship to add.</param>
    public static void AddOrUpdateRelationship(Wizard wizard, Relationship relationship) {
        using var session = s_store.OpenSession();

        var existingCharacter = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == wizard.CharId);
        if (existingCharacter is null) {
            return;
        }

        existingCharacter.FriendsBehavior ??= new();
        existingCharacter.FriendsBehavior.AddOrUpdateRelationship(relationship);

        session.SaveChanges();
    }

    /// <summary>
    /// Removes a relationship from the friends behavior of a wizard.
    /// </summary>
    /// <param name="wizard">The wizard to remove the relationship from.</param>
    /// <param name="friendId">The ID of the friend to remove.</param>
    public static void RemoveRelationship(Wizard wizard, ulong friendId) {
        using var session = s_store.OpenSession();

        var existingCharacter = session.Query<Wizard>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharId == wizard.CharId);
        if (existingCharacter is null) {
            return;
        }

        if (existingCharacter.FriendsBehavior is null) {
            return;
        }

        existingCharacter.FriendsBehavior.Breakup(friendId);

        session.SaveChanges();
    }

    /// <summary>
    /// Update the quest behavior for a given wizard in the database.
    /// </summary>
    /// <param name="wizard">The wizard whose quest behavior needs to be updated.</param>
    /// <returns>True if the update was successful; otherwise, false.</returns>
    public static bool UpdateCharacterQuestBehavior(Wizard wizard) {
        if (wizard is null || wizard.QuestBehavior is null) {
            return false;
        }

        using var session = s_store.OpenSession();
        var dbWizard = session.Query<Wizard>(collectionName: CollectionName)
                              .FirstOrDefault(w => w.CharId == wizard.CharId);

        if (dbWizard is null) {
            return false;
        }

        dbWizard.QuestBehavior = wizard.QuestBehavior;
        session.SaveChanges();

        return true;
    }

    private static Wizard LoadWizard(Wizard wizard) {
        using var session = s_store.OpenSession();

        // `Wizard` only keeps track of the IDs of the items in the inventory.
        // The actual items are stored in the `WizClientObjectItem` collection.
        // Load the items in the inventory.
        var items = session.Query<WizClientObjectItem>(collectionName: WizardItemCollection.CollectionName)
            .Where(x => x.m_characterId == wizard.CharId)
            .ToList();

        wizard.InventoryBehavior.Items = [.. items
            .Where(i => wizard.InventoryBehavior.InventoryItemIds
            .Contains(i.m_globalID))
        ];

        // Load the character's equipment.
        // The equipped items are stored as global IDs in the character's EquipmentBehavior.
        // Find any items in the inventory that match the equipped item IDs.
        wizard.EquipmentBehavior.EquippedItems = [.. items
            .Where(i => wizard.EquipmentBehavior.EquippedItemIds
            .Any(e => i.m_globalID == e))
        ];

        // The friends list is expanded to include a 'relationship' model
        // which helps keep track of the relationship between two players for moderation purposes.
        // `Wizard` only keeps track of the IDs of the relationships.
        // The actual relationships are stored in the `BuddyRelationshipCollection`.
        var relationships = session
            .Query<Relationship>(collectionName: BuddyRelationshipCollection.CollectionName)
            .Where(x => x.FirstPlayerId == wizard.CharId || x.SecondPlayerId == wizard.CharId)
            .ToList();
        wizard.FriendsBehavior ??= new();
        wizard.FriendsBehavior.Relationships = relationships;

        // Load character dynamic modifications.
        var dynamods = session
            .Query<DynamodSet>(collectionName: DynamodCollection.CollectionName)
            .Where(d => d.CharId == wizard.CharId)
            .ToList();
        wizard.DynamodSet = dynamods.FirstOrDefault() ?? new DynamodSet(wizard.CharId);

        // Load the actual quests the character has.
        if (wizard.QuestBehavior != null) {
            var currentQuestIDs = wizard.QuestBehavior.CurrentQuestIDs;
            var myQuests = session
                .Query<QuestInstance>(collectionName: QuestInstanceCollection.CollectionName)
                .Where(q => q.OwnerCharId == wizard.CharId)
                .ToList();

            wizard.QuestBehavior.CurrentQuestInstances = myQuests;
        }

        // The wizard may still have some initialization to do. Inform the wizard
        // that their data has been loaded from the database.
        wizard.AfterDatabaseLoad();

        return wizard;
    }

}
