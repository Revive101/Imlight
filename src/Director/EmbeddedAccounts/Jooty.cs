/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.Director.EmbeddedAccounts;
using static Imlight.Common.Caches.TypeCache;

internal sealed class Jooty : EmbeddedAccount {
    public Jooty(string Username, string plaintextPassword, string Email, AuthLevel AuthLevel)
        : base(Username, plaintextPassword, Email, AuthLevel) { }

    protected override Wizard CreateDefaultWizard() {
        var charCreationinfo = new WizardCharacterCreationInfo {
            m_avatarBehavior = new WizardCharacterBehavior {
                m_eGender = eGender.Female,
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
            m_schoolOfFocus = (uint) MagicSchool.Fire,
            m_name = "Jooty",
        };
        var newCharacter = CharacterHelper.CreateCharacterFromCreationInfo(charCreationinfo);
        newCharacter.SetNameOverride("Jooty");

        // Add default items.
        newCharacter.AddItemToInventory(1523097, out var hat);
        newCharacter.AddItemToInventory(1577261, out var robe);
        newCharacter.AddItemToInventory(1577277, out var boots);
        newCharacter.AddItemToInventory(1302115, out var staff);

        // Dye the items black.
        DyeMapper.ApplyPrimaryDye(hat, DyeColor.Black);
        DyeMapper.ApplySecondaryDye(hat, DyeColor.Black);
        DyeMapper.ApplyPrimaryDye(robe, DyeColor.Black);
        DyeMapper.ApplySecondaryDye(robe, DyeColor.Black);
        DyeMapper.ApplyPrimaryDye(boots, DyeColor.Black);
        DyeMapper.ApplySecondaryDye(boots, DyeColor.Black);

        // Now equip the items.
        newCharacter.InventoryToEquipmentTransfer(hat.m_globalID, out var _, out var _);
        newCharacter.InventoryToEquipmentTransfer(robe.m_globalID, out var _, out var _);
        newCharacter.InventoryToEquipmentTransfer(boots.m_globalID, out var _, out var _);
        newCharacter.InventoryToEquipmentTransfer(staff.m_globalID, out var _, out var _);

        return newCharacter;
    }
}
