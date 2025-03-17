using Imlight.Common;
using Imlight.Common.Cryptography;
using System;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Effects;

internal static class GameEffectFactory {
    /// <summary>
    /// Creates a game effect from the given effect info.
    /// </summary>
    /// <param name="info">The information for the effect.</param>
    /// <param name="itemSlotId">The slot of equipment this effect is from.</param>
    /// <returns>The instance of the effect.</returns>
    /// <exception cref="NotImplementedException"></exception>
    internal static GameEffectBase CreateEffectFromInfo(GameEffectInfo info, uint itemSlotId)
    {
        switch (info)
        {
            case ProvideSpellEffectInfo provideSpellEffectInfo:
                return CreateProvideSpellEffect(provideSpellEffectInfo, itemSlotId);
            case StartingPipEffectInfo startingPipEffectInfo:
                return CreateStartingPipEffect(startingPipEffectInfo, itemSlotId);
            case SpeedEffectInfo speedEffectInfo:
                return CreateSpeedEffect(speedEffectInfo, itemSlotId);
            case StatisticEffectInfo statisticEffectInfo:
                return CreateWizStatisticEffect(statisticEffectInfo, itemSlotId);
            default:
                Logger.Warning("Unknown effect type: {0}", Logger.Args(info.GetType().Name));
                return null;
        }
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
        // Specific school effects (such as fire damage) are already handled by the client using
        // the effect name and lookup index.
        var effect = new WizStatisticEffect() {
            m_lookupIndex = info.m_lookupIndex,
            m_effectNameID = StringHash.Compute(info.m_effectName),
            m_itemSlotID = itemSlotId
        };
        var val = CanonicalStatEffects.GetCanonicalStatValue(info);

        // Broad category effects are property specific.
        var effectName = info.m_effectName.ToString();
        switch (effectName) {
            case var _ when effectName.Contains("MaxMana") : effect.m_manaBonus = val; break;
            case var _ when effectName.Contains("MaxHealth") : effect.m_hitPointBonus = val; break;
            case var _ when effectName.Contains("MaxEnergy") : effect.m_energyBonus = val; break;
            case var _ when effectName.Contains("FlatReduceDamage"): effect.m_damageReduceFlat = val; break;
            case var _ when effectName.Contains("FlatDamage") : effect.m_damageBonusFlat = val; break;
            case var _ when effectName.Contains("CriticalHit") : effect.m_criticalHitRating = val; break;
            case var _ when effectName.Contains("Block") : effect.m_blockRating = val; break;
            case var _ when effectName.Contains("PipConversion") : effect.m_pipConversionRating = val; break;
            case var _ when effectName.Contains("ShadowPipRating") : effect.m_shadowPipRating = val; break;
            case var _ when effectName.Contains("PowerPip") : effect.m_powerPipBonusPercent = val; break;
            case var _ when effectName.Contains("ReduceDamage") : effect.m_damageReducePercent = val; break;
            case var _ when effectName.Contains("Damage") : effect.m_damageBonusPercent = val; break;
            case var _ when effectName.Contains("Accuracy") : effect.m_accuracyBonusPercent = val; break;
            case var _ when effectName.Contains("ArmorPiercing") : effect.m_armorPiercingBonusPercent = val; break;
            case var _ when effectName.Contains("FishingLuck") : effect.m_fishingLuckBonusPercent =val; break;
            case var _ when effectName.Contains("IncHealing") : effect.m_healIncBonusPercent = val; break;
            case var _ when effectName.Contains("LifeHealing") : effect.m_healBonusPercent = val; break;
            case var _ when effectName.Contains("StunResistance") : effect.m_stunResistancePercent = val; break;
            case var _ when effectName.Contains("XPPercent") : effect.m_expPercent = val; break;
            case var _ when effectName.Contains("GoldPercent") : effect.m_goldPercent = val; break;
            case "CanonicalStormMastery" : effect.m_stormMastery = 1; break;
            case "CanonicalFireMastery" : effect.m_fireMastery = 1; break;
            case "CanonicalIceMastery" : effect.m_iceMastery = 1; break;
            case "CanonicalLifeMastery" : effect.m_lifeMastery = 1; break;
            case "CanonicalDeathMastery" : effect.m_deathMastery = 1; break;
            case "CanonicalMythMastery" : effect.m_mythMastery = 1; break;
            case "CanonicalBalanceMastery" : effect.m_balanceMastery = 1; break;
            default : break;
        }

        return effect;
    }
}
