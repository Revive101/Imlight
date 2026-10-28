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
 * PET MORPHING CONFIGURATION
 * ========================================================================
 *
 * PURPOSE:
 * Loads the pet morphing settings of the root archive and prices an in-person hatch the way the
 * client's hatching window does.
 *
 * USAGE EXAMPLE:
 * var gold = PetMorphingConfig.GetInPersonHatchCost(firstParent, secondParent);
 *
 * NOTE:
 * The client shows this price before a wizard confirms, so the server charges the same.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/22/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Pet.Config;

/// <summary>
/// The hatching prices, as defined in PetMorphing.xml.
/// </summary>
internal sealed class PetMorphingConfig : RootSingleResourceSingleton<PetMorphingConfig>, IMemoryStreamDisposable {

    private const int FIRST_PRICED_PET_LEVEL = 3;
    private const int LAST_PRICED_PET_LEVEL = 7;

    protected override string ResourceName => "PetMorphing.xml";

    private static BasePetMorphManager s_config;

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        if (!serializer.Deserialize<BasePetMorphManager>(Stream.ToArray(), out var config)) {
            throw new InvalidOperationException("Failed to deserialize PetMorphing.xml.");
        }

        s_config = config;
        Logger.Information("Loaded the pet morphing configuration.");

        DisposeStream();
    }

    /// <summary>
    /// Prices hatching two pets in person, as the client's hatching window shows it.
    /// </summary>
    /// <param name="first">One parent's pet behavior.</param>
    /// <param name="second">The other parent's pet behavior.</param>
    /// <returns>The gold the hatching wizard pays.</returns>
    internal static int GetInPersonHatchCost(ClientPetItemBehavior first, ClientPetItemBehavior second) {
        var level = Math.Clamp((int) Math.Max(first.m_level, second.m_level), FIRST_PRICED_PET_LEVEL, LAST_PRICED_PET_LEVEL);
        var tier = level - FIRST_PRICED_PET_LEVEL;
        var pedigree = Math.Max(first.m_overallRating, second.m_overallRating);

        var combat = GetTalentCost(s_config.m_inPersonCombatTalentCostMin,
                                   s_config.m_inPersonCombatTalentCostGrowth,
                                   tier,
                                   pedigree,
                                   s_config.m_inPersonCombatDivisor,
                                   s_config.m_inPersonCombatExponent);
        var derby = GetTalentCost(s_config.m_inPersonDerbyTalentCostMin,
                                  s_config.m_inPersonDerbyTalentCostGrowth,
                                  tier,
                                  pedigree,
                                  s_config.m_inPersonDerbyDivisor,
                                  s_config.m_inPersonDerbyExponent);

        return combat + derby;
    }

    private static int GetTalentCost(List<int> minimums, List<int> growths, int tier, uint pedigree, int divisor, int exponent) {
        var minimum = minimums?.ElementAtOrDefault(tier) ?? 0;
        var growth = growths?.ElementAtOrDefault(tier) ?? 0;

        // The client subtracts unsigned, so a pedigree under the minimum prices at the top.
        var pedigreeAboveMinimum = (double) unchecked(pedigree - (uint) s_config.m_inPersonPedigreeMin);
        var pedigreeLeftToDivisor = Math.Max(0, divisor - (int) pedigree);
        var steps = Math.Floor(Math.Pow(pedigreeAboveMinimum / (pedigreeLeftToDivisor + 1), exponent));

        return minimum + growth - (int) Math.Floor(growth / (steps + 1));
    }

    public void DisposeStream()
        => base.Stream.Dispose();

}
