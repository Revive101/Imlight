/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using Imlight.Common.Utilities;
using Imlight.Server.Data;
using Imlight.Server.Data.Statistics;
using Imlight.Server.Game.Models;
using WizUnraveler.ObjectProperty;
using static WizUnraveler.Cache.TypeCache;

namespace Imlight.Server.Game;

public static class CharacterObjectLoader
{
    public static WizClientObject GetPlayerGameObject(ref Character character)
    {
        var clientObject = CoreObjectFactory.InitializeCoreObjectBehaviors(new WizClientObject(), 1);
         
        // Set the stats on the new object.
        clientObject.m_templateID = 1;
        clientObject.m_fScale = 1f;
        clientObject.m_globalID = character.CharId;
        clientObject.m_characterId = character.CharId;
        clientObject.m_permID = 0; // What is this?

        // Create the object at the location set in the character.
        clientObject.m_location = character.Location;
        clientObject.m_orientation = character.Orientation;
        clientObject.m_gameStats = character.GameStats;

        SetWizardAvatarBehavior(clientObject, ref character);
        SetEquipmentBehavior(clientObject, ref character);
        SetPlayerNameBehavior(clientObject, ref character);
        SetInventoryBehavior(clientObject, ref character);
        SetMagicSchoolBehavior(clientObject, ref character);
        SetSpellbookBehavior(clientObject, ref character);

        return clientObject;
    }

    private static void SetWizardAvatarBehavior(WizClientObject clientObject, ref Character character)
    {
        if (CoreObjectFactory.FindBehaviorInstance<WizardCharacterBehavior>(clientObject, out var avatarBehavior))
        {
            var idx = clientObject.m_inactiveBehaviors.IndexOf(avatarBehavior);
            clientObject.m_inactiveBehaviors[idx] = character.WizardAvatar;
        }
        else
            throw new Exception($"Behavior WizardCharacterBehavior was not found!");
    }

    private static void SetEquipmentBehavior(WizClientObject clientObject, ref Character character)
    {
        if (CoreObjectFactory.FindBehaviorInstance<ClientWizEquipmentBehavior>(clientObject, out var equipmentBehavior))
        {
            var slotList = new List<EquippedSlotInfo>();
            foreach (var slot in (EquipmentSlot[])Enum.GetValues(typeof(EquipmentSlot)))
            {
                slotList.Add(new EquippedSlotInfo()
                {
                    m_itemID = (GID)0,
                    m_itemSlotNameID = (uint)slot
                });
            }
            // TODO: Set the equipment list.
            //equipmentBehavior.m_publicItemList = CreationData.m_equipmentInfoList?.m_infoList;
            equipmentBehavior.m_equipmentSets = new List<EquipmentSet>();
            equipmentBehavior.m_slotList = slotList;
            equipmentBehavior.m_itemList = new List<CoreObject>();
        }
        else
            throw new Exception("Behavior ClientWizEquipmentBehavior not found!");
    }
    
    private static void SetPlayerNameBehavior(WizClientObject clientObject, ref Character character)
    {
        if (CoreObjectFactory.FindBehaviorInstance<ClientWizPlayerNameBehavior>(clientObject, out var nameBehavior))
        {
            nameBehavior.m_eGender = character.WizardAvatar.m_eGender;
            nameBehavior.m_eRace = character.WizardAvatar.m_eRace;
            nameBehavior.m_nameKeys = character.NameIndices;
            nameBehavior.m_wsNameOverride = character.NameOverride;
            nameBehavior.m_chatPermissions = 2; // todo: set this to the correct value.
            nameBehavior.m_friendlyPlayer = character.GameStats.m_friendlyPlayer;
            nameBehavior.m_volunteer = character.IsVolunteer;
        }
        else
            throw new Exception("Behavior ClientWizPlayerNameBehavior not found!");
    }
    
    private static void SetInventoryBehavior(WizClientObject clientObject, ref Character character)
    {
        if (CoreObjectFactory.FindBehaviorInstance<ClientWizInventoryBehavior>(clientObject, out var inventoryBehavior))
        {
            inventoryBehavior.m_numItemsAllowed = 75;
            inventoryBehavior.m_numJewelsAllowed = 100;
            inventoryBehavior.m_itemList = new List<CoreObject>();
            new List<ulong>() { 4740, 4705, 5030, 39068, 1363076, 1475149,
                1472644, 1317133, 1317126, 1317234, 1359455,
                1392077, 1352341, 87158, 87159, 87160, 1540397 }.ForEach(templateId =>
            {
                var template = CoreObjectFactory.GetCoreTemplate(templateId);
                var coreObject = template switch
                {
                    ItemTemplate => new WizClientObjectItem(),
                    _ => new ClientObject()
                };
                coreObject.m_globalID = RandomGen.GenerateGUID();
                coreObject.m_templateID = (GID)templateId;
                inventoryBehavior.m_itemList.Add(coreObject);
            });
        }
        else
            throw new Exception("Behavior ClientWizInventoryBehavior not found!");
    }
    
    private static void SetMagicSchoolBehavior(WizClientObject clientObject, ref Character character)
    {
        if (CoreObjectFactory.FindBehaviorInstance<ClientMagicSchoolBehavior>(clientObject, out var schoolBehavior))
        {
            schoolBehavior.m_equippedTeleportEffect = character.GameStats.m_equippedTeleportEffect;
            schoolBehavior.m_experiencePoints = character.XpToNextLevel;
            schoolBehavior.m_level = character.Level;
            schoolBehavior.m_trainingPoints = character.TrainingPoints;
            schoolBehavior.m_schoolOfFocus = (uint)character.WizardSchool;
        }
        else
            throw new Exception("Behavior ClientMagicSchoolBehavior not found!");
    }
    
    private static void SetSpellbookBehavior(WizClientObject clientObject, ref Character character)
    {
        if (CoreObjectFactory.FindBehaviorInstance<ClientSpellbookBehavior>(clientObject, out var spellbookBehavior))
        {
            spellbookBehavior.m_spellIDList = new List<SpellIDTracker>();
        }
        else
            throw new Exception("Behavior ClientSpellbookBehavior not found!");
    }
}