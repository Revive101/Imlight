using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WizUnraveler.Cache;
using WizUnraveler.ObjectProperty;
using static WizUnraveler.Cache.TypeCache;
using Imlight.Common;
using WizUnraveler;
using Imlight.Resources;

namespace Imlight.Data
{
    public class Character
    {
        private const int    STARTING_LEVEL = 1;
        private const string STARTING_LOCATION = "WizardCity/WC_Golem_Tower";
        private const int    STARTING_BASE_MANA = 15;
        private const int    STARTING_BASE_HEALTH = 500;
        private const int    STARTING_BASE_GOLD = 1000000;
        
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
            this.CreationData = creationData;
            this.CreationData.m_level = STARTING_LEVEL;  
            this.CreationData.m_location = STARTING_LOCATION;
            this.CreationData.m_globalID = RandomGen.GenerateGUID();
            this.CreationData.m_userID = accountId;
            this.CreationData.m_equipmentInfoList = new EquippedItemInfoList()
            {
                m_infoList = new List<EquippedItemInfo>()
            };
        }

        public WizClientObject GetWizClientObject()
        {
            var clientObject = CoreObjectFactory.InitializeCoreObject(
                new WizClientObject(), 
                (uint)CreationData.m_templateID);

            ReplaceWizAvatarWithCreationData(clientObject);
            SetWizClientBehaviors(ref clientObject);

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
            //if (CoreObjectFactory.FindBehaviorInstance<ClientWizEquipmentBehavior>(clientObject, out var equipmentBehavior))
            //{
            //    var esi = new List<EquippedSlotInfo>();
            //    foreach (var slot in (EquipmentSlot[])Enum.GetValues(typeof(EquipmentSlot)))
            //    {
            //        esi.Add(new EquippedSlotInfo()
            //        {
            //            m_itemID = (GID)0,
            //            m_itemSlotNameID = (uint)slot
            //        });
            //    }

            //    equipmentBehavior.m_publicItemList = CreationData.m_equipmentInfoList?.m_infoList;
            //    equipmentBehavior.m_equipmentSets = new List<EquipmentSet>();
            //    equipmentBehavior.m_slotList = esi;
            //    equipmentBehavior.m_itemList = new List<CoreObject>();
            //}
            //else
            //    throw new Exception("Behavior ClientWizEquipmentBehavior not found!");

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
            //if (CoreObjectFactory.FindBehaviorInstance<ClientWizInventoryBehavior>(clientObject, out var inventoryBehavior))
            //{
            //    inventoryBehavior.m_numItemsAllowed = 75;
            //    inventoryBehavior.m_numJewelsAllowed = 100;
            //    inventoryBehavior.m_itemList = new List<CoreObject>();
            //}
            //else
            //    throw new Exception("Behavior ClientWizInventoryBehavior not found!");

            // =========================================================
            // MAGIC SCHOOL
            // =========================================================
            //if (CoreObjectFactory.FindBehaviorInstance<ClientMagicSchoolBehavior>(clientObject, out var schoolBehavior))
            //{
            //    schoolBehavior.m_equippedTeleportEffect = 0;
            //    schoolBehavior.m_experiencePoints = 0;
            //    schoolBehavior.m_level = CreationData.m_level;
            //    schoolBehavior.m_trainingPoints = 0;
            //    schoolBehavior.m_schoolOfFocus = CreationData.m_schoolOfFocus;
            //}
            //else
            //    throw new Exception("Behavior ClientMagicSchoolBehavior not found!");
        }
    }
}
