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
 * PET FEEDING REWARDS
 * ========================================================================
 *
 * PURPOSE:
 * Loads the feeding reward tiers from the root archive: which loot tables a fed item rolls by its
 * intrinsic value, and the gold scaling and donation tables.
 *
 * USAGE EXAMPLE:
 * var tier = PetFeedingRewardConfig.FindTier(item.IntrinsicValue, isCrownsItem);
 *
 * NOTE:
 * Tiers are keyed by the least intrinsic value they apply to. The file keeps standard items and crowns
 * items in separate tier lists.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.ObjectProperty;
using Imlight.Common;
using Imlight.CoreLib.Game.Pet.Progression;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Pet.Config;

/// <summary>
/// The pet feeding reward tiers, gold scaling and donation loot tables, as defined in PetFeedingRewardConfig.xml.
/// </summary>
internal sealed class PetFeedingRewardConfig : RootSingleResourceSingleton<PetFeedingRewardConfig>, IMemoryStreamDisposable {

    protected override string ResourceName => "PetFeedingRewardConfig.xml";

    private static List<PetFeedingRewardInfo> s_standardTiers = [];
    private static List<PetFeedingRewardInfo> s_crownsTiers = [];
    private static List<string> s_donationLootTables = [];

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        if (!serializer.Deserialize<Imcodec.ObjectProperty.TypeCache.PetFeedingRewardConfig>(Stream.ToArray(), out var config)) {
            Logger.Error("Failed to deserialize PetFeedingRewardConfig.xml");

            return;
        }

        s_standardTiers = Validated("standard", config.m_standardItemFeedingRewards);
        s_crownsTiers = Validated("crowns", config.m_crownsFeedingRewards);
        s_donationLootTables = [.. config.m_petDonatingLootTables.Select(table => table.ToString())];
        GoldScalingFactor = config.m_fGoldScalingFactor;
        Logger.Information("Loaded {Standard} standard and {Crowns} crowns pet feeding reward tiers.",
            Logger.Args(s_standardTiers.Count, s_crownsTiers.Count));

        DisposeStream();
    }

    private static List<PetFeedingRewardInfo> Validated(string kind, List<PetFeedingRewardInfo> tiers) {
        var ordered = tiers.OrderBy(tier => tier.m_minIntrinsicValue).ToList();
        foreach (var tier in ordered.Where(tier => tier.m_lootTables.Count == 0 || tier.m_lootTables.Any(table => table.IsEmpty))) {
            Logger.Error("PetFeedingRewardConfig.xml: the {Kind} tier from {Minimum} names no loot table.",
                Logger.Args(kind, tier.m_minIntrinsicValue));
        }

        return ordered;
    }

    /// <summary>
    /// The factor that scales an item's value into gold when it is fed.
    /// </summary>
    internal static float GoldScalingFactor { get; private set; }

    /// <summary>
    /// The tiers for standard items, lowest minimum value first.
    /// </summary>
    internal static IReadOnlyList<PetFeedingRewardInfo> StandardTiers => s_standardTiers;

    /// <summary>
    /// The tiers for crowns items, lowest minimum value first.
    /// </summary>
    internal static IReadOnlyList<PetFeedingRewardInfo> CrownsTiers => s_crownsTiers;

    /// <summary>
    /// The loot tables a pet donation rolls, one per pet stage.
    /// </summary>
    internal static IReadOnlyList<string> DonationLootTables => s_donationLootTables;

    /// <summary>
    /// Finds the tier an item of the given intrinsic value feeds into.
    /// </summary>
    /// <param name="intrinsicValue">The item's intrinsic value.</param>
    /// <param name="crownsItem">True for a crowns item, false for a standard one.</param>
    /// <returns>The highest tier whose minimum the value reaches, or null if it reaches none.</returns>
    internal static PetFeedingRewardInfo FindTier(int intrinsicValue, bool crownsItem)
        => (crownsItem ? s_crownsTiers : s_standardTiers).LastOrDefault(tier => tier.m_minIntrinsicValue <= intrinsicValue);

    public void DisposeStream()
        => base.Stream.Dispose();

}
