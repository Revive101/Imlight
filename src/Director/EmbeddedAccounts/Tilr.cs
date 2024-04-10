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

internal sealed class Tilr : EmbeddedAccount {
    public Tilr(string Username, string plaintextPassword, string Email, AuthLevel AuthLevel)
        : base(Username, plaintextPassword, Email, AuthLevel) { }

    protected override Wizard CreateDefaultWizard() {
        var charCreationinfo = new WizardCharacterCreationInfo {
            m_avatarBehavior = new WizardCharacterBehavior {
                m_eGender = eGender.Male,
                m_eRace = eRace.Human,
                m_extendedHairColor = 0,
                m_nFeetColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 2,
                m_nFeetDecal = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 1,
                m_nHairColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui7) 12,
                m_nHairModel = (Imlight.Common.ObjectProperty.PropertyReflection.Bui4) 1,
                m_nHatColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 2,
                m_nHatDecal = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 6,
                m_nSkinColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui4) 0,
                m_nSkinDecal2 = 0,
                m_nTorsoColor = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 6,
                m_nTorsoDecal = (Imlight.Common.ObjectProperty.PropertyReflection.Bui5) 2,
                m_newPlayerOptions = 1900021346,
                m_newPlayerOptions2 = 10
            },
            m_nameIndices = 6481693,
            m_schoolOfFocus = (uint) MagicSchool.Fire,
            m_name = "Tilr",
        };
        var newCharacter = CharacterHelper.CreateCharacterFromCreationInfo(charCreationinfo);
        newCharacter.SetNameOverride("Tilr");

        return newCharacter;
    }
}
