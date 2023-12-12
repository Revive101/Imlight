using Imlight.Common.Cryptography;
using System;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Effects;

internal static class GameEffectFactory {
    internal static GameEffectBase CreateEffectFromInfo(GameEffectInfo info, uint itemSlotId) {
        return info switch {
            ProvideSpellEffectInfo provideSpellEffectInfo => CreateProvideSpellEffect(provideSpellEffectInfo, itemSlotId),
            StartingPipEffectInfo startingPipEffectInfo => CreateStartingPipEffect(startingPipEffectInfo, itemSlotId),
            SpeedEffectInfo speedEffectInfo => CreateSpeedEffect(speedEffectInfo, itemSlotId),
            StatisticEffectInfo statisticEffectInfo => CreateWizStatisticEffect(statisticEffectInfo, itemSlotId),
            _ => throw new NotImplementedException(),
        };
    }

    private static ProvideSpellEffect CreateProvideSpellEffect(ProvideSpellEffectInfo info, uint itemSlotId) {
        var effect = new ProvideSpellEffect() {
            m_spellName = info.m_spellName,
            m_numSpells = info.m_numSpells,
            m_vFX = info.m_vFX,
            m_vFXOverride = info.m_vFXOverride,
            m_sound = info.m_sound,
            m_effectNameID = StringHash.Compute(info.m_effectName),
            m_itemSlotID = itemSlotId
        };

        return effect;
    }

    private static StartingPipEffect CreateStartingPipEffect(StartingPipEffectInfo info, uint itemSlotId) {
        var effect = new StartingPipEffect() {
            m_pipsGiven = info.m_pipsGiven,
            m_powerPipsGiven = info.m_powerPipsGiven,
            m_effectNameID = StringHash.Compute(info.m_effectName),
            m_itemSlotID = itemSlotId
        };

        return effect;
    }

    private static SpeedEffect CreateSpeedEffect(SpeedEffectInfo info, uint itemSlotId) {
        var effect = new SpeedEffect() {
            m_speedMultiplier = info.m_speedMultiplier,
            m_effectNameID = StringHash.Compute(info.m_effectName),
            m_itemSlotID = itemSlotId
        };

        return effect;
    }

    private static WizStatisticEffect CreateWizStatisticEffect(StatisticEffectInfo info, uint itemSlotId) {
        var effect = new WizStatisticEffect() {
            m_lookupIndex = info.m_lookupIndex,
            m_effectNameID = StringHash.Compute(info.m_effectName),
            m_itemSlotID = itemSlotId
        };

        // Read it and weep.
        var effectName = info.m_effectName.ToString();
        switch (effectName) {
            case var _ when effectName.Contains("MaxMana")         : effect.m_manaBonus = info.m_lookupIndex + 98;                          break;
            case var _ when effectName.Contains("MaxHealth")       : effect.m_hitPointBonus = info.m_lookupIndex + 1;                       break;
            case var _ when effectName.Contains("MaxEnergy")       : effect.m_energyBonus = info.m_lookupIndex + 1;                         break;
            case var _ when effectName.Contains("FlatReduceDamage"): effect.m_damageReduceFlat = info.m_lookupIndex + 1;                    break;
            case var _ when effectName.Contains("FlatDamage")      : effect.m_damageBonusFlat = info.m_lookupIndex + 1;                     break;
            case var _ when effectName.Contains("CriticalHit")     : effect.m_criticalHitRating = info.m_lookupIndex + 1;                   break;
            case var _ when effectName.Contains("Block")           : effect.m_blockRating = info.m_lookupIndex + 1;                         break;
            case var _ when effectName.Contains("PipConversion")   : effect.m_pipConversionRating = info.m_lookupIndex + 1;                 break;
            case var _ when effectName.Contains("ShadowPipRating") : effect.m_shadowPipRating = info.m_lookupIndex + 1;                     break;
            case var _ when effectName.Contains("PowerPip")        : effect.m_powerPipBonusPercent = (info.m_lookupIndex - 99) / 100f;      break;
            case var _ when effectName.Contains("ReduceDamage")    : effect.m_damageReducePercent = (info.m_lookupIndex - 99) / 100f;       break;
            case var _ when effectName.Contains("Damage")          : effect.m_damageBonusPercent = (info.m_lookupIndex - 99) / 100f;        break;
            case var _ when effectName.Contains("Accuracy")        : effect.m_accuracyBonusPercent = (info.m_lookupIndex - 99) / 100f;      break;
            case var _ when effectName.Contains("ArmorPiercing")   : effect.m_armorPiercingBonusPercent = (info.m_lookupIndex - 99) / 100f; break;
            case var _ when effectName.Contains("FishingLuck")     : effect.m_fishingLuckBonusPercent = (info.m_lookupIndex - 99) / 100f;   break;
            case var _ when effectName.Contains("IncHealing")      : effect.m_healIncBonusPercent = (info.m_lookupIndex - 99) / 100f;       break;
            case var _ when effectName.Contains("LifeHealing")     : effect.m_healBonusPercent = (info.m_lookupIndex - 99) / 100f;          break;
            case var _ when effectName.Contains("StunResistance")  : effect.m_stunResistancePercent = (info.m_lookupIndex - 99) / 100f;     break;
            case var _ when effectName.Contains("XPPercent")       : effect.m_expPercent = (info.m_lookupIndex - 99) / 100f;                break;
            case var _ when effectName.Contains("GoldPercent")     : effect.m_goldPercent = (info.m_lookupIndex - 99) / 100f;               break;
            case "CanonicalStormMastery"                           : effect.m_stormMastery = 1;                                             break;
            case "CanonicalFireMastery"                            : effect.m_fireMastery = 1;                                              break;
            case "CanonicalIceMastery"                             : effect.m_iceMastery = 1;                                               break;
            case "CanonicalLifeMastery"                            : effect.m_lifeMastery = 1;                                              break;
            case "CanonicalDeathMastery"                           : effect.m_deathMastery = 1;                                             break;
            case "CanonicalMythMastery"                            : effect.m_mythMastery = 1;                                              break;
            case "CanonicalBalanceMastery"                         : effect.m_balanceMastery = 1;                                           break;
            default                                                :                                                                        break;
        }

        return effect;
    }
}
