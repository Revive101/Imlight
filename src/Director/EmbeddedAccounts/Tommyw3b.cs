/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.Director.EmbeddedAccounts;
using static Imlight.Common.Caches.TypeCache;

internal sealed class Tommyw3b : EmbeddedAccount {
    public Tommyw3b(string Username, string plaintextPassword, string Email, AuthLevel AuthLevel)
        : base(Username, plaintextPassword, Email, AuthLevel) { }

    protected override Wizard CreateDefaultWizard() {
        var charCreationinfo = new WizardCharacterCreationInfo {
            m_avatarBehavior = new WizardCharacterBehavior {
                m_eGender = eGender.Male,
                m_eRace = eRace.Human,
                m_extendedHairColor = 0,
                m_nFeetColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 7,
                m_nFeetDecal = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 4,
                m_nHairColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui7) 40,
                m_nHairModel = (Imlight.Common.ObjectProperty.PropertyReflection.Bui4) 4,
                m_nHatColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 2,
                m_nHatDecal = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 6,
                m_nSkinColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui4) 2,
                m_nSkinDecal2 = 0,
                m_nTorsoColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 4,
                m_nTorsoDecal = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 2,
                m_newPlayerOptions = 1631590496,
                m_newPlayerOptions2 = 22
            },
            m_nameIndices = 6481693,
            m_schoolOfFocus = (uint) MagicSchool.Myth,
            m_name = "Tommyw3b",
        };
        var newCharacter = CharacterHelper.CreateCharacterFromCreationInfo(charCreationinfo);
        newCharacter.SetNameOverride("Tommyw3b");

        // Add default items.
        newCharacter.AddItemToInventory(1483134, out var hat);  // The Cold Cut
        newCharacter.AddItemToInventory(212301, out var staff); // Fog Staff

        // Dye the items black.
        DyeMapper.ApplyPrimaryDye(hat, DyeColor.Black);
        DyeMapper.ApplySecondaryDye(hat, DyeColor.Black);

        // Now equip the items.
        newCharacter.InventoryToEquipmentTransfer(hat.m_globalID, out var _, out var _);
        newCharacter.InventoryToEquipmentTransfer(staff.m_globalID, out var _, out var _);

        return newCharacter;
    }
}
