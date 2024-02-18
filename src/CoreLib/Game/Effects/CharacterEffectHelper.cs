/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.Common.Cryptography;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Effects;

internal static class CharacterEffectHelper {
    internal static void RecalculateGameStats(Wizard wizard) {
        Logger.Debug("Recalculating game stats for {0}.", Logger.Args(wizard.PlayerNameBehavior.GetWizardName()));

        // Reset the base stats to the default values.
        CharacterHelper.SetBaseStats(wizard.GameStats, (byte) wizard.MagicSchoolBehavior.Level, wizard.MagicSchoolBehavior.MagicSchool);

        // Iterate through the equipped items and apply their effects.
        foreach (var item in wizard.EquipmentBehavior.EquippedItems) {
            var template = ItemHelper.GetItemTemplate(item);
            if (template is null) {
                continue;
            }

            var activatedEffects = AddEffectsFromTemplate(wizard, template);

            Logger.Debug("{0} Applied {1} effects for item {2}.",
                Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), activatedEffects.Count, template.m_objectName));
        }

        Logger.Debug("Game stats recalculated for {0}.", Logger.Args(wizard.PlayerNameBehavior.GetWizardName()));
    }

    /// <summary>
    /// Adds a game effect to the specified game statistics.
    /// </summary>
    /// <param name="stats">The game statistics to modify.</param>
    /// <param name="effectName">The category of the game effect.</param>
    /// <param name="statistic">The specific game effect to apply.</param>
    internal static void AddGameEffectToStats(WizGameStats stats, string effectName, WizStatisticEffect statistic) {
        // Apply effects that don't require a school.
        stats.m_baseHitpoints += (int) statistic.m_hitPointBonus;
        stats.m_baseMana += (int) statistic.m_manaBonus;
        stats.m_powerPipBonusPercentAll += statistic.m_powerPipBonusPercent;
        stats.m_healBonusPercentAll += statistic.m_healBonusPercent;
        stats.m_healIncBonusPercentAll += statistic.m_healIncBonusPercent;

        if (effectName.Contains("Damage") && !effectName.Contains("Reduce")) {
            ApplyDamageIncrease(stats, effectName, statistic);
        }
        else if (effectName.Contains("Accuracy") && !effectName.Contains("Rating")) {
            ApplyAccuracyIncrease(stats, effectName, statistic);
        }
        else if (effectName.Contains("ReduceDamage")) {
            ApplyReduceDamageIncrease(stats, effectName, statistic);
        }
        else if (effectName.Contains("Mastery")) {
            ApplySchoolMastery(stats, effectName);
        }
    }

    /// <summary>
    /// Removes a game effect from the specified game statistics.
    /// </summary>
    /// <param name="stats">The game statistics to remove the effect from.</param>
    /// <param name="effectName">The name of the effect to remove.</param>
    /// <param name="statistic">The statistic effect to remove.</param>
    internal static void RemoveGameEffectFromStats(WizGameStats stats, string effectName, WizStatisticEffect statistic) {
        // Remove effects that don't require a school.
        stats.m_baseHitpoints -= (int) statistic.m_hitPointBonus;
        stats.m_baseMana -= (int) statistic.m_manaBonus;
        stats.m_powerPipBonusPercentAll -= statistic.m_powerPipBonusPercent;
        stats.m_healBonusPercentAll -= statistic.m_healBonusPercent;
        stats.m_healIncBonusPercentAll -= statistic.m_healIncBonusPercent;

        if (effectName.Contains("Damage") && !effectName.Contains("Reduce")) {
            RemoveDamageIncrease(stats, effectName, statistic);
        }
        else if (effectName.Contains("Accuracy") && !effectName.Contains("Rating")) {
            RemoveAccuracyIncrease(stats, effectName, statistic);
        }
        else if (effectName.Contains("ReduceDamage")) {
            RemoveReduceDamageIncrease(stats, effectName, statistic);
        }
        else if (effectName.Contains("Mastery")) {
            RemoveSchoolMastery(stats, effectName);
        }
    }

    internal static List<GameEffectBase> AddEffectsFromTemplate(Wizard wizard, WizItemTemplate template) {
        var addedEffects = new List<GameEffectBase>();
        var slotHash = ItemHelper.GetItemSlotHash(template);

        // Apply the effects from the template.
        foreach (var effectInfo in template.m_equipEffects) {
            var gameEffect = GameEffectFactory.CreateEffectFromInfo(effectInfo, slotHash);
            gameEffect.m_internalID = wizard.GameEffects.Count;

            if (gameEffect is WizStatisticEffect canonicalEffect) {
                var canonicalEffectName = CanonicalStatEffects.GetEffectTemplate(effectInfo.m_effectName).m_effectName;
                AddGameEffectToStats(wizard.GameStats, canonicalEffectName, canonicalEffect);
            }

            wizard.GameEffects.Add(gameEffect);
            addedEffects.Add(gameEffect);
        }

        return addedEffects;
    }

    internal static List<GameEffectBase> RemoveEffectsFromTemplate(Wizard wizard, WizItemTemplate template) {
        var removedEffects = new List<GameEffectBase>();
        var slotHash = ItemHelper.GetItemSlotHash(template);

        // Apply the effects from the template.
        foreach (var effectInfo in template.m_equipEffects) {
            // Find the effect in the player's list of effects.
            var nameHash = StringHash.Compute(effectInfo.m_effectName);
            var gameEffect = wizard.GameEffects.Find(e => e.m_effectNameID == nameHash && e.m_itemSlotID == slotHash);
            if (gameEffect is null) {
                Logger.Warning("Could not find effect {0} in player's list of effects.", Logger.Args(effectInfo.m_effectName));
                continue;
            }

            removedEffects.Add(gameEffect);
            wizard.GameEffects.Remove(gameEffect);

            if (gameEffect is WizStatisticEffect canonicalEffect) {
                var canonicalEffectName = CanonicalStatEffects.GetEffectTemplate(effectInfo.m_effectName).m_effectName;
                RemoveGameEffectFromStats(wizard.GameStats, canonicalEffectName, canonicalEffect);
            }
        }

        return removedEffects;
    }

    private static void ApplySchoolEffect(ref List<float> effectList, string schoolName, float value) {
        // To apply the effects to the player, we need two parts of the effect:
        // 1. The effect template, which tells us what school the effect applies to.
        // 2. The effect itself, which contains the actual values of the effect.
        // For example, fire accuracy:
        // 1. The template has m_effectCategory "FireAccuracy."
        // 2. The effect iself has m_accuracyBonusPercent "0.01."

        // Set the list if it doesn't exist. Give it a count equal to how many schools there are.
        var schools = Enum.GetValues(typeof(MagicSchool));
        effectList ??= new List<float>(new float[schools.Length]);

        // Ensure that the effect list is the same length as the number of schools.
        if (effectList.Count != schools.Length) {
            effectList = new List<float>(new float[schools.Length]);
        }

        // For each school, check if the effect category contains the school name.
        for (var i = 0; i < schools.Length; i++) {
            var school = (MagicSchool) schools.GetValue(i);
            if (schoolName == school.ToString()) {
                effectList[i] += value;
                break;
            }
        }
    }

    private static void ApplySchoolMastery(WizGameStats stats, string effectCategory) {
        switch (effectCategory) {
            case var category when category.Contains("Ice"):
                stats.m_iceMastery = 1;
                break;
            case var category when category.Contains("Life"):
                stats.m_lifeMastery = 1;
                break;
            case var category when category.Contains("Fire"):
                stats.m_fireMastery = 1;
                break;
            case var category when category.Contains("Myth"):
                stats.m_mythMastery = 1;
                break;
            case var category when category.Contains("Death"):
                stats.m_deathMastery = 1;
                break;
            case var category when category.Contains("Storm"):
                stats.m_stormMastery = 1;
                break;
            case var category when category.Contains("Balance"):
                stats.m_balanceMastery = 1;
                break;
        }
    }

    private static void ApplyDamageIncrease(WizGameStats stats, string effectName, WizStatisticEffect statistic) {
        if (effectName.Contains("Flat")) {
            // Flat damage increase
            if (effectName.Contains("All")) {
                stats.m_dmgBonusFlatAll += statistic.m_damageBonusFlat;
            }
            else {
                // Extract the school name from the effect name.
                var schoolName = ExtractSchoolName(effectName);
                ApplySchoolEffect(ref stats.m_dmgBonusFlat, schoolName, statistic.m_damageBonusFlat);
            }
        }
        else {
            // Percent damage increase
            if (effectName.Contains("All")) {
                stats.m_dmgBonusPercentAll += statistic.m_damageBonusPercent;
            }
            else {
                // Extract the school name from the effect name.
                var schoolName = ExtractSchoolName(effectName);
                ApplySchoolEffect(ref stats.m_dmgBonusPercent, schoolName, statistic.m_damageBonusPercent);
            }
        }
    }

    private static void ApplyAccuracyIncrease(WizGameStats stats, string effectName, WizStatisticEffect statistic) {
        // Percent accuracy increase
        if (effectName.Contains("All")) {
            stats.m_accBonusPercentAll += statistic.m_accuracyBonusPercent;
        }
        else {
            // Extract the school name from the effect name.
            var schoolName = ExtractSchoolName(effectName);
            ApplySchoolEffect(ref stats.m_accBonusPercent, schoolName, statistic.m_accuracyBonusPercent);
        }
    }

    private static void ApplyReduceDamageIncrease(WizGameStats stats, string effectName, WizStatisticEffect statistic) {
        if (effectName.Contains("Flat")) {
            // Flat damage increase
            if (effectName.Contains("All")) {
                stats.m_dmgReduceFlatAll += statistic.m_damageReduceFlat;
            }
            else {
                // Extract the school name from the effect name.
                var schoolName = ExtractSchoolName(effectName);
                ApplySchoolEffect(ref stats.m_dmgReduceFlat, schoolName, statistic.m_damageReduceFlat);
            }
        }
        else {
            // Percent damage increase
            if (effectName.Contains("All")) {
                stats.m_dmgReducePercentAll += statistic.m_damageReducePercent;
            }
            else {
                // Extract the school name from the effect name.
                var schoolName = ExtractSchoolName(effectName);
                ApplySchoolEffect(ref stats.m_dmgReducePercent, schoolName, statistic.m_damageReducePercent);
            }
        }
    }

    private static void RemoveSchoolEffect(ref List<float> effectList, string schoolName, float value) {
        var schools = Enum.GetValues(typeof(MagicSchool));
        effectList ??= new List<float>(new float[schools.Length]);

        if (effectList.Count != schools.Length) {
            effectList = new List<float>(new float[schools.Length]);
        }

        for (var i = 0; i < schools.Length; i++) {
            var school = (MagicSchool) schools.GetValue(i);
            if (schoolName == school.ToString()) {
                effectList[i] -= value;
                break;
            }
        }
    }

    private static void RemoveSchoolMastery(WizGameStats stats, string effectCategory) {
        switch (effectCategory) {
            case var category when category.Contains("Ice"):
                stats.m_iceMastery = 0;
                break;
            case var category when category.Contains("Life"):
                stats.m_lifeMastery = 0;
                break;
            case var category when category.Contains("Fire"):
                stats.m_fireMastery = 0;
                break;
            case var category when category.Contains("Myth"):
                stats.m_mythMastery = 0;
                break;
            case var category when category.Contains("Death"):
                stats.m_deathMastery = 0;
                break;
            case var category when category.Contains("Storm"):
                stats.m_stormMastery = 0;
                break;
            case var category when category.Contains("Balance"):
                stats.m_balanceMastery = 0;
                break;
        }
    }

    private static void RemoveDamageIncrease(WizGameStats stats, string effectName, WizStatisticEffect statistic) {
        if (effectName.Contains("Flat")) {
            if (effectName.Contains("All")) {
                stats.m_dmgBonusFlatAll -= statistic.m_damageBonusFlat;
            }
            else {
                var schoolName = ExtractSchoolName(effectName);
                RemoveSchoolEffect(ref stats.m_dmgBonusFlat, schoolName, statistic.m_damageBonusFlat);
            }
        }
        else {
            if (effectName.Contains("All")) {
                stats.m_dmgBonusPercentAll -= statistic.m_damageBonusPercent;
            }
            else {
                var schoolName = ExtractSchoolName(effectName);
                RemoveSchoolEffect(ref stats.m_dmgBonusPercent, schoolName, statistic.m_damageBonusPercent);
            }
        }
    }

    private static void RemoveAccuracyIncrease(WizGameStats stats, string effectName, WizStatisticEffect statistic) {
        if (effectName.Contains("All")) {
            stats.m_accBonusPercentAll -= statistic.m_accuracyBonusPercent;
        }
        else {
            var schoolName = ExtractSchoolName(effectName);
            RemoveSchoolEffect(ref stats.m_accBonusPercent, schoolName, statistic.m_accuracyBonusPercent);
        }
    }

    private static void RemoveReduceDamageIncrease(WizGameStats stats, string effectName, WizStatisticEffect statistic) {
        if (effectName.Contains("Flat")) {
            if (effectName.Contains("All")) {
                stats.m_dmgReduceFlatAll -= statistic.m_damageReduceFlat;
            }
            else {
                var schoolName = ExtractSchoolName(effectName);
                RemoveSchoolEffect(ref stats.m_dmgReduceFlat, schoolName, statistic.m_damageReduceFlat);
            }
        }
        else {
            if (effectName.Contains("All")) {
                stats.m_dmgReducePercentAll -= statistic.m_damageReducePercent;
            }
            else {
                var schoolName = ExtractSchoolName(effectName);
                RemoveSchoolEffect(ref stats.m_dmgReducePercent, schoolName, statistic.m_damageReducePercent);
            }
        }
    }

    private static string ExtractSchoolName(string input) {
        // Define a regular expression pattern to match the word after "Canonical" and before any subsequent uppercase letters
        var regex = new Regex(@"Canonical([A-Z][a-z]*)");
        var match = regex.Match(input);

        if (match.Success) {
            // Extract and return the captured group value (the school name)
            return match.Groups[1].Value;
        }

        // Return null or an appropriate value if no match is found
        return null;
    }
}
