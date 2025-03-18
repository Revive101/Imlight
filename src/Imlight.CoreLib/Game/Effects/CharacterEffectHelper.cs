/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Imcodec.Cryptography;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Effects;

/// <summary>
/// Helper class for adding and removing effects to/from a wizard character.
/// </summary>
internal static class CharacterEffectHelper {

    /// <summary>
    /// Adds effects to a wizard based on a given template.
    /// </summary>
    /// <param name="wizard">The wizard to add effects to.</param>
    /// <param name="template">The template containing the effects to be added.</param>
    /// <returns>A list of the added effects.</returns>
    internal static List<GameEffectBase> AddEffectsToWizard(Wizard wizard, WizItemTemplate template) {
        var addedEffects = new List<GameEffectBase>();
        var slotHash = ItemHelper.GetItemSlotHash(template);

        // Apply the effects from the template.
        foreach (var effectInfo in template.m_equipEffects) {
            var gameEffect = GameEffectFactory.CreateEffectFromInfo(effectInfo, slotHash);
            if (gameEffect is null) {
                Logger.Warning("Could not create effect {0} from effect info.", Logger.Args(effectInfo.m_effectName));
                continue;
            }

            gameEffect.m_internalID = wizard.GameEffects.Count;
            var effect = AddGameEffectToStats(wizard.GameStats, effectInfo);

            if (gameEffect is ProvideSpellEffect provideSpellEffect) {
                var spells = SpellFactory.CreateSpellsFromEffect(provideSpellEffect);

                if (spells is null) {
                    continue;
                }

                foreach (var spell in spells) {
                    wizard.AddTemporarySpell(spell);
                }
            }

            wizard.GameEffects.Add(gameEffect);
            addedEffects.Add(gameEffect);
        }

        return addedEffects;
    }

    /// <summary>
    /// Adds the effects from a WizItemTemplate to the ServerWizGameStats.
    /// </summary>
    /// <param name="stats">The ServerWizGameStats to add the effects to.</param>
    /// <param name="template">The WizItemTemplate containing the effects.</param>
    internal static void AddEffectsToGameStats(ServerWizGameStats stats, WizItemTemplate template) {
        foreach (var effectInfo in template.m_equipEffects) {
            AddGameEffectToStats(stats, effectInfo);
        }
    }

    /// <summary>
    /// Removes the effects from the game stats based on the given template.
    /// </summary>
    /// <param name="stats">The game stats to remove the effects from.</param>
    /// <param name="template">The template containing the effects to be removed.</param>
    internal static void RemoveEffectsFromGameStats(ServerWizGameStats stats, WizItemTemplate template) {
        foreach (var effectInfo in template.m_equipEffects) {
            RemoveGameEffectFromStats(stats, effectInfo);
        }
    }

    /// <summary>
    /// Removes the effects from a wizard based on a given WizItemTemplate.
    /// </summary>
    /// <param name="wizard">The wizard to remove effects from.</param>
    /// <param name="template">The WizItemTemplate containing the effects to be removed.</param>
    /// <returns>A list of GameEffectBase objects that were removed from the wizard.</returns>
    internal static List<GameEffectBase> RemoveEffectsFromWizard(Wizard wizard, WizItemTemplate template) {
        var removedEffects = new List<GameEffectBase>();
        var slotHash = ItemHelper.GetItemSlotHash(template);

        // Apply the effects from the template.
        foreach (var effectInfo in template.m_equipEffects) {
            // Find the effect in the player's list of effects.
            var nameHash = StringHash.Compute(effectInfo.m_effectName);
            var gameEffect = wizard.GameEffects.Find(e => e.m_effectNameID == nameHash && e.m_itemSlotID == slotHash);
            if (gameEffect is null) {
                // It's fine if the effect isn't found. Just continue.
                continue;
            }

            removedEffects.Add(gameEffect);
            wizard.GameEffects.Remove(gameEffect);
            RemoveGameEffectFromStats(wizard.GameStats, effectInfo);

            if (gameEffect is ProvideSpellEffect provideSpellEffect) {
                for (var i = 0; i < provideSpellEffect.m_numSpells; i++) {
                    var hash = StringHash.Compute(provideSpellEffect.m_spellName);
                    wizard.RemoveTemporarySpell(hash);
                }
            }
        }

        return removedEffects;
    }

    internal static GameEffectBase AddGameEffectToStats(ServerWizGameStats stats, GameEffectInfo effectInfo) {
        var gameEffect = GameEffectFactory.CreateEffectFromInfo(effectInfo, 0);
        if (gameEffect is null) {
            Logger.Warning("Could not create effect {0} from effect info.", Logger.Args(effectInfo.m_effectName));
            return null;
        }

        if (gameEffect is WizStatisticEffect canonicalEffect) {
            var canonicalEffectName = CanonicalStatEffects.GetEffectTemplate(effectInfo.m_effectName).m_effectName;
            AddStatisticEffectToStats(stats, canonicalEffectName, canonicalEffect);
        }
        else if (gameEffect is StartingPipEffect pipEffect) {
            stats.m_startingPips += (byte) pipEffect.m_pipsGiven;
            stats.m_startingPowerPips += (byte) pipEffect.m_powerPipsGiven;
        }

        return gameEffect;
    }

    internal static GameEffectBase RemoveGameEffectFromStats(ServerWizGameStats stats, GameEffectInfo effectInfo) {
        var gameEffect = GameEffectFactory.CreateEffectFromInfo(effectInfo, 0);
        if (gameEffect is null) {
            Logger.Warning("Could not create effect {0} from effect info.", Logger.Args(effectInfo.m_effectName));
            return null;
        }

        if (gameEffect is WizStatisticEffect canonicalEffect) {
            var canonicalEffectName = CanonicalStatEffects.GetEffectTemplate(effectInfo.m_effectName).m_effectName;
            RemoveStatisticEffectFromStats(stats, canonicalEffectName, canonicalEffect);
        }
        else if (gameEffect is StartingPipEffect pipEffect) {
            stats.m_startingPips -= (byte) pipEffect.m_pipsGiven;
            stats.m_startingPowerPips -= (byte) pipEffect.m_powerPipsGiven;
        }

        return gameEffect;
    }

    /// <summary>
    /// Adds a game effect to the specified game statistics.
    /// </summary>
    /// <param name="stats">The game statistics to modify.</param>
    /// <param name="effectName">The category of the game effect.</param>
    /// <param name="statistic">The specific game effect to apply.</param>
    internal static void AddStatisticEffectToStats(ServerWizGameStats stats, string effectName, WizStatisticEffect statistic) {
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
    internal static void RemoveStatisticEffectFromStats(ServerWizGameStats stats, string effectName, WizStatisticEffect statistic) {
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

    private static void ApplySchoolEffect(ref List<float> effectList, string schoolName, float value) {
        var maxIndex = MagicSchools.GetMaxMagicSchoolIndex();

        // Set the list if it doesn't exist. Give it a count equal to how many schools there are.
        effectList ??= [.. Enumerable.Repeat(0f, (int) maxIndex)];

        // Ensure that the effect list is the same length as the number of schools.
        if (effectList.Count != maxIndex) {
            var compensationRequired = maxIndex - effectList.Count;
            effectList.AddRange(Enumerable.Repeat(0f, (int) compensationRequired));
        }

        var schoolTemplate = MagicSchools.GetMagicSchool(schoolName);
        if (schoolTemplate is null) {
            Logger.Warning("Could not find magic school {0}.", Logger.Args(schoolName));
            return;
        }

        var schoolIndex = schoolTemplate.m_schoolIndex;
        effectList[schoolIndex] += value;
    }

    private static void ApplySchoolMastery(ServerWizGameStats stats, string effectCategory) {
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

    private static void ApplyDamageIncrease(ServerWizGameStats stats, string effectName, WizStatisticEffect statistic) {
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

    private static void ApplyAccuracyIncrease(ServerWizGameStats stats, string effectName, WizStatisticEffect statistic) {
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

    private static void ApplyReduceDamageIncrease(ServerWizGameStats stats, string effectName, WizStatisticEffect statistic) {
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
        var maxIndex = MagicSchools.GetMaxMagicSchoolIndex();

        // Ensure that the effect list is the same length as the number of schools.
        effectList ??= new List<float>(new float[maxIndex]);

        if (effectList.Count != maxIndex) {
            effectList = new List<float>(new float[maxIndex]);
        }

        var index = MagicSchools.GetMagicSchool(schoolName)?.m_schoolIndex ?? -1;

        if (index == -1) {
            Logger.Warning("Could not find magic school {0}.", Logger.Args(schoolName));
            return;
        }

        effectList[index] -= value;
    }

    private static void RemoveSchoolMastery(ServerWizGameStats stats, string effectCategory) {
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

    private static void RemoveDamageIncrease(ServerWizGameStats stats, string effectName, WizStatisticEffect statistic) {
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

    private static void RemoveAccuracyIncrease(ServerWizGameStats stats, string effectName, WizStatisticEffect statistic) {
        if (effectName.Contains("All")) {
            stats.m_accBonusPercentAll -= statistic.m_accuracyBonusPercent;
        }
        else {
            var schoolName = ExtractSchoolName(effectName);
            RemoveSchoolEffect(ref stats.m_accBonusPercent, schoolName, statistic.m_accuracyBonusPercent);
        }
    }

    private static void RemoveReduceDamageIncrease(ServerWizGameStats stats, string effectName, WizStatisticEffect statistic) {
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
