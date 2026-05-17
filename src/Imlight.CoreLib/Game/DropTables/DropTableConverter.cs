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
 */

using System;
using System.Collections.Generic;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.Game.DropTables;

public static class DropTableConverter {

    /// <summary>
    /// Converts a DropTableResult to a LootInfoList for network transmission
    /// </summary>
    /// <param name="dropResult">The rolled drop table result to convert</param>
    /// <returns>LootInfoList ready for network serialization</returns>
    public static LootInfoList ToLootInfoList(DropTableResult dropResult) {
        ArgumentNullException.ThrowIfNull(dropResult);

        var lootList = new LootInfoList {
            m_loot = [],
            m_goldInfo = null,
            m_lootRarityList = new LootRarityList { m_loot = [] }
        };

        var lootItems = new List<LootInfo>();

        // Convert gold rewards.
        if (dropResult.GoldAmount > 0) {
            lootList.m_goldInfo = new GoldLootInfo {
                m_lootType = LOOT_TYPE.LOOT_TYPE_GOLD,
                m_goldAmount = dropResult.GoldAmount
            };
        }

        // Convert experience rewards.
        if (dropResult.ExperienceAmount > 0) {
            lootItems.Add(new MagicXPLootInfo {
                m_lootType = LOOT_TYPE.LOOT_TYPE_MAGIC_XP,
                m_experience = dropResult.ExperienceAmount,
                m_magicSchool = dropResult.MagicSchool
            });
        }

        // Convert training point rewards.
        if (dropResult.TrainingPoints > 0) {
            lootItems.Add(new TrainingPointLootInfo {
                m_lootType = LOOT_TYPE.LOOT_TYPE_TRAINING_POINTS,
                m_trainingPoints = dropResult.TrainingPoints
            });
        }

        // Convert item rewards.
        foreach (var itemResult in dropResult.Items) {
            if (!string.IsNullOrEmpty(itemResult.ItemId)) {
                if (TryParseItemId(itemResult.ItemId, out var itemGid)) {
                    lootItems.Add(new ItemLootInfo {
                        m_lootType = LOOT_TYPE.LOOT_TYPE_ITEM,
                        m_itemID = itemGid,
                        m_numItems = itemResult.Quantity
                    });
                }
            }
        }

        lootList.m_loot = lootItems;
        
        return lootList;
    }

    /// <summary>
    /// Creates an empty LootInfoList for cases where no rewards are given
    /// </summary>
    /// <returns>Empty LootInfoList</returns>
    public static LootInfoList CreateEmpty() 
        => new() {
            m_loot = [],
            m_goldInfo = null,
            m_lootRarityList = new LootRarityList { m_loot = [] }
        };

    /// <summary>
    /// Validates if a DropTableResult has any rewards to convert
    /// </summary>
    /// <param name="dropResult">Drop table result to validate</param>
    /// <returns>True if the result has rewards</returns>
    public static bool HasRewards(DropTableResult dropResult) 
        => dropResult?.HasRewards ?? false;

    private static bool TryParseItemId(string itemId, out GID gid) {
        gid = 0;
        
        if (string.IsNullOrWhiteSpace(itemId)) {
            return false;
        }

        if (ulong.TryParse(itemId, out var ulongValue)) {
            gid = ulongValue;
            return true;
        }

        if (itemId.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
            if (ulong.TryParse(itemId[2..], System.Globalization.NumberStyles.HexNumber, null, out ulongValue)) {
                gid = ulongValue;

                return true;
            }
        }

        return false;
    }

}