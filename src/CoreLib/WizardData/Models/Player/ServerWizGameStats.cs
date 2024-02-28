/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using Imlight.Common.Configuration;
using Imlight.Common.IO;
using Imlight.CoreLib.WizardData.Implementations;
using Newtonsoft.Json;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Models.Player;

[Serializable]
public class ServerWizGameStats : IClientTypeProvider<WizGameStats> {
    // These are stats relevant to the player's character, and not ones we can calculate from any other data.
    public int m_currentHitpoints;
    public int m_currentGold;
    public int m_currentEventCurrency1;
    public int m_currentEventCurrency2;
    public int m_currentPvPCurrency;
    public int m_currentMana;
    public int m_currentArenaPoints;
    public List<int> m_spellChargeBase;
    public float m_potionCharge;
    public Ladder m_pArenaLadder;
    public Ladder m_pDerbyLadder;
    public Ladder m_bracketLader;
    public int m_bonusHitpoints;
    public int m_bonusMana;
    public int m_bonusEnergy;
    public int m_referenceLevel;
    public byte m_gardeningLevel;
    public int m_gardeningXP;
    public bool m_invisibleToFriends;
    public bool m_showItemLock;
    public bool m_questFinderEnabled;
    public int m_buddyListLimit;
    public bool m_dontAllowFriendFinderCodes;
    public bool m_shadowMagicUnlocked;
    public byte m_fishingLevel;
    public int m_fishingXP;
    public uint m_subscriberBenefitFlags;
    public uint m_elixirBenefitFlags;
    public byte m_monsterMagicLevel;
    public int m_monsterMagicXP;
    public bool m_playerChatChannelIsPublic;
    public int m_extraInventorySpace;
    public bool m_rememberLastRealm;
    public bool m_newSpellbookLayoutWarning;
    public int m_pipConversionBaseAllSchools;
    public uint m_purchasedCustomEmotes1;
    public uint m_purchasedCustomTeleportEffects1;
    public uint m_equippedTeleportEffect;
    public uint m_highestWorld1ID;
    public uint m_highestWorld2ID;
    public List<uint> m_activeClassProjectsList;
    public List<uint> m_disabledItemSlotIDs;
    public uint m_adventurePowerCooldownTime;
    public uint m_purchasedCustomEmotes2;
    public uint m_purchasedCustomTeleportEffects2;
    public uint m_purchasedCustomEmotes3;
    public uint m_purchasedCustomTeleportEffects3;
    public bool m_friendlyPlayer;
    public int m_emojiSkinTone;
    public uint m_showPVPOption;
    public int m_favoriteSlot;
    public byte m_cantripLevel;
    public int m_cantripXP;
    public int m_levelScaled;
    public int m_baseGoldPouch;

    // These are stats that we can calculate from other data, and don't need to be stored in the player's character data.
    [JsonIgnore] public int m_baseHitpoints;
    [JsonIgnore] public int m_baseMana;
    [JsonIgnore] public int m_baseEventCurrency1Pouch;
    [JsonIgnore] public int m_baseEventCurrency2Pouch;
    [JsonIgnore] public int m_basePvPCurrencyPouch;
    [JsonIgnore] public int m_energyMax;
    [JsonIgnore] public float m_potionMax;
    [JsonIgnore] public List<float> m_dmgBonusPercent;
    [JsonIgnore] public List<float> m_dmgBonusFlat;
    [JsonIgnore] public List<float> m_accBonusPercent;
    [JsonIgnore] public List<float> m_apBonusPercent;
    [JsonIgnore] public List<float> m_dmgReducePercent;
    [JsonIgnore] public List<float> m_dmgReduceFlat;
    [JsonIgnore] public List<float> m_accReducePercent;
    [JsonIgnore] public List<float> m_healBonusPercent;
    [JsonIgnore] public List<float> m_healIncBonusPercent;
    [JsonIgnore] public List<int> m_spellChargeBonus;
    [JsonIgnore] public float m_dmgBonusPercentAll;
    [JsonIgnore] public float m_dmgBonusFlatAll;
    [JsonIgnore] public float m_accBonusPercentAll;
    [JsonIgnore] public float m_apBonusPercentAll;
    [JsonIgnore] public float m_dmgReducePercentAll;
    [JsonIgnore] public float m_dmgReduceFlatAll;
    [JsonIgnore] public float m_accReducePercentAll;
    [JsonIgnore] public float m_healBonusPercentAll;
    [JsonIgnore] public float m_healIncBonusPercentAll;
    [JsonIgnore] public int m_spellChargeBonusAll;
    [JsonIgnore] public float m_powerPipBase;
    [JsonIgnore] public float m_powerPipBonusPercentAll;
    [JsonIgnore] public float m_xpPercentIncrease;
    [JsonIgnore] public List<float> m_criticalHitPercentBySchool;
    [JsonIgnore] public List<float> m_blockPercentBySchool;
    [JsonIgnore] public List<float> m_criticalHitRatingBySchool;
    [JsonIgnore] public List<float> m_blockRatingBySchool;
    [JsonIgnore] public int m_balanceMastery;
    [JsonIgnore] public int m_deathMastery;
    [JsonIgnore] public int m_fireMastery;
    [JsonIgnore] public int m_iceMastery;
    [JsonIgnore] public int m_lifeMastery;
    [JsonIgnore] public int m_mythMastery;
    [JsonIgnore] public int m_stormMastery;
    [JsonIgnore] public int m_maximumNumberOfIslands;
    [JsonIgnore] public float m_stunResistancePercent;
    [JsonIgnore] public int m_shadowPipMax;
    [JsonIgnore] public List<float> m_fishingLuckBonusPercent;
    [JsonIgnore] public float m_fishingLuckBonusPercentAll;
    [JsonIgnore] public List<float> m_pipConversionRatingPerSchool;
    [JsonIgnore] public float m_pipConversionPercentAll;
    [JsonIgnore] public List<float> m_pipConversionPercentPerSchool;
    [JsonIgnore] public List<int> m_pipConversionBasePerSchool;
    [JsonIgnore] public float m_archmasteryBase;
    [JsonIgnore] public float m_archmasteryBonus;
    [JsonIgnore] public float m_criticalHitPercentAll;
    [JsonIgnore] public float m_blockPercentAll;
    [JsonIgnore] public float m_criticalHitRatingAll;
    [JsonIgnore] public float m_blockRatingAll;
    [JsonIgnore] public float m_shadowPipRating;
    [JsonIgnore] public float m_bonusShadowPipRating;
    [JsonIgnore] public float m_shadowPipRateAccumulated;
    [JsonIgnore] public float m_shadowPipRateThreshold;
    [JsonIgnore] public int m_shadowPipRatePercentage;
    [JsonIgnore] public int m_highestCharacterLevelOnAccount;
    [JsonIgnore] public int m_highestCharacterWorldOnAccount;
    [JsonIgnore] public uint m_schoolID;
    [JsonIgnore] public ByteString m_currentZoneName;
    [JsonIgnore] public int m_petActChance;
    [JsonIgnore] public float m_shadowPipBonusPercent;
    [JsonIgnore] public float m_wispBonusPercent;
    [JsonIgnore] public float m_pipConversionRatingAll;

    [JsonIgnore] public MagicSchool MagicSchool;
    [JsonIgnore] public int Level;

    // ctor
    public ServerWizGameStats(MagicSchool magicSchool, int level) {
        MagicSchool = magicSchool;
        Level = level;

        m_baseGoldPouch = ConfigurationManager.Settings.BaseGoldPouch;
    }

    internal void SetBaseStats() {
        var baseHealth = WizardClassData.GetClassHealthAtLevel(MagicSchool, Level);
        var baseMana = WizardClassData.GetManaAtLevel(Level);
        var powerPipChance = WizardClassData.GetPowerPipChanceAtLevel(Level);
        var energyMax = WizardClassData.GetPetEnergyAtLevel(Level);

        this.m_baseHitpoints = baseHealth;
        this.m_baseMana = baseMana;
        this.m_powerPipBase = powerPipChance;
        this.m_energyMax = energyMax;
    }

    public WizGameStats GetClientTypeAlternative() {
        return new WizGameStats() {
            // We want *only* base level/magic school stats here.
            // We can't send the character game stats because the EquipmentService will broadcast the equipment effects,
            // causing each stat to duplicate.
            m_baseHitpoints = WizardClassData.GetClassHealthAtLevel(MagicSchool, Level),
            m_baseMana = WizardClassData.GetManaAtLevel(Level),
            m_energyMax = WizardClassData.GetPetEnergyAtLevel(Level),
            m_powerPipBase = WizardClassData.GetPowerPipChanceAtLevel(Level),

            m_baseGoldPouch = m_baseGoldPouch,
            m_currentHitpoints = m_currentHitpoints,
            m_currentGold = m_currentGold,
            m_currentEventCurrency1 = m_currentEventCurrency1,
            m_currentEventCurrency2 = m_currentEventCurrency2,
            m_currentPvPCurrency = m_currentPvPCurrency,
            m_currentMana = m_currentMana,
            m_currentArenaPoints = m_currentArenaPoints,
            m_spellChargeBase = m_spellChargeBase,
            m_potionCharge = m_potionCharge,
            m_pArenaLadder = m_pArenaLadder,
            m_pDerbyLadder = m_pDerbyLadder,
            m_bracketLader = m_bracketLader,
            m_bonusHitpoints = m_bonusHitpoints,
            m_bonusMana = m_bonusMana,
            m_bonusEnergy = m_bonusEnergy,
            m_referenceLevel = m_referenceLevel,
            m_gardeningLevel = m_gardeningLevel,
            m_gardeningXP = m_gardeningXP,
            m_invisibleToFriends = m_invisibleToFriends,
            m_showItemLock = m_showItemLock,
            m_questFinderEnabled = m_questFinderEnabled,
            m_buddyListLimit = m_buddyListLimit,
            m_dontAllowFriendFinderCodes = m_dontAllowFriendFinderCodes,
            m_shadowMagicUnlocked = m_shadowMagicUnlocked,
            m_fishingLevel = m_fishingLevel,
            m_fishingXP = m_fishingXP,
            m_subscriberBenefitFlags = m_subscriberBenefitFlags,
            m_elixirBenefitFlags = m_elixirBenefitFlags,
            m_monsterMagicLevel = m_monsterMagicLevel,
            m_monsterMagicXP = m_monsterMagicXP,
            m_playerChatChannelIsPublic = m_playerChatChannelIsPublic,
            m_extraInventorySpace = m_extraInventorySpace,
            m_rememberLastRealm = m_rememberLastRealm,
            m_newSpellbookLayoutWarning = m_newSpellbookLayoutWarning,
            m_pipConversionBaseAllSchools = m_pipConversionBaseAllSchools,
            m_purchasedCustomEmotes1 = m_purchasedCustomEmotes1,
            m_purchasedCustomTeleportEffects1 = m_purchasedCustomTeleportEffects1,
            m_equippedTeleportEffect = m_equippedTeleportEffect,
            m_highestWorld1ID = m_highestWorld1ID,
            m_highestWorld2ID = m_highestWorld2ID,
            m_activeClassProjectsList = m_activeClassProjectsList,
            m_disabledItemSlotIDs = m_disabledItemSlotIDs,
            m_adventurePowerCooldownTime = m_adventurePowerCooldownTime,
            m_purchasedCustomEmotes2 = m_purchasedCustomEmotes2,
            m_purchasedCustomTeleportEffects2 = m_purchasedCustomTeleportEffects2,
            m_purchasedCustomEmotes3 = m_purchasedCustomEmotes3,
            m_purchasedCustomTeleportEffects3 = m_purchasedCustomTeleportEffects3,
            m_friendlyPlayer = m_friendlyPlayer,
            m_emojiSkinTone = m_emojiSkinTone,
            m_showPVPOption = m_showPVPOption,
            m_favoriteSlot = m_favoriteSlot,
            m_cantripLevel = m_cantripLevel,
            m_cantripXP = m_cantripXP,
            m_levelScaled = m_levelScaled
        };
    }
}
