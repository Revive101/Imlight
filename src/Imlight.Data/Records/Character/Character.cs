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

namespace Imlight.Data
{
    public class Character
    {
        /// <summary>
        /// The ID of the character.
        /// </summary>
        public ulong ID { get; private set; }

        /// <summary>
        /// The Template ID of a character. This is the ID of the model.
        /// </summary>
        public ulong TemplateID { get; private set; }

        /// <summary>
        /// The character creation data. Shown in the charater selection screen.
        /// </summary>
        public WizardCharacterCreationInfo CreationData { get; private set; }

        /// <summary>
        /// This is a custom name field. It does not use name indicates and can be any string.
        /// Will overwrite any other name.
        /// </summary>
        public string Name { get; private set; }

        public Character(WizardCharacterCreationInfo creationData)
        {
            this.ID = RandomGen.GenerateId();

            this.CreationData = creationData;
            this.CreationData.m_level = 1;
            this.CreationData.m_userID = new GID(RandomGen.GenerateId());
        }

        public WizClientObject GetWizClientObject()
        {
            var clientObject = CoreObjectFactory.InitializeCoreObject(new WizClientObject(), (uint)CreationData.m_templateID);

            SetWizClientBehaviors(ref clientObject);

            // get this from database instead
            clientObject.m_gameStats = new WizGameStats()
            {
                m_baseMana = 15,
                m_currentMana = 15,
                m_baseGoldPouch = 1000000,
                m_baseHitpoints = 500,
                m_currentHitpoints = 500
            };
            clientObject.m_fScale = 1f;
            clientObject.m_globalID = RandomGen.GenerateId();
            clientObject.m_characterId = new GID(RandomGen.GenerateId());
            clientObject.m_permID = 0; // What is this?
            clientObject.m_location = new SharpDX.Vector3(0, 0, 0);

            return clientObject;
        }

        private void SetWizClientBehaviors(ref WizClientObject clientObject)
        {
            // Very lazy setup. Debugging purposes only.
            if (CoreObjectFactory.FindBehaviorInstance<ClientWizPlayerNameBehavior>(clientObject, out var nameBehavior))
            {
                var nameObj = new ClientWizPlayerNameBehavior()
                {
                    m_eGender = CreationData.m_avatarBehavior.m_eGender,
                    m_eRace = CreationData.m_avatarBehavior.m_eRace,
                    m_nameKeys = CreationData.m_nameIndices,
                    m_useRank = true,
                    m_pvpIconID = 0,
                    m_badgeTitle = "Test",
                    m_chatPermissions = 0,
                };

                var idx = clientObject.m_inactiveBehaviors.IndexOf(nameBehavior);
                clientObject.m_inactiveBehaviors[idx] = nameObj;
            }
            else
                throw new Exception("Behavior ClientWizPlayerNameBehavior not found!");

            if (CoreObjectFactory.FindBehaviorInstance<ClientWizEquipmentBehavior>(clientObject, out var equipmentBehavior))
            {
                var esi = new List<EquippedSlotInfo>();
                foreach (var slot in (EquipmentSlot[])Enum.GetValues(typeof(EquipmentSlot)))
                {
                    esi.Add(new EquippedSlotInfo()
                    {
                        m_itemID = (GID)0,
                        m_itemSlotNameID = (uint)slot
                    });
                }

                var equipObj = new ClientWizEquipmentBehavior()
                {
                    m_publicItemList = CreationData.m_equipmentInfoList?.m_infoList,
                    m_slotList = esi,
                    m_itemList = new List<CoreObject>()
                };

                var index = clientObject.m_inactiveBehaviors.IndexOf(equipmentBehavior);
                clientObject.m_inactiveBehaviors[index] = (ClientWizEquipmentBehavior)equipObj;
            }
            else
                throw new Exception("Behavior ClientWizPlayerNameBehavior not found!");

            if (CoreObjectFactory.FindBehaviorInstance<ClientWizInventoryBehavior>(clientObject, out var inventoryBehavior))
            {
                var inventoryObj = new ClientWizInventoryBehavior()
                {
                    m_numItemsAllowed = 75,
                    m_numJewelsAllowed = 100,
                    m_itemList = new List<CoreObject>()
                };

                var index = clientObject.m_inactiveBehaviors.IndexOf(inventoryBehavior);
                clientObject.m_inactiveBehaviors[index] = (ClientWizInventoryBehavior)inventoryObj;
            }
            else
                throw new Exception("Behavior ClientWizPlayerNameBehavior not found!");

            if (CoreObjectFactory.FindBehaviorInstance<ClientMagicSchoolBehavior>(clientObject, out var schoolBehavior))
            {
                var magicObj = new ClientMagicSchoolBehavior()
                {
                    m_equippedTeleportEffect = 0,
                    m_experiencePoints = 0,
                    m_level = 1,
                    m_trainingPoints = 0,
                    m_schoolOfFocus = CreationData.m_schoolOfFocus
                };

                var index = clientObject.m_inactiveBehaviors.IndexOf(schoolBehavior);
                clientObject.m_inactiveBehaviors[index] = (ClientMagicSchoolBehavior)magicObj;
            }
            else
                throw new Exception("Behavior ClientWizPlayerNameBehavior not found!");
        }
    }
}
