using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.Director.EmbeddedAccounts;
internal sealed class Phill : EmbeddedAccount {
    public Phill(string Username, string plaintextPassword, string Email, AuthLevel AuthLevel)
    : base(Username, plaintextPassword, Email, AuthLevel) { }

    // Subject to change
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
            m_name = "Phill",
        };
        var newCharacter = CharacterHelper.CreateCharacterFromCreationInfo(charCreationinfo);
        newCharacter.SetNameOverride("Phill");
        newCharacter.PlayerNameBehavior.BadgeTitle = "Title_1";

        // Add default items.
        newCharacter.AddItemToInventory(1359664, out var hat);
        newCharacter.AddItemToInventory(1591141, out var robe);
        newCharacter.AddItemToInventory(1591260, out var boots);
        newCharacter.AddItemToInventory(1302115, out var staff);

        // Dye the items orange.
        DyeMapper.ApplyPrimaryDye(hat, DyeColor.Orange);
        DyeMapper.ApplySecondaryDye(hat, DyeColor.Orange);
        DyeMapper.ApplyPrimaryDye(robe, DyeColor.Orange);
        DyeMapper.ApplySecondaryDye(robe, DyeColor.Orange);
        DyeMapper.ApplyPrimaryDye(boots, DyeColor.Orange);
        DyeMapper.ApplySecondaryDye(boots, DyeColor.Orange);

        // Now equip the items.
        newCharacter.InventoryToEquipmentTransfer(hat.m_globalID, out var _, out var _);
        newCharacter.InventoryToEquipmentTransfer(robe.m_globalID, out var _, out var _);
        newCharacter.InventoryToEquipmentTransfer(boots.m_globalID, out var _, out var _);
        newCharacter.InventoryToEquipmentTransfer(staff.m_globalID, out var _, out var _);

        return newCharacter;
    }
}
