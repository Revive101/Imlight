/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models.Player;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game;

public static class WizardObjectLoader {
    public static WizClientObject GetPlayerGameObject(ref Wizard character) {
        var clientObject = CoreObjectFactory.InitializeCoreObjectBehaviors(new WizClientObject(), 1);
        CharacterHelper.ResetStats(character.GameStats, (byte) character.MagicSchoolBehavior.Level, character.MagicSchoolBehavior.MagicSchool);

        // Set the stats on the new object.
        clientObject.m_templateID = 1;
        clientObject.m_fScale = 1f;
        clientObject.m_globalID = character.CharId;
        clientObject.m_characterId = (GID) character.CharId;
        clientObject.m_permID = 0; // What is this?

        // Create the object at the location set in the character.
        clientObject.m_location = character.Location;
        clientObject.m_orientation = character.Orientation;
        clientObject.m_gameStats = character.GameStats;

        SetWizardAvatarBehavior(clientObject, ref character);
        SetEquipmentBehavior(clientObject, character);
        SetPlayerNameBehavior(clientObject, ref character);
        SetInventoryBehavior(clientObject, ref character);
        SetMagicSchoolBehavior(clientObject, ref character);
        SetSpellbookBehavior(clientObject, ref character);
        SetMountOwnerBehavior(clientObject, ref character);

        return clientObject;
    }

    public static void SetWizardAvatarBehavior(WizClientObject clientObject, ref Wizard character) {
        if (CoreObjectFactory.FindBehaviorInstance<WizardCharacterBehavior>(clientObject, out var avatarBehavior)) {
            var idx = clientObject.m_inactiveBehaviors.IndexOf(avatarBehavior);
            clientObject.m_inactiveBehaviors[idx] = character.WizardAvatar;
        }
        else {
            throw new Exception($"Behavior WizardCharacterBehavior was not found!");
        }
    }

    public static void SetInventoryBehavior(WizClientObject clientObject, ref Wizard character) {
        if (CoreObjectFactory.FindBehaviorInstance<ClientWizInventoryBehavior>(clientObject, out var inventoryBehavior)) {
            var idx = clientObject.m_inactiveBehaviors.IndexOf(inventoryBehavior);
            clientObject.m_inactiveBehaviors[idx] = character.InventoryBehavior.GetClientBehaviorInstance();
        }
        else {
            throw new Exception("Behavior ClientWizInventoryBehavior not found!");
        }
    }

    public static void SetEquipmentBehavior(WizClientObject clientObject, Wizard character) {
        if (CoreObjectFactory.FindBehaviorInstance<ClientWizEquipmentBehavior>(clientObject, out var equipmentBehavior)) {
            var idx = clientObject.m_inactiveBehaviors.IndexOf(equipmentBehavior);
            clientObject.m_inactiveBehaviors[idx] = character.EquipmentBehavior.GetClientBehaviorInstance();
        }
        else {
            throw new Exception("Behavior ClientWizEquipmentBehavior not found!");
        }
    }

    public static void SetGameEffectBehavior(WizClientObject clientObject, ref Wizard character) {
        if (CoreObjectFactory.FindBehaviorInstance<BaseGameEffectBehavior>(clientObject, out var effectBehavior)) {
            var effectContainer = new GameEffectContainer {
                // m_publicEffects = character.GameEffects
            };

            effectBehavior.m_gameEffects = effectContainer;
        }
        else {
            throw new Exception("Behavior ClientGameEffectBehavior not found!");
        }
    }

    public static void SetPlayerNameBehavior(WizClientObject clientObject, ref Wizard character) {
        if (CoreObjectFactory.FindBehaviorInstance<ClientWizPlayerNameBehavior>(clientObject, out var nameBehavior)) {
            var idx = clientObject.m_inactiveBehaviors.IndexOf(nameBehavior);
            clientObject.m_inactiveBehaviors[idx] = character.PlayerNameBehavior.GetClientBehaviorInstance();
        }
        else {
            throw new Exception("Behavior ClientWizPlayerNameBehavior not found!");
        }
    }

    public static void SetMagicSchoolBehavior(WizClientObject clientObject, ref Wizard character) {
        if (CoreObjectFactory.FindBehaviorInstance<ClientMagicSchoolBehavior>(clientObject, out var schoolBehavior)) {
            var idx = clientObject.m_inactiveBehaviors.IndexOf(schoolBehavior);
            clientObject.m_inactiveBehaviors[idx] = character.MagicSchoolBehavior.GetClientBehaviorInstance();
        }
        else {
            throw new Exception("Behavior ClientMagicSchoolBehavior not found!");
        }
    }

    public static void SetSpellbookBehavior(WizClientObject clientObject, ref Wizard character) {
        if (CoreObjectFactory.FindBehaviorInstance<ClientSpellbookBehavior>(clientObject, out var spellbookBehavior)) {
            var idx = clientObject.m_inactiveBehaviors.IndexOf(spellbookBehavior);
            clientObject.m_inactiveBehaviors[idx] = character.SpellbookBehavior.GetClientBehaviorInstance();
        }
        else {
            throw new Exception("Behavior ClientSpellbookBehavior not found!");
        }
    }

    public static void SetMountOwnerBehavior(WizClientObject clientObject, ref Wizard character) {
        if (CoreObjectFactory.FindBehaviorInstance<ClientMountOwnerBehavior>(clientObject, out var mountOwnerBehavior)) {
            var idx = clientObject.m_inactiveBehaviors.IndexOf(mountOwnerBehavior);
            clientObject.m_inactiveBehaviors[idx] = character.MountOwnerBehavior.GetClientBehaviorInstance();
        }
        else {
            throw new Exception("Behavior ClientMountOwnerBehavior not found!");
        }
    }
}
