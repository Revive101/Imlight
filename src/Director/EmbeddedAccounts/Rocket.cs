/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Character;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.Director.EmbeddedAccounts;
using static Imlight.Common.Caches.TypeCache;

internal class Rocket : EmbeddedAccount {

    public Rocket(string Username, string plaintextPassword, string Email, AuthLevel AuthLevel)
        : base(Username, plaintextPassword, Email, AuthLevel) { }

    protected override Wizard CreateDefaultWizard() {
        var charCreationinfo = new WizardCharacterCreationInfo {
            m_avatarBehavior = new WizardCharacterBehavior {
                m_eGender = eGender.Male,
                m_eRace = eRace.Human,
                m_extendedHairColor = 0,
                m_nFeetColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 0,
                m_nFeetDecal = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 8,
                m_nHairColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui7) 40,
                m_nHairModel = (Imlight.Common.ObjectProperty.PropertyReflection.Bui4) 4,
                m_nHatColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 6,
                m_nHatDecal = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 6,
                m_nSkinColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui4) 0,
                m_nSkinDecal2 = 0,
                m_nTorsoColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 0,
                m_nTorsoDecal = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 8,
                m_newPlayerOptions = 58759169,
                m_newPlayerOptions2 = 3
            },
            m_nameIndices = 6481693,
            m_schoolOfFocus = (uint) MagicSchool.Death,
            m_name = "Rocket",
        };
        var newCharacter = CharacterHelper.CreateCharacterFromCreationInfo(charCreationinfo);
        newCharacter.SetNameOverride("Rocket");

        // Add default items.
        newCharacter.AddItemToInventory(1523111, out var hat);
        newCharacter.AddItemToInventory(1577276, out var robe);
        newCharacter.AddItemToInventory(1577292, out var boots);
        newCharacter.AddItemToInventory(1302125, out var staff);

        // Now equip the items.
        newCharacter.InventoryToEquipmentTransfer(hat.m_globalID, out var _, out var _);
        newCharacter.InventoryToEquipmentTransfer(robe.m_globalID, out var _, out var _);
        newCharacter.InventoryToEquipmentTransfer(boots.m_globalID, out var _, out var _);
        newCharacter.InventoryToEquipmentTransfer(staff.m_globalID, out var _, out var _);

        return newCharacter;
    }
}
