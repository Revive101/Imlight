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

namespace Imlight.Director.EmbeddedAccounts;

internal sealed class Storm : EmbeddedAccount {
    public Storm(string Username, string plaintextPassword, string Email, AuthLevel AuthLevel)
        : base(Username, plaintextPassword, Email, AuthLevel) { }

    protected override Wizard CreateDefaultWizard() {
        var charCreationinfo = new WizardCharacterCreationInfo {
            m_avatarBehavior = new WizardCharacterBehavior {
                m_eGender = eGender.Female,
                m_eRace = eRace.Human,
                m_extendedHairColor = 1,
                m_nFeetColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5)7,
                m_nFeetDecal = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5)4,
                m_nHairColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui7)48,
                m_nHairModel = (Imlight.Common.ObjectProperty.PropertyReflection.Bui4)4,
                m_nHatColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5)7,
                m_nHatDecal = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5)7,
                m_nSkinColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui4)4,
                m_nSkinDecal2 = 11,
                m_nTorsoColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5)7,
                m_nTorsoDecal = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5)7,
                m_newPlayerOptions = 1384124992,
                m_newPlayerOptions2 = 27
            },
            m_nameIndices = 6481693,
            m_schoolOfFocus = (uint)MagicSchool.Fire,
            m_name = "Storm",
        };
        var newCharacter = CharacterHelper.CreateCharacterFromCreationInfo(charCreationinfo);
        newCharacter.SetNameOverride("Storm");

        // Add default items.
        newCharacter.AddItemToInventory(1523823, out var hat);
        newCharacter.AddItemToInventory(97328, out var robe);
        newCharacter.AddItemToInventory(1456623, out var boots);

        // Dye the items black.
        DyeMapper.ApplyPrimaryDye(hat, DyeColor.Red);
        DyeMapper.ApplySecondaryDye(hat, DyeColor.Pink);
        DyeMapper.ApplyPrimaryDye(robe, DyeColor.Red);
        DyeMapper.ApplySecondaryDye(robe, DyeColor.Pink);
        DyeMapper.ApplyPrimaryDye(boots, DyeColor.Red);
        DyeMapper.ApplySecondaryDye(boots, DyeColor.Pink);

        // Now equip the items.
        newCharacter.InventoryToEquipmentTransfer(hat.m_globalID, out var _, out var _);
        newCharacter.InventoryToEquipmentTransfer(robe.m_globalID, out var _, out var _);
        newCharacter.InventoryToEquipmentTransfer(boots.m_globalID, out var _, out var _);

        return newCharacter;
    }
}
