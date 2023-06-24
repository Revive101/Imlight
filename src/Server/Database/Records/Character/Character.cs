/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using WizUnraveler.ObjectProperty;
using Imlight.Common.Utilities;
using Imlight.Server.Database.Records.Character;
using static WizUnraveler.Cache.TypeCache;
using Newtonsoft.Json;

namespace Imlight.Server.Database
{
    public class Character
    {
        private const int    STARTING_LEVEL = 15;
        private const string STARTING_LOCATION = "MooShu/MS_Hub";
        private const int    STARTING_BASE_MANA = 15;
        private const int    STARTING_BASE_HEALTH = 500;
        private const int    STARTING_BASE_GOLD = 1000000;

        public string nextZone = ""; // TESTING ONLY
        public string LastGameServerIp = "";
        public ushort LastGameServerPort;

        // Cached Behaviors
        public ClientWizEquipmentBehavior equipmentBehaviorCache;
        public ClientWizInventoryBehavior inventoryBehaviorCache;

        public ulong Id 
        { 
            get
            {
                if (CreationData is null) return 0;
                return CreationData.m_globalID;
            }
        }
        public ulong TemplateId { get; private set; }
        public WizardCharacterCreationInfo CreationData { get; private set; }

        public Character(WizardCharacterCreationInfo creationData, GID accountId)
        {
            // Check to see if the starting location exists in the access pass.
            if (!AccessPassManager.DoesZoneExist(STARTING_LOCATION))
                throw new Exception($"Starting location {STARTING_LOCATION} does not exist in the access pass!");
            
            this.CreationData = creationData;
            this.CreationData.m_level = STARTING_LEVEL;  
            this.CreationData.m_location = STARTING_LOCATION;
            this.CreationData.m_world = 1;
            this.CreationData.m_globalID = RandomGen.GenerateGUID();
            this.CreationData.m_userID = accountId;
            this.CreationData.m_equipmentInfoList = new EquippedItemInfoList()
            {
                m_infoList = new List<EquippedItemInfo>()
            };
        }

        public WizClientObject GetWizClientObject()
        {
            var clientObject = CoreObjectFactory.InitializeCoreObjectBehaviors(
                new WizClientObject(), 
                (ulong)CreationData.m_templateID);

            ReplaceWizAvatarWithCreationData(clientObject);
            SetWizClientBehaviors(ref clientObject);

            clientObject.m_templateID = (ulong)CreationData.m_templateID;
            clientObject.m_fScale = 1f;
            clientObject.m_globalID = CreationData.m_globalID;
            clientObject.m_characterId = (GID)Id;
            clientObject.m_permID = 0; // What is this?
            clientObject.m_location = new SharpDX.Vector3(0, 0, 0);
            
            // @todo: source these stats from the API, probably
            clientObject.m_gameStats = new WizGameStats()
            {
                m_baseMana = STARTING_BASE_MANA,
                m_currentMana = STARTING_BASE_MANA,
                m_baseGoldPouch = STARTING_BASE_GOLD,
                m_baseHitpoints = STARTING_BASE_HEALTH,
                m_currentHitpoints = STARTING_BASE_HEALTH,
            };

            return clientObject;
        } 

        private void ReplaceWizAvatarWithCreationData(WizClientObject clientObject)
        {
            if (CoreObjectFactory.FindBehaviorInstance<WizardCharacterBehavior>(clientObject, out var avatarBehavior))
            {
                var idx = clientObject.m_inactiveBehaviors.IndexOf(avatarBehavior);
                clientObject.m_inactiveBehaviors[idx] = CreationData.m_avatarBehavior;
            }
            else
                throw new Exception($"Behavior WizardCharacterBehavior was not found!");
        }

        private void SetWizClientBehaviors(ref WizClientObject clientObject)
        {
            // =========================================================
            // EQUIPMENT
            // =========================================================
            if (CoreObjectFactory.FindBehaviorInstance<ClientWizEquipmentBehavior>(clientObject, out var equipmentBehavior))
            {
                Log.Logger.Information("Found ClientWizEquipmentBehavior...");

                var slotList = new List<EquippedSlotInfo>();
                for (int i = 0; i < Enum.GetValues(typeof(EquipmentSlot)).Length; i++)
                {
                    slotList.Add(new EquippedSlotInfo()
                    {
                        m_itemID = (GID)0,
                        m_itemSlotNameID = (uint)i
                    });
                }

                var equipmentBehaviorNew = new ClientWizEquipmentBehavior()
                {
                    m_publicItemList = CreationData.m_equipmentInfoList?.m_infoList,
                    m_equipmentSets = new List<EquipmentSet>(),
                    m_slotList = slotList,
                    m_itemList = new List<CoreObject>()
                };

                equipmentBehaviorCache = equipmentBehaviorNew;
                equipmentBehavior = equipmentBehaviorNew;
            }
            else
                throw new Exception("Behavior ClientWizEquipmentBehavior not found!");

            // =========================================================
            // PLAYER NAME
            // =========================================================
            if (CoreObjectFactory.FindBehaviorInstance<ClientWizPlayerNameBehavior>(clientObject, out var nameBehavior))
            {
                nameBehavior.m_eGender = CreationData.m_avatarBehavior.m_eGender;
                nameBehavior.m_eRace = CreationData.m_avatarBehavior.m_eRace;
                nameBehavior.m_nameKeys = CreationData.m_nameIndices;
            }
            else
                throw new Exception("Behavior ClientWizPlayerNameBehavior not found!");

            // =========================================================
            // INVENTORY
            // =========================================================
            if (CoreObjectFactory.FindBehaviorInstance<ClientWizInventoryBehavior>(clientObject, out var inventoryBehavior))
            {
                inventoryBehavior.m_numItemsAllowed = 75;
                inventoryBehavior.m_numJewelsAllowed = 100;
                inventoryBehavior.m_itemList = new List<CoreObject>();

                new List<ulong>() { 4740, 4705, 5030, 39068, 1363076, 1475149,
                    1472644, 1317133, 1317126, 1317234, 1359455,
                    1392077, 1352341, 1540397 }.ForEach(templateId =>
                {
                    CoreTemplate template = CoreObjectFactory.GetCoreTemplate(templateId);

                    var coreObject = template switch
                    {
                        ItemTemplate => new WizClientObjectItem(),
                        _ => new ClientObject()
                    };

                    coreObject.m_globalID = RandomGen.GenerateGUID();
                    coreObject.m_templateID = (GID)templateId;

                    inventoryBehavior.m_itemList.Add(coreObject);
                });

                inventoryBehaviorCache = inventoryBehavior;
            }
            else
                throw new Exception("Behavior ClientWizInventoryBehavior not found!");

            // =========================================================
            // MAGIC SCHOOL
            // =========================================================
            if (CoreObjectFactory.FindBehaviorInstance<ClientMagicSchoolBehavior>(clientObject, out var schoolBehavior))
            {
                schoolBehavior.m_equippedTeleportEffect = 0;
                schoolBehavior.m_experiencePoints = 0;
                schoolBehavior.m_level = CreationData.m_level;
                schoolBehavior.m_trainingPoints = 0;
                schoolBehavior.m_schoolOfFocus = CreationData.m_schoolOfFocus;
            }
            else
                throw new Exception("Behavior ClientMagicSchoolBehavior not found!");

            // =========================================================
            // SPELLS
            // =========================================================
            if (CoreObjectFactory.FindBehaviorInstance<ClientSpellbookBehavior>(clientObject, out var spellbookBehavior))
            {
                spellbookBehavior.m_spellIDList = new();
            }
            else
                throw new Exception("Behavior ClientSpellbookBehavior not found!");
        }
    }
}
