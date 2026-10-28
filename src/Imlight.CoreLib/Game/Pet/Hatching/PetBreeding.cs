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
 * PET BREEDING
 * ========================================================================
 *
 * PURPOSE:
 * Makes the egg two parent pets hatch: its kind and colors, its talent genes, its maximum stats,
 * a random name and its hatch time. The parents are not changed.
 *
 * USAGE EXAMPLE:
 * var egg = PetBreeding.CreateEgg(wizard.CharId, firstParent, secondParent, Random.Shared);
 *
 * NOTE:
 * Kinds, morphing exceptions, talent pools, stat maximums and hatch times come from the pet templates.
 *
 * TODO:
 * - The game's talent inheritance rule is not known; shown talents are passed on more often than hidden genes.
 * - Maximum stats are capped at the egg kind's own maximum; does the server have a higher cap?
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/22/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Pet.Config;
using Imlight.CoreLib.Game.Pet.Progression;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Pet.Hatching;

/// <summary>
/// Breeds two pets into an egg.
/// </summary>
internal static class PetBreeding {

    private const double SHOWN_TALENT_INHERIT_CHANCE = 0.5;
    private const double HIDDEN_GENE_INHERIT_CHANCE = 0.2;
    private const int MAX_STAT_SPREAD_FROM_PARENT_AVERAGE = 5;

    /// <summary>
    /// Creates the egg two parents make. It is a level 0 pet item that is not stored anywhere yet.
    /// </summary>
    /// <param name="ownerCharId">The character that gets the egg.</param>
    /// <param name="firstParent">One parent pet.</param>
    /// <param name="secondParent">The other parent pet.</param>
    /// <param name="random">The random source.</param>
    /// <returns>The egg, or null if a parent is not a known pet or the egg's kind has no template.</returns>
    internal static WizClientObjectItem CreateEgg(ulong ownerCharId,
                                                  WizClientObjectItem firstParent,
                                                  WizClientObjectItem secondParent,
                                                  Random random) {
        if (!PetProgression.TryGetPet(firstParent, out var first, out _) || !PetProgression.TryGetPet(secondParent, out var second, out _)) {
            return null;
        }

        var (eggTemplateId, colorParent) = PickKind(firstParent, secondParent, random);
        var egg = PetFactory.CreatePet(ownerCharId, eggTemplateId);
        if (!PetProgression.TryGetPet(egg, out var eggBehavior, out var eggTemplate)) {
            return null;
        }

        egg.m_primaryColor = colorParent.m_primaryColor;
        egg.m_secondaryColor = colorParent.m_secondaryColor;
        egg.m_pattern = colorParent.m_pattern;

        eggBehavior.m_allTalents = PetProgression.RollGenes(eggTemplate.m_guaranteedTalents, eggTemplate.m_talents, random,
            Inherit(first.m_allTalents, first.m_expressedTalents, second.m_allTalents, second.m_expressedTalents, random));
        eggBehavior.m_allDerbyTalents = PetProgression.RollGenes(eggTemplate.m_guaranteedDerbyTalents, eggTemplate.m_derbyTalents, random,
            Inherit(first.m_allDerbyTalents, first.m_expressedDerbyTalents, second.m_allDerbyTalents, second.m_expressedDerbyTalents, random));
        eggBehavior.m_maxStats = AverageMaxStats(first, second, eggTemplate, random);
        eggBehavior.m_hatchedTimeSecs = (uint) (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + GetHatchSeconds(eggTemplateId));
        PetFactory.TrySetPetName(egg, WizardNameBank.CreateRandomPetName(random));

        return egg;
    }

    /// <summary>
    /// Gets how long an egg of a pet template takes to hatch on this server.
    /// </summary>
    /// <param name="eggTemplateId">The pet template of the egg.</param>
    /// <returns>The seconds from the [Hatching] setting, or the template's own hatch time.</returns>
    internal static uint GetHatchSeconds(uint eggTemplateId)
        => HatchingRules.HatchMinutes is { } minutes
            ? (uint) TimeSpan.FromMinutes(minutes).TotalSeconds
            : PetFactory.GetHatchSeconds(eggTemplateId);

    private static (uint TemplateId, WizClientObjectItem ColorParent) PickKind(WizClientObjectItem firstParent,
                                                                              WizClientObjectItem secondParent,
                                                                              Random random) {
        if (TryMorphingException(firstParent, secondParent, random, out var exceptionEgg)) {
            return (exceptionEgg, firstParent);
        }

        if (TryMorphingException(secondParent, firstParent, random, out exceptionEgg)) {
            return (exceptionEgg, secondParent);
        }

        // todo: is hatches-as the kind an egg of this template turns into?
        var kindParent = random.Next(2) == 0 ? firstParent : secondParent;
        var hatchesAs = (uint) (PetFactory.GetPetItemBehaviorTemplate((uint) kindParent.m_templateID)?.m_hatchesAsID ?? 0);

        return (hatchesAs != 0 ? hatchesAs : (uint) kindParent.m_templateID, kindParent);
    }

    private static bool TryMorphingException(WizClientObjectItem parent,
                                             WizClientObjectItem mate,
                                             Random random,
                                             out uint eggTemplateId) {
        eggTemplateId = 0;
        var exceptions = PetFactory.GetPetItemBehaviorTemplate((uint) parent.m_templateID)?.m_morphingExceptions;
        var exception = exceptions?.FirstOrDefault(entry => (uint) entry.m_secondPetTemplateID == (uint) mate.m_templateID);
        if (exception is null || random.NextDouble() >= exception.m_probability) {
            return false;
        }

        eggTemplateId = (uint) exception.m_eggTemplateID;

        return eggTemplateId != 0;
    }

    private static List<uint> Inherit(List<uint> firstGenes,
                                      List<uint> firstShown,
                                      List<uint> secondGenes,
                                      List<uint> secondShown,
                                      Random random) {
        var shown = (firstShown ?? []).Concat(secondShown ?? []).Distinct().ToArray();
        var hidden = (firstGenes ?? []).Concat(secondGenes ?? []).Except(shown).ToArray();
        random.Shuffle(shown);
        random.Shuffle(hidden);

        return [
            .. shown.Where(_ => random.NextDouble() < SHOWN_TALENT_INHERIT_CHANCE),
            .. hidden.Where(_ => random.NextDouble() < HIDDEN_GENE_INHERIT_CHANCE)
        ];
    }

    private static List<PetStat> AverageMaxStats(ClientPetItemBehavior first,
                                                 ClientPetItemBehavior second,
                                                 PetItemBehaviorTemplate eggTemplate,
                                                 Random random) {
        var stats = new List<PetStat>();
        foreach (var kindMaximum in eggTemplate.m_maxStats ?? []) {
            var firstMaximum = first.m_maxStats?.FirstOrDefault(stat => stat.m_name == kindMaximum.m_name)?.m_value ?? kindMaximum.m_value;
            var secondMaximum = second.m_maxStats?.FirstOrDefault(stat => stat.m_name == kindMaximum.m_name)?.m_value ?? kindMaximum.m_value;
            var spread = random.Next(-MAX_STAT_SPREAD_FROM_PARENT_AVERAGE, MAX_STAT_SPREAD_FROM_PARENT_AVERAGE + 1);
            var value = Math.Clamp(((firstMaximum + secondMaximum) / 2) + spread, 1, Math.Max(1, kindMaximum.m_value));

            stats.Add(PetProgression.CopyStat(kindMaximum, value));
        }

        return stats;
    }

}
