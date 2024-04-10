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

internal sealed class Mitsu : EmbeddedAccount {
    public Mitsu(string Username, string plaintextPassword, string Email, AuthLevel AuthLevel)
        : base(Username, plaintextPassword, Email, AuthLevel) { }

    protected override Wizard CreateDefaultWizard() {
        var charCreationinfo = new WizardCharacterCreationInfo {
            m_avatarBehavior = new WizardCharacterBehavior {
                m_eGender = eGender.Female,
                m_eRace = eRace.Human,
                m_extendedHairColor = 1,
                m_nFeetColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 6,
                m_nFeetDecal = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 6,
                m_nHairColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui7) 99,
                m_nHairModel = (Imlight.Common.ObjectProperty.PropertyReflection.Bui4) 4,
                m_nHatColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 6,
                m_nHatDecal = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 6,
                m_nSkinColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui4) 2,
                m_nSkinDecal2 = 11,
                m_nTorsoColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 6,
                m_nTorsoDecal = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 6,
                m_newPlayerOptions = 1094747217,
                m_newPlayerOptions2 = 21
            },
            m_nameIndices = 6481693,
            m_schoolOfFocus = (uint) MagicSchool.Death,
            m_name = "Mitsu",
        };
        var newCharacter = CharacterHelper.CreateCharacterFromCreationInfo(charCreationinfo);
        newCharacter.SetNameOverride("Mitsu");

        // Add default items.
        newCharacter.AddItemToInventory(1359665, out var hat);   // The Cat Ears
        newCharacter.AddItemToInventory(1376831, out var robe);  // Polarian Luhkka
        newCharacter.AddItemToInventory(191351,  out var boots); // Swashbuckler Boots
        newCharacter.AddItemToInventory(1384669, out var staff); // Blood Moon Staff

        // Dye the items black.
        DyeMapper.ApplyPrimaryDye(robe, DyeColor.Black);
        DyeMapper.ApplySecondaryDye(robe, DyeColor.Black);
        DyeMapper.ApplyPrimaryDye(boots, DyeColor.Black);
        DyeMapper.ApplySecondaryDye(boots, DyeColor.Black);

        // Now equip the items.
        newCharacter.InventoryToEquipmentTransfer(hat.m_globalID,   out var _, out var _);
        newCharacter.InventoryToEquipmentTransfer(robe.m_globalID,  out var _, out var _);
        newCharacter.InventoryToEquipmentTransfer(boots.m_globalID, out var _, out var _);
        newCharacter.InventoryToEquipmentTransfer(staff.m_globalID, out var _, out var _);

        return newCharacter;
    }
}
