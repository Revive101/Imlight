/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * PET PROGRESSION
 * ========================================================================
 *
 * PURPOSE:
 * Changes the state a pet keeps in its item behavior: genes, stats, happiness, XP and level,
 * expressed talents and ratings. Nothing here saves or sends; callers do that (see PetXpAward).
 *
 * USAGE EXAMPLE:
 * if (PetProgression.EnsureInitialized(pet)) { wizard.SavePet(pet); }
 *
 * NOTE:
 * A pet at level L has L - 1 expressed combat talents and L - 1 expressed derby talents, taken in
 * gene order. The rating lists show the genes in ascending id order: an expressed gene shows its
 * talent name, any other shows the rank label.
 *
 * TODO:
 * - Pets with no start stats use their conversion stats, and any stat neither lists starts at 1; unverified.
 * - m_rarity and m_costToMorph are left at 0, their formulas are unknown.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/22/2026
 */

using System.Collections.Generic;
using System.Linq;
using System;
using Imcodec.Cryptography;
using Imcodec.IO;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Pet.Config;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Pet.Progression;

/// <summary>
/// Changes the progression state in a pet item's behavior.
/// </summary>
internal static class PetProgression {

    /// <summary>
    /// Finds the pet behavior of an item and the template that backs it.
    /// </summary>
    /// <param name="pet">The pet item.</param>
    /// <param name="behavior">The item's pet behavior.</param>
    /// <param name="template">The pet's template behavior.</param>
    /// <returns>True if the item is a pet and its template is known.</returns>
    internal static bool TryGetPet(WizClientObjectItem pet, out ClientPetItemBehavior behavior, out PetItemBehaviorTemplate template) {
        behavior = null;
        template = null;
        if (pet?.m_inactiveBehaviors is null || !CoreObjectFactory.FindBehaviorInstance(pet, out behavior)) {
            return false;
        }

        template = PetFactory.GetPetItemBehaviorTemplate((uint) pet.m_templateID);
        if (template is null) {
            Logger.Warning("Pet {PetId} has the template {TemplateId}, which has no pet item behavior.",
                Logger.Args(pet.m_globalID.Full, pet.m_templateID.Full));
        }

        return template is not null;
    }

    /// <summary>
    /// Fills in whatever a hatched pet is missing and brings the derived values up to date: rolls the genes,
    /// sets the stats and happiness, expresses the talents its level has earned and recomputes the ratings.
    /// </summary>
    /// <param name="pet">The pet item.</param>
    /// <returns>True if the behavior changed, so it needs saving. Eggs and unknown pets are left alone.</returns>
    internal static bool EnsureInitialized(WizClientObjectItem pet) {
        if (!TryGetPet(pet, out var behavior, out var template) || behavior.m_level == 0) {
            return false;
        }

        var changed = EnsureGenes(behavior, template);
        changed |= EnsureStats(behavior, template);
        changed |= EnsureHappiness(behavior);
        changed |= EnsureLevel(behavior, template);
        changed |= EnsureExpressed(behavior);
        changed |= RefreshRatings(behavior);

        return changed;
    }

    /// <summary>
    /// Turns an egg into a level 1 pet.
    /// </summary>
    /// <param name="pet">The pet item.</param>
    /// <param name="hatchedTimeSecs">The epoch second the egg hatched.</param>
    /// <returns>False if the item is not a known pet.</returns>
    internal static bool Hatch(WizClientObjectItem pet, uint hatchedTimeSecs) {
        if (!TryGetPet(pet, out var behavior, out _)) {
            return false;
        }

        if (behavior.m_level == 0) {
            behavior.m_level = 1;
            behavior.m_XP = 0;
        }

        behavior.m_hatchedTimeSecs = hatchedTimeSecs;
        EnsureInitialized(pet);

        return true;
    }

    /// <summary>
    /// Adds XP to the pet's current level, carrying any overflow into the next levels.
    /// </summary>
    /// <param name="pet">The pet item.</param>
    /// <param name="xp">The XP to add, before any bonus for the source.</param>
    /// <param name="source">Where the XP comes from, which decides the bonus.</param>
    /// <returns>What the pet received and the levels it reached, or <see cref="PetXpResult.None"/> 
    /// if it cannot gain XP (not hatched, no XP ladder, at the top level, unknown).</returns>
    internal static PetXpResult AddXp(WizClientObjectItem pet, uint xp, PetXpSource source) {
        if (xp == 0 || !TryGetPet(pet, out var behavior, out var template) || behavior.m_level == 0) {
            return PetXpResult.None;
        }

        EnsureInitialized(pet);
        var maxLevel = MaxLevel(template);
        var startLevel = (int) behavior.m_level;
        if (startLevel >= maxLevel || RequiredXp(template, startLevel) == 0) {
            return PetXpResult.None;
        }

        var granted = (uint) Math.Min((ulong) xp * (uint) PetRules.GetXpMultiplier(source), uint.MaxValue);
        var talentsBefore = behavior.m_expressedTalents.Count;
        var derbyBefore = behavior.m_expressedDerbyTalents.Count;
        var activeRating = (int) behavior.m_activeRating;

        // A zero rung stops the walk, so a pet with an all-zero ladder cannot loop.
        var total = (ulong) behavior.m_XP + granted;
        var level = startLevel;
        while (level < maxLevel) {
            var required = RequiredXp(template, level);
            if (required == 0 || total < required) {
                break;
            }

            total -= required;
            level++;
        }

        behavior.m_level = (byte) level;
        behavior.m_XP = level >= maxLevel ? 0 : (uint) total;
        EnsureInitialized(pet);

        List<PetLevelUp> levelUps = [];
        for (var gained = 0; gained < level - startLevel; gained++) {
            var talent = behavior.m_expressedTalents.ElementAtOrDefault(talentsBefore + gained);
            var derbyPower = behavior.m_expressedDerbyTalents.ElementAtOrDefault(derbyBefore + gained);
            activeRating += RankOf(talent) + RankOf(derbyPower);
            levelUps.Add(new PetLevelUp((byte) (startLevel + 1 + gained), talent, derbyPower, (byte) activeRating));
        }

        return new PetXpResult(granted, levelUps);
    }

    /// <summary>
    /// Adds a set of stat changes to the pet's current stats, holding every stat at its maximum.
    /// </summary>
    /// <param name="pet">The pet item.</param>
    /// <param name="mods">The changes, such as a snack's or a game track's. The set itself is left as it is.</param>
    /// <returns>The points that raised stats and the points the maximums turned away, with a filled-in copy of the set. 
    /// A pet that cannot take stats (not hatched, unknown) takes none.</returns>
    internal static PetStatModResult ApplyStatMods(WizClientObjectItem pet, PetStatModificationSet mods) {
        var changes = mods?.m_modifications ?? [];
        var taken = new uint[changes.Count];
        ulong applied = 0;
        ulong overflow = 0;

        if (changes.Count > 0 && TryGetPet(pet, out var behavior, out _) && behavior.m_level > 0) {
            EnsureInitialized(pet);
            for (var index = 0; index < changes.Count; index++) {
                var points = ApplyStatChange(pet, behavior, changes[index]);
                taken[index] = (uint) points;
                applied += (uint) points;
                overflow += (uint) (changes[index].m_change - points);
            }
        }

        var copy = new PetStatModificationSet {
            m_name = mods?.m_name ?? default,
            m_scene = mods?.m_scene ?? default,
            m_gameScoreFactor = [.. mods?.m_gameScoreFactor ?? []],
            m_modifications = [.. changes.Select((change, index) => new PetStatModification {
                m_name = change.m_name,
                m_change = change.m_change,
                m_actualChange = taken[index]
            })]
        };

        return new PetStatModResult(copy, (uint) Math.Min(applied, uint.MaxValue), (uint) Math.Min(overflow, uint.MaxValue));
    }

    /// <summary>
    /// Sets the pet's level, clears its XP and expresses exactly the talents that level earns.
    /// </summary>
    /// <param name="pet">The pet item.</param>
    /// <param name="level">The new level, from 1 to the pet's highest level.</param>
    /// <returns>False if the pet is unknown, not hatched, or the level is out of range.</returns>
    internal static bool SetLevel(WizClientObjectItem pet, int level) {
        if (!TryGetPet(pet, out var behavior, out var template) || behavior.m_level == 0 || level < 1 || level > MaxLevel(template)) {
            return false;
        }

        EnsureInitialized(pet);
        behavior.m_level = (byte) level;
        behavior.m_XP = 0;
        behavior.m_expressedTalents = [.. behavior.m_allTalents.Take(level - 1)];
        behavior.m_expressedDerbyTalents = [.. behavior.m_allDerbyTalents.Take(level - 1)];
        EnsureInitialized(pet);

        return true;
    }

    /// <summary>
    /// Sets the pet's happiness, capped at its maximum.
    /// </summary>
    /// <param name="pet">The pet item.</param>
    /// <param name="happiness">The new happiness.</param>
    /// <returns>False if the pet is unknown or not hatched.</returns>
    internal static bool SetHappiness(WizClientObjectItem pet, uint happiness) {
        if (!TryGetPet(pet, out var behavior, out _) || behavior.m_level == 0) {
            return false;
        }

        EnsureInitialized(pet);
        behavior.m_happiness = Math.Min(happiness, behavior.m_maximumHappiness);

        return true;
    }

    /// <summary>
    /// Gives the pet a talent and expresses it, whatever the pet's level.
    /// </summary>
    /// <param name="pet">The pet item.</param>
    /// <param name="talent">The talent. Derby talents go on the derby lists, anything else on the combat lists.</param>
    /// <returns>False if the pet is unknown or not hatched.</returns>
    internal static bool GiveTalent(WizClientObjectItem pet, PetTalentInfo talent) {
        if (!TryGetPet(pet, out var behavior, out _) || behavior.m_level == 0) {
            return false;
        }

        EnsureInitialized(pet);
        var derby = talent.Kind == PetTalentKind.Derby;
        var genes = derby ? behavior.m_allDerbyTalents : behavior.m_allTalents;
        var expressed = derby ? behavior.m_expressedDerbyTalents : behavior.m_expressedTalents;
        if (!genes.Contains(talent.Id)) {
            genes.Add(talent.Id);
        }

        if (!expressed.Contains(talent.Id)) {
            expressed.Add(talent.Id);
        }

        RefreshRatings(behavior);

        return true;
    }

    /// <summary>
    /// Rolls a pet's genes: the guaranteed talents first, then the inherited ones, then the rest of the pool in random order.
    /// </summary>
    /// <param name="guaranteed">The guaranteed talent names, which may lie outside the pool.</param>
    /// <param name="pool">The pool of talent names. Its size is the number of genes.</param>
    /// <param name="random">The random source.</param>
    /// <param name="inherited">The talent ids passed on by parents, in the order they should be expressed; they may lie outside the pool.</param>
    /// <returns>The talent ids, without duplicates, in expression order.</returns>
    internal static List<uint> RollGenes(IEnumerable<ByteString> guaranteed, IEnumerable<ByteString> pool, Random random, IEnumerable<uint> inherited = null) {
        var poolIds = ResolveIds(pool);
        var genes = new List<uint>(poolIds.Count);
        foreach (var id in ResolveIds(guaranteed).Concat(inherited ?? [])) {
            if (genes.Count < poolIds.Count && !genes.Contains(id)) {
                genes.Add(id);
            }
        }

        var rest = poolIds.Where(id => !genes.Contains(id)).ToArray();
        random.Shuffle(rest);
        genes.AddRange(rest.Take(poolIds.Count - genes.Count));

        return genes;
    }

    private static int ApplyStatChange(WizClientObjectItem pet, ClientPetItemBehavior behavior, PetStatModification change) {
        if (change.m_change <= 0) {
            if (change.m_change < 0) {
                Logger.Warning("The stat change {Stat} of {Change} is negative, so it is skipped.", Logger.Args(change.m_name.ToString(), change.m_change));
            }

            return 0;
        }

        var current = behavior.m_currentStats?.FirstOrDefault(stat => stat.m_name == change.m_name);
        var maximum = behavior.m_maxStats?.FirstOrDefault(stat => stat.m_name == change.m_name);
        if (current is null || maximum is null) {
            Logger.Warning("The pet {PetId} has no stat {Stat}, so its {Change} points are not applied.", 
                Logger.Args(pet.m_globalID.Full, change.m_name.ToString(), change.m_change));

            return 0;
        }

        var points = Math.Min(change.m_change, Math.Max(0, maximum.m_value - current.m_value));
        current.m_value += points;

        return points;
    }

    private static List<uint> ResolveIds(IEnumerable<ByteString> names) {
        var ids = new List<uint>();
        foreach (var name in names ?? []) {
            if (!PetTalentRegistry.TryGetByName(name.ToString(), out var talent)) {
                Logger.Warning("The pet talent {Talent} has no template, so it is not a gene.", 
                    Logger.Args(name.ToString()));

                continue;
            }

            if (!ids.Contains(talent.Id)) {
                ids.Add(talent.Id);
            }
        }

        return ids;
    }

    private static bool EnsureGenes(ClientPetItemBehavior behavior, PetItemBehaviorTemplate template) {
        var changed = false;
        if (behavior.m_allTalents is not { Count: > 0 }) {
            behavior.m_allTalents = RollGenes(template.m_guaranteedTalents, template.m_talents, Random.Shared);
            changed = behavior.m_allTalents.Count > 0;
        }

        if (behavior.m_allDerbyTalents is not { Count: > 0 }) {
            behavior.m_allDerbyTalents = RollGenes(template.m_guaranteedDerbyTalents, template.m_derbyTalents, Random.Shared);
            changed |= behavior.m_allDerbyTalents.Count > 0;
        }

        return changed;
    }

    private static bool EnsureStats(ClientPetItemBehavior behavior, PetItemBehaviorTemplate template) {
        var changed = false;
        if (behavior.m_maxStats is not { Count: > 0 } && template.m_maxStats is { Count: > 0 }) {
            behavior.m_maxStats = [.. template.m_maxStats.Select(stat => CopyStat(stat, stat.m_value))];
            changed = true;
        }

        if (behavior.m_currentStats is not { Count: > 0 } && behavior.m_maxStats is { Count: > 0 }) {
            var start = template.m_startStats is { Count: > 0 } ? template.m_startStats
                : template.m_conversionStats is { Count: > 0 } ? template.m_conversionStats
                : [];
            behavior.m_currentStats = [.. behavior.m_maxStats.Select(max
                => CopyStat(max, start.FirstOrDefault(stat => stat.m_name == max.m_name)?.m_value ?? 1))];
            changed = true;
        }

        return changed;
    }

    internal static PetStat CopyStat(PetStat stat, int value)
        => new() { m_name = stat.m_name, m_statID = StringHash.Compute(stat.m_name), m_value = value };

    private static bool EnsureHappiness(ClientPetItemBehavior behavior) {
        var maximum = (uint) PetRules.MaximumHappiness;
        var changed = false;
        if (behavior.m_maximumHappiness == 0) {
            behavior.m_happiness = Math.Min((uint) PetRules.StartingHappiness, maximum);
            changed = true;
        }

        if (behavior.m_maximumHappiness != maximum) {
            behavior.m_maximumHappiness = maximum;
            changed = true;
        }

        if (behavior.m_happiness > maximum) {
            behavior.m_happiness = maximum;
            changed = true;
        }

        return changed;
    }

    private static bool EnsureLevel(ClientPetItemBehavior behavior, PetItemBehaviorTemplate template) {
        var maxLevel = MaxLevel(template);
        if (maxLevel == 0) {
            return false;
        }

        var changed = false;
        if (behavior.m_level > maxLevel) {
            behavior.m_level = (byte) maxLevel;
            changed = true;
        }

        var atMaxLevel = behavior.m_level >= maxLevel;
        var required = atMaxLevel ? 0 : RequiredXp(template, behavior.m_level);
        if (behavior.m_requiredXP != required) {
            behavior.m_requiredXP = required;
            changed = true;
        }

        if (atMaxLevel && behavior.m_XP != 0) {
            behavior.m_XP = 0;
            changed = true;
        }

        return changed;
    }

    private static bool EnsureExpressed(ClientPetItemBehavior behavior) {
        var earned = behavior.m_level - 1;
        var changed = ExpressUpTo(behavior.m_allTalents, behavior.m_expressedTalents ??= [], earned);
        changed |= ExpressUpTo(behavior.m_allDerbyTalents, behavior.m_expressedDerbyTalents ??= [], earned);

        return changed;
    }

    private static bool ExpressUpTo(List<uint> genes, List<uint> expressed, int count) {
        var changed = false;
        foreach (var gene in genes) {
            if (expressed.Count >= count) {
                break;
            }

            if (!expressed.Contains(gene)) {
                expressed.Add(gene);
                changed = true;
            }
        }

        return changed;
    }

    private static bool RefreshRatings(ClientPetItemBehavior behavior) {
        var talentRatings = BuildRatings(behavior.m_allTalents, behavior.m_expressedTalents);
        var powerRatings = BuildRatings(behavior.m_allDerbyTalents, behavior.m_expressedDerbyTalents);
        var overall = (uint) behavior.m_allTalents.Concat(behavior.m_allDerbyTalents).Sum(RankOf);
        var active = (uint) behavior.m_expressedTalents.Concat(behavior.m_expressedDerbyTalents).Sum(RankOf);

        var changed = !SameStrings(behavior.m_talentRatings, talentRatings) || !SameStrings(behavior.m_powerRatings, powerRatings)
            || behavior.m_overallRating != overall || behavior.m_activeRating != active;
        if (changed) {
            behavior.m_talentRatings = [.. talentRatings.Select(rating => (ByteString) rating)];
            behavior.m_powerRatings = [.. powerRatings.Select(rating => (ByteString) rating)];
            behavior.m_overallRating = overall;
            behavior.m_activeRating = active;
        }

        return changed;
    }

    private static List<string> BuildRatings(List<uint> genes, List<uint> expressed)
        => [.. genes.Order().Select(gene => expressed.Contains(gene) && PetTalentRegistry.TryGetById(gene, out var talent)
            ? talent.Name
            : RankLabel(RankOf(gene)))];

    private static bool SameStrings(List<ByteString> current, List<string> wanted)
        => current is not null && current.Select(rating => rating.ToString()).SequenceEqual(wanted, StringComparer.Ordinal);

    private static int RankOf(uint talentId)
        => PetTalentRegistry.TryGetById(talentId, out var talent) ? talent.Rank : 0;

    private static string RankLabel(int rank) => rank switch {
        2 => "TalentRankUncommon",
        3 => "TalentRankRare",
        4 => "TalentRankUltraRare",
        5 => "TalentRankEpic",
        _ => "TalentRankCommon"
    };

    private static int MaxLevel(PetItemBehaviorTemplate template)
        => template.m_Levels is { Count: > 0 } ? template.m_Levels.Max(info => info.m_level) : 0;

    private static uint RequiredXp(PetItemBehaviorTemplate template, int level)
        => template.m_Levels?.FirstOrDefault(info => info.m_level == level)?.m_requiredXP ?? 0;

}
