/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.Director.EmbeddedAccounts;
using static Imlight.Common.Caches.TypeCache;

internal class Joji : EmbeddedAccount {

    public Joji(string Username, string plaintextPassword, string Email, AuthLevel AuthLevel)
        : base(Username, plaintextPassword, Email, AuthLevel) { }

    protected override Wizard CreateDefaultWizard() {
        var charCreationinfo = new WizardCharacterCreationInfo {
            m_avatarBehavior = new WizardCharacterBehavior {
                m_eGender = eGender.Male,
                m_eRace = eRace.Human,
                m_extendedHairColor = 0,
                m_nFeetColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 5,
                m_nFeetDecal = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 2,
                m_nHairColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui7) 60,
                m_nHairModel = (Imlight.Common.ObjectProperty.PropertyReflection.Bui4) 6,
                m_nHatColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 5,
                m_nHatDecal = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 2,
                m_nSkinColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui4) 0,
                m_nSkinDecal2 = 0,
                m_nTorsoColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 5,
                m_nTorsoDecal = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 2,
                m_newPlayerOptions = 402654785,
                m_newPlayerOptions2 = 1
            },
            m_nameIndices = 11272192,
            m_schoolOfFocus = (uint) MagicSchool.Storm,
            m_name = "Joji"
        };
        var newCharacter = CharacterHelper.CreateCharacterFromCreationInfo(charCreationinfo);
        newCharacter.SetNameOverride("Joji");
        newCharacter.PlayerNameBehavior.BadgeTitle = "QuestTitle_00000001";

        // Add default items.
        newCharacter.AddItemToInventory(532298, out var hat);
        newCharacter.AddItemToInventory(97330, out var robe);
        newCharacter.AddItemToInventory(97931, out var boots);
        newCharacter.AddItemToInventory(191223, out var staff);

        // Dye the items black.
        DyeMapper.ApplyPrimaryDye(hat, DyeColor.Black);
        DyeMapper.ApplySecondaryDye(hat, DyeColor.LightPurple);
        DyeMapper.ApplyPrimaryDye(robe, DyeColor.DarkPurple);
        DyeMapper.ApplySecondaryDye(robe, DyeColor.Black);
        DyeMapper.ApplyPrimaryDye(boots, DyeColor.DarkPurple);
        DyeMapper.ApplySecondaryDye(boots, DyeColor.Black);

        // Now equip the items.
        newCharacter.InventoryToEquipmentTransfer(hat.m_globalID, out var _, out var _);
        newCharacter.InventoryToEquipmentTransfer(robe.m_globalID, out var _, out var _);
        newCharacter.InventoryToEquipmentTransfer(boots.m_globalID, out var _, out var _);
        newCharacter.InventoryToEquipmentTransfer(staff.m_globalID, out var _, out var _);

        return newCharacter;
    }
}
